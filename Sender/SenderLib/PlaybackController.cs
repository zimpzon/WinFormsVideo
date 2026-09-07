using System.Collections.Concurrent;
using Protocol;

namespace SenderLib;

/// <summary>
/// The sender pipeline core: owns the <see cref="IVideoSource"/>, <see cref="IPlaybackClock"/>
/// and <see cref="IVideoStreamServer"/>, and drives the read -> pace -> broadcast loop on a
/// background thread. Fully constructor-injected so it can be unit tested with fakes.
/// </summary>
/// <remarks>
/// Threading: control methods run on the caller's thread and never block on the pump. After the
/// initial <see cref="Open"/>, the pump thread is the only thread that touches <see cref="IVideoSource"/>;
/// control methods hand it work through a command queue. Events are raised on the pump thread — the
/// WinForms frontend is responsible for marshaling them to the UI thread.
/// </remarks>
internal sealed class PlaybackController : IDisposable
{
    private static readonly TimeSpan DropThreshold = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan MaxWaitSlice = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StatsInterval = TimeSpan.FromMilliseconds(500);

    private readonly SenderConfiguration _configuration;
    private readonly IVideoSource _videoSource;
    private readonly IVideoStreamServer _streamServer;
    private readonly IPlaybackClock _clock;

    private readonly object _gate = new();
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly ConcurrentQueue<PumpCommand> _commands = new();

    private PlaybackState _state = PlaybackState.Idle;
    private SenderStatistics _statistics = new();

    private Thread? _pumpThread;
    private CancellationTokenSource? _pumpCts;
    private WaitHandle[]? _waitHandles;

    // Pump-thread-only state.
    private bool _hasPendingFrame;
    private EncodedFrame _pendingFrame;
    private long _framesDecoded;
    private long _framesSent;
    private long _framesDropped;
    private long _bytesSent;
    private long _lastStatsTick;
    private long _lastStatsFrames;
    private long _lastStatsBytes;

    private bool _disposed;

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

    public PlaybackState State
    {
        get { lock (_gate) return _state; }
    }

    public VideoInfo? VideoInfo { get; private set; }

    public TimeSpan Position => _clock.Position;

    public TimeSpan Duration => VideoInfo?.Duration ?? TimeSpan.Zero;

    public SenderStatistics Statistics => Volatile.Read(ref _statistics);

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

    public void Open(string path)
    {
        if (_disposed)
        {
            return;
        }

        StopPump();

        try
        {
            _videoSource.Open(path);
            VideoInfo info = _videoSource.VideoInfo;
            VideoInfo = info;
            _streamServer.Start(new StreamInfo(info.CodecId, info.Width, info.Height, info.CodecExtradata));
            _clock.Reset();
            ResetCounters();
            StartPump();
            SetState(PlaybackState.Ready);
        }
        catch (Exception ex)
        {
            SetState(PlaybackState.Faulted);
            RaiseError(ex);
        }
    }

    public void Start()
    {
        if (!RequireState(nameof(Start), PlaybackState.Ready, PlaybackState.Paused, PlaybackState.Stopped, PlaybackState.Ended))
        {
            return;
        }

        if (State is PlaybackState.Stopped or PlaybackState.Ended)
        {
            Enqueue(PumpCommand.Seek(TimeSpan.Zero));
        }

        _clock.Start();
        SetState(PlaybackState.Playing);
        _wake.Set();
    }

    public void Pause()
    {
        if (!RequireState(nameof(Pause), PlaybackState.Playing))
        {
            return;
        }

        _clock.Pause();
        SetState(PlaybackState.Paused);
        _wake.Set();
    }

    public void Resume()
    {
        if (!RequireState(nameof(Resume), PlaybackState.Paused))
        {
            return;
        }

        _clock.Start();
        SetState(PlaybackState.Playing);
        _wake.Set();
    }

    public void Stop()
    {
        if (!RequireState(nameof(Stop), PlaybackState.Playing, PlaybackState.Paused, PlaybackState.Ended))
        {
            return;
        }

        SetState(PlaybackState.Stopped);
        Enqueue(PumpCommand.Stop());
    }

