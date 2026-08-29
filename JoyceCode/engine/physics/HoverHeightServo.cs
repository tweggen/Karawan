using System;

namespace engine.physics;


/**
 * The vertical half of a hover vehicle's controller, as a pure function.
 *
 * The controller holds the ship a fixed distance above a ground sample by asking for a
 * vertical SPEED and letting an inner loop chase it with a force. This decides that
 * speed from the height error, and it is deliberately ASYMMETRIC.
 *
 * Climbing keeps full authority. Being slow to rise is how a hover vehicle ends up
 * inside a hillside, and the loop is the only collision the terrain has - the terrain
 * carries no collider at all - so the moment the ship is under its height it is asked
 * for everything it has.
 *
 * DESCENDING IS TRIMMING, AND MUST NOT OUT-MUSCLE WHAT THE SHIP IS STANDING ON. A
 * constant full-thrust descent command is fine while the only thing under the ship is
 * the terrain sample it was computed from, because it stops the instant the ship
 * arrives. It is not fine when something BUILT holds the ship above that sample - a
 * street relaxed to a buildable gradient standing proud of the hill it crosses, a deck,
 * a kerb - because then the command never stops: the ship is pressed onto that surface
 * for as long as it drives along it, with a normal load of mass times the commanded
 * deceleration. At the shipped constants that load exceeded the ship's own maximum
 * thrust, so the vehicle could not move at all. That is the bug this shape exists to
 * remove.
 *
 * So the descent is proportional to the error and only saturates far from the target:
 * a surface standing 1 m proud is leaned on with a fifth of the force, 0.5 m with a
 * tenth, and the ship slides along it instead of being pinned to it. Beyond
 * levelDownThrust * levelDownApproachTime the answer is exactly what it always was, so
 * the far field - a ship high over ground that has fallen away - is unchanged.
 *
 * The second thing this buys is the end of the undershoot. With a constant command the
 * ship only stops being told to descend once it is AT its height, by which time it
 * carries several m/s downwards and sinks through by a decimetre or so before the
 * inner loop arrests it. Proportional, it arrives and stops.
 */
public static class HoverHeightServo
{
    /**
     * Time constant of the proportional descent, in seconds: the error is divided by
     * this to get the commanded speed.
     *
     * Short enough that the descent rate is unchanged more than a few metres out - a
     * ship 10 m up reaches its height only fractionally later than it used to - and
     * long enough that a surface a metre proud is not shoved at.
     */
    public const float DefaultLevelDownApproachTime = 0.15f;


    /**
     * Height error, in metres, inside which the ship is simply left alone.
     */
    public const float DefaultDeadBand = 0.01f;


    /**
     * The vertical speed the ship should be asked to fly at, in m/s, positive up.
     *
     * @param deltaY
     *     How far the ship is ABOVE the height it wants to hold, in metres. Negative
     *     means it is too low.
     * @param levelUpThrust
     *     Climb speed asked for whenever the ship is below its height.
     * @param levelDownThrust
     *     Fastest descent that may ever be asked for.
     * @param levelDownApproachTime
     *     Seconds; see DefaultLevelDownApproachTime. Zero divides to infinity and the
     *     minimum below then returns levelDownThrust, i.e. the old constant command -
     *     which is the right thing for a degenerate setting to degrade to.
     */
    public static float CommandedVerticalVelocity(
        float deltaY,
        float levelUpThrust,
        float levelDownThrust,
        float levelDownApproachTime,
        float deadBand)
    {
        if (deltaY < -deadBand)
        {
            return levelUpThrust;
        }

        if (deltaY > deadBand)
        {
            return -Single.Min(levelDownThrust, deltaY / levelDownApproachTime);
        }

        return 0f;
    }
}
