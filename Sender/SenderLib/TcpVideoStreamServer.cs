using System.Net;
using System.Net.Sockets;
using Protocol;

namespace SenderLib;

/// <summary>
/// TCP implementation of <see cref="IVideoStreamServer"/>: listens on the configured endpoint,
/// tracks connected <see cref="ReceiverConnection"/>s and fans out frames using
/// <see cref="StreamProtocol"/>. Runs independently of playback — broadcasting with zero receivers
/// is a no-op, and a receiver dropping never faults the sender.
/// </summary>
internal sealed class TcpVideoStreamServer : IVideoStreamServer
{
    private readonly SenderConfiguration _configuration;
    private readonly object _gate = new();
    private readonly List<ReceiverConnection> _connections = new();

    // Immutable snapshot of _connections, rebuilt only when the set changes — so Broadcast
    // does not allocate per frame.
    private ReceiverConnection[] _snapshot = Array.Empty<ReceiverConnection>();
    private bool _snapshotDirty;

    /// <summary>Test hook: how many times <see cref="Broadcast"/> has rebuilt the connection snapshot.</summary>
    internal int SnapshotRebuildCount { get; private set; }

    private TcpListener? _listener;
    private Thread? _acceptThread;
    private CancellationTokenSource? _cts;
    private volatile bool _running;
    private long _sequence;

    private StreamInfo _streamInfo;
    private byte[] _lastKeyframe = Array.Empty<byte>();
    private bool _hasLastKeyframe;
    private int _lastKeyframeLength;
    private long _lastKeyframeTicks;
    private long _lastKeyframeSequence;

    public TcpVideoStreamServer(SenderConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>Per-receiver send timeout; after this the receiver is considered dead and dropped.</summary>
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverConnected;

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverDisconnected;

    public int ReceiverCount
    {
        get { lock (_gate) return _connections.Count; }
    }

    /// <summary>The endpoint the listener actually bound to (useful when the configured port is 0).</summary>
    public IPEndPoint? LocalEndPoint => _listener?.LocalEndpoint as IPEndPoint;

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
        _listener = new TcpListener(address, _configuration.ListenPort);
        _listener.Start();
        _running = true;

        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "SenderLib.Accept" };
        _acceptThread.Start();
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* already stopped */ }
        _acceptThread?.Join();

        ReceiverConnection[] connections;
        lock (_gate)
        {
            connections = _connections.ToArray();
            _connections.Clear();
            _snapshot = Array.Empty<ReceiverConnection>();
            _snapshotDirty = false;
        }

        foreach (ReceiverConnection connection in connections)
        {
            connection.Dispose();
            RaiseDisconnected(connection);
        }

        FreeLastKeyframe();

        _cts?.Dispose();
        _cts = null;
        _listener = null;
        _acceptThread = null;
    }

    public void Broadcast(in EncodedFrame frame)
    {
        if (!_running)
        {
            return;
        }

        long sequence = Interlocked.Increment(ref _sequence);

        if (frame.IsKeyFrame)
        {
            StoreLastKeyframe(in frame, sequence);
        }

        ReceiverConnection[] snapshot;
        lock (_gate)
        {
            if (_snapshotDirty)
            {
                _snapshot = _connections.Count == 0
                    ? Array.Empty<ReceiverConnection>()
                    : _connections.ToArray();
                _snapshotDirty = false;
                SnapshotRebuildCount++;
            }

            snapshot = _snapshot;
        }

        foreach (ReceiverConnection connection in snapshot)
        {
            if (connection.IsClosed)
            {
                Reap(connection);
                continue;
            }

            connection.Send(in frame, sequence);
        }
    }

    public void Dispose() => Stop();

    private void AcceptLoop()
    {
        while (_running)
        {
            Socket socket;
            try
            {
                socket = _listener!.AcceptSocket();
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }

            var connection = new ReceiverConnection(socket, OnConnectionClosed, SendTimeout);
            if (!connection.WriteHandshake(_streamInfo))
            {
                connection.Dispose();
                continue;
            }

            lock (_gate)
            {
                if (_hasLastKeyframe)
                {
                    var keyframe = new EncodedFrame(
                        new TimeSpan(_lastKeyframeTicks),
                        isKeyFrame: true,
                        _lastKeyframe.AsMemory(0, _lastKeyframeLength));
                    connection.Send(in keyframe, _lastKeyframeSequence);
                }

                _connections.Add(connection);
                _snapshotDirty = true;
            }

            RaiseConnected(connection);
        }
    }

    private void OnConnectionClosed(ReceiverConnection connection) => Reap(connection);

    private void Reap(ReceiverConnection connection)
    {
        bool removed;
        lock (_gate)
        {
            removed = _connections.Remove(connection);
            if (removed)
            {
                _snapshotDirty = true;
            }
        }

        if (!removed)
        {
            return;
        }

        RaiseDisconnected(connection);

        // Dispose off the current thread — it may be the connection's own writer/reader thread.
        ThreadPool.QueueUserWorkItem(static state => ((ReceiverConnection)state!).Dispose(), connection);
    }

    private void StoreLastKeyframe(in EncodedFrame frame, long sequence)
    {
        lock (_gate)
        {
            int length = frame.Data.Length;
            BufferUtil.EnsureCapacity(ref _lastKeyframe, length); // grows only on a bigger keyframe
            frame.Data.Span.CopyTo(_lastKeyframe);

            _hasLastKeyframe = true;
            _lastKeyframeLength = length;
            _lastKeyframeTicks = frame.Timestamp.Ticks;
            _lastKeyframeSequence = sequence;
        }
    }

    private void FreeLastKeyframe()
    {
        lock (_gate)
        {
            _hasLastKeyframe = false;
            _lastKeyframeLength = 0;
        }
    }

    private void RaiseConnected(ReceiverConnection connection) =>
        ReceiverConnected?.Invoke(this, ToEventArgs(connection));

    private void RaiseDisconnected(ReceiverConnection connection) =>
        ReceiverDisconnected?.Invoke(this, ToEventArgs(connection));

    private static ReceiverConnectionEventArgs ToEventArgs(ReceiverConnection connection) =>
        new(connection.Id, connection.RemoteEndPoint, DateTimeOffset.UtcNow);

    private static IPAddress ResolveAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "0.0.0.0")
        {
            return IPAddress.Any;
        }

        return value == "::" ? IPAddress.IPv6Any : IPAddress.Parse(value);
    }
}
