namespace ReceiverLib;

/// <summary>Category of a <see cref="ReceiverError"/>, so the UI can react without parsing messages.</summary>
public enum ReceiverErrorKind
{
    /// <summary>Could not open the connection to the sender.</summary>
    ConnectionFailed,

    /// <summary>The connection dropped mid-stream.</summary>
    ConnectionLost,

    /// <summary>The bytes on the wire did not match <see cref="Protocol.StreamProtocol"/>.</summary>
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
