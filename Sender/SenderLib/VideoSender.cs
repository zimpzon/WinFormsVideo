namespace SenderLib;

/// <summary>
/// Public entry point for the sender. Opens a video file and streams it over UDP to
/// zero or more receivers, respecting the source frame timing. This type is a thin
/// facade over <see cref="PlaybackController"/>; all real work happens off the caller's
/// thread and this class never touches WinForms.
/// </summary>
public sealed class VideoSender : IDisposable
{
    private readonly PlaybackController _controller;

    /// <summary>Create a sender with the default FFmpeg + UDP pipeline.</summary>
    public VideoSender(SenderConfiguration configuration)
        : this(new PlaybackController(
            configuration,
            new FFmpegVideoSource(),
            new UdpVideoStreamServer(configuration),
            new PlaybackClock()))
    {
    }

    /// <summary>Create a sender around a pre-built pipeline. Used by tests to inject fakes.</summary>
    internal VideoSender(PlaybackController controller)
    {
        _controller = controller;
        _controller.StateChanged += (_, e) => StateChanged?.Invoke(this, e);
        _controller.StatisticsUpdated += (_, e) => StatisticsUpdated?.Invoke(this, e);
        _controller.ReceiverConnected += (_, e) => ReceiverConnected?.Invoke(this, e);
        _controller.ReceiverDisconnected += (_, e) => ReceiverDisconnected?.Invoke(this, e);
        _controller.ErrorOccurred += (_, e) => ErrorOccurred?.Invoke(this, e);
        _controller.EndOfVideoReached += (_, e) => EndOfVideoReached?.Invoke(this, e);
    }

    /// <summary>Current pipeline state.</summary>
    public PlaybackState State => _controller.State;

    /// <summary>Details of the opened video, or null before <see cref="Open"/> succeeds.</summary>
    public VideoInfo? VideoInfo => _controller.VideoInfo;

    /// <summary>Current playback position.</summary>
    public TimeSpan Position => _controller.Position;

    /// <summary>Total duration of the opened video.</summary>
    public TimeSpan Duration => _controller.Duration;

    /// <summary>Latest statistics snapshot.</summary>
    public SenderStatistics Statistics => _controller.Statistics;

    /// <summary>Number of receivers currently connected.</summary>
    public int ReceiverCount => _controller.ReceiverCount;

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    public event EventHandler<StatisticsUpdatedEventArgs>? StatisticsUpdated;

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverConnected;

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverDisconnected;

    public event EventHandler<SenderErrorEventArgs>? ErrorOccurred;

    public event EventHandler? EndOfVideoReached;

    /// <summary>
    /// Open and probe a video file. Does not start streaming. Calling it again re-opens (the current
    /// video, if any, is closed first).
    /// </summary>
    public void Open(string path) => _controller.Open(path);

    /// <summary>
    /// Start the playback clock and begin streaming from the current position. A no-op if already
    /// playing or if no video is open.
    /// </summary>
    public void Start() => _controller.Start();

    /// <summary>Pause playback; the stream holds at the current position. A no-op unless playing.</summary>
    public void Pause() => _controller.Pause();

    /// <summary>Resume playback after a <see cref="Pause"/>. A no-op unless paused.</summary>
    public void Resume() => _controller.Resume();

    /// <summary>Stop playback and reset to the start. A no-op if already stopped or not started.</summary>
    public void Stop() => _controller.Stop();

    /// <summary>Seek to the beginning and continue playing. A no-op if no video is open.</summary>
    public void Restart() => _controller.Restart();

    /// <summary>Seek to <paramref name="position"/> within the video. A no-op if no video is open.</summary>
    public void Seek(TimeSpan position) => _controller.Seek(position);

    /// <summary>
    /// Close the current video and release the source, keeping the instance usable. A no-op if
    /// nothing is open.
    /// </summary>
    public void Close() => _controller.Close();

    public void Dispose() => _controller.Dispose();
}
