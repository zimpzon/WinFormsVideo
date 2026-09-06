namespace SenderLib;

/// <summary>
/// Tracks playback time so frames can be paced against their presentation timestamps
/// instead of being sent as fast as they decode.
/// </summary>
public interface IPlaybackClock
{
    /// <summary>Current playback position.</summary>
    TimeSpan Position { get; }

    /// <summary>Start the clock from the current position.</summary>
    void Start();

    /// <summary>Freeze the clock at the current position.</summary>
    void Pause();

    /// <summary>Resume advancing from where <see cref="Pause"/> stopped.</summary>
    void Resume();

    /// <summary>Stop and reset the position to zero.</summary>
    void Reset();

    /// <summary>Jump the clock to <paramref name="position"/>.</summary>
    void SeekTo(TimeSpan position);
}
