using System.Net;
using System.Net.Sockets;
using Protocol;

namespace SenderLib;

/// <summary>
/// UDP implementation of <see cref="IVideoStreamServer"/>. Connectionless: receivers send a
/// <see cref="DatagramType.Subscribe"/> datagram (also used as a keepalive); the server tracks their
/// endpoints and fans out each frame as MTU-safe <see cref="DatagramType.FrameFragment"/> datagrams.
/// No back-pressure — the OS socket buffer is the buffer, and a lost datagram just means a dropped
/// frame on the receiver. Streaming with zero subscribers is a no-op.
/// </summary>
internal sealed class UdpVideoStreamServer : IVideoStreamServer
{
    private readonly SenderConfiguration _configuration;
    private readonly object _gate = new();
    private readonly Dictionary<IPEndPoint, Subscriber> _subscribers = new();
    private readonly List<IPEndPoint> _expiredScratch = new();
    private readonly byte[] _sendBuffer = new byte[DatagramProtocol.MaxDatagramSize];

    // Snapshot of subscriber socket addresses, rebuilt only when the set changes — Broadcast never allocates.
    private SocketAddress[] _snapshot = Array.Empty<SocketAddress>();
    private bool _snapshotDirty;

    private Socket? _socket;
    private Thread? _receiveThread;
    private CancellationTokenSource? _cts;
    private volatile bool _running;
    private long _sequence;
    private long _lastReapTicks;

    private StreamInfo _streamInfo;
    private byte[] _lastKeyframe = Array.Empty<byte>();
    private bool _hasLastKeyframe;
    private int _lastKeyframeLength;
    private long _lastKeyframeTicks;
    private long _lastKeyframeSequence;

    public UdpVideoStreamServer(SenderConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>A subscriber that hasn't re-subscribed within this window is dropped.</summary>
    public TimeSpan SubscriberTimeout { get; init; } = TimeSpan.FromSeconds(6);

    /// <summary>Frame-payload bytes per fragment datagram. Tests shrink this to force many fragments.</summary>
    public int FragmentPayloadSize { get; init; } = DatagramProtocol.MaxFragmentPayload;

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverConnected;

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverDisconnected;

    public int ReceiverCount
    {
        get { lock (_gate) return _subscribers.Count; }
    }

    public IPEndPoint? LocalEndPoint => _socket?.LocalEndPoint as IPEndPoint;

    public void Start(StreamInfo streamInfo)
    {
        if (_running)
        {
            return;
        }

        if (_configuration.ListenPort is < 0 or > 65535)
        {
            throw new ArgumentException($"Invalid listen port {_configuration.ListenPort}.");
        }

        _streamInfo = streamInfo;
        IPAddress address = ResolveAddress(_configuration.ListenAddress);

        _cts = new CancellationTokenSource();
        _socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(address, _configuration.ListenPort));
        try { _socket.SendBufferSize = 4 * 1024 * 1024; } catch { /* best effort */ }
        _running = true;
        _lastReapTicks = Environment.TickCount64;

        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "SenderLib.UdpReceive" };
        _receiveThread.Start();
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _cts?.Cancel();
        try { _socket?.Close(); } catch { /* already closed */ }
        _receiveThread?.Join();

        Subscriber[] gone;
        lock (_gate)
        {
            gone = _subscribers.Values.ToArray();
            _subscribers.Clear();
            _snapshot = Array.Empty<SocketAddress>();
            _snapshotDirty = false;
            _hasLastKeyframe = false;
        }

        foreach (Subscriber s in gone)
        {
            RaiseDisconnected(s);
        }

