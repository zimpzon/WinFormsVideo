using System.Diagnostics;
using System.Net.Sockets;
using Protocol;
using SenderLib;

namespace SenderLib.Tests;

public class UdpVideoStreamServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly StreamInfo TestInfo = new(27, 320, 240, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

    private static UdpVideoStreamServer StartServer(
        int fragmentPayloadSize = DatagramProtocol.MaxFragmentPayload,
        TimeSpan? subscriberTimeout = null)
    {
        var server = new UdpVideoStreamServer(
            new SenderConfiguration { ListenAddress = "127.0.0.1", ListenPort = 0 })
        {
            FragmentPayloadSize = fragmentPayloadSize,
            SubscriberTimeout = subscriberTimeout ?? TimeSpan.FromSeconds(6),
        };
        server.Start(TestInfo);
        return server;
    }

    private static Socket ConnectPeer(UdpVideoStreamServer server)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(server.LocalEndPoint!);
        socket.ReceiveTimeout = 1500;
        return socket;
    }

    private static void Subscribe(Socket peer)
    {
        var buffer = new byte[DatagramProtocol.PrefixSize];
        DatagramProtocol.WriteSubscribe(buffer);
        peer.Send(buffer);
    }

    private static void Bye(Socket peer)
    {
        var buffer = new byte[DatagramProtocol.PrefixSize];
        DatagramProtocol.WriteBye(buffer);
        peer.Send(buffer);
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

    private static StreamInfo ReadStreamInfo(Socket peer)
    {
        var buffer = new byte[DatagramProtocol.MaxDatagramSize];
        DateTime deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            int n;
            try { n = peer.Receive(buffer); } catch (SocketException) { continue; }
            if (DatagramProtocol.TryReadStreamInfo(buffer.AsSpan(0, n), out StreamInfo info))
            {
                return info;
            }
        }

        throw new TimeoutException("no stream info datagram");
    }

    /// <summary>Reassembles one frame from fragment datagrams; returns (payload, fragmentCount).</summary>
    private static (byte[] Payload, int FragmentCount, bool KeyFrame) ReadFrame(Socket peer)
    {
        var buffer = new byte[DatagramProtocol.MaxDatagramSize];
        byte[]? frame = null;
        long seq = -1;
        int received = 0, expected = 0;
        bool key = false;

        DateTime deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            int n;
            try { n = peer.Receive(buffer); } catch (SocketException) { continue; }
            if (!DatagramProtocol.TryReadFrameFragment(buffer.AsSpan(0, n), out FragmentHeader h, out ReadOnlySpan<byte> frag))
            {
                continue;
            }

            if (h.FrameSequence != seq)
            {
                seq = h.FrameSequence;
                frame = new byte[h.TotalLength];
                received = 0;
                expected = h.FragmentCount;
                key = h.IsKeyFrame;
            }

            int offset = h.FragmentIndex == h.FragmentCount - 1
                ? h.TotalLength - h.FragmentLength
                : h.FragmentIndex * h.FragmentLength;
            frag.CopyTo(frame!.AsSpan(offset));
            if (++received == expected)
            {
                return (frame!, expected, key);
            }
        }

        throw new TimeoutException("no complete frame");
    }

    private static byte[] Bytes(byte fill, int size)
    {
        var data = new byte[size];
        Array.Fill(data, fill);
        return data;
    }

    private static EncodedFrame Frame(double seconds, bool key, byte fill, int size) =>
        new(TimeSpan.FromSeconds(seconds), key, Bytes(fill, size));

    [Fact]
    public void Subscribe_GetsStreamInfo_ThenBroadcastsReassembleByteExact()
    {
        using var server = StartServer();
        using Socket peer = ConnectPeer(server);
        Subscribe(peer);

        StreamInfo info = ReadStreamInfo(peer);
        Assert.Equal(TestInfo.CodecId, info.CodecId);
        Assert.Equal(TestInfo.Width, info.Width);
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, info.Extradata.ToArray());
        Assert.True(SpinUntil(() => server.ReceiverCount == 1));

        server.Broadcast(Frame(0, key: true, fill: 0x7C, size: 500));

        var (payload, _, keyFrame) = ReadFrame(peer);
        Assert.True(keyFrame);
        Assert.Equal(500, payload.Length);
        Assert.All(payload, b => Assert.Equal(0x7C, b));
    }

    [Fact]
    public void LargeFrame_IsSplitIntoManyFragments_AndReassembles()
    {
        using var server = StartServer(fragmentPayloadSize: 64);
        using Socket peer = ConnectPeer(server);
        Subscribe(peer);
        ReadStreamInfo(peer);
        Assert.True(SpinUntil(() => server.ReceiverCount == 1));

        var frame = new byte[1000];
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] = (byte)(i * 7);
        }

        server.Broadcast(new EncodedFrame(TimeSpan.FromMilliseconds(33), false, frame));

        var (payload, fragmentCount, _) = ReadFrame(peer);
        Assert.Equal(16, fragmentCount); // ceil(1000 / 64)
        Assert.Equal(frame, payload);
    }

    [Fact]
    public void ReceiverConnected_OnSubscribe_ReceiverDisconnected_OnBye()
    {
        using var server = StartServer();
        int connected = 0, disconnected = 0;
        server.ReceiverConnected += (_, _) => Interlocked.Increment(ref connected);
        server.ReceiverDisconnected += (_, _) => Interlocked.Increment(ref disconnected);

        using Socket peer = ConnectPeer(server);
        Subscribe(peer);
        Assert.True(SpinUntil(() => Volatile.Read(ref connected) == 1));
        Assert.Equal(1, server.ReceiverCount);

        Bye(peer);
        Assert.True(SpinUntil(() => Volatile.Read(ref disconnected) == 1));
        Assert.Equal(0, server.ReceiverCount);
    }

    [Fact]
    public void Subscriber_ThatStopsKeepingAlive_TimesOut()
    {
        using var server = StartServer(subscriberTimeout: TimeSpan.FromMilliseconds(400));
        int disconnected = 0;
        server.ReceiverDisconnected += (_, _) => Interlocked.Increment(ref disconnected);

        using Socket peer = ConnectPeer(server);
        Subscribe(peer); // ...and never again
        Assert.True(SpinUntil(() => server.ReceiverCount == 1));

        // Broadcast keeps the reaper running.
        DateTime deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline && server.ReceiverCount != 0)
        {
            server.Broadcast(Frame(0, false, 1, 64));
            Thread.Sleep(50);
        }

        Assert.Equal(0, server.ReceiverCount);
        Assert.Equal(1, Volatile.Read(ref disconnected));
    }

    [Fact]
    public void Broadcast_WithNoSubscribers_IsANoOp()
    {
        using var server = StartServer();
        Assert.Null(Record.Exception(() => server.Broadcast(Frame(0, true, 1, 200))));
    }

    [Fact]
    public void Broadcast_DoesNotAllocatePerFrame()
    {
        using var server = StartServer();
        using Socket peer = ConnectPeer(server);
        Subscribe(peer);
        ReadStreamInfo(peer);
        Assert.True(SpinUntil(() => server.ReceiverCount == 1));

        // keep the peer draining so its socket buffer never blocks anything
        var drain = new Thread(() =>
        {
            var b = new byte[DatagramProtocol.MaxDatagramSize];
            try { while (true) peer.Receive(b); } catch { /* closed */ }
        }) { IsBackground = true };
        drain.Start();

        var frame = new EncodedFrame(TimeSpan.FromMilliseconds(33), false, Bytes(0x11, 1000));

        for (int i = 0; i < 5; i++)
        {
            server.Broadcast(frame);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 30; i++)
        {
            server.Broadcast(frame);
        }

        long delta = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(delta < 4096, $"allocated {delta} bytes broadcasting 30 frames");
    }
}
