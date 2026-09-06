namespace SenderLib;

/// <summary>
/// One compressed video access unit as read from the source and put on the wire.
/// The sender does not decode to pixels in the streaming-only design; it forwards
/// encoded frames paced against their presentation timestamps.
/// </summary>
public readonly struct EncodedFrame
{
    public EncodedFrame(TimeSpan timestamp, bool isKeyFrame, ReadOnlyMemory<byte> data)
    {
        Timestamp = timestamp;
        IsKeyFrame = isKeyFrame;
        Data = data;
    }

    /// <summary>Presentation timestamp relative to the start of the video.</summary>
    public TimeSpan Timestamp { get; }

    /// <summary>True if this frame can be decoded without any preceding frame.</summary>
    public bool IsKeyFrame { get; }

    /// <summary>The encoded payload. Ownership/lifetime is defined by the producing <see cref="IVideoSource"/>.</summary>
    public ReadOnlyMemory<byte> Data { get; }
}
