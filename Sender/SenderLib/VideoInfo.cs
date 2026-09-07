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
        int codecId,
        long totalFrames,
        ReadOnlyMemory<byte> codecExtradata = default)
    {
        Width = width;
        Height = height;
        Duration = duration;
        FrameRate = frameRate;
        CodecName = codecName;
        CodecId = codecId;
        TotalFrames = totalFrames;
        CodecExtradata = codecExtradata;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Total playback duration of the source.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Nominal frames per second.</summary>
    public double FrameRate { get; }

    /// <summary>Codec short name (e.g. "h264"), for display only.</summary>
    public string CodecName { get; }

    /// <summary>FFmpeg <c>AVCodecID</c> value — sent to the receiver so it can pick a decoder.</summary>
    public int CodecId { get; }

    /// <summary>Total number of video frames, or 0 if unknown.</summary>
    public long TotalFrames { get; }

    /// <summary>
    /// The container's codec init data (avcC / SPS+PPS). Sent to the receiver in the handshake so it
    /// can decode the raw packets that follow. Empty if the codec carries its parameters in-band.
    /// </summary>
    public ReadOnlyMemory<byte> CodecExtradata { get; }
}
