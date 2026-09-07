using SenderLib;

namespace SenderLib.Tests;

public class FramePacingTests
{
    private static readonly TimeSpan DropThreshold = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void FrameInThePast_IsSent()
    {
        PacingAction action = FramePacing.Decide(
            frameTimestamp: TimeSpan.FromSeconds(1),
            clockPosition: TimeSpan.FromSeconds(1.1),
            isKeyFrame: false,
            DropThreshold);

        Assert.Equal(PacingKind.Send, action.Kind);
    }

    [Fact]
    public void FrameExactlyOnTime_IsSent()
    {
        PacingAction action = FramePacing.Decide(
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), isKeyFrame: false, DropThreshold);

        Assert.Equal(PacingKind.Send, action.Kind);
    }

    [Fact]
    public void FrameInTheFuture_WaitsForTheExactRemainingTime()
    {
        PacingAction action = FramePacing.Decide(
            frameTimestamp: TimeSpan.FromSeconds(5),
            clockPosition: TimeSpan.FromSeconds(3),
            isKeyFrame: false,
            DropThreshold);

        Assert.Equal(PacingKind.Wait, action.Kind);
        Assert.Equal(TimeSpan.FromSeconds(2), action.Delay);
    }

    [Fact]
    public void NonKeyFrameFurtherBehindThanTheThreshold_IsDropped()
    {
        PacingAction action = FramePacing.Decide(
            frameTimestamp: TimeSpan.FromSeconds(1),
            clockPosition: TimeSpan.FromSeconds(1) + DropThreshold + TimeSpan.FromMilliseconds(1),
            isKeyFrame: false,
            DropThreshold);

        Assert.Equal(PacingKind.Drop, action.Kind);
    }

    [Fact]
    public void KeyFrameFurtherBehindThanTheThreshold_IsStillSent()
    {
        PacingAction action = FramePacing.Decide(
            frameTimestamp: TimeSpan.FromSeconds(1),
            clockPosition: TimeSpan.FromSeconds(30),
            isKeyFrame: true,
            DropThreshold);

        Assert.Equal(PacingKind.Send, action.Kind);
    }

    [Fact]
    public void NonKeyFrameBehindButWithinTheThreshold_IsSent()
    {
        PacingAction action = FramePacing.Decide(
            frameTimestamp: TimeSpan.FromSeconds(1),
            clockPosition: TimeSpan.FromSeconds(1) + TimeSpan.FromMilliseconds(50),
            isKeyFrame: false,
            DropThreshold);

        Assert.Equal(PacingKind.Send, action.Kind);
    }
}
