using SenderLib;

namespace SenderLib.Tests;

public class FFmpegVideoSourceTests
{
    [Fact]
    public void Open_ReadsContainerMetadataIntoVideoInfo()
    {
        using var file = TestVideo.CreateFile();
        using var source = new FFmpegVideoSource();

        source.Open(file.Path);

        Assert.Equal(TestVideo.Width, source.VideoInfo.Width);
        Assert.Equal(TestVideo.Height, source.VideoInfo.Height);
        Assert.Contains(source.VideoInfo.CodecName, new[] { "h264", "mpeg4" });
        Assert.NotEqual(0, source.VideoInfo.CodecId);
        Assert.NotEmpty(source.VideoInfo.CodecExtradata.ToArray()); // avcC / SPS+PPS for the wire
        Assert.InRange(
            source.VideoInfo.Duration,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(4));
    }

    [Fact]
    public void TryReadNextFrame_YieldsEveryFrame_InOrder_StartingWithAKeyframe()
    {
        using var file = TestVideo.CreateFile();
        using var source = new FFmpegVideoSource();
        source.Open(file.Path);

        var timestamps = new List<TimeSpan>();
        bool firstIsKey = false;
        bool first = true;

        while (source.TryReadNextFrame(out EncodedFrame frame))
        {
            Assert.NotEqual(0, frame.Data.Length);
            timestamps.Add(frame.Timestamp);
            if (first)
            {
                firstIsKey = frame.IsKeyFrame;
                first = false;
            }
        }

        Assert.True(firstIsKey, "first frame must be a keyframe");
        Assert.Equal(TestVideo.FrameCount, timestamps.Count);
        Assert.Equal(TimeSpan.Zero, timestamps[0]);
        for (int i = 1; i < timestamps.Count; i++)
        {
            Assert.True(timestamps[i] >= timestamps[i - 1], "timestamps must be non-decreasing");
        }
    }

    [Fact]
    public void TryReadNextFrame_ReturnsFalseAtEndOfStream()
    {
        using var file = TestVideo.CreateFile();
        using var source = new FFmpegVideoSource();
        source.Open(file.Path);

        while (source.TryReadNextFrame(out _))
        {
        }

        Assert.False(source.TryReadNextFrame(out _));
    }

    [Fact]
    public void Seek_JumpsToAKeyframeAtOrBeforeTheTarget()
    {
        using var file = TestVideo.CreateFile();
        using var source = new FFmpegVideoSource();
        source.Open(file.Path);

        var target = TimeSpan.FromSeconds(1);
        source.Seek(target);

        Assert.True(source.TryReadNextFrame(out EncodedFrame frame));
        Assert.True(frame.IsKeyFrame, "seek should land on a keyframe");
        Assert.True(frame.Timestamp <= target + TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void TryReadNextFrame_DoesNotAllocatePerFrame()
    {
        using var file = TestVideo.CreateFile();
        using var source = new FFmpegVideoSource();
        source.Open(file.Path);

        // First pass grows the reusable buffer to the largest packet; second pass must not allocate.
        while (source.TryReadNextFrame(out _))
        {
        }

        source.Seek(TimeSpan.Zero);

        long before = GC.GetAllocatedBytesForCurrentThread();
        int frames = 0;
        while (source.TryReadNextFrame(out _))
        {
            frames++;
        }

        long delta = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(frames > 0);
        Assert.True(delta < 4096, $"allocated {delta} bytes reading {frames} frames");
    }

    [Fact]
    public void Open_MissingFile_ThrowsFileNotFound()
    {
        using var source = new FFmpegVideoSource();
        Assert.Throws<FileNotFoundException>(() => source.Open(@"Z:\does\not\exist.mp4"));
    }

    [Fact]
    public void Open_NonMediaFile_ThrowsNotSupported()
    {
        string path = Path.Combine(Path.GetTempPath(), $"senderlib-junk-{Guid.NewGuid():N}.mp4");
        File.WriteAllText(path, "this is not a video");
        try
        {
            using var source = new FFmpegVideoSource();
            Assert.Throws<NotSupportedException>(() => source.Open(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Reopen_ReplacesThePreviousVideo()
    {
        using var file = TestVideo.CreateFile();
        using var source = new FFmpegVideoSource();

        source.Open(file.Path);
        Assert.True(source.TryReadNextFrame(out _));

        source.Open(file.Path);
        Assert.True(source.TryReadNextFrame(out EncodedFrame frame));
        Assert.Equal(TimeSpan.Zero, frame.Timestamp);
    }
}
