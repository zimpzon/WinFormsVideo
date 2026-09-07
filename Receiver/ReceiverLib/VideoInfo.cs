namespace ReceiverLib;

/// <summary>
/// Description of the incoming video, learned from the stream handshake.
/// The receiver's own type — independent of the sender's <c>SenderLib.VideoInfo</c>.
/// </summary>
public sealed class VideoInfo
{
    public VideoInfo(int width, int height, double frameRate, string codecName)
    {
        Width = width;
        Height = height;
        FrameRate = frameRate;
        CodecName = codecName;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Nominal frames per second, or 0 if unknown.</summary>
    public double FrameRate { get; }

    /// <summary>Codec short name (e.g. "h264"), for display.</summary>
    public string CodecName { get; }
}
