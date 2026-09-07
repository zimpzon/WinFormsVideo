using Microsoft.Extensions.Time.Testing;
using SenderLib;

namespace SenderLib.Tests;

public class PlaybackClockTests
{
    private static (PlaybackClock Clock, FakeTimeProvider Time) Create()
    {
        var time = new FakeTimeProvider();
        return (new PlaybackClock(time), time);
    }

    [Fact]
    public void NewClock_IsAtZero()
    {
        var (clock, _) = Create();
        Assert.Equal(TimeSpan.Zero, clock.Position);
    }

    [Fact]
    public void WhileRunning_PositionAdvancesWithWallClock()
    {
        var (clock, time) = Create();
        clock.Start();

        time.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(3), clock.Position);
    }

    [Fact]
    public void Pause_FreezesPosition_ResumeContinues()
    {
        var (clock, time) = Create();
        clock.Start();
        time.Advance(TimeSpan.FromSeconds(2));

        clock.Pause();
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.FromSeconds(2), clock.Position);

        clock.Resume();
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(3), clock.Position);
    }

    [Fact]
    public void SeekTo_WhileRunning_RepositionsAndKeepsRunning()
    {
        var (clock, time) = Create();
        clock.Start();
        time.Advance(TimeSpan.FromSeconds(1));

        clock.SeekTo(TimeSpan.FromMinutes(1));
        time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(2), clock.Position);
    }

    [Fact]
    public void SeekTo_WhilePaused_RepositionsAndStaysPaused()
    {
        var (clock, time) = Create();
        clock.Start();
        clock.Pause();

        clock.SeekTo(TimeSpan.FromSeconds(30));
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(30), clock.Position);
    }

    [Fact]
    public void Reset_ZeroesAndStops()
    {
        var (clock, time) = Create();
        clock.Start();
        time.Advance(TimeSpan.FromSeconds(4));

        clock.Reset();
        time.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(TimeSpan.Zero, clock.Position);
    }

    [Fact]
    public void NegativeSeek_ClampsToZero()
    {
        var (clock, _) = Create();
        clock.SeekTo(TimeSpan.FromSeconds(-5));
        Assert.Equal(TimeSpan.Zero, clock.Position);
    }
}
