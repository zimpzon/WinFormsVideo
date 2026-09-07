using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Protocol;

namespace ReceiverLib;

/// <summary>
/// UDP implementation of <see cref="IVideoClient"/>. Sends a <see cref="DatagramType.Subscribe"/>
/// datagram (repeated as a keepalive), reassembles <see cref="DatagramType.FrameFragment"/> datagrams
/// into whole frames, and <b>drops any frame that has a missing or out-of-order fragment</b> — no
/// retransmit, no reorder buffer.
/// </summary>
internal sealed class UdpVideoClient : IVideoClient
{
    private readonly ReceiverConfiguration _configuration;
    private readonly byte[] _recvBuffer = new byte[DatagramProtocol.MaxDatagramSize];
    private readonly byte[] _control = new byte[DatagramProtocol.PrefixSize];

    private Socket? _socket;
    private int _disconnectRaised;
    private long _lastKeepaliveTicks;
    private long _framesDropped;

    // reassembly state (grow-only buffers → no per-frame allocation)
    private byte[] _frameBuffer = Array.Empty<byte>();
    private bool[] _fragmentSeen = Array.Empty<bool>();
    private long _currentSequence = -1;
    private int _expectedFragments;
    private int _receivedFragments;
    private int _currentTotalLength;
    private long _currentTicks;
    private FrameFlags _currentFlags;
    private bool _currentEmitted;

