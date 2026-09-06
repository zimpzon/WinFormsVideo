namespace SenderLib;

/// <summary>
/// Category of a <see cref="SenderError"/>, so the UI can react without parsing messages.
/// </summary>
public enum SenderErrorKind
{
    /// <summary>The file does not exist or is not a readable media container.</summary>
    InvalidFile,

    /// <summary>The container was read but its video codec is not supported.</summary>
    UnsupportedCodec,

    /// <summary>Decoding / demuxing failed part-way through the stream.</summary>
    DecodeError,

    /// <summary>A network / socket operation failed.</summary>
    NetworkError,

    /// <summary>The supplied <see cref="SenderConfiguration"/> is invalid.</summary>
    ConfigurationError,

    /// <summary>Anything not covered by the other kinds.</summary>
    Unknown,
}
