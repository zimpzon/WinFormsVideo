using System.Buffers.Binary;
using Protocol;

namespace SenderLib.Tests;

public class StreamInfoTests
{
    [Fact]
    public void RoundTrips_WithExtradata()
    {
        var info = new StreamInfo(codecId: 27, width: 640, height: 480, extradata: new byte[] { 1, 2, 3, 4, 5 });
        var buffer = new byte[info.SerializedSize];

        Assert.True(info.TryWrite(buffer, out int written));
        Assert.Equal(buffer.Length, written);
        Assert.True(StreamInfo.TryRead(buffer, out StreamInfo read));

        Assert.Equal(27, read.CodecId);
        Assert.Equal(640, read.Width);
        Assert.Equal(480, read.Height);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, read.Extradata.ToArray());
    }

    [Fact]
    public void RoundTrips_WithEmptyExtradata()
    {
        var info = new StreamInfo(1, 320, 240, ReadOnlyMemory<byte>.Empty);
        var buffer = new byte[info.SerializedSize];

        Assert.True(info.TryWrite(buffer, out _));
        Assert.True(StreamInfo.TryRead(buffer, out StreamInfo read));
        Assert.Equal(0, read.Extradata.Length);
    }

    [Fact]
    public void TryWrite_RejectsTooSmallBuffer()
    {
        var info = new StreamInfo(1, 2, 3, new byte[] { 9 });
        Assert.False(info.TryWrite(new byte[info.SerializedSize - 1], out _));
    }

    [Fact]
    public void TryReadExtradataLength_RejectsOversizedLength()
    {
        var fixedFields = new byte[StreamInfo.FixedSize];
        BinaryPrimitives.WriteInt32LittleEndian(fixedFields.AsSpan(12, 4), StreamInfo.MaxExtradataLength + 1);

        Assert.False(StreamInfo.TryReadExtradataLength(fixedFields, out _));
    }

    [Fact]
    public void TryRead_RejectsTruncatedSpan()
    {
        var info = new StreamInfo(1, 2, 3, new byte[] { 4, 5, 6 });
        var buffer = new byte[info.SerializedSize];
        info.TryWrite(buffer, out _);

        Assert.False(StreamInfo.TryRead(buffer.AsSpan(0, buffer.Length - 1), out _));
    }
}
