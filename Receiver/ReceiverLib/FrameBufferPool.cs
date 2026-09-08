using System.Runtime.InteropServices;

namespace ReceiverLib;

/// <summary>
/// The receiver's rotating pool of decoded-frame buffers, sized to the stream resolution and laid
/// out as packed <see cref="FramePixelFormat.Bgra32"/> (<see cref="Stride"/> = <see cref="Width"/> * 4).
/// It exists so the frontend can display frames with <b>no per-frame copy</b>: wrap each
/// <see cref="BufferAddress"/> in a GDI+ <c>Bitmap</c> exactly once, then in <c>OnPaint</c> call
/// <see cref="TryAcquireFrame"/> and draw the wrapper for <see cref="RentedFrame.BufferIndex"/>.
/// </summary>
/// <remarks>
/// <para>
/// Internally a triple buffer: the decoder writes one slot, the newest decoded frame waits in a
/// second, the frontend holds a third. The decoder never blocks on the frontend — if the frontend
/// is slow the decoder simply overwrites the waiting slot, and those frames are counted in
/// <see cref="SupersededCount"/>. Only the small slot-index swaps are locked; frame pixels are
/// never copied or locked. The producer thread (the receive pump) owns the write slot; the consumer
/// thread (the UI) owns the display slot.
/// </para>
/// <para>
/// Lifetime: valid from the first successful connect until the owning <see cref="VideoReceiver"/> is
/// disposed. A reconnect at the <i>same</i> resolution keeps the same buffers; a reconnect at a new
/// resolution replaces them and bumps <see cref="Generation"/> — rebuild the <c>Bitmap</c> wrappers
/// whenever <see cref="Generation"/> changes, and dispose them before the receiver.
/// </para>
/// </remarks>
public sealed class FrameBufferPool
{
    private const int SlotCount = 3;

    private readonly byte[][] _slots;
    private readonly IntPtr[] _addresses;
    private readonly int _frameSize;
    private readonly object _gate = new();

    private int _writeSlot;         // owned by the producer (pump) thread
    private int _pendingSlot = -1;  // producer -> consumer hand-off; -1 when empty
    private int _displaySlot = -1;  // owned by the consumer (UI) thread; -1 before the first frame

    private long _pendingSeq;
    private long _pendingTicks;
    private long _displaySeq;
    private long _displayTicks;

    private long _presented;
    private long _superseded;

    internal FrameBufferPool(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Frame dimensions must be positive.");
        }

        Width = width;
        Height = height;
        Stride = width * 4;
        _frameSize = Stride * height;

        _slots = new byte[SlotCount][];
        _addresses = new IntPtr[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            // Pinned Object Heap: the array never moves, so the address is stable for the pool's
            // lifetime and safe to hand to GDI+.
            _slots[i] = GC.AllocateArray<byte>(_frameSize, pinned: true);
            _addresses[i] = Marshal.UnsafeAddrOfPinnedArrayElement(_slots[i], 0);
        }
    }

    /// <summary>Number of buffers in the pool.</summary>
    public int Count => SlotCount;

    /// <summary>Frame width in pixels.</summary>
    public int Width { get; }

    /// <summary>Frame height in pixels.</summary>
    public int Height { get; }

    /// <summary>Bytes per row (<see cref="Width"/> * 4).</summary>
    public int Stride { get; }

    /// <summary>Pixel layout of every buffer.</summary>
    public FramePixelFormat PixelFormat => FramePixelFormat.Bgra32;

    /// <summary>
    /// Bumped whenever the underlying buffers are reallocated (a resolution change). The frontend
    /// should rebuild its <c>Bitmap</c> wrappers whenever this value changes.
    /// </summary>
    public int Generation { get; internal set; }

    /// <summary>Stable base address of buffer <paramref name="index"/> — wrap it in a Bitmap once.</summary>
    public IntPtr BufferAddress(int index)
    {
        if ((uint)index >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _addresses[index];
    }

    /// <summary>Total frames handed out by <see cref="TryAcquireFrame"/> over the pool's lifetime.</summary>
    public long PresentedCount => Interlocked.Read(ref _presented);

    /// <summary>Total decoded frames overwritten before the frontend acquired them.</summary>
    public long SupersededCount => Interlocked.Read(ref _superseded);

    /// <summary>
    /// Take the most recently decoded frame for display. Returns <c>false</c> when no new frame has
    /// been decoded since the previous call — in that case repaint the buffer from the last
    /// successful call. Acquiring a new frame releases the previously acquired buffer back to the
    /// decoder, so do not read a buffer after the next <see cref="TryAcquireFrame"/> call. Intended
    /// to be called from the UI thread (e.g. in <c>OnPaint</c>).
    /// </summary>
    public bool TryAcquireFrame(out RentedFrame frame)
    {
        lock (_gate)
        {
            if (_pendingSlot < 0)
            {
                frame = default;
                return false;
            }

            _displaySlot = _pendingSlot;
            _displaySeq = _pendingSeq;
            _displayTicks = _pendingTicks;
            _pendingSlot = -1;
            _presented++;

            frame = new RentedFrame(_displaySlot, _displaySeq, new TimeSpan(_displayTicks));
            return true;
        }
    }

    /// <summary>The buffer the decoder should scale its next frame into. Producer thread only.</summary>
    internal FrameTarget CurrentWriteTarget() => new(_addresses[_writeSlot], Stride);

    /// <summary>
    /// Managed view over the current write buffer, for the legacy <c>FrameReady</c> path. Capture it
    /// <i>before</i> <see cref="Commit"/> (which advances the write slot). Producer thread only.
    /// </summary>
    internal ReadOnlyMemory<byte> CurrentWriteMemory() => _slots[_writeSlot].AsMemory(0, _frameSize);

    /// <summary>
    /// Publish the current write buffer as the newest frame and advance the write slot to a free
    /// buffer. Producer thread only.
    /// </summary>
    internal void Commit(long sequenceNumber, TimeSpan timestamp)
    {
        lock (_gate)
        {
            if (_pendingSlot >= 0)
            {
                _superseded++; // the previous newest frame was never acquired
            }

            _pendingSlot = _writeSlot;
            _pendingSeq = sequenceNumber;
            _pendingTicks = timestamp.Ticks;
            _writeSlot = FreeSlot(_pendingSlot, _displaySlot);
        }
    }

    private static int FreeSlot(int avoidA, int avoidB)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (i != avoidA && i != avoidB)
            {
                return i;
            }
        }

        return 0; // unreachable: 3 slots, at most 2 distinct slots avoided
    }
}
