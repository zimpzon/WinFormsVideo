using ReceiverLib;
using ReceiverLib.Tests.Fakes;

namespace ReceiverLib.Tests;

public class VideoReceiverTests
{
    private static VideoReceiver CreateReceiver(
        out FakeVideoClient client,
        out FakeVideoDecoder decoder,
        out FakePlaybackClock clock)
    {
        var configuration = new ReceiverConfiguration { SenderPort = 55555 };
        client = new FakeVideoClient();
        decoder = new FakeVideoDecoder();
        clock = new FakePlaybackClock();
        var pipeline = new ReceivePipeline(configuration, client, decoder, clock);
        return new VideoReceiver(pipeline);
    }

    [Fact]
    public void NewReceiver_StartsIdle_WithNoVideoInfo()
    {
        using var receiver = CreateReceiver(out _, out _, out _);

        Assert.Equal(ReceiverState.Idle, receiver.State);
        Assert.Null(receiver.VideoInfo);
        Assert.Equal(ReceiverState.Idle, receiver.Statistics.ConnectionState);
    }

    [Fact]
    public void Dispose_DisposesTheInjectedPipeline()
    {
        var receiver = CreateReceiver(out var client, out var decoder, out _);

        receiver.Dispose();

        Assert.True(client.IsDisposed);
        Assert.True(decoder.IsDisposed);
    }

    [Fact]
    public void DefaultConstructor_WiresTheRealPipelineWithoutTouchingTheNetwork()
    {
        using var receiver = new VideoReceiver(new ReceiverConfiguration
        {
            SenderAddress = "127.0.0.1",
            SenderPort = 65000,
        });

        Assert.Equal(ReceiverState.Idle, receiver.State);
    }

    [Fact]
    public void Connect_DrivesThePipeline_AndBubblesFramesAndState()
    {
        var configuration = new ReceiverConfiguration { SenderPort = 1 };
        var client = new FakeVideoClient(packets: new[]
        {
            new ReceivedPacket(0, TimeSpan.Zero, true, new byte[8]),
            new ReceivedPacket(1, TimeSpan.FromMilliseconds(40), true, new byte[8]),
        });
        var pipeline = new ReceivePipeline(configuration, client, new FakeVideoDecoder(), new FakePlaybackClock());
        using var receiver = new VideoReceiver(pipeline);

        int frames = 0;
        var states = new System.Collections.Concurrent.ConcurrentQueue<ReceiverState>();
        receiver.FrameReady += (object? _, in VideoFrame _) => Interlocked.Increment(ref frames);
        receiver.StateChanged += (_, e) => states.Enqueue(e.NewState);

        receiver.Connect();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && receiver.State != ReceiverState.Stopped)
        {
            Thread.Sleep(5);
        }

        Assert.Equal(ReceiverState.Stopped, receiver.State);
        Assert.Equal(2, Volatile.Read(ref frames));
        Assert.Contains(ReceiverState.Playing, states);
        Assert.NotNull(receiver.VideoInfo);
    }

    [Fact]
    public void Connect_ExposesFrameBuffers_AndTryAcquireFrame_HandsOutTheLatestFrame()
    {
        var configuration = new ReceiverConfiguration { SenderPort = 1 };
        var client = new FakeVideoClient(packets: new[]
        {
            new ReceivedPacket(0, TimeSpan.Zero, true, new byte[8]),
            new ReceivedPacket(1, TimeSpan.FromMilliseconds(40), true, new byte[8]),
            new ReceivedPacket(2, TimeSpan.FromMilliseconds(80), true, new byte[8]),
        });
        var pipeline = new ReceivePipeline(configuration, client, new FakeVideoDecoder(), new FakePlaybackClock());
        using var receiver = new VideoReceiver(pipeline);

        Assert.Null(receiver.FrameBuffers); // nothing before connect

        receiver.Connect();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && receiver.State != ReceiverState.Stopped)
        {
            Thread.Sleep(5);
        }

        Assert.Equal(ReceiverState.Stopped, receiver.State);

        FrameBufferPool? buffers = receiver.FrameBuffers;
        Assert.NotNull(buffers);
        Assert.Equal(3, buffers!.Count);
        Assert.Equal(320, buffers.Width); // FakeVideoClient's default StreamInfo
        Assert.Equal(320 * 4, buffers.Stride);

        Assert.True(receiver.TryAcquireFrame(out RentedFrame frame));
        Assert.Equal(2, frame.SequenceNumber); // the most recent decoded frame
        Assert.InRange(frame.BufferIndex, 0, 2);

        Assert.False(receiver.TryAcquireFrame(out _)); // nothing newer
    }

    [Fact]
    public void ConnectionFailure_BubblesErrorAndFaults()
    {
        var client = new FakeVideoClient { ThrowOnConnect = new System.Net.Sockets.SocketException(10061) };
        var pipeline = new ReceivePipeline(
            new ReceiverConfiguration { SenderPort = 1 }, client, new FakeVideoDecoder(), new FakePlaybackClock());
        using var receiver = new VideoReceiver(pipeline);

        ReceiverError? error = null;
        receiver.ErrorOccurred += (_, e) => error = e.Error;

        receiver.Connect();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && receiver.State != ReceiverState.Faulted)
        {
            Thread.Sleep(5);
        }

        Assert.Equal(ReceiverState.Faulted, receiver.State);
        Assert.NotNull(error);
    }
}
