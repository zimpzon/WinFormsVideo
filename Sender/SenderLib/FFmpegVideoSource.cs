using Protocol;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Common;
using Sdcb.FFmpeg.Formats;
using Sdcb.FFmpeg.Raw;
using static Sdcb.FFmpeg.Raw.ffmpeg;

namespace SenderLib;

/// <summary>
/// FFmpeg-backed <see cref="IVideoSource"/> (via Sdcb.FFmpeg).
/// </summary>
/// <remarks>
/// ALL FFmpeg / native interop belongs in this file. It is a pure demuxer: it reads compressed
/// video packets straight from the container (no decoding) and hands them out as
/// <see cref="EncodedFrame"/>s. Per the <see cref="IVideoSource"/> contract every member is called
/// from the pipeline pump thread (after the initial <see cref="Open"/>), so no internal locking.
/// </remarks>
internal sealed class FFmpegVideoSource : IVideoSource
{
    private FormatContext? _format;
    private Packet? _packet;
    private int _videoStreamIndex = -1;
    private AVRational _timeBase;
    private long _startPts;
    private TimeSpan _lastTimestamp;
    private VideoInfo? _videoInfo;

    // Reused across frames — EncodedFrame.Data points into this until the next read.
    private byte[] _packetBuffer = Array.Empty<byte>();

    public VideoInfo VideoInfo =>
        _videoInfo ?? throw new InvalidOperationException("No video is open.");

    public void Open(string path)
    {
        Close();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Video file not found.", path);
        }

        FormatContext format;
        try
        {
            format = FormatContext.OpenInputUrl(path);
            format.LoadStreamInfo();
        }
        catch (FFmpegException ex)
        {
            throw new NotSupportedException($"Could not open '{path}': {ex.Message}", ex);
        }

        try
        {
            MediaStream stream = format.GetVideoStream();
            CodecParameters codecpar = stream.Codecpar
                ?? throw new NotSupportedException("Video stream has no codec parameters.");

            _videoStreamIndex = stream.Index;
            _timeBase = stream.TimeBase;
            _startPts = stream.StartTime != AV_NOPTS_VALUE ? stream.StartTime : 0;
            _lastTimestamp = TimeSpan.Zero;

            _videoInfo = new VideoInfo(
                width: codecpar.Width,
                height: codecpar.Height,
                duration: ResolveDuration(format, stream),
                frameRate: ResolveFrameRate(stream),
                codecName: avcodec_get_name(codecpar.CodecId),
                codecId: (int)codecpar.CodecId,
                totalFrames: stream.NbFrames > 0 ? stream.NbFrames : 0,
                codecExtradata: ReadExtradata(codecpar));

            _packet = new Packet();
            _format = format;
        }
        catch
        {
            format.Dispose();
            throw;
        }
    }

    public bool TryReadNextFrame(out EncodedFrame frame)
    {
        frame = default;
        FormatContext format = _format ?? throw new InvalidOperationException("No video is open.");
        Packet packet = _packet!;

        while (true)
        {
            CodecResult result = format.ReadFrame(packet);
            if (result == CodecResult.EOF)
            {
                return false;
            }

            if (packet.StreamIndex != _videoStreamIndex)
            {
                packet.Unref();
                continue;
            }

            try
            {
                ReadOnlySpan<byte> payload = packet.Data.AsSpan();
                BufferUtil.EnsureCapacity(ref _packetBuffer, payload.Length);
                payload.CopyTo(_packetBuffer);

                frame = new EncodedFrame(
                    ResolveTimestamp(packet),
                    ((uint)packet.Flags & AV_PKT_FLAG_KEY) != 0,
                    _packetBuffer.AsMemory(0, payload.Length));
                return true;
            }
            finally
            {
                packet.Unref();
            }
        }
    }

    public void Seek(TimeSpan position)
    {
        FormatContext format = _format ?? throw new InvalidOperationException("No video is open.");

        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        long target = _startPts + SecondsToStreamUnits(position.TotalSeconds);
        format.SeekFrame(target, _videoStreamIndex, AVSEEK_FLAG.Backward);
        _lastTimestamp = position;
    }

    public void Close()
    {
        _packet?.Dispose();
        _packet = null;
        _format?.Dispose();
        _format = null;
        _videoStreamIndex = -1;
        _videoInfo = null;
        _lastTimestamp = TimeSpan.Zero;
        _startPts = 0;
    }

    public void Dispose() => Close();

    private TimeSpan ResolveTimestamp(Packet packet)
    {
        long pts = packet.Pts != AV_NOPTS_VALUE ? packet.Pts
            : packet.Dts != AV_NOPTS_VALUE ? packet.Dts
            : long.MinValue;

        if (pts == long.MinValue)
        {
            return _lastTimestamp;
        }

        double seconds = (pts - _startPts) * _timeBase.Num / (double)_timeBase.Den;
        TimeSpan timestamp = seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
        _lastTimestamp = timestamp;
        return timestamp;
    }

    private long SecondsToStreamUnits(double seconds) =>
        _timeBase.Num == 0 ? 0 : (long)(seconds * _timeBase.Den / _timeBase.Num);

    private static TimeSpan ResolveDuration(FormatContext format, MediaStream stream)
    {
        if (stream.Duration != AV_NOPTS_VALUE && stream.Duration > 0)
        {
            return TimeSpan.FromSeconds(stream.Duration * stream.TimeBase.Num / (double)stream.TimeBase.Den);
        }

        return format.Duration > 0
            ? TimeSpan.FromSeconds(format.Duration / (double)AV_TIME_BASE)
            : TimeSpan.Zero;
    }

    private static byte[] ReadExtradata(CodecParameters codecpar)
    {
        if (codecpar.Extradata == IntPtr.Zero || codecpar.ExtradataSize <= 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new byte[codecpar.ExtradataSize];
        System.Runtime.InteropServices.Marshal.Copy(codecpar.Extradata, buffer, 0, buffer.Length);
        return buffer;
    }

    private static double ResolveFrameRate(MediaStream stream)
    {
        if (stream.AvgFrameRate.Num > 0 && stream.AvgFrameRate.Den > 0)
        {
            return stream.AvgFrameRate.ToDouble();
        }

        return stream.RFrameRate is { Num: > 0, Den: > 0 } r ? r.ToDouble() : 0;
    }
}
