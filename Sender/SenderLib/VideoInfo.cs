namespace SenderLib;

/// <summary>
/// Immutable description of an opened video, probed from the source container.
/// </summary>
public sealed class VideoInfo
{
    public VideoInfo(
        int width,
        int height,
        TimeSpan duration,
        double frameRate,
        string codecName,
        long totalFrames)
    {
        Width = width;
        Height = height;
        Duration = duration;
        FrameRate = frameRate;
        CodecName = codecName;
        TotalFrames = totalFrames;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Total playback duration of the source.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Nominal frames per second.</summary>
    public double FrameRate { get; }

    /// <summary>Codec short name (e.g. "h264"), for display only.</summary>
    public string CodecName { get; }

    /// <summary>Total number of video frames, or 0 if unknown.</summary>
    public long TotalFrames { get; }
}
