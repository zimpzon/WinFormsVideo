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

    public ReceiverErrorKind Kind { get; }

    public string Message { get; }

    public Exception? Exception { get; }
}
