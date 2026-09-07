using Microsoft.Extensions.Time.Testing;
using ReceiverLib;

namespace ReceiverLib.Tests;

public class PlaybackClockTests
{
    private static (IPlaybackClock Clock, FakeTimeProvider Time) Create()
    {
        var time = new FakeTimeProvider();
        return (new PlaybackClock(time), time);
    }

    [Fact]
    public void WhileRunning_PositionAdvancesWithWallClock()
    {
        var (clock, time) = Create();
        clock.Start();
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(2), clock.Position);
    }

    [Fact]
    public void Pause_FreezesPosition_ResumeContinues()
    {
        var (clock, time) = Create();
        clock.Start();
        time.Advance(TimeSpan.FromSeconds(1));
        clock.Pause();
        time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(1), clock.Position);
        clock.Resume();
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(2), clock.Position);
    }

    [Fact]
    public void Reset_ZeroesAndStops()
    {
        var (clock, time) = Create();
        clock.Start();
        time.Advance(TimeSpan.FromSeconds(3));
        clock.Reset();
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.Zero, clock.Position);
    }
}
