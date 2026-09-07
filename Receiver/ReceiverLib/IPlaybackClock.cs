namespace ReceiverLib;

/// <summary>
/// Tracks playback time so decoded frames can be presented on schedule instead of as fast as they
/// decode.
/// </summary>
// TODO: identical in shape to SenderLib.IPlaybackClock — move both to the Protocol project (or a
// new shared timing project) when the receive orchestration increment needs it.
public interface IPlaybackClock
{
    TimeSpan Position { get; }

    void Start();

    void Pause();

    void Resume();

    void Reset();

    void SeekTo(TimeSpan position);
}
