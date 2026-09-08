using Protocol;
using ReceiverLib;

namespace ReceiverLib.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IVideoDecoder"/>: publishes a frame to a real <see cref="FrameBufferPool"/>
/// for every packet (pixels left zeroed — tests care about flow and metadata, not content).
/// </summary>
internal sealed class FakeVideoDecoder : IVideoDecoder
{
    private FrameBufferPool? _buffers;

    public StreamInfo? ConfiguredWith { get; private set; }

    public int FlushCount { get; private set; }

    public bool IsDisposed { get; private set; }

    public string CodecName { get; private set; } = string.Empty;

    public FrameBufferPool? OutputBuffers => _buffers;

    public void Configure(StreamInfo info)
    {
        ConfiguredWith = info;
        CodecName = "fake";
        int width = info.Width > 0 ? info.Width : 2;
        int height = info.Height > 0 ? info.Height : 2;
        if (_buffers is null || _buffers.Width != width || _buffers.Height != height)
        {
            int generation = (_buffers?.Generation ?? -1) + 1;
            _buffers = new FrameBufferPool(width, height) { Generation = generation };
        }
    }

    public bool TryDecode(in ReceivedPacket packet, out VideoFrame frame)
    {
        FrameBufferPool buffers = _buffers ?? throw new InvalidOperationException("Decoder is not configured.");
        ReadOnlyMemory<byte> pixels = buffers.CurrentWriteMemory();
        buffers.Commit(packet.SequenceNumber, packet.Timestamp);
        frame = new VideoFrame(
            buffers.Width, buffers.Height, buffers.Stride, FramePixelFormat.Bgra32, pixels, packet.Timestamp);
        return true;
    }

    public void Flush() => FlushCount++;

    public void Dispose()
    {
        IsDisposed = true;
        _buffers = null;
    }
}
