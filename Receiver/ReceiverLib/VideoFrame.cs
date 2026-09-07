namespace ReceiverLib;

/// <summary>Pixel layout of a decoded <see cref="VideoFrame"/>.</summary>
public enum FramePixelFormat
{
    /// <summary>8 bits per channel, blue-green-red-alpha byte order (matches WinForms 32bppPArgb).</summary>
    Bgra32,
}

/// <summary>
/// A decoded frame ready to be displayed. Deliberately free of WinForms (or any UI) types — the
/// frontend copies <see cref="Pixels"/> into whatever surface it renders to. The shape here
/// (raw buffer + stride + format) is also what a future GPU/texture upload path would consume, so
/// nothing about this type should assume CPU-side blitting.
/// </summary>
public readonly struct VideoFrame
{
    public VideoFrame(
        int width,
        int height,
        int stride,
        FramePixelFormat pixelFormat,
        ReadOnlyMemory<byte> pixels,
        TimeSpan timestamp)
    {
        Width = width;
        Height = height;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
        Timestamp = timestamp;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Bytes per row, including any padding.</summary>
    public int Stride { get; }

    public FramePixelFormat PixelFormat { get; }

    /// <summary>
    /// The pixel data, borrowed from a small ring of buffers the decoder rotates. Only valid for the
    /// duration of the <c>FrameReady</c> callback — copy it (e.g. into a <c>Bitmap</c>) to keep it.
    /// </summary>
    public ReadOnlyMemory<byte> Pixels { get; }

    /// <summary>Presentation timestamp relative to the start of the stream.</summary>
    public TimeSpan Timestamp { get; }
}
