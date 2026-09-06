using SenderLib;

namespace SenderLib.Tests.Fakes;

/// <summary>Manually-advanced <see cref="IPlaybackClock"/> so tests control playback time.</summary>
internal sealed class FakePlaybackClock : IPlaybackClock
{
    public TimeSpan Position { get; set; }

    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Pause() => IsRunning = false;

    public void Resume() => IsRunning = true;

    public void Reset()
    {
        IsRunning = false;
        Position = TimeSpan.Zero;
    }

    public void SeekTo(TimeSpan position) => Position = position;
}
