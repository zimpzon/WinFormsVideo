using System.Net;
using System.Net.Sockets;
using Protocol;
using ReceiverLib;

namespace ReceiverLib.Tests;

public class UdpVideoClientTests
{
    private static readonly StreamInfo TestInfo = new(27, 160, 120, new byte[] { 0xAA, 0xBB, 0xCC });

    private static void RunBackground(Action action)
    {
        var thread = new Thread(() => action()) { IsBackground = true };
        thread.Start();
    }

    private static byte[] Payload(byte fill, int size)
    {
        var data = new byte[size];
        Array.Fill(data, fill);
        return data;
    }

    /// <summary>Runs <see cref="IVideoClient.TryReadPacket"/> with a deadline so a stall fails the test rather than hangs.</summary>
    private static ReceivedPacket ReadOne(UdpVideoClient client, int timeoutMs = 3000)
    {
        ReceivedPacket result = default;
        bool ok = false;
        var t = new Thread(() => ok = client.TryReadPacket(out result)) { IsBackground = true };
        t.Start();
        if (!t.Join(timeoutMs))
        {
            throw new TimeoutException("TryReadPacket did not return in time");
        }

        Assert.True(ok);
        return result;
    }

    /// <summary>Raw UDP peer that plays the sender.</summary>
    private sealed class FakeSender : IDisposable
    {
        private readonly Socket _socket;
        private EndPoint _client = new IPEndPoint(IPAddress.Any, 0);

        public FakeSender()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _socket.ReceiveTimeout = 3000;
        }

        public int Port => ((IPEndPoint)_socket.LocalEndPoint!).Port;

        public int SubscribesReceived { get; private set; }

        public void AcceptSubscribe()
        {
            var buffer = new byte[DatagramProtocol.MaxDatagramSize];
            _socket.ReceiveFrom(buffer, ref _client);
            SubscribesReceived++;
            SendStreamInfo();
        }

        public void CountSubscribes(TimeSpan duration)
        {
            var buffer = new byte[DatagramProtocol.MaxDatagramSize];
            DateTime end = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < end)
            {
                try
                {
                    _socket.ReceiveFrom(buffer, ref _client);
                    SubscribesReceived++;
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { return; }
            }
        }

        public void SendStreamInfo()
        {
            var buffer = new byte[DatagramProtocol.PrefixSize + TestInfo.SerializedSize];
            int len = DatagramProtocol.WriteStreamInfo(buffer, TestInfo);
            _socket.SendTo(buffer, 0, len, SocketFlags.None, _client);
        }

        public void SendFrame(long seq, TimeSpan ts, bool key, byte[] payload, int fragmentSize = 400,
            HashSet<int>? skip = null, bool reverse = false)
        {
            int count = Math.Max(1, (payload.Length + fragmentSize - 1) / fragmentSize);
            var buffer = new byte[DatagramProtocol.MaxDatagramSize];

            IEnumerable<int> order = Enumerable.Range(0, count);
            if (reverse)
            {
                order = order.Reverse();
            }

            foreach (int i in order)
            {
                if (skip is not null && skip.Contains(i))
                {
                    continue;
                }

                int offset = i * fragmentSize;
                int length = Math.Min(fragmentSize, payload.Length - offset);
                int len = DatagramProtocol.WriteFrameFragment(
                    buffer, seq, ts.Ticks, key ? FrameFlags.KeyFrame : FrameFlags.None,
                    payload.Length, i, count, payload.AsSpan(offset, length));
                _socket.SendTo(buffer, 0, len, SocketFlags.None, _client);
            }
        }

        public void SendRawFragment(long seq, int index, int count, byte[] fragment, int totalLength)
        {
            var buffer = new byte[DatagramProtocol.MaxDatagramSize];
            int len = DatagramProtocol.WriteFrameFragment(
                buffer, seq, 0, FrameFlags.None, totalLength, index, count, fragment);
            _socket.SendTo(buffer, 0, len, SocketFlags.None, _client);
        }

