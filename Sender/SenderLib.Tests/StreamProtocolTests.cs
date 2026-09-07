using Protocol;

namespace SenderLib.Tests;

public class StreamProtocolTests
{
    private static readonly StreamInfo SampleInfo =
        new(codecId: 27, width: 640, height: 480, extradata: new byte[] { 1, 2, 3, 4, 5 });

    [Fact]
    public void Handshake_RoundTrips_IncludingStreamInfo()
    {
        var buffer = new byte[StreamProtocol.HandshakeSize(SampleInfo)];

        Assert.True(StreamProtocol.TryWriteHandshake(buffer, SampleInfo));
        Assert.True(StreamProtocol.TryReadHandshake(buffer, out byte version, out StreamInfo info));

        Assert.Equal(StreamProtocol.Version, version);
        Assert.Equal(27, info.CodecId);
        Assert.Equal(640, info.Width);
        Assert.Equal(480, info.Height);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, info.Extradata.ToArray());
    }

    [Fact]
    public void Handshake_RoundTrips_WithEmptyExtradata()
    {
        var info = new StreamInfo(1, 320, 240, ReadOnlyMemory<byte>.Empty);
        var buffer = new byte[StreamProtocol.HandshakeSize(info)];

        Assert.True(StreamProtocol.TryWriteHandshake(buffer, info));
        Assert.True(StreamProtocol.TryReadHandshake(buffer, out _, out StreamInfo read));
        Assert.Equal(0, read.Extradata.Length);
    }

    [Fact]
    public void ReadHandshake_RejectsWrongMagic()
    {
        var buffer = new byte[StreamProtocol.HandshakeSize(SampleInfo)];
        StreamProtocol.TryWriteHandshake(buffer, SampleInfo);
        buffer[0] = (byte)'X';

        Assert.False(StreamProtocol.TryReadHandshake(buffer, out _, out _));
        Assert.False(StreamProtocol.TryReadHandshakeExtradataLength(buffer, out _));
    }

    [Fact]
    public void WriteHandshake_RejectsTooSmallBuffer()
    {
        Assert.False(StreamProtocol.TryWriteHandshake(
            new byte[StreamProtocol.HandshakeSize(SampleInfo) - 1], SampleInfo));
    }

    [Fact]
    public void ReadHandshakeExtradataLength_ReportsHowManyMoreBytesToRead()
    {
        var buffer = new byte[StreamProtocol.HandshakeSize(SampleInfo)];
        StreamProtocol.TryWriteHandshake(buffer, SampleInfo);

        Assert.True(StreamProtocol.TryReadHandshakeExtradataLength(
            buffer.AsSpan(0, StreamProtocol.HandshakePrefixSize), out int length));
        Assert.Equal(5, length);
    }

    [Fact]
    public void StreamInfo_RejectsOversizedExtradata()
    {
        var buffer = new byte[StreamProtocol.HandshakePrefixSize];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(5 + 12, 4), StreamInfo.MaxExtradataLength + 1);
        buffer[0] = (byte)'W';
        buffer[1] = (byte)'F';
        buffer[2] = (byte)'V';
        buffer[3] = (byte)'1';

        Assert.False(StreamProtocol.TryReadHandshakeExtradataLength(buffer, out _));
    }

    [Theory]
    [InlineData(0L, 0L, FrameFlags.None, 0)]
    [InlineData(42L, 123_456_789L, FrameFlags.KeyFrame, 1500)]
    [InlineData(long.MaxValue, long.MinValue, FrameFlags.KeyFrame, StreamProtocol.MaxPayloadLength)]
    public void FrameHeader_RoundTrips(long sequence, long ticks, FrameFlags flags, int payloadLength)
    {
        bool isKeyFrame = (flags & FrameFlags.KeyFrame) != 0;
        var header = new FrameHeader(sequence, ticks, flags, payloadLength);
        var buffer = new byte[StreamProtocol.HeaderSize];

        Assert.True(header.TryWrite(buffer));
        Assert.True(FrameHeader.TryRead(buffer, out FrameHeader read));

        Assert.Equal(sequence, read.SequenceNumber);
        Assert.Equal(ticks, read.TimestampTicks);
        Assert.Equal(flags, read.Flags);
        Assert.Equal(payloadLength, read.PayloadLength);
        Assert.Equal(isKeyFrame, read.IsKeyFrame);
    }

    [Fact]
    public void FrameHeader_WriteUsesExactlyHeaderSizeBytes()
    {
        var header = new FrameHeader(1, 2, FrameFlags.KeyFrame, 3);
        Assert.False(header.TryWrite(new byte[StreamProtocol.HeaderSize - 1]));
        Assert.True(header.TryWrite(new byte[StreamProtocol.HeaderSize]));
    }

    [Fact]
    public void ReadFrameHeader_RejectsTooShortSpan()
    {
        Assert.False(FrameHeader.TryRead(new byte[StreamProtocol.HeaderSize - 1], out _));
    }

    [Fact]
    public void ReadFrameHeader_RejectsOversizedPayloadLength()
    {
        var header = new FrameHeader(1, 2, FrameFlags.None, StreamProtocol.MaxPayloadLength);
        var buffer = new byte[StreamProtocol.HeaderSize];
        header.TryWrite(buffer);

        // bump the payload length field (last 4 bytes) past the max
        buffer[20] = 0xFF;
        buffer[21] = 0xFF;
        buffer[22] = 0xFF;
        buffer[23] = 0x7F;

        Assert.False(FrameHeader.TryRead(buffer, out _));
    }

    [Fact]
    public void ReadFrameHeader_RejectsNegativePayloadLength()
    {
        var buffer = new byte[StreamProtocol.HeaderSize];
        buffer[23] = 0x80; // sign bit of the little-endian int32

        Assert.False(FrameHeader.TryRead(buffer, out _));
    }
}
