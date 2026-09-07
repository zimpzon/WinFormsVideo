using Protocol;
using ReceiverLib;

namespace ReceiverLib.Tests.Fakes;

/// <summary>In-memory <see cref="IVideoClient"/> for tests: replays a scripted packet list.</summary>
internal sealed class FakeVideoClient : IVideoClient
{
    private readonly object _gate = new();
    private readonly Queue<ReceivedPacket> _packets = new();
    private readonly ManualResetEventSlim _signal = new(false);
    private bool _ended;

    public FakeVideoClient(StreamInfo? streamInfo = null, IEnumerable<ReceivedPacket>? packets = null, bool blockWhenEmpty = false)
    {
        StreamInfo = streamInfo ?? new StreamInfo(27, 320, 240, new byte[] { 1, 2, 3, 4 });
        BlockWhenEmpty = blockWhenEmpty;
        if (packets is not null)
        {
            foreach (ReceivedPacket packet in packets)
            {
                _packets.Enqueue(packet);
            }
        }
    }

    /// <summary>When true, <see cref="TryReadPacket"/> blocks on an empty queue until more packets
    /// are added or the client is disconnected (so the pump stays alive).</summary>
    public bool BlockWhenEmpty { get; }

    public StreamInfo? StreamInfo { get; }

    public long FramesDropped { get; set; }

    public bool IsConnected { get; private set; }

    public bool IsDisposed { get; private set; }

    public event EventHandler? Connected;

    public event EventHandler? Disconnected;

    /// <summary>When set, <see cref="Connect"/> throws this.</summary>
    public Exception? ThrowOnConnect { get; set; }

    public void Connect()
    {
        if (ThrowOnConnect is not null)
        {
            throw ThrowOnConnect;
        }

        IsConnected = true;
        Connected?.Invoke(this, EventArgs.Empty);
    }

    public void Disconnect()
    {
        if (!IsConnected)
        {
            return;
        }

        IsConnected = false;
        _ended = true;
        _signal.Set();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Feed more packets to a running pump.</summary>
    public void Push(params ReceivedPacket[] packets)
    {
        lock (_gate)
        {
            foreach (ReceivedPacket p in packets)
            {
                _packets.Enqueue(p);
            }
        }

        _signal.Set();
    }

    /// <summary>Signal end-of-stream to a blocking pump.</summary>
    public void EndStream()
    {
        _ended = true;
        _signal.Set();
    }

    public bool TryReadPacket(out ReceivedPacket packet)
    {
        while (true)
        {
            lock (_gate)
            {
                if (_packets.Count > 0)
                {
                    packet = _packets.Dequeue();
                    return true;
                }

                if (_ended || !BlockWhenEmpty)
                {
                    packet = default;
                    return false;
                }

                _signal.Reset();
            }

            _signal.Wait();
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        _ended = true;
        _signal.Set();
    }
}
