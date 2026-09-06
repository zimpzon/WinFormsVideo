namespace SenderLib;

/// <summary>
/// FFmpeg-backed <see cref="IVideoSource"/>.
/// </summary>
/// <remarks>
/// ALL FFmpeg / native interop belongs in this file. Nothing else in the library
/// should reference FFmpeg types, so the decoding backend can be swapped later.
/// </remarks>
internal sealed class FFmpegVideoSource : IVideoSource
{
    public VideoInfo VideoInfo => throw new NotImplementedException();

    public void Open(string path) => throw new NotImplementedException();

    public bool TryReadNextFrame(out EncodedFrame frame) => throw new NotImplementedException();

    public void Seek(TimeSpan position) => throw new NotImplementedException();

    public void Close() => throw new NotImplementedException();

    public void Dispose()
    {
        // TODO: release FFmpeg contexts.
    }
}
