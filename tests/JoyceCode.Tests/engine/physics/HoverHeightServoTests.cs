using System;
using engine.physics;
using Xunit;

namespace JoyceCode.Tests.engine.physics;


/**
 * The hover loop's vertical command, and what it does to the ship.
 *
 * The controller itself needs a booted engine and a physics simulation and cannot be
 * tested; the DECISION it makes each frame is arithmetic and is, and so is the closed
 * loop the ship flies, because every term that touches the ship's vertical velocity is
 * a number in HoverController with no dependency on the world. _Fly below integrates
 * exactly those terms, and it is what tells the two command shapes apart: the constant
 * one sinks through its own hover height and leans on whatever is under it with its
 * full authority, and neither of those is visible from a single call.
 */
public class HoverHeightServoTests
{
    private const float UpThrust = 48f;
    private const float DownThrust = 16f;
    private const float ApproachTime = HoverHeightServo.DefaultLevelDownApproachTime;
    private const float DeadBand = HoverHeightServo.DefaultDeadBand;


    private static float _command(float deltaY)
        => HoverHeightServo.CommandedVerticalVelocity(
            deltaY, UpThrust, DownThrust, ApproachTime, DeadBand);


    /**
     * Below its height the ship is asked for everything it has, at any depth. This is
     * the safety direction - the terrain has no collider, so being slow to rise is
     * being inside the hill - and it is deliberately NOT proportional.
     */
    [Fact]
    public void TheClimbKeepsFullAuthorityAtAnyDepth()
    {
        Assert.Equal(UpThrust, _command(-0.02f), 4);
        Assert.Equal(UpThrust, _command(-1f), 4);
        Assert.Equal(UpThrust, _command(-40f), 4);
    }


    [Fact]
    public void InsideTheDeadBandTheShipIsLeftAlone()
    {
        Assert.Equal(0f, _command(0f), 4);
        Assert.Equal(0f, _command(DeadBand * 0.5f), 4);
        Assert.Equal(0f, _command(-DeadBand * 0.5f), 4);
    }


    /**
     * The bug, as one assertion.
     *
     * A road relaxed to a buildable gradient stands proud of the hill it crosses, so
     * the ship rests on it while its ground sample is metres below. The command issued
     * there is a sustained one - it does not stop until the ship gets down to a height
     * it cannot reach - and it is what sets the normal load the surface has to carry.
     * At a constant LevelDownThrust the answer was 16 m/s no matter how small the
     * discrepancy.
     */
    [Fact]
    public void ASurfaceStandingProudIsNotShovedAt()
    {
        Assert.Equal(-0.5f / ApproachTime, _command(0.5f), 4);
        Assert.Equal(-1f / ApproachTime, _command(1f), 4);

        Assert.True(Single.Abs(_command(1f)) < DownThrust / 2f,
            $"a surface 1 m proud is leaned on at {-_command(1f):F2} m/s, which is not "
            + $"meaningfully gentler than the {DownThrust} m/s that pinned the ship.");

        /*
         * And it keeps shrinking. The load a surface has to carry has to fall away with
         * the discrepancy, not merely be smaller than it was - a smaller constant would
         * pin the ship just as thoroughly wherever the road happens to stand proud by a
         * little less.
         */
        Assert.Equal(4f, _command(1f) / _command(0.25f), 3);
    }


    /**
     * Far from its height nothing has changed. A ship high over ground that has fallen
     * away descends at exactly the rate it always did, so the only part of the flight
     * this touches is the last couple of metres of an approach.
     */
    [Fact]
    public void TheFarFieldDescentIsUnchanged()
    {
        float saturatesAt = DownThrust * ApproachTime;

        Assert.Equal(-DownThrust, _command(saturatesAt + 0.001f), 4);
        Assert.Equal(-DownThrust, _command(10f), 4);
        Assert.Equal(-DownThrust, _command(100f), 4);
    }


    /**
     * Every term HoverController applies to the ship's vertical velocity, integrated at
     * the logical frame rate.
     *
     * Impulses there are applied as vTotalImpulse * dt * mass, so each term is read
     * directly as an acceleration:
     *
     *   +9.81            gravity compensation, against the simulation's own -9.81
     *   (vCmd - vy)      the velocity servo this file is about
     *   -0.6 * vy        LinearDamping
     *   -2.0 * vy        the "remove velocity that is not along the nose" term, which
     *                    for a level ship is the whole of the vertical component
     *   +10              the one-sided shove applied while under the ground sample
     *
     * @param fCommand
     *     Height error in metres (positive = too high) to commanded vertical speed.
     * @return
     *     The lowest height, relative to the target, that the ship reached.
     */
    private static float _fly(Func<float, float> fCommand, float y0, int nFrames = 900)
    {
        const float dt = 1f / 60f;

        float y = y0;
        float vy = 0f;
        float lowest = y0;

        for (int i = 0; i < nFrames; ++i)
        {
            float a = fCommand(y) - 3.6f * vy;
            if (y < 0f) a += 10f;

            vy += a * dt;
            y += vy * dt;

            lowest = Single.Min(lowest, y);
        }

        return lowest;
    }


    /**
     * The other half of the same defect, and the one that is visible in a flat city:
     * with a constant descent command the ship is only told to stop descending once it
     * has ARRIVED, by which time it carries several m/s downwards, and it sinks through
     * its own hover height before the servo can arrest it. Proportional, it arrives.
     *
     * The margin matters. The flat city's floor plane sits 1 m under the hover height,
     * so an undershoot has somewhere to go before it becomes a contact - but a city
     * that keeps its terrain has road surfaces at every offset, and an undershoot is a
     * collision waiting for one to be at the wrong depth.
     */
    [Theory]
    [InlineData(0.5f)]
    [InlineData(2f)]
    [InlineData(10f)]
    public void DescendingOntoTheHoverHeightDoesNotSinkThroughIt(float fromHeight)
    {
        float lowest = _fly(_command, fromHeight);

        Assert.True(lowest > -0.05f,
            $"descending from {fromHeight} m the ship sank {-lowest:F3} m below its "
            + "hover height before the servo caught it.");
    }


    /**
     * And it still gets there. A proportional descent that never converged would pass
     * the test above trivially by hanging in the air.
     */
    [Theory]
    [InlineData(0.5f)]
    [InlineData(2f)]
    [InlineData(10f)]
    public void TheShipStillReachesItsHoverHeight(float fromHeight)
    {
        const float dt = 1f / 60f;

        float y = fromHeight;
        float vy = 0f;

        for (int i = 0; i < 600; ++i)
        {
            float a = _command(y) - 3.6f * vy;
            if (y < 0f) a += 10f;

            vy += a * dt;
            y += vy * dt;
        }

        Assert.True(Single.Abs(y) < 0.05f,
            $"after 10 s from {fromHeight} m the ship is {y:F3} m off its hover height.");
    }
}
