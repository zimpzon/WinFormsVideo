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
/// packet is a borrowed pointer, the sws plane/stride arrays are reused, and the output is scaled
/// straight into a <see cref="FrameBufferPool"/> (a triple buffer), so <see cref="VideoFrame.Pixels"/>
/// is only valid for the callback while the pool also feeds the frontend's zero-copy pull path.
/// Assumes one packet produces one frame (true for the sender's zero-latency / no-B-frame stream).
/// </remarks>
internal sealed unsafe class FFmpegVideoDecoder : IVideoDecoder
{
    private CodecContext? _codecContext;
    private Packet? _packet;
    private Frame? _decoded;
    private SwsContext* _sws;

    private readonly byte*[] _srcPlanes = new byte*[4];
    private readonly int[] _srcStrides = new int[4];
    private readonly byte*[] _dstPlanes = new byte*[4];
    private readonly int[] _dstStrides = new int[4];

    private FrameBufferPool? _buffers;
    private int _width;
    private int _height;

    public string CodecName { get; private set; } = string.Empty;

    public FrameBufferPool? OutputBuffers => _buffers;

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
        _packet = new Packet();
        _decoded = new Frame();

        // Keep the pool (and its stable buffer addresses) across a same-resolution reconfigure so
        // the frontend's Bitmap wrappers survive a reconnect; replace it on a resolution change.
        if (_buffers is null || _buffers.Width != _width || _buffers.Height != _height)
        {
            int generation = (_buffers?.Generation ?? -1) + 1;
            _buffers = new FrameBufferPool(_width, _height) { Generation = generation };
        }

        _dstStrides[0] = _buffers.Stride;
    }

    public bool TryDecode(in ReceivedPacket packet, out VideoFrame frame)
    {
        frame = default;
        CodecContext context = _codecContext ?? throw new InvalidOperationException("Decoder is not configured.");
        FrameBufferPool buffers = _buffers ?? throw new InvalidOperationException("Decoder is not configured.");

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

            // Scale straight into the pool's current write buffer — no intermediate copy — then
            // publish it. The managed view is captured before Commit advances the write slot.
            FrameTarget target = buffers.CurrentWriteTarget();
            ReadOnlyMemory<byte> output = buffers.CurrentWriteMemory();

            _dstPlanes[0] = (byte*)target.Scan0;
            _dstStrides[0] = target.Stride;
            sws_scale(_sws, _srcPlanes, _srcStrides, 0, decoded.Height, _dstPlanes, _dstStrides);

            buffers.Commit(packet.SequenceNumber, packet.Timestamp);

            frame = new VideoFrame(
                _width,
                _height,
                target.Stride,
                FramePixelFormat.Bgra32,
                output,
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

    public void Dispose()
    {
        Reset();
        _buffers = null; // released here, not on reconfigure — the frontend's wrappers depend on it
    }

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
