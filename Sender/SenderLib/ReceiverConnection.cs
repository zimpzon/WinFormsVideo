namespace SenderLib;

/// <summary>
/// Represents a single receiver connected to the <see cref="TcpVideoStreamServer"/>.
/// Owns that receiver's socket and its outbound send buffer.
/// </summary>
internal sealed class ReceiverConnection
{
    public ReceiverConnection(Guid id, string remoteEndPoint, DateTimeOffset connectedAt)
    {
        Id = id;
        RemoteEndPoint = remoteEndPoint;
        ConnectedAt = connectedAt;
    }

    public Guid Id { get; }

    public string RemoteEndPoint { get; }

    public DateTimeOffset ConnectedAt { get; }

    /// <summary>Queue/send a frame to this receiver, dropping stale frames if it falls behind.</summary>
    public void Send(in EncodedFrame frame) => throw new NotImplementedException();

    /// <summary>Close the socket for this receiver.</summary>
    public void Close() => throw new NotImplementedException();
}
