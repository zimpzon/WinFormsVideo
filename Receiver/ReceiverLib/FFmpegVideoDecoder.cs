using Protocol;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Common;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Toolboxs.Extensions;
using Sdcb.FFmpeg.Utils;
using static Sdcb.FFmpeg.Raw.ffmpeg;

namespace ReceiverLib;

/// <summary>
/// FFmpeg-backed <see cref="IVideoDecoder"/> (via Sdcb.FFmpeg).
/// </summary>
/// <remarks>
/// ALL FFmpeg / native interop belongs in this file. It builds a decoder from the handshake
/// <see cref="StreamInfo"/> (codec id + <c>extradata</c>), decodes the sender's raw (AVCC) packets
/// and scales each frame to <see cref="FramePixelFormat.Bgra32"/>. No per-frame allocation: the
/// packet is a borrowed pointer, the sws plane/stride arrays are reused, and the output rotates a
/// small ring of buffers (so <see cref="VideoFrame.Pixels"/> is only valid for the callback).
/// Assumes one packet produces one frame (true for the sender's zero-latency / no-B-frame stream).
/// </remarks>
internal sealed unsafe class FFmpegVideoDecoder : IVideoDecoder
{
    private const int RingSize = 3;

    private CodecContext? _codecContext;
    private Packet? _packet;
    private Frame? _decoded;
    private SwsContext* _sws;

    private readonly byte*[] _srcPlanes = new byte*[4];
    private readonly int[] _srcStrides = new int[4];
    private readonly byte*[] _dstPlanes = new byte*[4];
    private readonly int[] _dstStrides = new int[4];

    private byte[][] _ring = Array.Empty<byte[]>();
    private int _ringIndex;
    private int _frameSize;
    private int _width;
    private int _height;

    public string CodecName { get; private set; } = string.Empty;

    public void Configure(StreamInfo info)
    {
        Reset();

        Codec codec;
        try
        {
            codec = Codec.FindDecoderById((AVCodecID)info.CodecId);
        }
        catch (FFmpegException ex)
        {
            throw new NotSupportedException($"No decoder for codec id {info.CodecId}.", ex);
        }

        var context = new CodecContext(codec)
        {
            Width = info.Width,
            Height = info.Height,
        };

        if (!info.Extradata.IsEmpty)
        {
            SetExtradata(context, info.Extradata.Span);
        }

        try
        {
            context.Open(codec);
        }
        catch (FFmpegException ex)
        {
            context.Dispose();
            throw new NotSupportedException($"Could not open the {codec.Name} decoder.", ex);
        }

        _codecContext = context;
        CodecName = codec.Name;
        _width = info.Width;
        _height = info.Height;
        _frameSize = info.Width * info.Height * 4;
        _packet = new Packet();
        _decoded = new Frame();

        _ring = new byte[RingSize][];
        for (int i = 0; i < RingSize; i++)
        {
            _ring[i] = new byte[_frameSize];
        }

        _ringIndex = 0;
        _dstStrides[0] = info.Width * 4;
    }

    public bool TryDecode(in ReceivedPacket packet, out VideoFrame frame)
    {
        frame = default;
        CodecContext context = _codecContext ?? throw new InvalidOperationException("Decoder is not configured.");

        ReadOnlySpan<byte> data = packet.Data.Span;
        try
        {
            fixed (byte* p = data)
            {
                _packet!.SetData((IntPtr)p, data.Length);
                context.SendPacket(_packet);
            }
        }
        catch (FFmpegException)
        {
            _packet!.SetData(IntPtr.Zero, 0);
            return false; // skip a bad packet rather than crash
        }

        _packet!.SetData(IntPtr.Zero, 0);

        Frame decoded = _decoded!;
        if (context.ReceiveFrame(decoded) != CodecResult.Success)
        {
            return false;
        }

        try
        {
            _sws = sws_getCachedContext(
                _sws,
                decoded.Width, decoded.Height, (AVPixelFormat)decoded.Format,
                _width, _height, AVPixelFormat.Bgra,
                (int)SWS.Bilinear, null, null, null);

            for (int i = 0; i < 4; i++)
            {
                _srcPlanes[i] = (byte*)decoded.Data[i];
                _srcStrides[i] = decoded.Linesize[i];
            }

            byte[] output = _ring[_ringIndex];
            _ringIndex = (_ringIndex + 1) % RingSize;

            fixed (byte* dst = output)
            {
                _dstPlanes[0] = dst;
                sws_scale(_sws, _srcPlanes, _srcStrides, 0, decoded.Height, _dstPlanes, _dstStrides);
            }

            frame = new VideoFrame(
                _width,
                _height,
                _width * 4,
                FramePixelFormat.Bgra32,
                output.AsMemory(0, _frameSize),
                packet.Timestamp);
            return true;
        }
        finally
        {
            decoded.Unref();
        }
    }

    public void Flush()
    {
        if (_codecContext is { } context)
        {
            avcodec_flush_buffers(context);
        }
    }

    public void Dispose() => Reset();

    private void Reset()
    {
        CodecName = string.Empty;

        if (_sws is not null)
        {
            sws_freeContext(_sws);
            _sws = null;
        }

        _decoded?.Dispose();
        _decoded = null;
        _packet?.Dispose();
        _packet = null;
        _codecContext?.Dispose(); // also frees the extradata we handed it
        _codecContext = null;
        _ring = Array.Empty<byte[]>();
        _ringIndex = 0;
    }

    private static void SetExtradata(CodecContext context, ReadOnlySpan<byte> extradata)
    {
        int size = extradata.Length;
        var buffer = (byte*)av_malloc((ulong)(size + AV_INPUT_BUFFER_PADDING_SIZE));
        if (buffer is null)
        {
            throw new OutOfMemoryException("av_malloc failed allocating codec extradata.");
        }

        extradata.CopyTo(new Span<byte>(buffer, size));
        new Span<byte>(buffer + size, AV_INPUT_BUFFER_PADDING_SIZE).Clear();

        context.Extradata = (IntPtr)buffer;
        context.ExtradataSize = size;
    }
}
