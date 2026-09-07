namespace ReceiverLib;

/// <summary>High-level state of the receiver pipeline.</summary>
public enum ReceiverState
{
    /// <summary>Not connected.</summary>
    Idle,

    /// <summary>Opening the TCP connection and reading the handshake.</summary>
    Connecting,

    /// <summary>Connected; filling the jitter buffer before playback starts.</summary>
    Buffering,

    /// <summary>Decoding and presenting frames.</summary>
    Playing,

    /// <summary>Viewing paused; the connection is kept open.</summary>
    Paused,

    /// <summary>Disconnected by the user or because the stream ended.</summary>
    Stopped,

    /// <summary>An unrecoverable error occurred; see the reported <see cref="ReceiverError"/>.</summary>
    Faulted,
}
