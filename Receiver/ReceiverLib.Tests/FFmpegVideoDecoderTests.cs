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
    public void OutputBuffers_ArePublished_AndAcquirableWithoutCopying()
    {
        using var decoder = new FFmpegVideoDecoder();
        Assert.Null(decoder.OutputBuffers);

        decoder.Configure(DecoderTestData.StreamInfo);

        FrameBufferPool? pool = decoder.OutputBuffers;
        Assert.NotNull(pool);
        Assert.Equal(DecoderTestData.Width, pool!.Width);
        Assert.Equal(DecoderTestData.Height, pool.Height);
        Assert.Equal(3, pool.Count);

        long acquired = 0;
        foreach (ReceivedPacket packet in DecoderTestData.Packets)
        {
            if (!decoder.TryDecode(packet, out VideoFrame frame) || !pool.TryAcquireFrame(out RentedFrame rented))
            {
                continue;
            }

            // The rented buffer is the very memory the decoder scaled into — no copy in between.
            Assert.True(System.Runtime.InteropServices.MemoryMarshal.TryGetArray(frame.Pixels, out ArraySegment<byte> segment));
            IntPtr framePixelsAddress = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(
                segment.Array!, segment.Offset);
            Assert.Equal(pool.BufferAddress(rented.BufferIndex), framePixelsAddress);
            Assert.Equal(frame.Timestamp, rented.Timestamp);
            acquired++;
        }

        Assert.True(acquired > 0);
        Assert.Equal(acquired, pool.PresentedCount);
    }

    [Fact]
    public void Configure_AtTheSameResolution_KeepsTheSameBuffers()
    {
        using var decoder = new FFmpegVideoDecoder();
        decoder.Configure(DecoderTestData.StreamInfo);
        FrameBufferPool first = decoder.OutputBuffers!;
        IntPtr firstAddress = first.BufferAddress(0);
        int firstGeneration = first.Generation;

        decoder.Configure(DecoderTestData.StreamInfo);

        Assert.Same(first, decoder.OutputBuffers);
        Assert.Equal(firstAddress, decoder.OutputBuffers!.BufferAddress(0));
        Assert.Equal(firstGeneration, decoder.OutputBuffers.Generation);
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
