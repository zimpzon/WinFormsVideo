namespace ReceiverLib;

/// <summary>
/// A decoded frame checked out of a <see cref="FrameBufferPool"/> for display. Rather than carrying
/// pixels it names the pool buffer that holds them (<see cref="BufferIndex"/>), so the frontend can
/// draw the matching pre-built <c>Bitmap</c> wrapper with no copy. Valid until the next
/// <see cref="FrameBufferPool.TryAcquireFrame"/> call.
/// </summary>
public readonly struct RentedFrame
{
    internal RentedFrame(int bufferIndex, long sequenceNumber, TimeSpan timestamp)
    {
        BufferIndex = bufferIndex;
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;
    }

    /// <summary>Index into the <see cref="FrameBufferPool"/> of the buffer holding this frame.</summary>
    public int BufferIndex { get; }

    /// <summary>Sender-assigned sequence number of the frame.</summary>
    public long SequenceNumber { get; }

    /// <summary>Presentation timestamp relative to the start of the stream.</summary>
    public TimeSpan Timestamp { get; }
}
