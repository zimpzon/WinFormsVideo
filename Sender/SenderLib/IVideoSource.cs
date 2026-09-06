namespace SenderLib;

/// <summary>
/// A source of encoded video frames read from a file/container. The concrete
/// implementation (FFmpeg) is an internal detail; the pipeline depends only on
/// this abstraction so it can be faked in tests.
/// </summary>
public interface IVideoSource : IDisposable
{
    /// <summary>Open and probe the given media file.</summary>
    void Open(string path);

    /// <summary>Description of the currently open video. Valid after <see cref="Open"/>.</summary>
    VideoInfo VideoInfo { get; }

    /// <summary>
    /// Read the next encoded frame in presentation order.
    /// Returns false at end of stream.
    /// </summary>
    bool TryReadNextFrame(out EncodedFrame frame);

    /// <summary>Seek so the next read returns the frame at or before <paramref name="position"/>.</summary>
    void Seek(TimeSpan position);

    /// <summary>Release the open container without disposing the instance.</summary>
    void Close();
}
