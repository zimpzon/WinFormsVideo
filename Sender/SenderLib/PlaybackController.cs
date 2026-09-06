namespace SenderLib;

/// <summary>
/// The sender pipeline core: owns the <see cref="IVideoSource"/>, <see cref="IPlaybackClock"/>
/// and <see cref="IVideoStreamServer"/>, and will drive the decode -> pace -> broadcast loop
/// on a background thread. Fully constructor-injected so it can be unit tested with fakes.
/// </summary>
internal sealed class PlaybackController : IDisposable
{
    private readonly SenderConfiguration _configuration;
    private readonly IVideoSource _videoSource;
    private readonly IVideoStreamServer _streamServer;
    private readonly IPlaybackClock _clock;

    public PlaybackController(
        SenderConfiguration configuration,
        IVideoSource videoSource,
        IVideoStreamServer streamServer,
        IPlaybackClock clock)
    {
        _configuration = configuration;
        _videoSource = videoSource;
        _streamServer = streamServer;
        _clock = clock;
    }

    public SenderConfiguration Configuration => _configuration;

    public PlaybackState State { get; private set; } = PlaybackState.Idle;

    public VideoInfo? VideoInfo { get; private set; }

    public TimeSpan Position => _clock.Position;

    public TimeSpan Duration => VideoInfo?.Duration ?? TimeSpan.Zero;

    public SenderStatistics Statistics { get; private set; } = new();

    public int ReceiverCount => _streamServer.ReceiverCount;

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    public event EventHandler<StatisticsUpdatedEventArgs>? StatisticsUpdated;

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverConnected
    {
        add => _streamServer.ReceiverConnected += value;
        remove => _streamServer.ReceiverConnected -= value;
    }

    public event EventHandler<ReceiverConnectionEventArgs>? ReceiverDisconnected
    {
        add => _streamServer.ReceiverDisconnected += value;
        remove => _streamServer.ReceiverDisconnected -= value;
    }

    public event EventHandler<SenderErrorEventArgs>? ErrorOccurred;

    public event EventHandler? EndOfVideoReached;

    public void Open(string path) => throw new NotImplementedException();

    public void Start() => throw new NotImplementedException();

    public void Pause() => throw new NotImplementedException();

    public void Resume() => throw new NotImplementedException();

    public void Stop() => throw new NotImplementedException();

    public void Restart() => throw new NotImplementedException();

    public void Seek(TimeSpan position) => throw new NotImplementedException();

    public void Close() => throw new NotImplementedException();

    public void Dispose()
    {
        _videoSource.Dispose();
        _streamServer.Dispose();
    }
}
