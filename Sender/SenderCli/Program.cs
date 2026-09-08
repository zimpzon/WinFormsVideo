using System.Globalization;
using SenderLib;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; // stable number formatting in the log

// Streams a video file using SenderLib and prints progress.
// Usage: SenderCli [video-file] [port] [listen-address]
// A reference for the WinForms sender — the flow is: configure -> new VideoSender ->
// subscribe to events -> Open -> Start -> watch Statistics/State.

string[] positional = args.Where(a => !a.StartsWith('-')).ToArray();

string path = positional.Length >= 1 ? positional[0] : @"c:\temp\mfpallytime.mp4";
int port = positional.Length >= 2 ? int.Parse(positional[1]) : 9000;
string address = positional.Length >= 3 ? positional[2] : "0.0.0.0";

using var sender = new VideoSender(new SenderConfiguration
{
    ListenAddress = address,
    ListenPort = port,
});

sender.StateChanged += (_, e) => Console.WriteLine($"[state]    {e.OldState} -> {e.NewState}");
sender.ReceiverConnected += (_, e) => Console.WriteLine($"[receiver] connected {e.RemoteEndPoint}  ({sender.ReceiverCount} total)");
sender.ReceiverDisconnected += (_, e) => Console.WriteLine($"[receiver] left {e.RemoteEndPoint}  ({sender.ReceiverCount} total)");
sender.ErrorOccurred += (_, e) => Console.WriteLine($"[error]    {e.Error.Kind}: {e.Error.Message}");

var finished = new ManualResetEventSlim(false);
sender.EndOfVideoReached += (_, _) => { Console.WriteLine("[end]      end of video reached"); finished.Set(); };
Console.CancelKeyPress += (_, e) => { e.Cancel = true; Console.WriteLine("[stop]     Ctrl+C"); finished.Set(); };

Console.WriteLine($"Opening {path} ...");
sender.Open(path);
if (sender.State == PlaybackState.Faulted)
{
    return 2;
}

VideoInfo info = sender.VideoInfo!;
Console.WriteLine($"Opened     {info.Width}x{info.Height} {info.CodecName} {info.FrameRate:0.##}fps  " +
                  $"duration {info.Duration:hh\\:mm\\:ss}  ({info.TotalFrames} frames)");
Console.WriteLine($"Listening  {address}:{port} [UDP]  (streaming continues with or without receivers)");

sender.Start();

while (true)
{
    bool stop = finished.Wait(TimeSpan.FromSeconds(1));

    SenderStatistics s = sender.Statistics;
    Console.WriteLine(
        $"{s.PlaybackPosition:hh\\:mm\\:ss} / {sender.Duration:hh\\:mm\\:ss}  {sender.State,-7}  " +
        $"rx={s.ReceiverCount}  sent={s.FramesSent} dropped={s.FramesDropped}  " +
        $"{s.CurrentFps:0.0}fps  {s.BitrateBitsPerSecond / 1_000_000.0:0.00}Mbit/s");

    if (stop || sender.State is PlaybackState.Ended or PlaybackState.Stopped or PlaybackState.Faulted)
    {
        break;
    }
}

Console.WriteLine("Done.");
return 0;
