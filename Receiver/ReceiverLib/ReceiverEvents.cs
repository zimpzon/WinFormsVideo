namespace ReceiverLib;

/// <summary>Raised when the receiver's <see cref="ReceiverState"/> changes.</summary>
public sealed class ReceiverStateChangedEventArgs : EventArgs
{
    public ReceiverStateChangedEventArgs(ReceiverState oldState, ReceiverState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public ReceiverState OldState { get; }

    public ReceiverState NewState { get; }
}

/// <summary>Raised periodically with a fresh <see cref="ReceiverStatistics"/> snapshot.</summary>
public sealed class StatisticsUpdatedEventArgs : EventArgs
{
    public StatisticsUpdatedEventArgs(ReceiverStatistics statistics)
    {
        Statistics = statistics;
    }

    public ReceiverStatistics Statistics { get; }
}

/// <summary>
/// Handler for a decoded frame. <paramref name="frame"/>'s <see cref="VideoFrame.Pixels"/> is
/// borrowed and only valid for the duration of the call — copy it if you need it later.
/// </summary>
public delegate void VideoFrameHandler(object? sender, in VideoFrame frame);

/// <summary>Raised when the receiver encounters a recoverable or unrecoverable error.</summary>
public sealed class ReceiverErrorEventArgs : EventArgs
{
    public ReceiverErrorEventArgs(ReceiverError error)
    {
        Error = error;
    }

    public ReceiverError Error { get; }
}
