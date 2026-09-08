using SenderLib;
using SenderLib.Tests.Fakes;

namespace SenderLib.Tests;

public class VideoSenderTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static EncodedFrame Frame(double seconds, bool key = false) =>
        new(TimeSpan.FromSeconds(seconds), key, new byte[512]);

    private static VideoSender CreateSender(
        out FakeVideoSource source,
        out FakeVideoStreamServer server,
        out FakePlaybackClock clock,
        IEnumerable<EncodedFrame>? frames = null)
    {
        source = new FakeVideoSource(frames: frames);
        server = new FakeVideoStreamServer();
        clock = new FakePlaybackClock();
        var controller = new PlaybackController(
            new SenderConfiguration { ListenPort = 55555 }, source, server, clock);
        return new VideoSender(controller);
    }

    [Fact]
    public void NewSender_StartsIdle_WithNoVideoAndNoReceivers()
    {
        using var sender = CreateSender(out _, out _, out _);

        Assert.Equal(PlaybackState.Idle, sender.State);
        Assert.Null(sender.VideoInfo);
        Assert.Equal(TimeSpan.Zero, sender.Duration);
        Assert.Equal(0, sender.ReceiverCount);
    }

    [Fact]
    public void ReceiverConnected_OnTheStreamServer_BubblesThroughVideoSender()
    {
        using var sender = CreateSender(out _, out var server, out _);
        ReceiverConnectionEventArgs? received = null;
        sender.ReceiverConnected += (_, e) => received = e;

        var id = Guid.NewGuid();
        server.SimulateReceiverConnected(id);

        Assert.NotNull(received);
        Assert.Equal(id, received!.ReceiverId);
    }

    [Fact]
    public void Dispose_DisposesTheInjectedPipeline()
    {
        var sender = CreateSender(out var source, out var server, out _);

        sender.Dispose();

        Assert.True(source.IsDisposed);
        Assert.True(server.IsDisposed);
    }

    [Fact]
    public void Open_ForwardsToTheController_AndExposesVideoInfo()
    {
        using var sender = CreateSender(out var source, out _, out _);

        sender.Open("clip.mp4");

        Assert.Equal("clip.mp4", source.OpenedPath);
        Assert.Equal(PlaybackState.Ready, sender.State);
        Assert.Same(source.VideoInfo, sender.VideoInfo);
        Assert.Equal(source.VideoInfo.Duration, sender.Duration);
    }

    [Fact]
    public void StateChanged_BubblesThroughVideoSender()
    {
        using var sender = CreateSender(out _, out _, out _);
        var seen = new List<PlaybackState>();
        sender.StateChanged += (_, e) => seen.Add(e.NewState);

        sender.Open("clip.mp4");

        Assert.Contains(PlaybackState.Ready, seen);
    }

    [Fact]
    public void Start_StreamsThroughTheFacadeWithNoReceivers()
    {
        using var sender = CreateSender(
            out _, out var server, out var clock,
            frames: new[] { Frame(0, key: true), Frame(1, key: true), Frame(2, key: true) });

        var ended = new ManualResetEventSlim();
        sender.EndOfVideoReached += (_, _) => ended.Set();

        sender.Open("clip.mp4");
        clock.Position = TimeSpan.FromHours(1);
        sender.Start();

        Assert.True(ended.Wait(Timeout));
        Assert.Equal(PlaybackState.Ended, sender.State);
        Assert.Equal(3, server.BroadcastCount);
    }

    [Fact]
    public void StatisticsUpdated_BubblesThroughVideoSender_OnEndOfVideo()
    {
        using var sender = CreateSender(
            out _, out _, out var clock,
            frames: new[] { Frame(0, key: true), Frame(1, key: true) });

        StatisticsUpdatedEventArgs? stats = null;
        sender.StatisticsUpdated += (_, e) => stats = e;
        var ended = new ManualResetEventSlim();
        sender.EndOfVideoReached += (_, _) => ended.Set();

        sender.Open("clip.mp4");
        clock.Position = TimeSpan.FromHours(1);
        sender.Start();

        Assert.True(ended.Wait(Timeout));
        Assert.NotNull(stats);
        Assert.Equal(2, stats!.Statistics.FramesSent);
    }

    // End-to-end coverage of the default new VideoSender(config) pipeline over a real socket lives
    // in ReceiverLib.Tests/SenderToReceiverEndToEndTests (real sender -> real receiver, real file).
}
