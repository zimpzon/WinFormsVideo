using Protocol;

namespace SenderLib;

/// <summary>
/// Configuration for a <see cref="VideoSender"/>.
/// </summary>
/// <remarks>
/// The sender binds <see cref="ListenAddress"/>:<see cref="ListenPort"/> and streams to whichever
/// receivers connect/subscribe (including zero). The WinForms sender's "destination IP / port"
/// fields map onto this listen endpoint.
/// </remarks>
public sealed class SenderConfiguration
{
    /// <summary>Local address to bind the stream server to. Defaults to all interfaces.</summary>
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>Port receivers connect to.</summary>
    public int ListenPort { get; set; }

    /// <summary>Network transport. Defaults to reliable TCP.</summary>
    public TransportKind Transport { get; set; } = TransportKind.Tcp;

    // TODO: encoder / bitrate / keyframe-interval settings once encoding is implemented.
}
