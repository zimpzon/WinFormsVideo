namespace SenderLib;

/// <summary>
/// High-level state of the sender's playback/streaming pipeline.
/// </summary>
public enum PlaybackState
{
    /// <summary>No video opened.</summary>
    Idle,

    /// <summary>A video file is being opened / probed.</summary>
    Opening,

    /// <summary>A video is opened and ready, but playback has not started.</summary>
    Ready,

    /// <summary>Playback clock is running and frames are being produced/sent.</summary>
    Playing,

    /// <summary>Playback is paused; the stream is held at the current position.</summary>
    Paused,

    /// <summary>Playback was explicitly stopped.</summary>
    Stopped,

    /// <summary>The end of the video was reached.</summary>
    Ended,

    /// <summary>An unrecoverable error occurred; see the reported <see cref="SenderError"/>.</summary>
    Faulted,
}
