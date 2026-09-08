namespace ReceiverLib;

/// <summary>Category of a <see cref="ReceiverError"/>, so the UI can react without parsing messages.</summary>
public enum ReceiverErrorKind
{
    /// <summary>
    /// <see cref="VideoReceiver.Connect"/> could not establish a stream — nothing is being streamed
    /// at that address/port, or the sender never completed the handshake. This is the "no video to
    /// show" case; the receiver ends in <see cref="ReceiverState.Faulted"/> and
    /// <see cref="VideoReceiver.Connect"/> can be called again to retry.
    /// </summary>
    ConnectionFailed,

    /// <summary>The stream was running and then the connection dropped (sender went away, network).</summary>
    ConnectionLost,

    /// <summary>The bytes on the wire did not match <see cref="Protocol.DatagramProtocol"/>.</summary>
    ProtocolError,

    /// <summary>The decoder failed on a received packet.</summary>
    DecodeError,

    /// <summary>The stream's codec is not supported.</summary>
    UnsupportedCodec,

    /// <summary>The supplied <see cref="ReceiverConfiguration"/> is invalid.</summary>
    ConfigurationError,

    /// <summary>Anything not covered by the other kinds.</summary>
    Unknown,
}
