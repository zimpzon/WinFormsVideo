namespace SenderLib;

/// <summary>Raised when the sender's <see cref="PlaybackState"/> changes.</summary>
public sealed class PlaybackStateChangedEventArgs : EventArgs
{
    public PlaybackStateChangedEventArgs(PlaybackState oldState, PlaybackState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public PlaybackState OldState { get; }

    public PlaybackState NewState { get; }
}
