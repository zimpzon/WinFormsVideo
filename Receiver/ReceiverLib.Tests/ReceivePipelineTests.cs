using System.Collections.Concurrent;
using System.Net.Sockets;
using ReceiverLib;
using ReceiverLib.Tests.Fakes;

namespace ReceiverLib.Tests;

public class ReceivePipelineTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static ReceivedPacket Packet(long seq, double seconds, bool key = true) =>
        new(seq, TimeSpan.FromSeconds(seconds), key, new byte[16]);

    private static ReceivePipeline Create(
        FakeVideoClient client,
        out FakeVideoDecoder decoder,
        out FakePlaybackClock clock)
    {
        decoder = new FakeVideoDecoder();
        clock = new FakePlaybackClock();
        return new ReceivePipeline(new ReceiverConfiguration { SenderPort = 5000 }, client, decoder, clock);
    }

    private static bool SpinUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + Timeout;
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
    public void Connect_ConfiguresTheDecoder_ReportsVideoInfo_PresentsEveryFrame_ThenStops()
    {
        var client = new FakeVideoClient(packets: new[] { Packet(0, 0), Packet(1, 0.1), Packet(2, 0.2) });
        using ReceivePipeline pipeline = Create(client, out FakeVideoDecoder decoder, out _);
        var frames = new ConcurrentQueue<VideoFrame>();
        pipeline.FrameReady += (object? _, in VideoFrame f) => frames.Enqueue(f);

        pipeline.Connect();

        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Stopped));
        Assert.Equal(3, frames.Count);
        Assert.NotNull(decoder.ConfiguredWith);
        Assert.Equal(320, pipeline.VideoInfo!.Width);
        Assert.Equal("fake", pipeline.VideoInfo!.CodecName);

        TimeSpan previous = TimeSpan.MinValue;
        foreach (VideoFrame f in frames)
        {
            Assert.True(f.Timestamp >= previous);
            previous = f.Timestamp;
        }
    }

    [Fact]
    public void FramesThatAreTooLate_AreDropped_NotPresented()
    {
        var client = new FakeVideoClient(packets: new[] { Packet(0, 0) }, blockWhenEmpty: true);
        using ReceivePipeline pipeline = Create(client, out _, out FakePlaybackClock clock);
        var frames = new ConcurrentQueue<VideoFrame>();
        pipeline.FrameReady += (object? _, in VideoFrame f) => frames.Enqueue(f);

        pipeline.Connect();
        Assert.True(SpinUntil(() => frames.Count == 1)); // baseline established at ts 0

        clock.Position = TimeSpan.FromSeconds(10); // playback is now way ahead
        client.Push(Packet(1, 0.1), Packet(2, 0.2));
        client.EndStream();

        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Stopped));
        Assert.Single(frames);
        Assert.True(pipeline.Statistics.DroppedFrames >= 2);
    }

    [Fact]
    public void Pause_HoldsPresentation_Resume_JumpsBackToLive()
    {
        var client = new FakeVideoClient(packets: new[] { Packet(0, 0) }, blockWhenEmpty: true);
        using ReceivePipeline pipeline = Create(client, out _, out _);
        var frames = new ConcurrentQueue<VideoFrame>();
        pipeline.FrameReady += (object? _, in VideoFrame f) => frames.Enqueue(f);

        pipeline.Connect();
        Assert.True(SpinUntil(() => frames.Count == 1 && pipeline.State == ReceiverState.Playing));

        pipeline.Pause();
        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Paused));
        client.Push(Packet(1, 1), Packet(2, 2));
        Thread.Sleep(80);
        Assert.Single(frames); // nothing presented while paused

        pipeline.Resume();
        client.Push(Packet(3, 3));
        Assert.True(SpinUntil(() => frames.Count == 2));
    }

    [Fact]
    public void Connect_WhenNoSenderIsReachable_Faults_WithConnectionFailed()
    {
        // 10054 = "An existing connection was forcibly closed" — what a dead UDP port produces on Windows
        var client = new FakeVideoClient { ThrowOnConnect = new SocketException(10054) };
        using ReceivePipeline pipeline = Create(client, out _, out _);
        ReceiverError? error = null;
        pipeline.ErrorOccurred += (_, e) => error = e.Error;

        pipeline.Connect();

        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Faulted));
        Assert.NotNull(error);
        Assert.Equal(ReceiverErrorKind.ConnectionFailed, error!.Kind);
        Assert.DoesNotContain("forcibly closed", error.Message); // the raw OS text is not surfaced
        Assert.IsType<SocketException>(error.Exception);         // ...but it's kept for logging
    }

    [Fact]
    public void MidStreamFailure_AfterConnecting_ReportsConnectionLost_NotConnectionFailed()
    {
        var client = new FakeVideoClient(packets: new[] { Packet(0, 0) }, blockWhenEmpty: true);
        using ReceivePipeline pipeline = Create(client, out _, out _);
        ReceiverError? error = null;
        pipeline.ErrorOccurred += (_, e) => error = e.Error;

        pipeline.Connect();
        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Playing));

        client.FailRead(new SocketException(10054));

        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Faulted));
        Assert.Equal(ReceiverErrorKind.ConnectionLost, error!.Kind);
    }

    [Fact]
    public void Disconnect_StopsThePump_AndReturnsToStopped()
    {
        var client = new FakeVideoClient(packets: new[] { Packet(0, 0) }, blockWhenEmpty: true);
        using ReceivePipeline pipeline = Create(client, out _, out _);

        pipeline.Connect();
        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Playing));

        pipeline.Disconnect();

        Assert.Equal(ReceiverState.Stopped, pipeline.State);
        Assert.Null(pipeline.VideoInfo);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Connect_WhileAlreadyConnected_IsANoOp()
    {
        var client = new FakeVideoClient(packets: new[] { Packet(0, 0) }, blockWhenEmpty: true);
        using ReceivePipeline pipeline = Create(client, out _, out _);
        pipeline.Connect();
        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Playing));

        ReceiverError? error = null;
        var stateChanges = 0;
        pipeline.ErrorOccurred += (_, e) => error = e.Error;
        pipeline.StateChanged += (_, _) => Interlocked.Increment(ref stateChanges);

        pipeline.Connect(); // redundant
        pipeline.Connect(); // redundant
        Thread.Sleep(50);

        Assert.Null(error); // no error — redundant Connect is a silent no-op
        Assert.Equal(0, Volatile.Read(ref stateChanges));
        Assert.Equal(ReceiverState.Playing, pipeline.State);
    }

    [Fact]
    public void ControlMethods_AreSafeToCallRepeatedlyAndInAnyOrder_WithoutErrors()
    {
        var client = new FakeVideoClient(packets: new[] { Packet(0, 0) }, blockWhenEmpty: true);
        using ReceivePipeline pipeline = Create(client, out _, out _);
        var errors = new ConcurrentQueue<ReceiverError>();
        pipeline.ErrorOccurred += (_, e) => errors.Enqueue(e.Error);

        // wrong-state calls before Connect — all no-ops
        pipeline.Pause();
        pipeline.Resume();
        pipeline.Disconnect();
        Assert.Equal(ReceiverState.Idle, pipeline.State);

        pipeline.Connect();
        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Playing));

        pipeline.Pause();
        pipeline.Pause();          // redundant
        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Paused));
        pipeline.Resume();
        pipeline.Resume();         // redundant
        client.Push(Packet(1, 0.1));
        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Playing));

        pipeline.Disconnect();
        pipeline.Disconnect();     // redundant
        Assert.Equal(ReceiverState.Stopped, pipeline.State);

        pipeline.Connect();        // reconnect after disconnect
        client.Push(Packet(2, 0.2));
        Assert.True(SpinUntil(() => pipeline.State == ReceiverState.Playing));

        Assert.Empty(errors);
    }
}
