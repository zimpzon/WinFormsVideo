using System.Buffers.Binary;

namespace Protocol;

/// <summary>Kind of a UDP datagram (byte 5, after <see cref="DatagramProtocol.Magic"/> + version).</summary>
public enum DatagramType : byte
{
    /// <summary>Receiver → sender: "send me the stream". Also used as a keepalive.</summary>
    Subscribe = 1,

    /// <summary>Sender → receiver: the <see cref="StreamInfo"/> block, in reply to a subscribe.</summary>
    StreamInfo = 2,

    /// <summary>Sender → receiver: one fragment of a frame.</summary>
    FrameFragment = 3,

    /// <summary>Either direction: "I'm leaving / stopping".</summary>
    Bye = 4,
}

/// <summary>The per-fragment header inside a <see cref="DatagramType.FrameFragment"/> datagram.</summary>
/// <remarks>
/// Layout after the 6-byte datagram prefix:
/// [frameSequence:8][timestampTicks:8][flags:1][totalLength:4][fragmentIndex:2][fragmentCount:2][fragmentLength:2].
/// <c>totalLength</c>, <c>timestampTicks</c>, <c>flags</c> and <c>fragmentCount</c> are identical on every
/// fragment of a frame.
/// </remarks>
public readonly struct FragmentHeader
{
    public FragmentHeader(
        long frameSequence,
        long timestampTicks,
        FrameFlags flags,
        int totalLength,
        int fragmentIndex,
        int fragmentCount,
        int fragmentLength)
    {
        FrameSequence = frameSequence;
        TimestampTicks = timestampTicks;
        Flags = flags;
        TotalLength = totalLength;
        FragmentIndex = fragmentIndex;
        FragmentCount = fragmentCount;
        FragmentLength = fragmentLength;
    }

    public long FrameSequence { get; }

    public long TimestampTicks { get; }

    public FrameFlags Flags { get; }

    public bool IsKeyFrame => (Flags & FrameFlags.KeyFrame) != 0;

    /// <summary>Length of the whole reassembled frame payload.</summary>
    public int TotalLength { get; }

    public int FragmentIndex { get; }

    public int FragmentCount { get; }

    /// <summary>Bytes of frame payload carried by this fragment.</summary>
    public int FragmentLength { get; }
}

/// <summary>
/// Wire format for the UDP transport. All datagrams start with <see cref="Magic"/> (4) +
/// <see cref="Version"/> (1) + a <see cref="DatagramType"/> (1). Everything is little-endian and
/// every method is span-based / allocation-free.
/// </summary>
public static class DatagramProtocol
{
    /// <summary>Magic bytes at the start of every datagram ("WFV1").</summary>
    public static ReadOnlySpan<byte> Magic => "WFV1"u8;

    /// <summary>Protocol version byte.</summary>
    public const byte Version = 1;

    /// <summary>Upper bound on a reassembled frame payload, so a receiver can reject garbage.</summary>
    public const int MaxPayloadLength = 8 * 1024 * 1024;

    /// <summary>Bytes before the type-specific body: magic (4) + version (1) + type (1).</summary>
    public const int PrefixSize = 6;

    /// <summary>Serialized size of a <see cref="FragmentHeader"/>.</summary>
    public const int FragmentHeaderSize = 8 + 8 + 1 + 4 + 2 + 2 + 2;

    /// <summary>
    /// Max frame-payload bytes per fragment. A full datagram is
    /// <see cref="PrefixSize"/> + <see cref="FragmentHeaderSize"/> + this ≈ 1235 bytes — MTU-safe.
    /// </summary>
    public const int MaxFragmentPayload = 1200;

    /// <summary>Largest datagram the receiver needs to buffer.</summary>
    public const int MaxDatagramSize = PrefixSize + FragmentHeaderSize + MaxFragmentPayload;

    // --- writing ---------------------------------------------------------

    public static int WriteSubscribe(Span<byte> destination) => WriteHeaderOnly(destination, DatagramType.Subscribe);

    public static int WriteBye(Span<byte> destination) => WriteHeaderOnly(destination, DatagramType.Bye);

    public static int WriteStreamInfo(Span<byte> destination, in StreamInfo info)
    {
        int total = PrefixSize + info.SerializedSize;
        if (destination.Length < total || !info.TryWrite(destination[PrefixSize..], out _))
        {
            return 0;
        }

        WritePrefix(destination, DatagramType.StreamInfo);
        return total;
    }

