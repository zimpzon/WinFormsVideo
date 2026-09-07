using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Formats;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Toolboxs.Extensions;
using Sdcb.FFmpeg.Toolboxs.Generators;
using Sdcb.FFmpeg.Utils;

namespace SenderLib.Tests;

/// <summary>
/// Builds a tiny real video with FFmpeg once, so <see cref="SenderLib.FFmpegVideoSource"/> can be
/// exercised against an actual container without committing a binary fixture.
/// </summary>
internal static class TestVideo
{
    public const int Width = 160;
    public const int Height = 120;
    public const int FrameCount = 30;
    public const int Fps = 15;
    public const int GopSize = 10;

    private static readonly Lazy<byte[]> Mp4 = new(BuildMp4);

    /// <summary>Writes the sample video to a fresh temp file. Dispose to delete it.</summary>
    public static TempFile CreateFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"senderlib-{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(path, Mp4.Value);
        return new TempFile(path);
    }

    private static byte[] BuildMp4()
    {
        Codec codec = Codec.FindEncoderByName("libx264")
            ?? Codec.FindEncoderByName("mpeg4")
            ?? throw new InvalidOperationException("No usable H.264/MPEG-4 encoder in this FFmpeg build.");

        using FormatContext fc = FormatContext.AllocOutput(formatName: "mp4");
        fc.VideoCodec = codec;
        MediaStream stream = fc.NewStream(codec);

        using CodecContext cc = new(codec)
        {
            Width = Width,
            Height = Height,
            TimeBase = new AVRational(1, Fps),
            PixelFormat = AVPixelFormat.Yuv420p,
            Flags = AV_CODEC_FLAG.GlobalHeader,
            GopSize = GopSize,
            MaxBFrames = 0,
        };
        cc.Open(codec, codec.Name == "libx264" ? new MediaDictionary { ["preset"] = "ultrafast" } : null);
        stream.Codecpar!.CopyFrom(cc);

        using DynamicIOContext io = IOContext.OpenDynamic();
        fc.Pb = io;
        fc.WriteHeader();
        foreach (Packet packet in VideoFrameGenerator.Yuv420pSequence(Width, Height, FrameCount).EncodeFrames(cc))
        {
            try
            {
                packet.RescaleTimestamp(cc.TimeBase, stream.TimeBase);
                packet.StreamIndex = stream.Index;
                fc.InterleavedWritePacket(packet);
            }
            finally
            {
                packet.Unref();
            }
        }

        fc.WriteTrailer();
        return io.GetBuffer().ToArray();
    }

    public sealed class TempFile : IDisposable
    {
        public TempFile(string path) => Path = path;

        public string Path { get; }

        public void Dispose()
        {
            try { File.Delete(Path); } catch { /* best effort */ }
        }
    }
}
