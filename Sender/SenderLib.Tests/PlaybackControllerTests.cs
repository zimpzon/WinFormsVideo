using System.Collections.Concurrent;
using SenderLib;
using SenderLib.Tests.Fakes;

namespace SenderLib.Tests;

public class PlaybackControllerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static EncodedFrame Frame(double seconds, bool key = false, int bytes = 1000) =>
        new(TimeSpan.FromSeconds(seconds), key, new byte[bytes]);

    private static PlaybackController Create(
        out FakeVideoSource source,
        out FakeVideoStreamServer server,
        out FakePlaybackClock clock,
        IEnumerable<EncodedFrame>? frames = null)
    {
        source = new FakeVideoSource(frames: frames);
        server = new FakeVideoStreamServer();
        clock = new FakePlaybackClock();
        return new PlaybackController(new SenderConfiguration { ListenPort = 5000 }, source, server, clock);
    }

    private static bool SpinUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(5);
        }

        return condition();
    }

    [Fact]
    public void Open_ProbesTheSource_StartsTheServer_AndBecomesReady()
    {
        using var controller = Create(out var source, out var server, out _);

        controller.Open("movie.mp4");

        Assert.Equal(PlaybackState.Ready, controller.State);
        Assert.Equal("movie.mp4", source.OpenedPath);
        Assert.Same(source.VideoInfo, controller.VideoInfo);
        Assert.True(server.IsRunning);
    }

    [Fact]
    public void Open_WhenTheSourceThrows_FaultsAndReportsTheError()
    {
        using var controller = Create(out var source, out _, out _);
        source.ThrowOnOpen = new FileNotFoundException("nope");
        SenderError? reported = null;
        controller.ErrorOccurred += (_, e) => reported = e.Error;

        controller.Open("missing.mp4");

        Assert.Equal(PlaybackState.Faulted, controller.State);
        Assert.NotNull(reported);
        Assert.Equal(SenderErrorKind.InvalidFile, reported!.Kind);
    }

    [Fact]
    public void Start_WithZeroReceivers_StillBroadcastsEveryFrameThenEnds()
    {
        using var controller = Create(
            out _, out var server, out var clock,
            frames: new[] { Frame(0, key: true), Frame(1, key: true), Frame(2, key: true) });
        server.ReceiverCount = 0;

        var ended = new ManualResetEventSlim();
        controller.EndOfVideoReached += (_, _) => ended.Set();

        controller.Open("m.mp4");
        clock.Position = TimeSpan.FromHours(1); // everything is due immediately
        controller.Start();

        Assert.True(ended.Wait(Timeout));
        Assert.Equal(PlaybackState.Ended, controller.State);
        Assert.Equal(3, server.BroadcastCount);
    }

    [Fact]
    public void FarBehind_NonKeyFramesAreDropped_KeyFramesKept()
    {
        using var controller = Create(
            out _, out var server, out var clock,
            frames: new[] { Frame(0, key: true), Frame(1), Frame(2), Frame(3, key: true) });

        var ended = new ManualResetEventSlim();
        controller.EndOfVideoReached += (_, _) => ended.Set();

        controller.Open("m.mp4");
        clock.Position = TimeSpan.FromHours(1);
        controller.Start();

        Assert.True(ended.Wait(Timeout));
        Assert.Equal(2, server.BroadcastCount); // the two keyframes
        Assert.All(server.Broadcasts, f => Assert.True(f.IsKeyFrame));
    }

    [Fact]
    public void Pause_HoldsTheStream_ResumeReleasesIt()
    {
        using var controller = Create(
            out _, out var server, out var clock,
            frames: new[] { Frame(0, key: true), Frame(1000, key: true) });
        clock.Position = TimeSpan.Zero;

        controller.Open("m.mp4");
        controller.Start();

        Assert.True(SpinUntil(() => server.BroadcastCount == 1)); // first frame due, second is far future
        controller.Pause();
        Assert.True(SpinUntil(() => controller.State == PlaybackState.Paused));
        Thread.Sleep(50);
        Assert.Equal(1, server.BroadcastCount);

        clock.Position = TimeSpan.FromHours(1);
        controller.Resume();

        Assert.True(SpinUntil(() => server.BroadcastCount == 2));
    }

    [Fact]
    public void Stop_ResetsTheClockAndRewindsTheSource()
    {
        using var controller = Create(
            out var source, out _, out var clock,
            frames: new[] { Frame(0, key: true), Frame(1000, key: true) });

        controller.Open("m.mp4");
        controller.Start();
        Assert.True(SpinUntil(() => source.SeekPositions.Count == 0 && controller.State == PlaybackState.Playing));

        controller.Stop();

        Assert.True(SpinUntil(() => controller.State == PlaybackState.Stopped));
        Assert.True(SpinUntil(() => source.SeekPositions.Contains(TimeSpan.Zero)));
        Assert.Equal(TimeSpan.Zero, clock.Position);
    }

    [Fact]
    public void Seek_RepositionsBothTheClockAndTheSource()
    {
        using var controller = Create(
            out var source, out _, out var clock,
            frames: new[] { Frame(0, key: true), Frame(1000, key: true) });
        clock.Position = TimeSpan.Zero;

        controller.Open("m.mp4");
        controller.Start();

        controller.Seek(TimeSpan.FromSeconds(42));

        Assert.True(SpinUntil(() => source.SeekPositions.Contains(TimeSpan.FromSeconds(42))));
        Assert.True(SpinUntil(() => clock.Position == TimeSpan.FromSeconds(42)));
        Assert.Equal(PlaybackState.Playing, controller.State);
    }

    [Fact]
    public void Restart_FromReady_GoesToPlayingFromZero()
    {
        using var controller = Create(
            out var source, out _, out _,
            frames: new[] { Frame(0, key: true) });

        controller.Open("m.mp4");
        controller.Restart();

        Assert.Equal(PlaybackState.Playing, controller.State);
        Assert.True(SpinUntil(() => source.SeekPositions.Contains(TimeSpan.Zero)));
    }

    [Fact]
    public void Start_BeforeOpen_IsANoOp()
    {
        using var controller = Create(out _, out _, out _);
        SenderError? reported = null;
        controller.ErrorOccurred += (_, e) => reported = e.Error;

        controller.Start();

        Assert.Equal(PlaybackState.Idle, controller.State);
        Assert.Null(reported); // no error — a control method in the wrong state is a silent no-op
    }

    [Fact]
    public void ControlMethods_AreSafeToCallRepeatedlyAndInAnyOrder_WithoutErrors()
    {
        using var controller = Create(
            out _, out _, out var clock,
            frames: new[] { Frame(0, key: true), Frame(1, key: true) });
        var errors = new ConcurrentQueue<SenderError>();
        controller.ErrorOccurred += (_, e) => errors.Enqueue(e.Error);

        // wrong-state calls before Open — all no-ops
        controller.Start();
        controller.Pause();
        controller.Resume();
        controller.Stop();
        controller.Restart();
        controller.Seek(TimeSpan.FromSeconds(1));

        controller.Open("m.mp4");
        controller.Open("m.mp4");   // re-open

        controller.Start();
        controller.Start();          // redundant
        controller.Pause();
        controller.Pause();          // redundant
        controller.Resume();
        controller.Resume();         // redundant
        controller.Stop();
        controller.Stop();           // redundant
        controller.Close();
        controller.Close();          // redundant

        Assert.Empty(errors);
    }

    [Fact]
    public void StateChanged_ReportsEachTransition()
    {
        using var controller = Create(
            out _, out _, out var clock,
            frames: new[] { Frame(0, key: true) });
        var transitions = new ConcurrentQueue<(PlaybackState From, PlaybackState To)>();
        controller.StateChanged += (_, e) => transitions.Enqueue((e.OldState, e.NewState));

        controller.Open("m.mp4");
        clock.Position = TimeSpan.FromHours(1);
        controller.Start();

        Assert.True(SpinUntil(() => transitions.Contains((PlaybackState.Playing, PlaybackState.Ended))));
        Assert.Contains((PlaybackState.Idle, PlaybackState.Ready), transitions);
        Assert.Contains((PlaybackState.Ready, PlaybackState.Playing), transitions);
    }

    [Fact]
    public void Close_StopsThePumpAndReturnsToIdle()
    {
        using var controller = Create(
            out var source, out var server, out _,
            frames: new[] { Frame(0, key: true), Frame(1000, key: true) });

        controller.Open("m.mp4");
        controller.Start();
        controller.Close();

        Assert.Equal(PlaybackState.Idle, controller.State);
        Assert.Null(controller.VideoInfo);
        Assert.False(server.IsRunning);
        Assert.Null(source.OpenedPath);
    }

    // Real-transport integration coverage lives in UdpVideoStreamServerTests (the server against a
    // loopback socket) and ReceiverLib.Tests/SenderToReceiverEndToEndTests (the whole real pipeline).
}
