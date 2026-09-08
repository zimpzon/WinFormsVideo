namespace ReceiverLib;

/// <summary>Configuration for a <see cref="VideoReceiver"/>.</summary>
public sealed class ReceiverConfiguration
{
    /// <summary>Address of the sender's stream server.</summary>
    public string SenderAddress { get; set; } = "127.0.0.1";

    /// <summary>Port of the sender's stream server.</summary>
    public int SenderPort { get; set; }

    // TODO: jitter-buffer depth / max latency once receive-side pacing is implemented.
}
