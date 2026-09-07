using System.Runtime.InteropServices;
using Protocol;
using ReceiverLib;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Formats;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Toolboxs.Extensions;
using Sdcb.FFmpeg.Toolboxs.Generators;
using Sdcb.FFmpeg.Utils;
using static Sdcb.FFmpeg.Raw.ffmpeg;

namespace ReceiverLib.Tests;

/// <summary>
/// Produces the exact inputs <see cref="FFmpegVideoDecoder"/> consumes — a handshake
/// <see cref="StreamInfo"/> and a list of raw AVCC <see cref="ReceivedPacket"/>s — by encoding a
/// tiny clip and demuxing it, both in memory. No committed fixture.
/// </summary>
internal static class DecoderTestData
{
    public const int Width = 160;
    public const int Height = 120;
    public const int FrameCount = 24;

    private static readonly Lazy<(StreamInfo Info, IReadOnlyList<ReceivedPacket> Packets)> Data = new(Build);

    public static StreamInfo StreamInfo => Data.Value.Info;

    public static IReadOnlyList<ReceivedPacket> Packets => Data.Value.Packets;

    /// <summary>Writes the sample clip to a fresh temp file. Delete it when done.</summary>
    public static string WriteTempMp4()
    {
        string path = Path.Combine(Path.GetTempPath(), $"receiverlib-e2e-{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(path, EncodeMp4());
        return path;
    }

    private static (StreamInfo, IReadOnlyList<ReceivedPacket>) Build()
    {
        byte[] mp4 = EncodeMp4();

        using IOContext io = IOContext.ReadStream(new MemoryStream(mp4));
        using FormatContext fc = FormatContext.OpenInputIO(io);
        fc.LoadStreamInfo();
        MediaStream video = fc.GetVideoStream();
        CodecParameters codecpar = video.Codecpar!;

        byte[] extradata = Array.Empty<byte>();
        if (codecpar.Extradata != IntPtr.Zero && codecpar.ExtradataSize > 0)
        {
            extradata = new byte[codecpar.ExtradataSize];
            Marshal.Copy(codecpar.Extradata, extradata, 0, extradata.Length);
        }

        var info = new StreamInfo((int)codecpar.CodecId, codecpar.Width, codecpar.Height, extradata);

        var packets = new List<ReceivedPacket>();
        long seq = 0;
        AVRational tb = video.TimeBase;
        foreach (Packet packet in fc.ReadPackets(video.Index))
        {
            long ts = packet.Pts != AV_NOPTS_VALUE ? packet.Pts : packet.Dts;
            TimeSpan timestamp = ts == AV_NOPTS_VALUE
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(ts * tb.Num / (double)tb.Den);

            packets.Add(new ReceivedPacket(
                seq++,
                timestamp < TimeSpan.Zero ? TimeSpan.Zero : timestamp,
                ((uint)packet.Flags & AV_PKT_FLAG_KEY) != 0,
                packet.Data.ToArray()));
            packet.Unref();
        }

        return (info, packets);
    }

    private static byte[] EncodeMp4()
    {
        Codec codec = Codec.FindEncoderByName("libx264")
            ?? Codec.FindEncoderByName("mpeg4")
            ?? throw new InvalidOperationException("No usable video encoder in this FFmpeg build.");

        using FormatContext fc = FormatContext.AllocOutput(formatName: "mp4");
        fc.VideoCodec = codec;
        MediaStream stream = fc.NewStream(codec);

        using var cc = new CodecContext(codec)
        {
            Width = Width,
            Height = Height,
            TimeBase = new AVRational(1, 12),
            PixelFormat = AVPixelFormat.Yuv420p,
            Flags = AV_CODEC_FLAG.GlobalHeader,
            GopSize = 8,
            MaxBFrames = 0,
        };
        cc.Open(codec, codec.Name == "libx264" ? new MediaDictionary { ["preset"] = "ultrafast", ["tune"] = "zerolatency" } : null);
        stream.Codecpar!.CopyFrom(cc);

        using DynamicIOContext outIo = IOContext.OpenDynamic();
        fc.Pb = outIo;
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
        return outIo.GetBuffer().ToArray();
    }
}
