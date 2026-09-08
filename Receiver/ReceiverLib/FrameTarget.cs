namespace ReceiverLib;

/// <summary>
/// A destination for a decoded frame: the base address of a caller-owned buffer plus its row
/// stride. Handed to the decoder by <see cref="FrameBufferPool.CurrentWriteTarget"/> so it can
/// scale straight into the pool with no intermediate copy.
/// </summary>
internal readonly struct FrameTarget
{
    public FrameTarget(IntPtr scan0, int stride)
    {
        Scan0 = scan0;
        Stride = stride;
    }

    /// <summary>Base address of the top-left pixel.</summary>
    public IntPtr Scan0 { get; }

    /// <summary>Bytes per row.</summary>
    public int Stride { get; }
}