    public UdpVideoClient(ReceiverConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan KeepaliveInterval { get; init; } = TimeSpan.FromSeconds(2);

    public StreamInfo? StreamInfo { get; private set; }

    public long FramesDropped => Interlocked.Read(ref _framesDropped);

    public event EventHandler? Connected;

    public event EventHandler? Disconnected;

    public void Connect()
    {
        if (_socket is not null)
        {
            return;
        }

        IPEndPoint senderEndPoint = ResolveSender();
        var socket = new Socket(senderEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            socket.Bind(new IPEndPoint(
                senderEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
            try { socket.ReceiveBufferSize = 4 * 1024 * 1024; } catch { /* best effort */ }
            socket.Connect(senderEndPoint); // fixes the peer: Send/Receive need no EndPoint, others are filtered out
            socket.ReceiveTimeout = (int)KeepaliveInterval.TotalMilliseconds;

            StreamInfo info = Handshake(socket);

            _socket = socket;
            _disconnectRaised = 0;
            _lastKeepaliveTicks = Environment.TickCount64;
            StreamInfo = info;
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        Connected?.Invoke(this, EventArgs.Empty);
    }

    public bool TryReadPacket(out ReceivedPacket packet)
    {
        packet = default;
        Socket? socket = _socket;
        if (socket is null)
        {
            return false;
        }

        while (true)
        {
            SendKeepaliveIfDue(socket);

            int n;
            try
            {
                n = socket.Receive(_recvBuffer);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                continue; // wake up just to re-send the keepalive
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                MarkDisconnected();
                return false;
            }

            ReadOnlySpan<byte> datagram = _recvBuffer.AsSpan(0, n);
            if (!DatagramProtocol.TryReadType(datagram, out DatagramType type))
            {
                continue;
            }

            if (type == DatagramType.Bye)
            {
                MarkDisconnected();
                return false;
            }

            if (type == DatagramType.StreamInfo)
            {
                if (DatagramProtocol.TryReadStreamInfo(datagram, out StreamInfo refreshed))
                {
                    StreamInfo = refreshed;
                }

                continue;
            }

            if (type != DatagramType.FrameFragment ||
                !DatagramProtocol.TryReadFrameFragment(datagram, out FragmentHeader header, out ReadOnlySpan<byte> fragment))
            {
                continue;
            }

            if (TryAccept(header, fragment, out packet))
            {
                return true;
            }
        }
    }

    public void Disconnect()
    {
        Socket? socket = _socket;
        _socket = null;

        if (socket is not null)
        {
            try
            {
                DatagramProtocol.WriteBye(_control);
                socket.Send(_control);
            }
            catch { /* best effort */ }

            try { socket.Dispose(); } catch { /* already gone */ }
        }

        MarkDisconnected();
    }

    public void Dispose() => Disconnect();

    // --- reassembly ---------------------------------------------------

    private bool TryAccept(in FragmentHeader header, ReadOnlySpan<byte> fragment, out ReceivedPacket packet)
    {
        packet = default;

        if (_currentSequence >= 0 && header.FrameSequence < _currentSequence)
        {
            return false; // out of order / late — drop
        }

        if (header.FrameSequence != _currentSequence)
        {
            StartFrame(header); // abandons the previous frame if it was incomplete
        }

        if (_currentEmitted || _fragmentSeen[header.FragmentIndex])
        {
            return false;
        }

        int offset = header.FragmentIndex == header.FragmentCount - 1
            ? _currentTotalLength - header.FragmentLength
            : header.FragmentIndex * header.FragmentLength;

        if (offset < 0 || offset + header.FragmentLength > _currentTotalLength)
        {
            return false; // malformed
        }

        fragment.CopyTo(_frameBuffer.AsSpan(offset, header.FragmentLength));
        _fragmentSeen[header.FragmentIndex] = true;
        _receivedFragments++;

        if (_receivedFragments != _expectedFragments)
        {
            return false;
        }

        _currentEmitted = true;
        packet = new ReceivedPacket(
            _currentSequence,
            new TimeSpan(_currentTicks),
            (_currentFlags & FrameFlags.KeyFrame) != 0,
            _frameBuffer.AsMemory(0, _currentTotalLength));
        return true;
    }

    private void StartFrame(in FragmentHeader header)
    {
        if (_currentSequence >= 0)
        {
            long dropped = header.FrameSequence - _currentSequence - 1; // frames skipped entirely
            if (!_currentEmitted && _receivedFragments > 0)
            {
                dropped++; // the current frame arrived partially but never completed
            }

            if (dropped > 0)
            {
                Interlocked.Add(ref _framesDropped, dropped);
            }
        }

        _currentSequence = header.FrameSequence;
        _currentTotalLength = header.TotalLength;
        _currentTicks = header.TimestampTicks;
        _currentFlags = header.Flags;
        _expectedFragments = header.FragmentCount;
        _receivedFragments = 0;
        _currentEmitted = false;

        BufferUtil.EnsureCapacity(ref _frameBuffer, header.TotalLength);
        if (_fragmentSeen.Length < header.FragmentCount)
        {
            _fragmentSeen = new bool[header.FragmentCount];
        }
        else
        {
            Array.Clear(_fragmentSeen, 0, header.FragmentCount);
        }
    }

    // --- socket helpers --------------------------------------------

    private StreamInfo Handshake(Socket socket)
    {
        var deadline = Stopwatch.StartNew();
        DatagramProtocol.WriteSubscribe(_control);

        while (deadline.Elapsed < HandshakeTimeout)
        {
            socket.Send(_control);

            int n;
            try
            {
                n = socket.Receive(_recvBuffer);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                continue;
            }

            if (DatagramProtocol.TryReadStreamInfo(_recvBuffer.AsSpan(0, n), out StreamInfo info))
            {
                return info;
            }
        }

        throw new SocketException((int)SocketError.TimedOut);
    }

    private void SendKeepaliveIfDue(Socket socket)
    {
        long now = Environment.TickCount64;
        if (now - _lastKeepaliveTicks < KeepaliveInterval.TotalMilliseconds)
        {
            return;
        }

        _lastKeepaliveTicks = now;
        try
        {
            DatagramProtocol.WriteSubscribe(_control);
            socket.Send(_control);
        }
        catch { /* best effort */ }
    }

    private void MarkDisconnected()
    {
        if (Interlocked.Exchange(ref _disconnectRaised, 1) == 0)
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private IPEndPoint ResolveSender()
    {
        string host = _configuration.SenderAddress;
        if (!IPAddress.TryParse(host, out IPAddress? address))
        {
            address = Array.Find(Dns.GetHostAddresses(host), a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? Dns.GetHostAddresses(host)[0];
        }

        return new IPEndPoint(address, _configuration.SenderPort);
    }
}
