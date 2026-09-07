using System.Net.Sockets;
using Protocol;

namespace SenderLib.Tests;

/// <summary>Helpers for reading the <see cref="StreamProtocol"/> stream from a raw client socket.</summary>
internal static class TcpTestIo
{
    public static void ReadExact(Stream stream, byte[] buffer, int count)
    {
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n <= 0)
            {
                throw new EndOfStreamException();
            }

            read += n;
        }
    }

    public static (byte Version, StreamInfo Info) ReadHandshake(Stream stream)
    {
        var prefix = new byte[StreamProtocol.HandshakePrefixSize];
        ReadExact(stream, prefix, prefix.Length);
        Assert.True(StreamProtocol.TryReadHandshakeExtradataLength(prefix, out int extradataLength));

        var full = new byte[StreamProtocol.HandshakePrefixSize + extradataLength];
        prefix.CopyTo(full, 0);
        if (extradataLength > 0)
        {
            var extradata = new byte[extradataLength];
            ReadExact(stream, extradata, extradata.Length);
            extradata.CopyTo(full, StreamProtocol.HandshakePrefixSize);
        }

        Assert.True(StreamProtocol.TryReadHandshake(full, out byte version, out StreamInfo info));
        return (version, info);
    }

    public static (FrameHeader Header, byte[] Payload) ReadFrame(Stream stream)
    {
        var headerBuffer = new byte[StreamProtocol.HeaderSize];
        ReadExact(stream, headerBuffer, headerBuffer.Length);
        Assert.True(FrameHeader.TryRead(headerBuffer, out FrameHeader header));

        var payload = new byte[header.PayloadLength];
        ReadExact(stream, payload, payload.Length);
        return (header, payload);
    }

    public static byte[] Bytes(byte fill, int size)
    {
        var data = new byte[size];
        Array.Fill(data, fill);
        return data;
    }
}
