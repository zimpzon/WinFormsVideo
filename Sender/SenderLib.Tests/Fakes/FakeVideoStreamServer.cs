using SenderLib;

namespace SenderLib.Tests.Fakes;

/// <summary>In-memory <see cref="IVideoStreamServer"/> that records broadcast frames.</summary>
internal sealed class FakeVideoStreamServer : IVideoStreamServer
{
    public List<EncodedFrame> Broadcasts { get; } = new();

    public bool IsRunning { get; private set; }

    public bool IsDisposed { get; private set; }

    public int ReceiverCount { get; set; }

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverConnected;

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverDisconnected;

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void Broadcast(in EncodedFrame frame) => Broadcasts.Add(frame);

    public void Dispose() => IsDisposed = true;

    /// <summary>Test helper: simulate a receiver connecting.</summary>
    public void SimulateReceiverConnected(Guid id) =>
        ReceiverConnected?.Invoke(this, new ReceiverConnectionEventArgs(id, "127.0.0.1:0", DateTimeOffset.UtcNow));

    /// <summary>Test helper: simulate a receiver disconnecting.</summary>
    public void SimulateReceiverDisconnected(Guid id) =>
        ReceiverDisconnected?.Invoke(this, new ReceiverConnectionEventArgs(id, "127.0.0.1:0", DateTimeOffset.UtcNow));
}
