using System.Buffers.Binary;

namespace Protocol;

/// <summary>
/// Codec/stream description the sender sends to a receiver so it can build a decoder without
/// touching the container. Layout: [codecId:4][width:4][height:4][extradataLength:4][extradata],
/// all little-endian. Carried in the UDP handshake (see <see cref="DatagramProtocol"/>).
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

/// <summary>Per-frame flags carried in the datagram fragment header.</summary>
[Flags]
public enum FrameFlags : byte
{
    None = 0,
    KeyFrame = 1 << 0,
}
