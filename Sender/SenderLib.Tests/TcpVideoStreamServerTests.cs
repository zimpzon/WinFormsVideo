using System.Diagnostics;
using System.Net.Sockets;
using Protocol;
using SenderLib;

namespace SenderLib.Tests;

public class TcpVideoStreamServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly StreamInfo TestStreamInfo =
        new(codecId: 27, width: 160, height: 120, extradata: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

    private static TcpVideoStreamServer StartServer(TimeSpan? sendTimeout = null)
    {
        var server = new TcpVideoStreamServer(
            new SenderConfiguration { ListenAddress = "127.0.0.1", ListenPort = 0 })
        {
            SendTimeout = sendTimeout ?? TimeSpan.FromSeconds(5),
        };
        server.Start(TestStreamInfo);
        return server;
    }

    private static TcpClient Connect(TcpVideoStreamServer server)
    {
        var endpoint = server.LocalEndPoint!;
        var client = new TcpClient();
        client.Connect(endpoint.Address, endpoint.Port);
        return client;
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

            Thread.Sleep(10);
        }

        return condition();
    }

    private static EncodedFrame Frame(double seconds, bool key, byte fill, int size = 64) =>
        new(TimeSpan.FromSeconds(seconds), key, TcpTestIo.Bytes(fill, size));

    [Fact]
    public void Connect_ThenDisconnect_MovesReceiverCount_AndRaisesEvents()
    {
        using var server = StartServer();
        ReceiverConnectionEventArgs? connected = null;
        ReceiverConnectionEventArgs? disconnected = null;
        server.ReceiverConnected += (_, e) => connected = e;
        server.ReceiverDisconnected += (_, e) => disconnected = e;

        var client = Connect(server);
        Assert.True(SpinUntil(() => server.ReceiverCount == 1));
        Assert.NotNull(connected);

        client.Close();
        Assert.True(SpinUntil(() => server.ReceiverCount == 0));
        Assert.NotNull(disconnected);
        Assert.Equal(connected!.ReceiverId, disconnected!.ReceiverId);
    }

    [Fact]
    public void Broadcast_DeliversFramesInOrder_ByteExact()
    {
        using var server = StartServer();
        using var client = Connect(server);
        Assert.True(SpinUntil(() => server.ReceiverCount == 1));
        NetworkStream stream = client.GetStream();
        var (version, info) = TcpTestIo.ReadHandshake(stream);
        Assert.Equal(StreamProtocol.Version, version);
        Assert.Equal(TestStreamInfo.CodecId, info.CodecId);
        Assert.Equal(TestStreamInfo.Width, info.Width);
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, info.Extradata.ToArray());

        server.Broadcast(Frame(0, key: true, 0xAA, 100));
        server.Broadcast(Frame(1, key: false, 0xBB, 50));

        var first = TcpTestIo.ReadFrame(stream);
        Assert.True(first.Header.IsKeyFrame);
        Assert.Equal(TimeSpan.Zero.Ticks, first.Header.TimestampTicks);
        Assert.Equal(100, first.Payload.Length);
        Assert.All(first.Payload, b => Assert.Equal(0xAA, b));

        var second = TcpTestIo.ReadFrame(stream);
        Assert.False(second.Header.IsKeyFrame);
        Assert.Equal(50, second.Payload.Length);
        Assert.Equal(first.Header.SequenceNumber + 1, second.Header.SequenceNumber);
    }

    [Fact]
    public void Broadcast_WithNoReceivers_IsANoOp()
    {
        using var server = StartServer();
        Assert.Null(Record.Exception(() => server.Broadcast(Frame(0, key: true, 1))));
    }

    [Fact]
    public void LateJoiner_ReceivesTheMostRecentKeyframeFirst()
    {
        using var server = StartServer();
        using var early = Connect(server);
        Assert.True(SpinUntil(() => server.ReceiverCount == 1));
        NetworkStream earlyStream = early.GetStream();
        TcpTestIo.ReadHandshake(earlyStream);

        server.Broadcast(Frame(0, key: true, 0x11, 40));
        server.Broadcast(Frame(1, key: false, 0x22, 40));
        TcpTestIo.ReadFrame(earlyStream);
        TcpTestIo.ReadFrame(earlyStream);

        using var late = Connect(server);
        Assert.True(SpinUntil(() => server.ReceiverCount == 2));
        NetworkStream lateStream = late.GetStream();
        TcpTestIo.ReadHandshake(lateStream);

        var first = TcpTestIo.ReadFrame(lateStream);
        Assert.True(first.Header.IsKeyFrame);
        Assert.All(first.Payload, b => Assert.Equal(0x11, b));
    }

    [Fact]
    public void SlowReceiver_NeverBlocksBroadcast_AndIsEventuallyDropped()
    {
        using var server = StartServer(sendTimeout: TimeSpan.FromMilliseconds(300));
        using var slow = Connect(server); // deliberately never read
        Assert.True(SpinUntil(() => server.ReceiverCount == 1));

        for (int i = 0; i < 400; i++)
        {
            long start = Stopwatch.GetTimestamp();
            server.Broadcast(Frame(i * 0.01, key: i % 30 == 0, fill: (byte)i, size: 64 * 1024));
            Assert.True(
                Stopwatch.GetElapsedTime(start) < TimeSpan.FromMilliseconds(250),
                "Broadcast blocked on a slow receiver");
        }

        Assert.True(SpinUntil(() => server.ReceiverCount == 0));
    }

    [Fact]
    public void Stop_DisconnectsEveryone_AndIsIdempotent()
    {
        using var server = StartServer();
        using var c1 = Connect(server);
        using var c2 = Connect(server);
        Assert.True(SpinUntil(() => server.ReceiverCount == 2));

        int disconnects = 0;
        server.ReceiverDisconnected += (_, _) => Interlocked.Increment(ref disconnects);

        server.Stop();

        Assert.Equal(0, server.ReceiverCount);
        Assert.Equal(2, disconnects);
        Assert.Null(Record.Exception(() => server.Stop()));
        Assert.Null(Record.Exception(() => server.Dispose()));
    }

    [Fact]
    public void Broadcast_DoesNotRebuildTheConnectionSnapshotPerFrame()
    {
        using var server = StartServer();
        using var client = Connect(server);
        Assert.True(SpinUntil(() => server.ReceiverCount == 1));

        for (int i = 0; i < 30; i++)
        {
            server.Broadcast(Frame(i * 0.03, key: i == 0, fill: (byte)i, size: 256));
        }

        Assert.Equal(1, server.SnapshotRebuildCount); // rebuilt once, when the client connected
    }

    [Fact]
    public void Start_WithAnInvalidListenAddress_Throws()
    {
        var server = new TcpVideoStreamServer(
            new SenderConfiguration { ListenAddress = "not-an-ip", ListenPort = 0 });

        Assert.ThrowsAny<Exception>(() => server.Start(TestStreamInfo));
    }
}
