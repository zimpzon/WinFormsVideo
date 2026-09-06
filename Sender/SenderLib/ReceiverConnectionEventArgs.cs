namespace SenderLib;

/// <summary>Raised when a receiver connects to or disconnects from the stream server.</summary>
public sealed class ReceiverConnectionEventArgs : EventArgs
{
    public ReceiverConnectionEventArgs(Guid receiverId, string remoteEndPoint, DateTimeOffset timestamp)
    {
        ReceiverId = receiverId;
        RemoteEndPoint = remoteEndPoint;
        Timestamp = timestamp;
    }

    /// <summary>Stable identifier for the receiver connection.</summary>
    public Guid ReceiverId { get; }

    /// <summary>Remote endpoint as "address:port".</summary>
    public string RemoteEndPoint { get; }

    /// <summary>When the connect/disconnect happened.</summary>
    public DateTimeOffset Timestamp { get; }
}
