namespace SenderLib;

/// <summary>Raised when the sender encounters a recoverable or unrecoverable error.</summary>
public sealed class SenderErrorEventArgs : EventArgs
{
    public SenderErrorEventArgs(SenderError error)
    {
        Error = error;
    }

    public SenderError Error { get; }
}
