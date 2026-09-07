using System.Buffers.Binary;

namespace Protocol;

/// <summary>
/// Wire format shared by the sender's TCP stream server and the receiver's TCP client.
/// On connect the server writes a handshake: <see cref="Magic"/> (4) + <see cref="Version"/> (1) +
/// a <see cref="StreamInfo"/> block (<see cref="StreamInfo.FixedSize"/> bytes + extradata). After
/// that the stream is a sequence of [<see cref="FrameHeader"/>][<see cref="FrameHeader.PayloadLength"/> bytes].
/// All multi-byte fields are little-endian.
/// </summary>
public static class StreamProtocol
{
    /// <summary>Magic bytes at the start of the stream ("WFV1").</summary>
    public static ReadOnlySpan<byte> Magic => "WFV1"u8;

    /// <summary>Protocol version, sent in the handshake.</summary>
    public const byte Version = 1;

    /// <summary>
    /// Size of the fixed part of the handshake (<see cref="Magic"/> + <see cref="Version"/> +
    /// the <see cref="StreamInfo"/> fixed fields) — read this many bytes first, then the extradata.
    /// </summary>
    public const int HandshakePrefixSize = 4 + 1 + StreamInfo.FixedSize;

    /// <summary>Serialized size of <see cref="FrameHeader"/> in bytes.</summary>
    public const int HeaderSize = 24;

    /// <summary>Upper bound on a single frame payload, so a receiver can reject garbage.</summary>
    public const int MaxPayloadLength = 8 * 1024 * 1024;

    /// <summary>Total bytes the handshake for <paramref name="info"/> occupies.</summary>
    public static int HandshakeSize(in StreamInfo info) => HandshakePrefixSize + info.Extradata.Length;

    public static bool TryWriteHandshake(Span<byte> destination, in StreamInfo info)
    {
        if (destination.Length < HandshakeSize(info))
        {
            return false;
        }

        Magic.CopyTo(destination);
        destination[4] = Version;
        return info.TryWrite(destination[5..], out _);
    }

    /// <summary>
    /// From the first <see cref="HandshakePrefixSize"/> bytes, validate the magic and read how many
    /// extradata bytes still need to be read off the socket.
    /// </summary>
    public static bool TryReadHandshakeExtradataLength(ReadOnlySpan<byte> prefix, out int extradataLength)
    {
        extradataLength = 0;
        if (prefix.Length < HandshakePrefixSize || !prefix[..4].SequenceEqual(Magic))
        {
            return false;
        }

        return StreamInfo.TryReadExtradataLength(prefix[5..], out extradataLength);
    }

    /// <summary>Parse the whole handshake (prefix + extradata) once all the bytes are in hand.</summary>
    public static bool TryReadHandshake(ReadOnlySpan<byte> source, out byte version, out StreamInfo info)
    {
        version = 0;
        info = default;
        if (source.Length < HandshakePrefixSize || !source[..4].SequenceEqual(Magic))
        {
            return false;
        }

        version = source[4];
        return StreamInfo.TryRead(source[5..], out info);
    }
}

/// <summary>
/// Codec/stream description the sender sends in the handshake so a receiver can build a decoder
/// without touching the container. Layout: [codecId:4][width:4][height:4][extradataLength:4][extradata].
/// </summary>
public readonly struct StreamInfo
{
    /// <summary>Serialized size of the fixed fields (everything except the extradata bytes).</summary>
    public const int FixedSize = 16;

    /// <summary>Upper bound on the extradata block.</summary>
    public const int MaxExtradataLength = 64 * 1024;

    public StreamInfo(int codecId, int width, int height, ReadOnlyMemory<byte> extradata)
    {
        CodecId = codecId;
        Width = width;
        Height = height;
        Extradata = extradata;
    }

    /// <summary>FFmpeg <c>AVCodecID</c> value.</summary>
    public int CodecId { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Codec init data (the container's <c>extradata</c> / avcC parameter sets). May be empty.</summary>
    public ReadOnlyMemory<byte> Extradata { get; }

    public int SerializedSize => FixedSize + Extradata.Length;

    public bool TryWrite(Span<byte> destination, out int written)
    {
        written = 0;
        if (destination.Length < SerializedSize)
        {
            return false;
        }

        BinaryPrimitives.WriteInt32LittleEndian(destination[..4], CodecId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4, 4), Width);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(8, 4), Height);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(12, 4), Extradata.Length);
        Extradata.Span.CopyTo(destination[FixedSize..]);
        written = SerializedSize;
        return true;
    }

    public static bool TryReadExtradataLength(ReadOnlySpan<byte> fixedFields, out int extradataLength)
    {
        extradataLength = 0;
        if (fixedFields.Length < FixedSize)
        {
            return false;
        }

        int length = BinaryPrimitives.ReadInt32LittleEndian(fixedFields.Slice(12, 4));
        if (length < 0 || length > MaxExtradataLength)
        {
            return false;
        }

        extradataLength = length;
        return true;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out StreamInfo info)
    {
        info = default;
        if (!TryReadExtradataLength(source, out int extradataLength) ||
            source.Length < FixedSize + extradataLength)
        {
            return false;
        }

        info = new StreamInfo(
            BinaryPrimitives.ReadInt32LittleEndian(source[..4]),
            BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(source.Slice(8, 4)),
            source.Slice(FixedSize, extradataLength).ToArray());
        return true;
    }
}

/// <summary>Fixed-size header prefixing every frame on the wire.</summary>
/// <remarks>Layout: [sequenceNumber:8][timestampTicks:8][flags:1][reserved:3][payloadLength:4].</remarks>
public readonly struct FrameHeader
{
    public FrameHeader(long sequenceNumber, long timestampTicks, FrameFlags flags, int payloadLength)
    {
        SequenceNumber = sequenceNumber;
        TimestampTicks = timestampTicks;
        Flags = flags;
        PayloadLength = payloadLength;
    }

    /// <summary>Monotonic, server-wide frame counter so the receiver can detect drops.</summary>
    public long SequenceNumber { get; }

    /// <summary>Presentation timestamp as <see cref="TimeSpan.Ticks"/>.</summary>
    public long TimestampTicks { get; }

    public FrameFlags Flags { get; }

    /// <summary>Number of encoded bytes that follow this header.</summary>
    public int PayloadLength { get; }

    public bool IsKeyFrame => (Flags & FrameFlags.KeyFrame) != 0;

    public bool TryWrite(Span<byte> destination)
    {
        if (destination.Length < StreamProtocol.HeaderSize)
        {
            return false;
        }

        BinaryPrimitives.WriteInt64LittleEndian(destination[..8], SequenceNumber);
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(8, 8), TimestampTicks);
        destination[16] = (byte)Flags;
        destination[17] = 0;
        destination[18] = 0;
        destination[19] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(20, 4), PayloadLength);
        return true;
    }

    public static bool TryRead(ReadOnlySpan<byte> source, out FrameHeader header)
    {
        header = default;
        if (source.Length < StreamProtocol.HeaderSize)
        {
            return false;
        }

        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(20, 4));
        if (payloadLength < 0 || payloadLength > StreamProtocol.MaxPayloadLength)
        {
            return false;
        }

        header = new FrameHeader(
            BinaryPrimitives.ReadInt64LittleEndian(source[..8]),
            BinaryPrimitives.ReadInt64LittleEndian(source.Slice(8, 8)),
            (FrameFlags)source[16],
            payloadLength);
        return true;
    }
}

[Flags]
public enum FrameFlags : byte
{
    None = 0,
    KeyFrame = 1 << 0,
}
