using Protocol;

namespace SenderLib.Tests;

public class DatagramProtocolTests
{
    [Fact]
    public void Subscribe_And_Bye_RoundTrip()
    {
        var buffer = new byte[DatagramProtocol.PrefixSize];

        Assert.Equal(DatagramProtocol.PrefixSize, DatagramProtocol.WriteSubscribe(buffer));
        Assert.True(DatagramProtocol.TryReadType(buffer, out DatagramType t1));
        Assert.Equal(DatagramType.Subscribe, t1);

        DatagramProtocol.WriteBye(buffer);
        Assert.True(DatagramProtocol.TryReadType(buffer, out DatagramType t2));
        Assert.Equal(DatagramType.Bye, t2);
    }

    [Fact]
    public void StreamInfo_RoundTrips()
    {
        var info = new StreamInfo(27, 1920, 1080, new byte[] { 1, 2, 3, 4, 5 });
        var buffer = new byte[DatagramProtocol.PrefixSize + info.SerializedSize];

        int len = DatagramProtocol.WriteStreamInfo(buffer, info);
        Assert.Equal(buffer.Length, len);
        Assert.True(DatagramProtocol.TryReadStreamInfo(buffer, out StreamInfo read));
        Assert.Equal(27, read.CodecId);
        Assert.Equal(1920, read.Width);
        Assert.Equal(1080, read.Height);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, read.Extradata.ToArray());
    }

    [Fact]
    public void FrameFragment_RoundTrips_ByteExact()
    {
        var payload = new byte[900];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)i;
        }

        var buffer = new byte[DatagramProtocol.MaxDatagramSize];
        int len = DatagramProtocol.WriteFrameFragment(
            buffer, frameSequence: 42, timestampTicks: 123456, FrameFlags.KeyFrame,
            totalLength: 5000, fragmentIndex: 3, fragmentCount: 5, payload);

        Assert.Equal(DatagramProtocol.PrefixSize + DatagramProtocol.FragmentHeaderSize + payload.Length, len);

        Assert.True(DatagramProtocol.TryReadFrameFragment(
            buffer.AsSpan(0, len), out FragmentHeader h, out ReadOnlySpan<byte> got));

        Assert.Equal(42, h.FrameSequence);
        Assert.Equal(123456, h.TimestampTicks);
        Assert.True(h.IsKeyFrame);
        Assert.Equal(5000, h.TotalLength);
        Assert.Equal(3, h.FragmentIndex);
        Assert.Equal(5, h.FragmentCount);
        Assert.Equal(900, h.FragmentLength);
        Assert.True(got.SequenceEqual(payload));
    }

    [Fact]
    public void TryReadType_RejectsBadMagicOrVersion()
    {
        var buffer = new byte[DatagramProtocol.PrefixSize];
        DatagramProtocol.WriteSubscribe(buffer);

        buffer[0] = (byte)'X';
        Assert.False(DatagramProtocol.TryReadType(buffer, out _));

        DatagramProtocol.WriteSubscribe(buffer);
        buffer[4] = 99; // wrong version
        Assert.False(DatagramProtocol.TryReadType(buffer, out _));
    }

    [Fact]
    public void TryReadFrameFragment_RejectsIndexBeyondCount()
    {
        var buffer = new byte[DatagramProtocol.MaxDatagramSize];
        int len = DatagramProtocol.WriteFrameFragment(
            buffer, 1, 0, FrameFlags.None, 100, fragmentIndex: 3, fragmentCount: 3, new byte[10]);

        Assert.False(DatagramProtocol.TryReadFrameFragment(buffer.AsSpan(0, len), out _, out _));
    }

    [Fact]
    public void TryReadFrameFragment_RejectsTruncatedDatagram()
    {
        var buffer = new byte[DatagramProtocol.MaxDatagramSize];
        int len = DatagramProtocol.WriteFrameFragment(buffer, 1, 0, FrameFlags.None, 100, 0, 1, new byte[50]);

        Assert.False(DatagramProtocol.TryReadFrameFragment(buffer.AsSpan(0, len - 10), out _, out _));
    }
}
