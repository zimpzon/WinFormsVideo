using Protocol;
using ReceiverLib;

namespace ReceiverLib.Tests;

public class FFmpegVideoDecoderTests
{
    [Fact]
    public void Configure_ThenDecode_ProducesBgraFramesOfTheStreamResolution()
    {
        using var decoder = new FFmpegVideoDecoder();
        decoder.Configure(DecoderTestData.StreamInfo);

        var frames = new List<VideoFrame>();
        foreach (ReceivedPacket packet in DecoderTestData.Packets)
        {
            if (decoder.TryDecode(packet, out VideoFrame frame))
            {
                frames.Add(frame);
            }
        }

        Assert.NotEmpty(frames);
        Assert.All(frames, f =>
        {
            Assert.Equal(DecoderTestData.Width, f.Width);
            Assert.Equal(DecoderTestData.Height, f.Height);
            Assert.Equal(FramePixelFormat.Bgra32, f.PixelFormat);
            Assert.Equal(DecoderTestData.Width * 4, f.Stride);
            Assert.Equal(DecoderTestData.Width * DecoderTestData.Height * 4, f.Pixels.Length);
        });
    }

    [Fact]
    public void Decode_PreservesPacketTimestamps_InOrder()
    {
        using var decoder = new FFmpegVideoDecoder();
        decoder.Configure(DecoderTestData.StreamInfo);

        var timestamps = new List<TimeSpan>();
        foreach (ReceivedPacket packet in DecoderTestData.Packets)
        {
            if (decoder.TryDecode(packet, out VideoFrame frame))
            {
                timestamps.Add(frame.Timestamp);
            }
        }

        Assert.Equal(TimeSpan.Zero, timestamps[0]);
        for (int i = 1; i < timestamps.Count; i++)
        {
            Assert.True(timestamps[i] >= timestamps[i - 1]);
        }
    }

    [Fact]
    public void Decode_DoesNotAllocatePerFrame()
    {
        using var decoder = new FFmpegVideoDecoder();
        decoder.Configure(DecoderTestData.StreamInfo);
        IReadOnlyList<ReceivedPacket> packets = DecoderTestData.Packets;

        int warm = Math.Min(4, packets.Count);
        for (int i = 0; i < warm; i++)
        {
            decoder.TryDecode(packets[i], out _);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        int decoded = 0;
        for (int i = warm; i < packets.Count; i++)
        {
            if (decoder.TryDecode(packets[i], out _))
            {
                decoded++;
            }
        }

        long delta = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(decoded > 0);
        Assert.True(delta < 4096, $"allocated {delta} bytes decoding {decoded} frames");
    }

    [Fact]
    public void Configure_WithAnUnknownCodec_Throws()
    {
        using var decoder = new FFmpegVideoDecoder();
        var bogus = new StreamInfo(codecId: 999999, width: 16, height: 16, ReadOnlyMemory<byte>.Empty);

        Assert.Throws<NotSupportedException>(() => decoder.Configure(bogus));
    }

    [Fact]
    public void TryDecode_BeforeConfigure_Throws()
    {
        using var decoder = new FFmpegVideoDecoder();
        Assert.Throws<InvalidOperationException>(() =>
            decoder.TryDecode(new ReceivedPacket(0, TimeSpan.Zero, true, new byte[4]), out _));
    }

    [Fact]
    public void Dispose_AndReconfigure_IsClean()
    {
        var decoder = new FFmpegVideoDecoder();
        decoder.Configure(DecoderTestData.StreamInfo);
        decoder.TryDecode(DecoderTestData.Packets[0], out _);
        decoder.Dispose();

        // reusable after Dispose
        decoder.Configure(DecoderTestData.StreamInfo);
        Assert.True(decoder.TryDecode(DecoderTestData.Packets[0], out VideoFrame frame));
        Assert.Equal(DecoderTestData.Width, frame.Width);
        decoder.Dispose();
    }
}
