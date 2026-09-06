using SenderLib;

namespace SenderLib.Tests.Fakes;

/// <summary>In-memory <see cref="IVideoSource"/> for tests: replays a scripted frame list.</summary>
internal sealed class FakeVideoSource : IVideoSource
{
    private readonly Queue<EncodedFrame> _frames = new();

    public FakeVideoSource(VideoInfo? videoInfo = null, IEnumerable<EncodedFrame>? frames = null)
    {
        VideoInfo = videoInfo ?? new VideoInfo(1920, 1080, TimeSpan.FromSeconds(10), 30, "h264", 300);
        if (frames is not null)
        {
            foreach (var frame in frames)
            {
                _frames.Enqueue(frame);
            }
        }
    }

    public VideoInfo VideoInfo { get; private set; }

    public string? OpenedPath { get; private set; }

    public bool IsDisposed { get; private set; }

    public void Open(string path) => OpenedPath = path;

    public bool TryReadNextFrame(out EncodedFrame frame)
    {
        if (_frames.Count == 0)
        {
            frame = default;
            return false;
        }

        frame = _frames.Dequeue();
        return true;
    }

    public void Seek(TimeSpan position)
    {
    }

    public void Close() => OpenedPath = null;

    public void Dispose() => IsDisposed = true;
}