        public void Dispose() => _socket.Dispose();
    }

    private static UdpVideoClient Connected(FakeSender sender)
    {
        RunBackground(sender.AcceptSubscribe);
        var client = new UdpVideoClient(new ReceiverConfiguration
        {
            SenderAddress = "127.0.0.1",
            SenderPort = sender.Port,
        });
        client.Connect();
        return client;
    }

    [Fact]
    public void Connect_ObtainsStreamInfoFromTheSender()
    {
        using var sender = new FakeSender();
        using UdpVideoClient client = Connected(sender);

        Assert.NotNull(client.StreamInfo);
        Assert.Equal(TestInfo.CodecId, client.StreamInfo!.Value.CodecId);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, client.StreamInfo!.Value.Extradata.ToArray());
    }

    [Fact]
    public void Connect_WhenTheSenderNeverReplies_TimesOut()
    {
        using var sender = new FakeSender(); // bound but nobody answers
        var client = new UdpVideoClient(new ReceiverConfiguration
        {
            SenderAddress = "127.0.0.1",
            SenderPort = sender.Port,
        })
        {
            HandshakeTimeout = TimeSpan.FromMilliseconds(600),
            KeepaliveInterval = TimeSpan.FromMilliseconds(150),
        };

        Assert.ThrowsAny<SocketException>(() => client.Connect());
    }

    [Fact]
    public void TryReadPacket_ReassemblesFramesInOrder_ByteExact()
    {
        using var sender = new FakeSender();
        using UdpVideoClient client = Connected(sender);

        sender.SendFrame(1, TimeSpan.Zero, key: true, Payload(0x11, 1000));
        sender.SendFrame(2, TimeSpan.FromMilliseconds(33), key: false, Payload(0x22, 700));

        ReceivedPacket first = ReadOne(client);
        Assert.Equal(1, first.SequenceNumber);
        Assert.True(first.IsKeyFrame);
        Assert.Equal(1000, first.Data.Length);
        Assert.All(first.Data.ToArray(), b => Assert.Equal(0x11, b));

        ReceivedPacket second = ReadOne(client);
        Assert.Equal(2, second.SequenceNumber);
        Assert.Equal(700, second.Data.Length);
        Assert.All(second.Data.ToArray(), b => Assert.Equal(0x22, b));
    }

    [Fact]
    public void OutOfOrderFragmentsWithinAFrame_StillReassemble()
    {
        using var sender = new FakeSender();
        using UdpVideoClient client = Connected(sender);

        sender.SendFrame(1, TimeSpan.Zero, key: true, Payload(0x33, 1500), reverse: true);

        ReceivedPacket p = ReadOne(client);
        Assert.Equal(1500, p.Data.Length);
        Assert.All(p.Data.ToArray(), b => Assert.Equal(0x33, b));
    }

    [Fact]
    public void MissingFragment_DropsThatWholeFrame_AndDeliversTheNext()
    {
        using var sender = new FakeSender();
        using UdpVideoClient client = Connected(sender);

        sender.SendFrame(1, TimeSpan.Zero, key: true, Payload(0x44, 1200), skip: new HashSet<int> { 1 });
        sender.SendFrame(2, TimeSpan.FromMilliseconds(33), key: true, Payload(0x55, 800));

        ReceivedPacket p = ReadOne(client);
        Assert.Equal(2, p.SequenceNumber); // frame 1 was abandoned
        Assert.All(p.Data.ToArray(), b => Assert.Equal(0x55, b));
        Assert.True(client.FramesDropped >= 1);
    }

    [Fact]
    public void FragmentForAnAlreadyPassedFrame_IsDropped()
    {
        using var sender = new FakeSender();
        using UdpVideoClient client = Connected(sender);

        sender.SendFrame(5, TimeSpan.Zero, key: true, Payload(0x66, 300));
        Assert.Equal(5, ReadOne(client).SequenceNumber);

        sender.SendRawFragment(3, 0, 1, Payload(0x99, 100), 100); // stale — older than frame 5
        sender.SendFrame(6, TimeSpan.FromMilliseconds(33), key: true, Payload(0x77, 300));

        ReceivedPacket p2 = ReadOne(client);
        Assert.Equal(6, p2.SequenceNumber);
        Assert.All(p2.Data.ToArray(), b => Assert.Equal(0x77, b));
    }

    [Fact]
    public void Keepalive_SubscribeIsResentPeriodically()
    {
        using var sender = new FakeSender();
        RunBackground(sender.AcceptSubscribe);
        using var client = new UdpVideoClient(new ReceiverConfiguration
        {
            SenderAddress = "127.0.0.1",
            SenderPort = sender.Port,
        })
        {
            KeepaliveInterval = TimeSpan.FromMilliseconds(120),
        };
        client.Connect();

        var counter = new Thread(() => sender.CountSubscribes(TimeSpan.FromMilliseconds(600))) { IsBackground = true };
        counter.Start();

        var reader = new Thread(() =>
        {
            try { client.TryReadPacket(out _); } catch { /* closed at dispose */ }
        }) { IsBackground = true };
        reader.Start();

        counter.Join(2000);
        Assert.True(sender.SubscribesReceived >= 3, $"only {sender.SubscribesReceived} subscribes");
    }

    [Fact]
    public void TryReadPacket_DoesNotAllocatePerFrame()
    {
        // Loopback UDP with the client's 4 MB receive buffer + paced sends is lossless, so every
        // read returns and this can measure the reassembly path directly on the test thread.
        using var sender = new FakeSender();
        using UdpVideoClient client = Connected(sender);

        var payload = Payload(0x5A, 1500);
        for (int i = 0; i < 26; i++)
        {
            sender.SendFrame(i + 1, TimeSpan.FromMilliseconds(i * 33), key: i == 0, payload);
            Thread.Sleep(1); // spread the datagrams so the 4 MB receive buffer never overflows
        }

        for (int i = 0; i < 6; i++)
        {
            Assert.True(client.TryReadPacket(out _)); // warm: grows buffers, JIT
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        int read = 0;
        for (int i = 6; i < 26; i++)
        {
            if (client.TryReadPacket(out _))
            {
                read++;
            }
        }

        long delta = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(20, read);
        Assert.True(delta < 4096, $"allocated {delta} bytes reading {read} frames");
    }
}
