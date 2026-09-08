namespace ReceiverLib;

/// <summary>
/// Describes a runtime problem raised by the receiver. Reported through
/// <see cref="VideoReceiver.ErrorOccurred"/> rather than thrown across the public API.
/// </summary>
public sealed class ReceiverError
{
    public ReceiverError(ReceiverErrorKind kind, string message, Exception? exception = null)
    {
        Kind = kind;
        Message = message;
        Exception = exception;
    }

    /// <summary>What went wrong, as a stable category. Branch on this — not on <see cref="Message"/>.</summary>
    public ReceiverErrorKind Kind { get; }

    /// <summary>A human-readable description, suitable for a status line or a log.</summary>
    public string Message { get; }

    /// <summary>The underlying exception, when there was one — for logging, not for display.</summary>
    public Exception? Exception { get; }
}
