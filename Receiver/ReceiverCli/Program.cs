using System.Globalization;
using ReceiverLib;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; // stable number formatting in the log

// Connects to a SenderLib stream, decodes it, and prints progress.
// Usage: ReceiverCli [sender-address] [port]
// A reference for the WinForms receiver — the flow is: configure -> new VideoReceiver ->
// subscribe to events -> Connect -> watch Statistics/State, copy VideoFrame.Pixels in FrameReady.

string[] positional = args.Where(a => !a.StartsWith('-')).ToArray();

string address = positional.Length >= 1 ? positional[0] : "127.0.0.1";
int port = positional.Length >= 2 ? int.Parse(positional[1]) : 9000;

using var receiver = new VideoReceiver(new ReceiverConfiguration
{
    SenderAddress = address,
    SenderPort = port,
});

long framesShown = 0;
long lastFrameTicks = 0;

receiver.StateChanged += (_, e) => Console.WriteLine($"[state] {e.OldState} -> {e.NewState}");
receiver.ErrorOccurred += (_, e) => Console.WriteLine($"[error] {e.Error.Kind}: {e.Error.Message}");
receiver.FrameReady += (object? _, in VideoFrame f) =>
{
    // Pixels is borrowed for the duration of this call — a real UI copies it into a Bitmap here.
    Interlocked.Increment(ref framesShown);
    Interlocked.Exchange(ref lastFrameTicks, f.Timestamp.Ticks);
};

var finished = new ManualResetEventSlim(false);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; Console.WriteLine("[stop] Ctrl+C"); finished.Set(); };

Console.WriteLine($"Connecting to {address}:{port} [UDP] ...");
receiver.Connect();

while (true)
{
    bool stop = finished.Wait(TimeSpan.FromSeconds(1));

    ReceiverStatistics s = receiver.Statistics;
    VideoInfo? info = receiver.VideoInfo;
    string res = info is null ? "?" : $"{info.Width}x{info.Height} {info.CodecName}";
    var pos = new TimeSpan(Interlocked.Read(ref lastFrameTicks));

    Console.WriteLine(
        $"{receiver.State,-10}  {res}  shown={Interlocked.Read(ref framesShown)} @ {pos:hh\\:mm\\:ss}  " +
        $"net={s.ReceivedFps:0.0}fps  decoded={s.DecodedFps:0.0}fps  dropped={s.DroppedFrames}  " +
        $"{s.NetworkBitrateBitsPerSecond / 1_000_000.0:0.00}Mbit/s");

    if (stop || receiver.State is ReceiverState.Stopped or ReceiverState.Faulted)
    {
        break;
    }
}

receiver.Disconnect();
Console.WriteLine("Done.");
return 0;
