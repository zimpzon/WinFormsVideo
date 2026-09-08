namespace SenderLib;

/// <summary>
/// Configuration for a <see cref="VideoSender"/>.
/// </summary>
/// <remarks>
/// The sender binds <see cref="ListenAddress"/>:<see cref="ListenPort"/> and streams over UDP to
/// whichever receivers subscribe (including zero). The WinForms sender's "destination IP / port"
/// fields map onto this listen endpoint.
/// </remarks>
public sealed class SenderConfiguration
{
    /// <summary>Local address to bind the stream server to. Defaults to all interfaces.</summary>
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>Port receivers subscribe to.</summary>
    public int ListenPort { get; set; }

    // TODO: encoder / bitrate / keyframe-interval settings once encoding is implemented.
}
