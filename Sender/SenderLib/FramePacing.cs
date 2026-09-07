namespace SenderLib;

/// <summary>What the pump should do with a frame it has just read, given the playback clock.</summary>
internal enum PacingKind
{
    /// <summary>The frame is due (or overdue but must not be dropped) — send it now.</summary>
    Send,

    /// <summary>The frame is in the future — wait <see cref="PacingAction.Delay"/> then re-evaluate.</summary>
    Wait,

    /// <summary>Playback is far enough behind that this obsolete frame should be discarded.</summary>
    Drop,
}

/// <summary>Result of <see cref="FramePacing.Decide"/>.</summary>
internal readonly struct PacingAction
{
    private PacingAction(PacingKind kind, TimeSpan delay)
    {
        Kind = kind;
        Delay = delay;
    }

    public PacingKind Kind { get; }

    /// <summary>How long to wait before the frame is due. Only meaningful when <see cref="Kind"/> is <see cref="PacingKind.Wait"/>.</summary>
    public TimeSpan Delay { get; }

    public static PacingAction Send { get; } = new(PacingKind.Send, TimeSpan.Zero);

    public static PacingAction Drop { get; } = new(PacingKind.Drop, TimeSpan.Zero);

    public static PacingAction Wait(TimeSpan delay) => new(PacingKind.Wait, delay);
}

/// <summary>
/// Pure decision logic for pacing frames against the playback clock. Kept separate from the
/// pump loop so it can be unit tested exhaustively without threads or timing.
/// </summary>
internal static class FramePacing
{
    /// <summary>
    /// Decide what to do with a frame whose presentation timestamp is <paramref name="frameTimestamp"/>
    /// when the playback clock reads <paramref name="clockPosition"/>.
    /// </summary>
    /// <param name="dropThreshold">
    /// How far playback may run past a non-keyframe before that frame is considered obsolete.
    /// </param>
    public static PacingAction Decide(
        TimeSpan frameTimestamp,
        TimeSpan clockPosition,
        bool isKeyFrame,
        TimeSpan dropThreshold)
    {
        TimeSpan behind = clockPosition - frameTimestamp;

        if (behind < TimeSpan.Zero)
        {
            // Frame is in the future — hold it until it is due.
            return PacingAction.Wait(-behind);
        }

        if (!isKeyFrame && behind > dropThreshold)
        {
            // We are late and this frame is not needed to keep the stream decodable.
            return PacingAction.Drop;
        }

        return PacingAction.Send;
    }
}
