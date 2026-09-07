namespace ReceiverLib;

/// <summary>
/// Point-in-time snapshot of receiver statistics, pushed to the UI via
/// <see cref="VideoReceiver.StatisticsUpdated"/>.
/// </summary>
public sealed class ReceiverStatistics
{
    /// <summary>Packets arriving off the wire per second.</summary>
    public double ReceivedFps { get; init; }

    /// <summary>Frames successfully decoded per second.</summary>
    public double DecodedFps { get; init; }

    /// <summary>Frames discarded (dropped in transit, or too late to present).</summary>
    public long DroppedFrames { get; init; }

    /// <summary>Inbound bitrate in bits per second.</summary>
    public double NetworkBitrateBitsPerSecond { get; init; }

    /// <summary>End-to-end latency, if it can be measured.</summary>
    public TimeSpan? Latency { get; init; }

    /// <summary>Number of packets currently buffered awaiting decode/presentation.</summary>
    public int QueueDepth { get; init; }

    /// <summary>Current connection state.</summary>
    public ReceiverState ConnectionState { get; init; }

    /// <summary>Decoded video resolution, if known.</summary>
    public (int Width, int Height)? Resolution { get; init; }

    /// <summary>Codec short name, if known.</summary>
    public string? CodecName { get; init; }
}
