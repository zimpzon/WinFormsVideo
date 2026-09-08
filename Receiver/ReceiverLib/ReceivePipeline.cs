using System.Net.Sockets;

namespace ReceiverLib;

/// <summary>
/// The receiver pipeline core: owns the <see cref="IVideoClient"/>, <see cref="IVideoDecoder"/> and
/// <see cref="IPlaybackClock"/>, and drives the read -> decode -> pace -> present loop on a
/// background thread. Fully constructor-injected so it can be unit tested with fakes.
/// </summary>
/// <remarks>
/// Threading: control methods run on the caller's thread; the pump thread is the only one that
/// touches <see cref="IVideoClient"/> / <see cref="IVideoDecoder"/> after <see cref="Connect"/>.
/// Events are raised on the pump thread — the WinForms frontend marshals them.
/// Pacing is drop-only: frames are presented as they decode (the sender already paced them) and a
/// frame that is more than <see cref="MaxLateness"/> behind the clock is dropped rather than shown.
/// </remarks>
internal sealed class ReceivePipeline : IDisposable
{
    private static readonly TimeSpan MaxLateness = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan StatsInterval = TimeSpan.FromMilliseconds(500);

    private readonly ReceiverConfiguration _configuration;
    private readonly IVideoClient _client;
    private readonly IVideoDecoder _decoder;
    private readonly IPlaybackClock _clock;
    private readonly object _gate = new();

    private Thread? _pumpThread;
    private CancellationTokenSource? _cts;

    private ReceiverState _state = ReceiverState.Idle;
    private ReceiverStatistics _statistics = new();
    private volatile bool _rebase;
    private bool _disposed;

    // pump-thread-only stats
    private long _packetsReceived;
    private long _framesDecoded;
    private long _framesDropped;
    private long _bytesReceived;
    private long _lastStatsTick;
    private long _lastStatsPackets;
    private long _lastStatsFrames;
    private long _lastStatsBytes;
    private long _lastStatsPresented;

    public ReceivePipeline(
        ReceiverConfiguration configuration,
        IVideoClient client,
        IVideoDecoder decoder,
        IPlaybackClock clock)
    {
        _configuration = configuration;
        _client = client;
        _decoder = decoder;
        _clock = clock;
    }

    public ReceiverConfiguration Configuration => _configuration;

    public ReceiverState State
    {
        get { lock (_gate) return _state; }
    }

    public VideoInfo? VideoInfo { get; private set; }

    /// <summary>The decoder's zero-copy output pool. Non-null once the stream has been negotiated.</summary>
    public FrameBufferPool? FrameBuffers => _decoder.OutputBuffers;

    public ReceiverStatistics Statistics => Volatile.Read(ref _statistics);

    public event EventHandler<ReceiverStateChangedEventArgs>? StateChanged;

    public event EventHandler<StatisticsUpdatedEventArgs>? StatisticsUpdated;

    public event VideoFrameHandler? FrameReady;

    public event EventHandler<ReceiverErrorEventArgs>? ErrorOccurred;

    public void Connect()
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            if (_pumpThread is { IsAlive: true })
            {
                return; // already connecting / connected — no-op
            }

