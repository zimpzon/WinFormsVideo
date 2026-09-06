namespace SenderLib;

/// <summary>
/// Wire format shared by <see cref="TcpVideoStreamServer"/> and the receiver.
/// Each message on the stream is a <see cref="FrameHeader"/> followed by
/// <see cref="FrameHeader.PayloadLength"/> bytes of encoded frame data.
/// </summary>
internal static class StreamProtocol
{
    /// <summary>Magic bytes at the start of the stream ("WFV1").</summary>
    public static ReadOnlySpan<byte> Magic => "WFV1"u8;

    /// <summary>Protocol version negotiated at connect time.</summary>
    public const byte Version = 1;

    /// <summary>Serialized size of <see cref="FrameHeader"/> in bytes.</summary>
    public const int HeaderSize = 24;
}

/// <summary>Fixed-size header prefixing every frame on the wire.</summary>
internal readonly struct FrameHeader
{
    public FrameHeader(long sequenceNumber, long timestampTicks, FrameFlags flags, int payloadLength)
    {
        SequenceNumber = sequenceNumber;
        TimestampTicks = timestampTicks;
        Flags = flags;
        PayloadLength = payloadLength;
    }

    /// <summary>Monotonic frame counter, so the receiver can detect drops.</summary>
    public long SequenceNumber { get; }

    /// <summary>Presentation timestamp as <see cref="TimeSpan.Ticks"/>.</summary>
    public long TimestampTicks { get; }

    public FrameFlags Flags { get; }

    /// <summary>Number of encoded bytes that follow this header.</summary>
    public int PayloadLength { get; }
}

[Flags]
internal enum FrameFlags : byte
{
    None = 0,
    KeyFrame = 1 << 0,
}
