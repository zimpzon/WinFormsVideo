using SenderLib;

namespace SenderLib.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IVideoSource"/> for tests: replays a scripted frame list and records seeks.
/// </summary>
internal sealed class FakeVideoSource : IVideoSource
{
    private readonly List<EncodedFrame> _frames;
    private int _index;

    public FakeVideoSource(VideoInfo? videoInfo = null, IEnumerable<EncodedFrame>? frames = null)
    {
        VideoInfo = videoInfo ?? new VideoInfo(
            width: 1920,
            height: 1080,
            duration: TimeSpan.FromSeconds(10),
            frameRate: 30,
            codecName: "h264",
            codecId: 27, // AVCodecID.H264
            totalFrames: 300,
            codecExtradata: new byte[] { 0x01, 0x02, 0x03, 0x04 });
        _frames = frames?.ToList() ?? new List<EncodedFrame>();
    }

    /// <summary>When set, <see cref="Open"/> throws this exception.</summary>
    public Exception? ThrowOnOpen { get; set; }

    public VideoInfo VideoInfo { get; }

    public string? OpenedPath { get; private set; }

    public bool IsDisposed { get; private set; }

    /// <summary>Every position passed to <see cref="Seek"/>, in call order.</summary>
    public List<TimeSpan> SeekPositions { get; } = new();

    public void Open(string path)
    {
        if (ThrowOnOpen is not null)
        {
            throw ThrowOnOpen;
        }

        OpenedPath = path;
        _index = 0;
    }

    public bool TryReadNextFrame(out EncodedFrame frame)
    {
        if (_index >= _frames.Count)
        {
            frame = default;
            return false;
        }

        frame = _frames[_index++];
        return true;
    }

    public void Seek(TimeSpan position)
    {
        SeekPositions.Add(position);
        _index = _frames.FindIndex(f => f.Timestamp >= position);
        if (_index < 0)
        {
            _index = _frames.Count;
        }
    }

    public void Close() => OpenedPath = null;

    public void Dispose() => IsDisposed = true;
}
