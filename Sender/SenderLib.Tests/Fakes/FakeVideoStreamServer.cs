using Protocol;
using SenderLib;

namespace SenderLib.Tests.Fakes;

/// <summary>In-memory <see cref="IVideoStreamServer"/> that records broadcast frames.</summary>
internal sealed class FakeVideoStreamServer : IVideoStreamServer
{
    private readonly object _gate = new();
    private readonly List<EncodedFrame> _broadcasts = new();

    public bool IsRunning { get; private set; }

    public bool IsDisposed { get; private set; }

    public int ReceiverCount { get; set; }

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverConnected;

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverDisconnected;

    public int BroadcastCount
    {
        get { lock (_gate) return _broadcasts.Count; }
    }

    public IReadOnlyList<EncodedFrame> Broadcasts
    {
        get { lock (_gate) return _broadcasts.ToArray(); }
    }

    public StreamInfo StartedWith { get; private set; }

    public void Start(StreamInfo streamInfo)
    {
        StartedWith = streamInfo;
        IsRunning = true;
    }

    public void Stop() => IsRunning = false;

    public void Broadcast(in EncodedFrame frame)
    {
        lock (_gate)
        {
            _broadcasts.Add(frame);
        }
    }

    public void Dispose() => IsDisposed = true;

    public void SimulateReceiverConnected(Guid id) =>
        ReceiverConnected?.Invoke(this, new ReceiverConnectionEventArgs(id, "127.0.0.1:0", DateTimeOffset.UtcNow));

    public void SimulateReceiverDisconnected(Guid id) =>
        ReceiverDisconnected?.Invoke(this, new ReceiverConnectionEventArgs(id, "127.0.0.1:0", DateTimeOffset.UtcNow));
}
