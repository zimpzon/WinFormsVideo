using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ReceiverLib;

namespace ReceiverLib.WinForms;

/// <summary>Signature of <see cref="VideoReceiver.TryAcquireFrame"/>, for the testable constructor.</summary>
internal delegate bool FrameAcquirer(out RentedFrame frame);

/// <summary>
/// Bridges a <see cref="VideoReceiver"/>'s zero-copy <see cref="FrameBufferPool"/> to
/// <see cref="System.Drawing"/>. It wraps each pool buffer in a reused <see cref="Bitmap"/> exactly
/// once, so a WinForms control can paint the latest decoded frame with a single
/// <see cref="Graphics.DrawImage(Image, Rectangle)"/> — no per-frame copy or allocation.
/// </summary>
/// <remarks>
/// <para>
/// Threading: call <see cref="TryGetBitmap"/> / <see cref="Paint"/> on the UI thread, from
/// <c>OnPaint</c>. Subscribe to <see cref="VideoReceiver.FrameReady"/> only to marshal an
/// <c>Invalidate()</c> onto the UI thread (<c>BeginInvoke</c>); do not read pixels there. The
/// cross-thread hand-off is handled inside <see cref="FrameBufferPool"/> — no locking here.
/// </para>
/// <para>
/// Lifetime: <see cref="Dispose"/> this <b>before</b> the <see cref="VideoReceiver"/> — the wrapped
/// bitmaps point straight at pool memory. A stream that reconnects at a new resolution is handled
/// automatically (the bitmaps are rebuilt on the next call).
/// </para>
/// <para>The wrapped bitmaps are <see cref="PixelFormat.Format32bppPArgb"/> (the pool is packed BGRA32).</para>
/// </remarks>
public sealed class WinFormsFrameView : IDisposable
{
    private readonly Func<FrameBufferPool?> _frameBuffers;
    private readonly FrameAcquirer _acquire;

    private FrameBufferPool? _boundPool;   // the pool the current bitmaps wrap; keeps it alive
    private Bitmap[] _bitmaps = Array.Empty<Bitmap>();
    private int _currentIndex = -1;
    private bool _disposed;

    /// <summary>Create a view over <paramref name="receiver"/>'s decoded-frame buffers.</summary>
    public WinFormsFrameView(VideoReceiver receiver)
        : this(PoolAccessor(receiver), receiver.TryAcquireFrame)
    {
    }

    internal WinFormsFrameView(Func<FrameBufferPool?> frameBuffers, FrameAcquirer acquire)
    {
        _frameBuffers = frameBuffers;
        _acquire = acquire;
    }

    /// <summary>
    /// The latest decoded frame as a cached <see cref="Bitmap"/>. Returns <c>false</c> before the
    /// first frame, or when nothing new has decoded since the last call — in that case keep drawing
    /// the bitmap from the previous successful call. Never allocates or copies pixels. UI thread only.
    /// </summary>
    public bool TryGetBitmap(out Bitmap bitmap)
    {
        bitmap = null!;
        if (_disposed)
        {
            return false;
        }

        SyncBitmaps();
        if (_bitmaps.Length == 0)
        {
            return false; // not connected yet
        }

        if (_acquire(out RentedFrame frame) && (uint)frame.BufferIndex < (uint)_bitmaps.Length)
        {
            _currentIndex = frame.BufferIndex;
        }

        if ((uint)_currentIndex >= (uint)_bitmaps.Length)
        {
            return false;
        }

        bitmap = _bitmaps[_currentIndex];
        return true;
    }

    /// <summary>
    /// Convenience: take the latest frame and draw it into <paramref name="destination"/> with a
    /// fast opaque blit. A no-op when no frame is available yet.
    /// </summary>
    public void Paint(Graphics graphics, Rectangle destination)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (TryGetBitmap(out Bitmap bitmap))
        {
            CompositingMode previous = graphics.CompositingMode;
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImage(bitmap, destination);
            graphics.CompositingMode = previous;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeBitmaps();
        _boundPool = null;
    }

    private void SyncBitmaps()
    {
        FrameBufferPool? pool = _frameBuffers();
        if (ReferenceEquals(pool, _boundPool))
        {
            return;
        }

        DisposeBitmaps();
        _currentIndex = -1;
        _boundPool = pool;

        if (pool is null)
        {
            return;
        }

        var next = new Bitmap[pool.Count];
        for (int i = 0; i < pool.Count; i++)
        {
            next[i] = new Bitmap(
                pool.Width, pool.Height, pool.Stride, PixelFormat.Format32bppPArgb, pool.BufferAddress(i));
        }

        _bitmaps = next;
    }

    private void DisposeBitmaps()
    {
        foreach (Bitmap bitmap in _bitmaps)
        {
            bitmap.Dispose();
        }

        _bitmaps = Array.Empty<Bitmap>();
    }

    private static Func<FrameBufferPool?> PoolAccessor(VideoReceiver receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return () => receiver.FrameBuffers;
    }
}
