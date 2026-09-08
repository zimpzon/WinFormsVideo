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

    /// <summary>Create a receiver with the default FFmpeg + UDP pipeline.</summary>
    public VideoReceiver(ReceiverConfiguration configuration)
        : this(new ReceivePipeline(
            configuration,
            new UdpVideoClient(configuration),
            new FFmpegVideoDecoder(),
            new PlaybackClock()))
    {
    }

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

    /// <summary>
    /// The pool of decoded-frame buffers for zero-copy display, or null until the first connection
    /// is negotiated (non-null from the <see cref="ReceiverState.Buffering"/> state onward). Wrap
    /// each <see cref="FrameBufferPool.BufferAddress"/> in a GDI+ <c>Bitmap</c> once, then in
    /// <c>OnPaint</c> call <see cref="TryAcquireFrame"/> and <c>DrawImage</c> the wrapper for the
    /// returned <see cref="RentedFrame.BufferIndex"/> — a single draw call, no per-frame copy.
    /// Rebuild (and dispose the old) wrappers whenever <see cref="FrameBufferPool.Generation"/>
    /// changes, and dispose them before disposing this receiver.
    /// </summary>
    public FrameBufferPool? FrameBuffers => _pipeline.FrameBuffers;

    /// <summary>Latest statistics snapshot.</summary>
    public ReceiverStatistics Statistics => _pipeline.Statistics;

    public event EventHandler<ReceiverStateChangedEventArgs>? StateChanged;

    public event EventHandler<StatisticsUpdatedEventArgs>? StatisticsUpdated;

    /// <summary>Raised on a background thread when a decoded frame is ready. The frontend marshals.</summary>
    public event VideoFrameHandler? FrameReady;

    public event EventHandler<ReceiverErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Connect to the sender and begin viewing. A no-op if already connecting or connected. Safe to
    /// call again after <see cref="Disconnect"/> or a fault to reconnect.
    /// </summary>
    public void Connect() => _pipeline.Connect();

    /// <summary>Disconnect from the sender. A no-op if not connected.</summary>
    public void Disconnect() => _pipeline.Disconnect();

    /// <summary>Pause viewing; the connection stays open. A no-op unless currently viewing.</summary>
    public void Pause() => _pipeline.Pause();

    /// <summary>Resume viewing after a <see cref="Pause"/>. A no-op unless currently paused.</summary>
    public void Resume() => _pipeline.Resume();

    /// <summary>
    /// Take the latest decoded frame for display (call from the UI thread, e.g. in <c>OnPaint</c>).
    /// Returns false when no new frame has been decoded since the last call — repaint the buffer
    /// from the previous successful call. Convenience wrapper over
    /// <see cref="FrameBufferPool.TryAcquireFrame"/>.
    /// </summary>
    public bool TryAcquireFrame(out RentedFrame frame) => _pipeline.TryAcquireFrame(out frame);

    public void Dispose() => _pipeline.Dispose();
}
