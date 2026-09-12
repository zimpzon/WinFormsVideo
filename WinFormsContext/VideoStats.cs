namespace WinFormsReceiver.Context
{
    /// <summary>
    /// A snapshot of live playback stats for display in the UI. Safe to read from any
    /// thread at any time — it's just numbers, no synchronization needed to look at it.
    /// </summary>
    public readonly record struct VideoStats(
        bool IsConnected,
        int Width,
        int Height,
        string CodecName,
        string PixelFormat,
        double Fps,
        long BitRateBps,
        long FramesDecoded,
        long KeyFramesDecoded,
        long PacketsReceived,
        long BytesReceived,
        int ReconnectCount,
        TimeSpan TimePlayed,
        string? LastError);
}
