namespace SenderLib;

/// <summary>
/// Default <see cref="IPlaybackClock"/>. Will maintain a monotonic time base
/// (e.g. from <see cref="System.Diagnostics.Stopwatch"/>) offset by the current
/// seek position, so the pipeline can wait until each frame's timestamp is due.
/// </summary>
internal sealed class PlaybackClock : IPlaybackClock
{
    public TimeSpan Position => throw new NotImplementedException();

    public void Start() => throw new NotImplementedException();

    public void Pause() => throw new NotImplementedException();

    public void Resume() => throw new NotImplementedException();

    public void Reset() => throw new NotImplementedException();

    public void SeekTo(TimeSpan position) => throw new NotImplementedException();
}
