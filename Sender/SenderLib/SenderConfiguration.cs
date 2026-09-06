namespace SenderLib;

/// <summary>
/// Configuration for a <see cref="VideoSender"/>.
/// </summary>
/// <remarks>
/// The sender acts as a TCP server: it binds <see cref="ListenAddress"/>:<see cref="ListenPort"/>
/// and streams to whichever receivers connect (including zero). The WinForms sender's
/// "destination IP / port" fields map onto this listen endpoint.
/// </remarks>
public sealed class SenderConfiguration
{
    /// <summary>Local address to bind the stream server to. Defaults to all interfaces.</summary>
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>TCP port receivers connect to.</summary>
    public int ListenPort { get; set; }

    // TODO: encoder / bitrate / keyframe-interval settings once encoding is implemented.
}
