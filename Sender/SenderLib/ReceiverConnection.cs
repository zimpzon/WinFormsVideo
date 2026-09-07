using System.Buffers;
using System.Net.Sockets;
using Protocol;

namespace SenderLib;

/// <summary>
/// One receiver connected to <see cref="TcpVideoStreamServer"/>. Owns the socket and a bounded
/// outbound queue drained by a dedicated writer thread. If the receiver falls behind, obsolete
/// (non-key) frames are dropped rather than letting the queue — and latency — grow without bound.
/// </summary>
internal sealed class ReceiverConnection : IDisposable
{
    private const int MaxQueuedFrames = 120;
    private const long MaxLifetimeDrops = 600;

    private readonly Socket _socket;
    private readonly Action<ReceiverConnection>? _onClosed;
    private readonly object _gate = new();
    private readonly Queue<QueuedFrame> _queue = new();
    private readonly ManualResetEventSlim _signal = new(false);
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _writer;
    private readonly Thread _reader;

    private long _framesSent;
    private long _framesDropped;
    private int _closed;
    private int _disposed;

    public ReceiverConnection(Socket socket, Action<ReceiverConnection>? onClosed = null, TimeSpan sendTimeout = default)
    {
        _socket = socket;
        _socket.NoDelay = true;
        _socket.SendTimeout = (int)(sendTimeout == default ? TimeSpan.FromSeconds(5) : sendTimeout).TotalMilliseconds;
        _onClosed = onClosed;

        Id = Guid.NewGuid();
        RemoteEndPoint = socket.RemoteEndPoint?.ToString() ?? "unknown";
        ConnectedAt = DateTimeOffset.UtcNow;

        _writer = new Thread(WriterLoop) { IsBackground = true, Name = "SenderLib.ReceiverWriter" };
        _writer.Start();

        // The receiver never sends to us; a read that returns 0 / throws means it is gone.
        _reader = new Thread(ReaderLoop) { IsBackground = true, Name = "SenderLib.ReceiverReader" };
        _reader.Start();
    }

    public Guid Id { get; }

    public string RemoteEndPoint { get; }

    public DateTimeOffset ConnectedAt { get; }

    public long FramesSent => Interlocked.Read(ref _framesSent);

    public long FramesDropped => Interlocked.Read(ref _framesDropped);

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    /// <summary>Write the protocol handshake synchronously. Returns false if the socket is already dead.</summary>
    public bool WriteHandshake(in StreamInfo streamInfo)
    {
        var buffer = new byte[StreamProtocol.HandshakeSize(streamInfo)];
        StreamProtocol.TryWriteHandshake(buffer, streamInfo);
        try
        {
            SendAll(buffer);
            return true;
        }
        catch (Exception)
        {
            MarkClosed();
            return false;
        }
    }

    /// <summary>Queue a frame for this receiver. Never blocks the caller.</summary>
    public void Send(in EncodedFrame frame, long sequenceNumber)
    {
        if (IsClosed)
        {
            return;
        }

        int length = frame.Data.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        frame.Data.Span.CopyTo(buffer);
        var queued = new QueuedFrame(buffer, length, sequenceNumber, frame.Timestamp.Ticks, frame.IsKeyFrame);

        lock (_gate)
        {
            if (_queue.Count >= MaxQueuedFrames)
            {
                EvictOne();
            }

            _queue.Enqueue(queued);
        }

        _signal.Set();

        if (FramesDropped > MaxLifetimeDrops)
        {
            // Hopelessly behind — stop wasting memory on it.
            MarkClosed();
        }
    }

    public void Close() => MarkClosed();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        _signal.Set();
        MarkClosed();

        if (_writer != Thread.CurrentThread)
        {
            _writer.Join();
        }

        if (_reader != Thread.CurrentThread)
        {
            _reader.Join();
        }

        DrainQueue();
        _cts.Dispose();
        _signal.Dispose();
    }

    private void ReaderLoop()
    {
        var scratch = new byte[256];
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_socket.Receive(scratch, SocketFlags.None) <= 0)
                {
                    break;
                }
            }
        }
        catch (Exception)
        {
            // Socket error — the receiver is gone.
        }
        finally
        {
            MarkClosed();
        }
    }

    private void WriterLoop()
    {
        var headerBuffer = new byte[StreamProtocol.HeaderSize];

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                QueuedFrame item;
                bool hasItem;

                lock (_gate)
                {
                    hasItem = _queue.TryDequeue(out item);
                    if (!hasItem)
                    {
                        _signal.Reset();
                    }
                }

                if (!hasItem)
                {
                    _signal.Wait(_cts.Token);
                    continue;
                }

                try
                {
                    var header = new FrameHeader(
                        item.Sequence,
                        item.TimestampTicks,
                        item.IsKeyFrame ? FrameFlags.KeyFrame : FrameFlags.None,
                        item.Length);
                    header.TryWrite(headerBuffer);

                    SendAll(headerBuffer);
                    SendAll(item.Buffer.AsSpan(0, item.Length));
                    Interlocked.Increment(ref _framesSent);
                }
                catch (Exception)
                {
                    ArrayPool<byte>.Shared.Return(item.Buffer);
                    return;
                }

                ArrayPool<byte>.Shared.Return(item.Buffer);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            MarkClosed();
            DrainQueue();
        }
    }

    /// <summary>Caller holds <see cref="_gate"/>. Drops the oldest non-key frame, or the oldest frame if all are keys.</summary>
    private void EvictOne()
    {
        int count = _queue.Count;
        bool removed = false;

        for (int i = 0; i < count; i++)
        {
            QueuedFrame item = _queue.Dequeue();
            if (!removed && !item.IsKeyFrame)
            {
                ArrayPool<byte>.Shared.Return(item.Buffer);
                removed = true;
                continue;
            }

            _queue.Enqueue(item);
        }

        if (!removed && _queue.TryDequeue(out QueuedFrame oldest))
        {
            ArrayPool<byte>.Shared.Return(oldest.Buffer);
            removed = true;
        }

        if (removed)
        {
            Interlocked.Increment(ref _framesDropped);
        }
    }

    private void DrainQueue()
    {
        lock (_gate)
        {
            while (_queue.TryDequeue(out QueuedFrame item))
            {
                ArrayPool<byte>.Shared.Return(item.Buffer);
            }
        }
    }

    private void SendAll(ReadOnlySpan<byte> data)
    {
        while (!data.IsEmpty)
        {
            int sent = _socket.Send(data, SocketFlags.None);
            if (sent <= 0)
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }

            data = data[sent..];
        }
    }

    private void MarkClosed()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        try { _socket.Shutdown(SocketShutdown.Both); } catch { /* already gone */ }
        try { _socket.Close(); } catch { /* already gone */ }

        _onClosed?.Invoke(this);
    }

    private readonly struct QueuedFrame
    {
        public QueuedFrame(byte[] buffer, int length, long sequence, long timestampTicks, bool isKeyFrame)
        {
            Buffer = buffer;
            Length = length;
            Sequence = sequence;
            TimestampTicks = timestampTicks;
            IsKeyFrame = isKeyFrame;
        }

        public byte[] Buffer { get; }

        public int Length { get; }

        public long Sequence { get; }

        public long TimestampTicks { get; }

        public bool IsKeyFrame { get; }
    }
}
