namespace Protocol;

/// <summary>Which network transport the sender and receiver use.</summary>
public enum TransportKind
{
    /// <summary>Reliable, ordered, connection-oriented. Frames always arrive intact.</summary>
    Tcp,

    /// <summary>Connectionless, unreliable. A frame with a lost or out-of-order fragment is dropped whole.</summary>
    Udp,
}