        _socket?.Dispose();
        _socket = null;
        _cts?.Dispose();
        _cts = null;
        _receiveThread = null;
    }

    public void Broadcast(in EncodedFrame frame)
    {
        Socket? socket = _socket;
        if (!_running || socket is null)
        {
            return;
        }

        long sequence = Interlocked.Increment(ref _sequence);

        if (frame.IsKeyFrame)
        {
            StoreLastKeyframe(in frame, sequence);
        }

        ReapExpired();

        SocketAddress[] snapshot;
        lock (_gate)
        {
            if (_snapshotDirty)
            {
                RebuildSnapshot();
            }

            snapshot = _snapshot;
        }

        if (snapshot.Length == 0)
        {
            return;
        }

        ReadOnlySpan<byte> payload = frame.Data.Span;
        int fragmentCount = Math.Max(1, (payload.Length + FragmentPayloadSize - 1) / FragmentPayloadSize);
        long ticks = frame.Timestamp.Ticks;
        FrameFlags flags = frame.IsKeyFrame ? FrameFlags.KeyFrame : FrameFlags.None;

        for (int i = 0; i < fragmentCount; i++)
        {
            int offset = i * FragmentPayloadSize;
            int length = Math.Min(FragmentPayloadSize, payload.Length - offset);
            int datagram = DatagramProtocol.WriteFrameFragment(
                _sendBuffer, sequence, ticks, flags, payload.Length, i, fragmentCount, payload.Slice(offset, length));

            foreach (SocketAddress address in snapshot)
            {
                try
                {
                    socket.SendTo(_sendBuffer.AsSpan(0, datagram), SocketFlags.None, address);
                }
                catch (SocketException)
                {
                    // best effort — a stale endpoint is dropped by the keepalive reaper
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }
    }

    public void Dispose() => Stop();

    // --- receive thread -------------------------------------------------

    private void ReceiveLoop()
    {
        var buffer = new byte[DatagramProtocol.MaxDatagramSize];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (_running)
        {
            int n;
            try
            {
                n = _socket!.ReceiveFrom(buffer, ref remote);
            }
            catch (SocketException)
            {
                if (_running) continue;
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (!DatagramProtocol.TryReadType(buffer.AsSpan(0, n), out DatagramType type) ||
                remote is not IPEndPoint from)
            {
                continue;
            }

            if (type == DatagramType.Subscribe)
            {
                HandleSubscribe(from);
            }
            else if (type == DatagramType.Bye)
            {
                RemoveSubscriber(from);
            }
        }
    }

    private void HandleSubscribe(IPEndPoint from)
    {
        bool isNew;
        Subscriber subscriber;
        lock (_gate)
        {
            if (_subscribers.TryGetValue(from, out Subscriber? existing))
            {
                existing.LastSeenTicks = Environment.TickCount64;
                subscriber = existing;
                isNew = false;
            }
            else
            {
                // Own the key — the ref endpoint from ReceiveFrom is reused by the loop.
                var key = new IPEndPoint(from.Address, from.Port);
                subscriber = new Subscriber(Guid.NewGuid(), key, Environment.TickCount64);
                _subscribers[key] = subscriber;
                _snapshotDirty = true;
                isNew = true;
            }
        }

        // Reply to every subscribe with fresh stream info (cheap; also covers a lost first reply).
        SendStreamInfo(subscriber.EndPoint);

        if (isNew)
        {
            PrimeWithKeyframe(subscriber.EndPoint);
            RaiseConnected(subscriber);
        }
    }

    private void SendStreamInfo(IPEndPoint to)
    {
        var buffer = new byte[DatagramProtocol.PrefixSize + _streamInfo.SerializedSize];
        int len = DatagramProtocol.WriteStreamInfo(buffer, _streamInfo);
        TrySendTo(buffer.AsSpan(0, len), to);
    }

    private void PrimeWithKeyframe(IPEndPoint to)
    {
        byte[]? copy = null;
        int length = 0;
        long ticks = 0;
        long sequence = 0;

        lock (_gate)
        {
            if (_hasLastKeyframe)
            {
                copy = _lastKeyframe;
                length = _lastKeyframeLength;
                ticks = _lastKeyframeTicks;
                sequence = _lastKeyframeSequence;
            }
        }

        if (copy is null)
        {
            return;
        }

        int fragmentCount = Math.Max(1, (length + FragmentPayloadSize - 1) / FragmentPayloadSize);
        var scratch = new byte[DatagramProtocol.MaxDatagramSize];
        for (int i = 0; i < fragmentCount; i++)
        {
            int offset = i * FragmentPayloadSize;
            int fragLen = Math.Min(FragmentPayloadSize, length - offset);
            int datagram = DatagramProtocol.WriteFrameFragment(
                scratch, sequence, ticks, FrameFlags.KeyFrame, length, i, fragmentCount,
                copy.AsSpan(offset, fragLen));
            TrySendTo(scratch.AsSpan(0, datagram), to);
        }
    }

    private void TrySendTo(ReadOnlySpan<byte> data, IPEndPoint to)
    {
        try
        {
            _socket?.SendTo(data, SocketFlags.None, to);
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
    }

    // --- subscriber bookkeeping ---------------------------------------

    private void ReapExpired()
    {
        long now = Environment.TickCount64;
        if (now - _lastReapTicks < 1000)
        {
            return;
        }

        _lastReapTicks = now;
        long cutoff = (long)SubscriberTimeout.TotalMilliseconds;

        Subscriber[]? removed = null;
        lock (_gate)
        {
            _expiredScratch.Clear();
            foreach (KeyValuePair<IPEndPoint, Subscriber> kvp in _subscribers)
            {
                if (now - kvp.Value.LastSeenTicks > cutoff)
                {
                    _expiredScratch.Add(kvp.Key);
                }
            }

            if (_expiredScratch.Count > 0)
            {
                removed = new Subscriber[_expiredScratch.Count];
                for (int i = 0; i < _expiredScratch.Count; i++)
                {
                    removed[i] = _subscribers[_expiredScratch[i]];
                    _subscribers.Remove(_expiredScratch[i]);
                }

                _snapshotDirty = true;
            }
        }

        if (removed is not null)
        {
            foreach (Subscriber s in removed)
            {
                RaiseDisconnected(s);
            }
        }
    }

    private void RemoveSubscriber(IPEndPoint from)
    {
        Subscriber? subscriber;
        bool removed;
        lock (_gate)
        {
            removed = _subscribers.Remove(from, out subscriber);
            if (removed)
            {
                _snapshotDirty = true;
            }
        }

        if (removed)
        {
            RaiseDisconnected(subscriber!);
        }
    }

    private void RebuildSnapshot()
    {
        if (_subscribers.Count == 0)
        {
            _snapshot = Array.Empty<SocketAddress>();
        }
        else
        {
            var addresses = new SocketAddress[_subscribers.Count];
            int i = 0;
            foreach (Subscriber s in _subscribers.Values)
            {
                addresses[i++] = s.EndPoint.Serialize();
            }

            _snapshot = addresses;
        }

        _snapshotDirty = false;
    }

    private void StoreLastKeyframe(in EncodedFrame frame, long sequence)
    {
        lock (_gate)
        {
            int length = frame.Data.Length;
            BufferUtil.EnsureCapacity(ref _lastKeyframe, length);
            frame.Data.Span.CopyTo(_lastKeyframe);

            _hasLastKeyframe = true;
            _lastKeyframeLength = length;
            _lastKeyframeTicks = frame.Timestamp.Ticks;
            _lastKeyframeSequence = sequence;
        }
    }

    private void RaiseConnected(Subscriber s) =>
        ReceiverConnected?.Invoke(this, new ReceiverConnectionEventArgs(s.Id, s.EndPoint.ToString(), DateTimeOffset.UtcNow));

    private void RaiseDisconnected(Subscriber s) =>
        ReceiverDisconnected?.Invoke(this, new ReceiverConnectionEventArgs(s.Id, s.EndPoint.ToString(), DateTimeOffset.UtcNow));

    private static IPAddress ResolveAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "0.0.0.0")
        {
            return IPAddress.Any;
        }

        return value == "::" ? IPAddress.IPv6Any : IPAddress.Parse(value);
    }

    private sealed class Subscriber
    {
        public Subscriber(Guid id, IPEndPoint endPoint, long lastSeenTicks)
        {
            Id = id;
            EndPoint = endPoint;
            LastSeenTicks = lastSeenTicks;
        }

        public Guid Id { get; }

        public IPEndPoint EndPoint { get; }

        public long LastSeenTicks { get; set; }
    }
}