    public static int WriteFrameFragment(
        Span<byte> destination,
        long frameSequence,
        long timestampTicks,
        FrameFlags flags,
        int totalLength,
        int fragmentIndex,
        int fragmentCount,
        ReadOnlySpan<byte> fragmentPayload)
    {
        int total = PrefixSize + FragmentHeaderSize + fragmentPayload.Length;
        if (destination.Length < total)
        {
            return 0;
        }

        WritePrefix(destination, DatagramType.FrameFragment);

        Span<byte> h = destination.Slice(PrefixSize, FragmentHeaderSize);
        BinaryPrimitives.WriteInt64LittleEndian(h[..8], frameSequence);
        BinaryPrimitives.WriteInt64LittleEndian(h.Slice(8, 8), timestampTicks);
        h[16] = (byte)flags;
        BinaryPrimitives.WriteInt32LittleEndian(h.Slice(17, 4), totalLength);
        BinaryPrimitives.WriteUInt16LittleEndian(h.Slice(21, 2), (ushort)fragmentIndex);
        BinaryPrimitives.WriteUInt16LittleEndian(h.Slice(23, 2), (ushort)fragmentCount);
        BinaryPrimitives.WriteUInt16LittleEndian(h.Slice(25, 2), (ushort)fragmentPayload.Length);

        fragmentPayload.CopyTo(destination[(PrefixSize + FragmentHeaderSize)..]);
        return total;
    }

    // --- reading --------------------------------------------------------

    public static bool TryReadType(ReadOnlySpan<byte> datagram, out DatagramType type)
    {
        type = default;
        if (datagram.Length < PrefixSize ||
            !datagram[..4].SequenceEqual(Magic) ||
            datagram[4] != Version)
        {
            return false;
        }

        type = (DatagramType)datagram[5];
        return true;
    }

    public static bool TryReadStreamInfo(ReadOnlySpan<byte> datagram, out StreamInfo info)
    {
        info = default;
        return TryReadType(datagram, out DatagramType type)
            && type == DatagramType.StreamInfo
            && StreamInfo.TryRead(datagram[PrefixSize..], out info);
    }

    public static bool TryReadFrameFragment(
        ReadOnlySpan<byte> datagram,
        out FragmentHeader header,
        out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;

        if (!TryReadType(datagram, out DatagramType type) || type != DatagramType.FrameFragment ||
            datagram.Length < PrefixSize + FragmentHeaderSize)
        {
            return false;
        }

        ReadOnlySpan<byte> h = datagram.Slice(PrefixSize, FragmentHeaderSize);
        int totalLength = BinaryPrimitives.ReadInt32LittleEndian(h.Slice(17, 4));
        int fragmentIndex = BinaryPrimitives.ReadUInt16LittleEndian(h.Slice(21, 2));
        int fragmentCount = BinaryPrimitives.ReadUInt16LittleEndian(h.Slice(23, 2));
        int fragmentLength = BinaryPrimitives.ReadUInt16LittleEndian(h.Slice(25, 2));

        if (totalLength < 0 || totalLength > MaxPayloadLength ||
            fragmentCount == 0 || fragmentIndex >= fragmentCount ||
            fragmentLength > MaxFragmentPayload ||
            datagram.Length < PrefixSize + FragmentHeaderSize + fragmentLength)
        {
            return false;
        }

        header = new FragmentHeader(
            BinaryPrimitives.ReadInt64LittleEndian(h[..8]),
            BinaryPrimitives.ReadInt64LittleEndian(h.Slice(8, 8)),
            (FrameFlags)h[16],
            totalLength,
            fragmentIndex,
            fragmentCount,
            fragmentLength);
        payload = datagram.Slice(PrefixSize + FragmentHeaderSize, fragmentLength);
        return true;
    }

    // --- helpers -------------------------------------------------------

    private static int WriteHeaderOnly(Span<byte> destination, DatagramType type)
    {
        if (destination.Length < PrefixSize)
        {
            return 0;
        }

        WritePrefix(destination, type);
        return PrefixSize;
    }

    private static void WritePrefix(Span<byte> destination, DatagramType type)
    {
        Magic.CopyTo(destination);
        destination[4] = Version;
        destination[5] = (byte)type;
    }
}
