namespace SenderLib;

/// <summary>
/// Describes a runtime problem raised by the sender. Reported through
/// <see cref="VideoSender.ErrorOccurred"/> rather than thrown across the public API.
/// </summary>
public sealed class SenderError
{
    public SenderError(SenderErrorKind kind, string message, Exception? exception = null)
    {
        Kind = kind;
        Message = message;
        Exception = exception;
    }

    /// <summary>Category of the error.</summary>
    public SenderErrorKind Kind { get; }

    /// <summary>Human-readable description, suitable for display in the UI.</summary>
    public string Message { get; }

    /// <summary>The underlying exception, if any.</summary>
    public Exception? Exception { get; }
}
