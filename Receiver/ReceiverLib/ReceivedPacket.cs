namespace ReceiverLib;

/// <summary>
/// One compressed video access unit pulled off the wire, before decoding. The receive-side
/// counterpart of the sender's <c>EncodedFrame</c>.
/// </summary>
public readonly struct ReceivedPacket
{
    public ReceivedPacket(long sequenceNumber, TimeSpan timestamp, bool isKeyFrame, ReadOnlyMemory<byte> data)
    {
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;
        IsKeyFrame = isKeyFrame;
        Data = data;
    }

    /// <summary>Server-assigned sequence number; gaps indicate frames dropped in transit.</summary>
    public long SequenceNumber { get; }

    /// <summary>Presentation timestamp relative to the start of the stream.</summary>
    public TimeSpan Timestamp { get; }

    public bool IsKeyFrame { get; }

    /// <summary>
    /// The encoded payload, borrowed from a buffer the producing <see cref="IVideoClient"/> reuses —
    /// only valid until the next <c>TryReadPacket</c>. Copy to keep it.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }
}
