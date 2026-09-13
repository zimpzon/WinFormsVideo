using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Formats;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Swscales;
using Sdcb.FFmpeg.Toolboxs.Extensions;
using Sdcb.FFmpeg.Utils;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WinFormsReceiver.Context
{
    public sealed class Video : IVideo, IDisposable
    {
        /// <summary>
        /// A snapshot of live playback stats — cheap to read from any thread at any time.
        /// </summary>
        public VideoStats Stats
        {
            get
            {
                double seconds = _stopwatch.Elapsed.TotalSeconds;
                return new VideoStats(
                    IsConnected: _isConnected,
                    Width: _width,
                    Height: _height,
                    CodecName: _codecName,
                    PixelFormat: _pixelFormat,
                    Fps: seconds > 0 ? _framesDecoded / seconds : 0,
                    BitRateBps: seconds > 0 ? (long)(_bytesReceived * 8 / seconds) : 0,
                    FramesDecoded: _framesDecoded,
                    KeyFramesDecoded: _keyFramesDecoded,
                    PacketsReceived: _packetsReceived,
                    BytesReceived: _bytesReceived,
                    ReconnectCount: _reconnectCount,
                    TimePlayed: _stopwatch.Elapsed,
                    LastError: _lastError);
            }
        }

        /// <summary>
        /// Raised on a background decode thread once per frame, after the frame buffer
        /// is finalized. The Bitmap instance is reused across calls — do not cache a
        /// reference to it past this call; copy what you need into your own control's
        /// buffer before returning.
        /// </summary>
        public Action<Bitmap>? FrameReady { get; set; }

        private readonly Stopwatch _stopwatch = new();
        private CancellationTokenSource? _cts;
        private Thread? _decodeThread;

        // Reused frame buffer — only reallocated when dimensions change.
        private GCHandle _bufferHandle;
        private byte[]? _frameBuffer;
        private Bitmap? _frameBitmap;
        private int _width;
        private int _height;
        private int _stride;

        // Stats, for VideoStats above — updated alongside playback but never consulted
        // by it, so none of this changes playback behavior.
        private bool _isConnected;
        private string _codecName = "";
        private string _pixelFormat = "";
        private long _framesDecoded;
        private long _keyFramesDecoded;
        private long _packetsReceived;
        private long _bytesReceived;
        private int _reconnectCount;
        private string? _lastError;

        public void Play(Uri uri)
        {
            Stop();

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _decodeThread = new Thread(() => DecodeLoop(uri, token)) { IsBackground = true };
            _decodeThread.Start();

            _stopwatch.Restart();
        }

        // Runs on a background thread for the lifetime of the stream. Reconnects on
        // any non-cancellation error — the sender may drop/restart the stream at will
        // (see CLAUDE.md), and a transient network hiccup shouldn't require the user
        // to re-click Play.
        private void DecodeLoop(Uri uri, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var options = new MediaDictionary { ["rtsp_transport"] = "tcp" };
                    using var formatContext = FormatContext.OpenInputUrl(uri.ToString(), options: options);
                    formatContext.LoadStreamInfo();

                    var stream = formatContext.FindBestStreamOrNull(AVMediaType.Video)
                        ?? throw new InvalidOperationException("No video stream found in RTSP source.");

                    var codec = Codec.FindDecoderById(stream.Codecpar!.CodecId);
                    using var codecContext = new CodecContext(codec);
                    codecContext.FillParameters(stream.Codecpar);
                    codecContext.Open();

                    _codecName = codec.Name ?? stream.Codecpar.CodecId.ToString();
                    _isConnected = true;
                    _lastError = null;

                    using var converter = new VideoFrameConverter();
                    using var decodedFrame = new Frame();
                    using var bgraFrame = new Frame();

                    foreach (var packet in formatContext.ReadPackets(stream.Index))
                    {
                        if (token.IsCancellationRequested)
                            break;

                        _packetsReceived++;

                        foreach (var frame in codecContext.DecodePacket(packet, decodedFrame))
                        {
                            EmitFrame(converter, frame, bgraFrame);
                        }
                    }
                }
                catch (Exception ex) when (token.IsCancellationRequested)
                {
                    // Stop() tore the stream down from under us — expected.
                    Debug.WriteLine($"[Video] decode loop stopped: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Stream dropped/errored (e.g. sender restarted) — back off and reconnect.
                    Debug.WriteLine($"[Video] decode loop error, reconnecting: {ex}");
                    _isConnected = false;
                    _reconnectCount++;
                    _lastError = ex.Message;
                    Thread.Sleep(1000);
                }
            }
        }

        private void EmitFrame(VideoFrameConverter converter, Frame decoded, Frame bgraFrame)
        {
            _framesDecoded++;
            if ((decoded.Flags & ffmpeg.AV_FRAME_FLAG_KEY) != 0)
                _keyFramesDecoded++;

            // PktSize is obsolete upstream (superseded by AV_CODEC_FLAG_COPY_OPAQUE) but
            // still functional in 7.0 and the simplest way to measure received bitrate.
            if (decoded.PktSize > 0)
                _bytesReceived += decoded.PktSize;
            _pixelFormat = ((AVPixelFormat)decoded.Format).ToString();

            bgraFrame.Unref();
            bgraFrame.Width = decoded.Width;
            bgraFrame.Height = decoded.Height;
            bgraFrame.Format = (int)AVPixelFormat.Bgra;
            bgraFrame.EnsureBuffer();

            converter.ConvertFrame(decoded, bgraFrame);

            AllocateBuffer(decoded.Width, decoded.Height);

            // Bgra is a single packed plane — copy it row by row since ffmpeg's row
            // stride (Linesize[0], possibly padded for alignment) may differ from ours.
            IntPtr src = bgraFrame.Data[0];
            int srcStride = bgraFrame.Linesize[0];
            for (int y = 0; y < _height; y++)
                Marshal.Copy(src + y * srcStride, _frameBuffer!, y * _stride, _stride);

            if (_frameBitmap != null)
                FrameReady?.Invoke(_frameBitmap);
        }

        private void AllocateBuffer(int width, int height)
        {
            int stride = width * 4; // bgra = 4 bytes/pixel

            // Only reallocate if the size actually changed (e.g. stream renegotiated).
            if (_frameBuffer != null && _width == width && _height == height)
                return;

            FreeBuffer();

            _width = width;
            _height = height;
            _stride = stride;

            _frameBuffer = new byte[_stride * _height];
            _bufferHandle = GCHandle.Alloc(_frameBuffer, GCHandleType.Pinned);

            _frameBitmap = new Bitmap(
                _width, _height, _stride,
                PixelFormat.Format32bppRgb,
                _bufferHandle.AddrOfPinnedObject());
        }

        private void FreeBuffer()
        {
            _frameBitmap?.Dispose();
            _frameBitmap = null;

            if (_bufferHandle.IsAllocated)
                _bufferHandle.Free();

            _frameBuffer = null;
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
            _decodeThread = null;

            _stopwatch.Reset();
            _width = 0;
            _height = 0;
            FreeBuffer();

            _isConnected = false;
            _codecName = "";
            _pixelFormat = "";
            _framesDecoded = 0;
            _keyFramesDecoded = 0;
            _packetsReceived = 0;
            _bytesReceived = 0;
            _reconnectCount = 0;
            _lastError = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
