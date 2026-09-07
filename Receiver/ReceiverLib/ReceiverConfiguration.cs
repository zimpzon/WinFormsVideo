using Protocol;

namespace ReceiverLib;

/// <summary>Configuration for a <see cref="VideoReceiver"/>.</summary>
public sealed class ReceiverConfiguration
{
    /// <summary>Address of the sender's stream server.</summary>
    public string SenderAddress { get; set; } = "127.0.0.1";

    /// <summary>Port of the sender's stream server.</summary>
    public int SenderPort { get; set; }

    /// <summary>Network transport. Must match the sender. Defaults to reliable TCP.</summary>
    public TransportKind Transport { get; set; } = TransportKind.Tcp;

    // TODO: jitter-buffer depth / max latency once receive-side pacing is implemented.
}
