using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ReceiverLib;
using SenderLib;

namespace ReceiverLib.Tests;

/// <summary>
/// The whole pipeline: a real <see cref="VideoSender"/> streams a real file to a real
/// <see cref="VideoReceiver"/>, which decodes it to BGRA frames — over UDP.
/// </summary>
public class SenderToReceiverEndToEndTests
{
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public void Sender_StreamsAFile_ReceiverDecodesItToFrames()
    {
        int port = FreePort();
        string videoPath = DecoderTestData.WriteTempMp4();

        try
        {
            using var sender = new VideoSender(new SenderConfiguration
            {
                ListenAddress = "127.0.0.1",
                ListenPort = port,
            });
            using var receiver = new VideoReceiver(new ReceiverConfiguration
            {
                SenderAddress = "127.0.0.1",
                SenderPort = port,
            });

            var frames = new ConcurrentQueue<VideoFrame>();
            receiver.FrameReady += (object? _, in VideoFrame f) => frames.Enqueue(f);

            sender.Open(videoPath);
            Assert.Equal(PlaybackState.Ready, sender.State);

            receiver.Connect();
            Assert.True(SpinUntil(() => sender.ReceiverCount == 1), "receiver never connected");

            sender.Start();

            Assert.True(SpinUntil(() => frames.Count >= 5), $"only got {frames.Count} frames");

            VideoFrame sample = frames.First();
            Assert.Equal(DecoderTestData.Width, sample.Width);
            Assert.Equal(DecoderTestData.Height, sample.Height);
            Assert.Equal(FramePixelFormat.Bgra32, sample.PixelFormat);
            Assert.Equal(DecoderTestData.Width * DecoderTestData.Height * 4, sample.Pixels.Length);

            Assert.Contains(receiver.VideoInfo!.CodecName, new[] { "h264", "mpeg4" });
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    private static bool SpinUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return condition();
    }
}