    public void Restart()
    {
        if (!RequireState(
            nameof(Restart),
            PlaybackState.Ready,
            PlaybackState.Playing,
            PlaybackState.Paused,
            PlaybackState.Stopped,
            PlaybackState.Ended))
        {
            return;
        }

        _clock.Start();
        SetState(PlaybackState.Playing);
        Enqueue(PumpCommand.Seek(TimeSpan.Zero));
    }

    public void Seek(TimeSpan position)
    {
        if (!RequireState(
            nameof(Seek),
            PlaybackState.Ready,
            PlaybackState.Playing,
            PlaybackState.Paused,
            PlaybackState.Stopped,
            PlaybackState.Ended))
        {
            return;
        }

        Enqueue(PumpCommand.Seek(position < TimeSpan.Zero ? TimeSpan.Zero : position));
    }

    public void Close()
    {
        StopPump();

        try
        {
            _videoSource.Close();
            _streamServer.Stop();
        }
        catch (Exception ex)
        {
            RaiseError(ex);
        }

        _clock.Reset();
        ResetCounters();
        Volatile.Write(ref _statistics, new SenderStatistics());
        VideoInfo = null;

        if (State != PlaybackState.Faulted)
        {
            SetState(PlaybackState.Idle);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Close();
        _videoSource.Dispose();
        _streamServer.Dispose();
        _wake.Dispose();
        _pumpCts?.Dispose();
    }

    // --- pump lifecycle -------------------------------------------------------

    private void StartPump()
    {
        _pumpCts = new CancellationTokenSource();
        _waitHandles = new[] { _wake.WaitHandle, _pumpCts.Token.WaitHandle };
        _wake.Reset();
        while (_commands.TryDequeue(out _))
        {
        }

        _pumpThread = new Thread(() => PumpLoop(_pumpCts.Token))
        {
            IsBackground = true,
            Name = "SenderLib.PlaybackPump",
        };
        _pumpThread.Start();
    }

    private void StopPump()
    {
        var thread = _pumpThread;
        var cts = _pumpCts;
        if (thread is null || cts is null)
        {
            return;
        }

        cts.Cancel();
        _wake.Set();

        if (thread == Thread.CurrentThread)
        {
            // Re-entered from an event handler running on the pump thread; the loop will
            // observe the cancellation and unwind on its own — joining self would deadlock.
            return;
        }

        thread.Join();
        cts.Dispose();

        _pumpThread = null;
        _pumpCts = null;
        _waitHandles = null;
        _hasPendingFrame = false;
    }

    // --- pump loop -----------------------------------------------------------

    private void PumpLoop(CancellationToken token)
    {
        _lastStatsTick = Environment.TickCount64;

        try
        {
            while (!token.IsCancellationRequested)
            {
                DrainCommands();

                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (State != PlaybackState.Playing)
                {
                    WaitForWork(MaxWaitSlice);
                    continue;
                }

                if (!_hasPendingFrame)
                {
                    if (!_videoSource.TryReadNextFrame(out _pendingFrame))
                    {
                        HandleEndOfVideo();
                        continue;
                    }

                    _hasPendingFrame = true;
                    _framesDecoded++;
                }

                PacingAction action = FramePacing.Decide(
                    _pendingFrame.Timestamp,
                    _clock.Position,
                    _pendingFrame.IsKeyFrame,
                    DropThreshold);

                switch (action.Kind)
                {
                    case PacingKind.Send:
                        _streamServer.Broadcast(in _pendingFrame);
                        _framesSent++;
                        _bytesSent += _pendingFrame.Data.Length;
                        _hasPendingFrame = false;
                        break;

                    case PacingKind.Drop:
                        _framesDropped++;
                        _hasPendingFrame = false;
                        break;

                    case PacingKind.Wait:
                        WaitForWork(Min(action.Delay, MaxWaitSlice));
                        break;
                }

                MaybeEmitStatistics();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            SetState(PlaybackState.Faulted);
            RaiseError(ex);
        }
    }

    private void DrainCommands()
    {
        while (_commands.TryDequeue(out PumpCommand command))
        {
            switch (command.Kind)
            {
                case PumpCommandKind.Seek:
                    _videoSource.Seek(command.Position);
                    _clock.SeekTo(command.Position);
                    _hasPendingFrame = false;
                    break;

                case PumpCommandKind.Stop:
                    _videoSource.Seek(TimeSpan.Zero);
                    _clock.Reset();
                    _hasPendingFrame = false;
                    break;
            }
        }
    }

    private void HandleEndOfVideo()
    {
        _clock.Pause();
        SetState(PlaybackState.Ended);
        EmitStatistics();
        EndOfVideoReached?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Block until woken by a control method, cancelled, or <paramref name="timeout"/> elapses.</summary>
    private void WaitForWork(TimeSpan timeout)
    {
        WaitHandle[]? handles = _waitHandles;
        if (handles is null)
        {
            return;
        }

        int index = WaitHandle.WaitAny(handles, timeout);
        if (index == 0)
        {
            _wake.Reset();
        }
        else if (index == 1)
        {
            throw new OperationCanceledException();
        }
    }

    // --- statistics ---------------------------------------------------------

    private void MaybeEmitStatistics()
    {
        if (Environment.TickCount64 - _lastStatsTick >= StatsInterval.TotalMilliseconds)
        {
            EmitStatistics();
        }
    }

    private void EmitStatistics()
    {
        long now = Environment.TickCount64;
        double seconds = Math.Max(1, now - _lastStatsTick) / 1000.0;

        double fps = (_framesSent - _lastStatsFrames) / seconds;
        double bitrate = (_bytesSent - _lastStatsBytes) * 8 / seconds;

        var snapshot = new SenderStatistics
        {
            FramesDecoded = _framesDecoded,
            FramesSent = _framesSent,
            FramesDropped = _framesDropped,
            CurrentFps = fps,
            BitrateBitsPerSecond = bitrate,
            PlaybackPosition = _clock.Position,
            Duration = Duration,
            ReceiverCount = _streamServer.ReceiverCount,
        };

        Volatile.Write(ref _statistics, snapshot);

        _lastStatsTick = now;
        _lastStatsFrames = _framesSent;
        _lastStatsBytes = _bytesSent;

        StatisticsUpdated?.Invoke(this, new StatisticsUpdatedEventArgs(snapshot));
    }

    // --- helpers ----------------------------------------------------------

    private void ResetCounters()
    {
        _framesDecoded = 0;
        _framesSent = 0;
        _framesDropped = 0;
        _bytesSent = 0;
        _lastStatsFrames = 0;
        _lastStatsBytes = 0;
    }

    private void Enqueue(PumpCommand command)
    {
        _commands.Enqueue(command);
        _wake.Set();
    }

    private bool RequireState(string operation, params ReadOnlySpan<PlaybackState> allowed)
    {
        if (_disposed)
        {
            return false;
        }

        PlaybackState current = State;
        foreach (PlaybackState state in allowed)
        {
            if (state == current)
            {
                return true;
            }
        }

        RaiseError(new SenderError(
            SenderErrorKind.ConfigurationError,
            $"Cannot {operation} while playback state is {current}."));
        return false;
    }

    private void SetState(PlaybackState next)
    {
        PlaybackState previous;
        lock (_gate)
        {
            if (_state == next)
            {
                return;
            }

            previous = _state;
            _state = next;
        }

        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(previous, next));
    }

    private void RaiseError(Exception exception) =>
        RaiseError(new SenderError(MapErrorKind(exception), exception.Message, exception));

    private void RaiseError(SenderError error) =>
        ErrorOccurred?.Invoke(this, new SenderErrorEventArgs(error));

    private static SenderErrorKind MapErrorKind(Exception exception) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => SenderErrorKind.InvalidFile,
        NotSupportedException => SenderErrorKind.UnsupportedCodec,
        System.Net.Sockets.SocketException or IOException => SenderErrorKind.NetworkError,
        ArgumentException or FormatException => SenderErrorKind.ConfigurationError,
        _ => SenderErrorKind.Unknown,
    };

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private enum PumpCommandKind
    {
        Seek,
        Stop,
    }

    private readonly struct PumpCommand
    {
        private PumpCommand(PumpCommandKind kind, TimeSpan position)
        {
            Kind = kind;
            Position = position;
        }

        public PumpCommandKind Kind { get; }

        public TimeSpan Position { get; }

        public static PumpCommand Seek(TimeSpan position) => new(PumpCommandKind.Seek, position);

        public static PumpCommand Stop() => new(PumpCommandKind.Stop, TimeSpan.Zero);
    }
}
