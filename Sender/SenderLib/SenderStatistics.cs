namespace SenderLib;

/// <summary>
/// Point-in-time snapshot of sender/streaming statistics, pushed to the UI via
/// <see cref="VideoSender.StatisticsUpdated"/>.
/// </summary>
public sealed class SenderStatistics
{
    /// <summary>Frames read/decoded from the source since the video was opened.</summary>
    public long FramesDecoded { get; init; }

    /// <summary>Frames handed to the stream server for transmission.</summary>
    public long FramesSent { get; init; }

    /// <summary>Frames discarded because playback fell behind the clock.</summary>
    public long FramesDropped { get; init; }

    /// <summary>Current send rate in frames per second.</summary>
    public double CurrentFps { get; init; }

    /// <summary>Current outbound bitrate in bits per second.</summary>
    public double BitrateBitsPerSecond { get; init; }

    /// <summary>Current playback position.</summary>
    public TimeSpan PlaybackPosition { get; init; }

    /// <summary>Total duration of the opened video.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Number of receivers currently connected.</summary>
    public int ReceiverCount { get; init; }
}