            _pumpThread = null;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            ResetCounters();
            _pumpThread = new Thread(() => PumpLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "ReceiverLib.ReceivePump",
            };
            _pumpThread.Start();
        }
    }

    public void Disconnect()
    {
        lock (_gate)
        {
            if (_pumpThread is null && _state is ReceiverState.Idle or ReceiverState.Stopped)
            {
                return; // never connected, or already disconnected — no-op
            }
        }

        StopPump();

        try
        {
            _client.Disconnect();
        }
        catch (Exception ex)
        {
            RaiseError(MapStreamError(ex));
        }

        _clock.Reset();
        VideoInfo = null;
        Volatile.Write(ref _statistics, new ReceiverStatistics());

        if (State != ReceiverState.Faulted)
        {
            SetState(ReceiverState.Stopped);
        }
    }

    public void Pause()
    {
        if (_disposed || State is not (ReceiverState.Playing or ReceiverState.Buffering))
        {
            return; // not viewing — nothing to pause
        }

        _clock.Pause();
        SetState(ReceiverState.Paused);
    }

    public void Resume()
    {
        if (_disposed || State != ReceiverState.Paused)
        {
            return; // not paused — nothing to resume
        }

        _rebase = true; // next decoded frame re-establishes the timeline (jump to live)
        SetState(ReceiverState.Buffering);
    }

    /// <summary>Hand the newest decoded frame to a display consumer (UI thread).</summary>
    public bool TryAcquireFrame(out RentedFrame frame)
    {
        FrameBufferPool? buffers = _decoder.OutputBuffers;
        if (buffers is null)
        {
            frame = default;
            return false;
        }

        return buffers.TryAcquireFrame(out frame);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopPump();
        _client.Dispose();
        _decoder.Dispose();
        _cts?.Dispose();
    }

    // --- pump ---------------------------------------------------------------

    private void PumpLoop(CancellationToken token)
    {
        bool streamEstablished = false;
        try
        {
            SetState(ReceiverState.Connecting);
            _client.Connect();

            Protocol.StreamInfo info = _client.StreamInfo
                ?? throw new InvalidDataException("Sender did not send stream info in the handshake.");
            _decoder.Configure(info);
            VideoInfo = new VideoInfo(info.Width, info.Height, frameRate: 0, _decoder.CodecName);

            _lastStatsTick = Environment.TickCount64;
            _lastStatsPresented = _decoder.OutputBuffers?.PresentedCount ?? 0;
            _rebase = true;
            streamEstablished = true; // past this point a failure is a lost stream, not a failed connect
            SetState(ReceiverState.Buffering);

            long baselineTicks = 0;
            bool haveBaseline = false;

            while (!token.IsCancellationRequested)
            {
                if (!_client.TryReadPacket(out ReceivedPacket packet))
                {
                    break; // stream ended / disconnected
                }

                _packetsReceived++;
                _bytesReceived += packet.Data.Length;

                if (!_decoder.TryDecode(packet, out VideoFrame frame))
                {
                    MaybeEmitStatistics();
                    continue;
                }

                _framesDecoded++;

                if (State == ReceiverState.Paused)
                {
                    _rebase = true; // discard while paused; re-sync on resume
                    MaybeEmitStatistics();
                    continue;
                }

                if (_rebase || !haveBaseline)
                {
                    baselineTicks = frame.Timestamp.Ticks;
                    haveBaseline = true;
                    _rebase = false;
                    _clock.Reset();
                    _clock.Start();
                    SetState(ReceiverState.Playing);
                }

                TimeSpan streamPosition = TimeSpan.FromTicks(frame.Timestamp.Ticks - baselineTicks);
                if (_clock.Position - streamPosition > MaxLateness)
                {
                    _framesDropped++;
                    MaybeEmitStatistics();
                    continue;
                }

                FrameReady?.Invoke(this, in frame);
                MaybeEmitStatistics();
            }

            if (!token.IsCancellationRequested)
            {
                EmitStatistics();
                SetState(ReceiverState.Stopped);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            SetState(ReceiverState.Faulted);
            RaiseError(streamEstablished ? MapStreamError(ex) : MapConnectError(ex));
        }
    }

    private void StopPump()
    {
        Thread? thread;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            thread = _pumpThread;
            cts = _cts;
            _pumpThread = null;
        }

        if (thread is null)
        {
            return;
        }

        cts?.Cancel();
        try
        {
            _client.Disconnect(); // unblock a blocked TryReadPacket
        }
        catch
        {
            // best effort
        }

        if (thread != Thread.CurrentThread)
        {
            thread.Join();
        }
    }

    // --- statistics -------------------------------------------------------

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
        long presented = _decoder.OutputBuffers?.PresentedCount ?? 0;

        var snapshot = new ReceiverStatistics
        {
            ReceivedFps = (_packetsReceived - _lastStatsPackets) / seconds,
            DecodedFps = (_framesDecoded - _lastStatsFrames) / seconds,
            PresentedFps = Math.Max(0, presented - _lastStatsPresented) / seconds,
            DroppedFrames = _framesDropped + _client.FramesDropped, // late frames + transport losses (UDP)
            NetworkBitrateBitsPerSecond = (_bytesReceived - _lastStatsBytes) * 8 / seconds,
            Latency = null,
            QueueDepth = 0,
            ConnectionState = State,
            Resolution = VideoInfo is { } v ? (v.Width, v.Height) : null,
            CodecName = VideoInfo?.CodecName,
        };

        Volatile.Write(ref _statistics, snapshot);
        _lastStatsTick = now;
        _lastStatsPackets = _packetsReceived;
        _lastStatsFrames = _framesDecoded;
        _lastStatsBytes = _bytesReceived;
        _lastStatsPresented = presented;

        StatisticsUpdated?.Invoke(this, new StatisticsUpdatedEventArgs(snapshot));
    }

    // --- helpers --------------------------------------------------------

    private void ResetCounters()
    {
        _packetsReceived = _framesDecoded = _framesDropped = _bytesReceived = 0;
        _lastStatsPackets = _lastStatsFrames = _lastStatsBytes = _lastStatsPresented = 0;
    }

    private void SetState(ReceiverState next)
    {
        ReceiverState previous;
        lock (_gate)
        {
            if (_state == next)
            {
                return;
            }

            previous = _state;
            _state = next;
        }

        StateChanged?.Invoke(this, new ReceiverStateChangedEventArgs(previous, next));
    }

    private void RaiseError(ReceiverError error) =>
        ErrorOccurred?.Invoke(this, new ReceiverErrorEventArgs(error));

    /// <summary>
    /// Map a failure that happened <b>while establishing the stream</b> (connect + handshake +
    /// decoder setup). "No sender at that endpoint" lands here — a timed-out or reset handshake
    /// socket — and becomes <see cref="ReceiverErrorKind.ConnectionFailed"/> with a clean message,
    /// not the raw OS text. Switch on <see cref="ReceiverError.Kind"/>, never the message.
    /// </summary>
    private ReceiverError MapConnectError(Exception exception) => exception switch
    {
        NotSupportedException => new ReceiverError(ReceiverErrorKind.UnsupportedCodec, exception.Message, exception),
        InvalidDataException => new ReceiverError(ReceiverErrorKind.ProtocolError, exception.Message, exception),
        _ => new ReceiverError(
            ReceiverErrorKind.ConnectionFailed,
            $"No video stream at {_configuration.SenderAddress}:{_configuration.SenderPort}.",
            exception),
    };

    /// <summary>Map a failure that happened once the stream was already running.</summary>
    private static ReceiverError MapStreamError(Exception exception) => exception switch
    {
        SocketException => new ReceiverError(ReceiverErrorKind.ConnectionLost, exception.Message, exception),
        InvalidDataException => new ReceiverError(ReceiverErrorKind.ProtocolError, exception.Message, exception),
        EndOfStreamException => new ReceiverError(ReceiverErrorKind.ConnectionLost, exception.Message, exception),
        NotSupportedException => new ReceiverError(ReceiverErrorKind.UnsupportedCodec, exception.Message, exception),
        _ => new ReceiverError(ReceiverErrorKind.Unknown, exception.Message, exception),
    };
}
