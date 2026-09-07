using System.Net;
using System.Net.Sockets;
using ReceiverLib;

namespace ReceiverLib.Tests;

public class TcpVideoClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static byte[] Payload(byte fill, int size)
    {
        var data = new byte[size];
        Array.Fill(data, fill);
        return data;
    }

    private static void RunBackground(Action action)
    {
        var thread = new Thread(() => action()) { IsBackground = true };
        thread.Start();
    }

    private static TcpVideoClient Connect(ProtocolTestServer server)
    {
        RunBackground(server.AcceptAndHandshake);
        var client = new TcpVideoClient(new ReceiverConfiguration
        {
            SenderAddress = "127.0.0.1",
            SenderPort = server.Port,
        });
        client.Connect(); // blocks until the handshake is read, so the accept has completed
        return client;
    }

    [Fact]
    public void Connect_ReadsTheHandshake_AndRaisesConnected()
    {
        using var server = new ProtocolTestServer();
        RunBackground(server.AcceptAndHandshake);
        using var client = new TcpVideoClient(new ReceiverConfiguration
        {
            SenderAddress = "127.0.0.1",
            SenderPort = server.Port,
        });
        bool connected = false;
        client.Connected += (_, _) => connected = true;

        client.Connect();

        Assert.True(connected);
        Assert.NotNull(client.StreamInfo);
        Assert.Equal(ProtocolTestServer.DefaultStreamInfo.CodecId, client.StreamInfo!.Value.CodecId);
        Assert.Equal(ProtocolTestServer.DefaultStreamInfo.Width, client.StreamInfo!.Value.Width);
        Assert.Equal(
            ProtocolTestServer.DefaultStreamInfo.Extradata.ToArray(),
            client.StreamInfo!.Value.Extradata.ToArray());
    }

    [Fact]
    public void Connect_WithAnUnrecognisedHandshake_Throws()
    {
        using var server = new ProtocolTestServer();
        RunBackground(() => server.AcceptAndSend(new byte[Protocol.StreamProtocol.HandshakePrefixSize])); // all-zero => bad magic
        using var client = new TcpVideoClient(new ReceiverConfiguration
        {
            SenderAddress = "127.0.0.1",
            SenderPort = server.Port,
        });

        Assert.Throws<InvalidDataException>(() => client.Connect());
    }

    [Fact]
    public void Connect_WhenNothingIsListening_Throws()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int deadPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        using var client = new TcpVideoClient(new ReceiverConfiguration
        {
            SenderAddress = "127.0.0.1",
            SenderPort = deadPort,
        });

        Assert.ThrowsAny<SocketException>(() => client.Connect());
    }

    [Fact]
    public void TryReadPacket_ReturnsFramesInOrder_ByteExact()
    {
        using var server = new ProtocolTestServer();
        using TcpVideoClient client = Connect(server);

        server.SendFrame(1, TimeSpan.Zero, isKeyFrame: true, Payload(0xAA, 64));
        server.SendFrame(2, TimeSpan.FromMilliseconds(33), isKeyFrame: false, Payload(0xBB, 40));

        Assert.True(client.TryReadPacket(out ReceivedPacket first));
        Assert.Equal(1, first.SequenceNumber);
        Assert.True(first.IsKeyFrame);
        Assert.Equal(TimeSpan.Zero, first.Timestamp);
        Assert.All(first.Data.ToArray(), b => Assert.Equal(0xAA, b));

        Assert.True(client.TryReadPacket(out ReceivedPacket second));
        Assert.Equal(2, second.SequenceNumber);
        Assert.False(second.IsKeyFrame);
        Assert.Equal(TimeSpan.FromMilliseconds(33), second.Timestamp);
        Assert.Equal(40, second.Data.Length);
    }

    [Fact]
    public void TryReadPacket_ReturnsFalse_AndRaisesDisconnected_WhenTheServerCloses()
    {
        using var server = new ProtocolTestServer();
        using TcpVideoClient client = Connect(server);
        bool disconnected = false;
        client.Disconnected += (_, _) => disconnected = true;

        server.CloseClient();

        Assert.False(client.TryReadPacket(out _));
        Assert.True(disconnected);
    }

    [Fact]
    public void TryReadPacket_DoesNotAllocatePerFrame()
    {
        using var server = new ProtocolTestServer();
        using TcpVideoClient client = Connect(server);

        RunBackground(() =>
        {
            for (int i = 0; i < 30; i++)
            {
                server.SendFrame(i, TimeSpan.FromMilliseconds(i * 10), isKeyFrame: i == 0, Payload((byte)i, 2048));
            }
        });

        for (int i = 0; i < 5; i++)
        {
            Assert.True(client.TryReadPacket(out _)); // warm: grows _payloadBuffer, JIT
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        int read = 0;
        for (int i = 5; i < 30; i++)
        {
            if (client.TryReadPacket(out _))
            {
                read++;
            }
        }

        long delta = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(25, read);
        Assert.True(delta < 4096, $"allocated {delta} bytes reading {read} packets");
    }

    [Fact]
    public void Disconnect_IsIdempotent_AndMakesFurtherReadsReturnFalse()
    {
        using var server = new ProtocolTestServer();
        TcpVideoClient client = Connect(server);

        client.Disconnect();
        client.Disconnect();

        Assert.False(client.TryReadPacket(out _));
    }
}
