using Protocol;
using ReceiverLib;

namespace ReceiverLib.Tests.Fakes;

/// <summary>In-memory <see cref="IVideoDecoder"/>: emits a 1x1 frame for every packet.</summary>
internal sealed class FakeVideoDecoder : IVideoDecoder
{
    public StreamInfo? ConfiguredWith { get; private set; }

    public int FlushCount { get; private set; }

    public bool IsDisposed { get; private set; }

    public string CodecName { get; private set; } = string.Empty;

    public void Configure(StreamInfo info)
    {
        ConfiguredWith = info;
        CodecName = "fake";
    }

    public bool TryDecode(in ReceivedPacket packet, out VideoFrame frame)
    {
        frame = new VideoFrame(1, 1, 4, FramePixelFormat.Bgra32, new byte[4], packet.Timestamp);
        return true;
    }

    public void Flush() => FlushCount++;

    public void Dispose() => IsDisposed = true;
}
