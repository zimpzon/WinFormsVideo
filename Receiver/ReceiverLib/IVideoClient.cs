using Protocol;

namespace ReceiverLib;

/// <summary>
/// Connects to the sender's stream server, reads the <see cref="DatagramProtocol"/> handshake
/// and then the packet stream. The concrete implementation (UDP) is an internal detail; the
/// pipeline depends only on this abstraction so it can be faked in tests.
/// </summary>
public interface IVideoClient : IDisposable
{
    /// <summary>Open the connection and read the handshake.</summary>
    void Connect();

    /// <summary>Close the connection.</summary>
    void Disconnect();

    /// <summary>The stream description from the handshake. Null until <see cref="Connect"/> succeeds.</summary>
    StreamInfo? StreamInfo { get; }

    /// <summary>
    /// Whole frames the transport lost (missing or out-of-order fragments). Always 0 for a reliable
    /// transport (TCP).
    /// </summary>
    long FramesDropped { get; }

    /// <summary>Read the next packet in arrival order. Returns false when the stream ends.</summary>
    bool TryReadPacket(out ReceivedPacket packet);

    /// <summary>Raised after the connection is established and the handshake read.</summary>
    event EventHandler? Connected;

    /// <summary>Raised after the connection closes for any reason.</summary>
    event EventHandler? Disconnected;
}
