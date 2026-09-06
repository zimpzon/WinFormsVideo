using SenderLib;
using SenderLib.Tests.Fakes;

namespace SenderLib.Tests;

public class VideoSenderTests
{
    private static VideoSender CreateSender(
        out FakeVideoSource source,
        out FakeVideoStreamServer server,
        out FakePlaybackClock clock)
    {
        var configuration = new SenderConfiguration { ListenPort = 55555 };
        source = new FakeVideoSource();
        server = new FakeVideoStreamServer();
        clock = new FakePlaybackClock();
        var controller = new PlaybackController(configuration, source, server, clock);
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

    // Behaviours to cover once the pipeline logic is implemented:
    [Fact(Skip = "Pipeline logic not implemented yet")]
    public void Start_WithNoReceivers_StillAdvancesPlaybackAndBroadcasts()
    {
    }

    [Fact(Skip = "Pipeline logic not implemented yet")]
    public void Playback_SendsFramesPacedToTheirPresentationTimestamps()
    {
    }

    [Fact(Skip = "Pipeline logic not implemented yet")]
    public void Seek_RepositionsTheClockAndResumesFromTheNearestKeyframe()
    {
    }

    [Fact(Skip = "Pipeline logic not implemented yet")]
    public void LateReceiver_ConnectingMidStream_ReceivesFromCurrentPosition()
    {
    }
}
