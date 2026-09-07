using Protocol;

namespace ReceiverLib;

/// <summary>
/// Public entry point for the receiver. Connects to a sender, decodes the incoming stream and
/// raises decoded frames for the WinForms frontend to display. A thin facade over
/// <see cref="ReceivePipeline"/>; all real work happens off the caller's thread and this class
/// never touches WinForms.
/// </summary>
public sealed class VideoReceiver : IDisposable
{
    private readonly ReceivePipeline _pipeline;

    /// <summary>Create a receiver with the default FFmpeg pipeline over the configured transport.</summary>
    public VideoReceiver(ReceiverConfiguration configuration)
        : this(new ReceivePipeline(
            configuration,
            CreateClient(configuration),
            new FFmpegVideoDecoder(),
            new PlaybackClock()))
    {
    }

    private static IVideoClient CreateClient(ReceiverConfiguration configuration) =>
        configuration.Transport == TransportKind.Udp
            ? new UdpVideoClient(configuration)
            : new TcpVideoClient(configuration);

    /// <summary>Create a receiver around a pre-built pipeline. Used by tests to inject fakes.</summary>
    internal VideoReceiver(ReceivePipeline pipeline)
    {
        _pipeline = pipeline;
        _pipeline.StateChanged += (_, e) => StateChanged?.Invoke(this, e);
        _pipeline.StatisticsUpdated += (_, e) => StatisticsUpdated?.Invoke(this, e);
        _pipeline.FrameReady += (object? _, in VideoFrame f) => FrameReady?.Invoke(this, in f);
        _pipeline.ErrorOccurred += (_, e) => ErrorOccurred?.Invoke(this, e);
    }

    /// <summary>Current pipeline state.</summary>
    public ReceiverState State => _pipeline.State;

    /// <summary>Details of the incoming video once the handshake is read, otherwise null.</summary>
    public VideoInfo? VideoInfo => _pipeline.VideoInfo;

    /// <summary>Latest statistics snapshot.</summary>
    public ReceiverStatistics Statistics => _pipeline.Statistics;

    public event EventHandler<ReceiverStateChangedEventArgs>? StateChanged;

    public event EventHandler<StatisticsUpdatedEventArgs>? StatisticsUpdated;

    /// <summary>Raised on a background thread when a decoded frame is ready. The frontend marshals.</summary>
    public event VideoFrameHandler? FrameReady;

    public event EventHandler<ReceiverErrorEventArgs>? ErrorOccurred;

    /// <summary>Connect to the sender and begin viewing.</summary>
    public void Connect() => _pipeline.Connect();

    /// <summary>Disconnect from the sender.</summary>
    public void Disconnect() => _pipeline.Disconnect();

    /// <summary>Pause viewing; the connection stays open.</summary>
    public void Pause() => _pipeline.Pause();

    /// <summary>Resume viewing after a <see cref="Pause"/>.</summary>
    public void Resume() => _pipeline.Resume();

    public void Dispose() => _pipeline.Dispose();
}
