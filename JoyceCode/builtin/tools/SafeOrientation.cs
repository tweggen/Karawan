using System;
using System.Numerics;
using static engine.Logger;

namespace builtin.tools;

/**
 * Turn an arbitrary quaternion into one that is safe to hand to the physics engine.
 *
 * WHY THIS EXISTS
 *
 * BepuPhysics asserts on the way in:
 *
 *     Debug.Assert(..., "Orientation should be initialized to a unit length quaternion.")
 *     at BepuPhysics.Bodies.Add(BodyDescription& description)
 *
 * and a failed Debug.Assert on Android does not print and continue - it aborts the process.
 * A single bad quaternion in a persisted game state therefore becomes a boot loop: load,
 * create the player body, abort, relaunch, load the same state again.
 *
 * THE TRAP
 *
 * Quaternion.Normalize does NOT rescue a degenerate quaternion, it launders it. For the
 * all-zero default it computes 1/sqrt(0) = Infinity and then 0 * Infinity = NaN, so the
 * result is a quaternion of NaNs which still fails the unit-length assert - only now the
 * original zero is gone and the value looks like it came from a computation. NaN in
 * propagates to NaN out for the same reason. Calling Normalize on untrusted input is not a
 * validation step, and reading it as one is what hid this.
 *
 * default(Quaternion) is (0,0,0,0), NOT identity - Quaternion.Identity is (0,0,0,1). Any
 * struct field that nobody assigned, and any deserialisation that missed a field, hands you
 * the degenerate one.
 */
public static class SafeOrientation
{
    /**
     * How far |q| may stray from 1 before it is worth renormalising. Well inside what
     * Bepu's own assert tolerates, so anything this accepts, it accepts too.
     */
    private const float LengthTolerance = 1e-3f;

    /**
     * The smallest length still considered recoverable by scaling. Below this the direction
     * carries no information and normalising it amplifies noise into an arbitrary rotation,
     * so identity is the honest answer.
     */
    private const float DegenerateLength = 1e-6f;


    /**
     * Return a unit quaternion for <paramref name="q"/>, substituting identity when it
     * cannot be repaired.
     *
     * <param name="wasRepaired">
     * True when the input was NOT already a usable unit quaternion. Callers that read
     * persisted or platform-supplied data should log on this - reaching it means something
     * upstream stored a value that would have aborted the process.
     * </param>
     */
    public static Quaternion Sanitize(Quaternion q, out bool wasRepaired)
    {
        if (!Single.IsFinite(q.X) || !Single.IsFinite(q.Y)
            || !Single.IsFinite(q.Z) || !Single.IsFinite(q.W))
        {
            wasRepaired = true;
            return Quaternion.Identity;
        }

        float length = q.Length();

        if (!Single.IsFinite(length) || length < DegenerateLength)
        {
            wasRepaired = true;
            return Quaternion.Identity;
        }

        if (Single.Abs(length - 1f) <= LengthTolerance)
        {
            wasRepaired = false;
            return q;
        }

        /*
         * Recoverable: a real rotation that has drifted off the unit sphere, which is what
         * repeated incremental updates produce. Scale it back rather than discarding it.
         */
        wasRepaired = true;
        float inv = 1f / length;
        return new Quaternion(q.X * inv, q.Y * inv, q.Z * inv, q.W * inv);
    }


    /**
     * Convenience overload that logs an Error naming the offending value when it has to
     * repair, for the read-from-persistence case where a repair is always a defect
     * somewhere upstream.
     */
    public static Quaternion Sanitize(Quaternion q, string what)
    {
        Quaternion result = Sanitize(q, out bool wasRepaired);
        if (wasRepaired)
        {
            Error($"{what}: orientation {q} (length {q.Length()}) is not a unit quaternion; "
                  + $"using {result} instead. Physics would have aborted the process on this.");
        }

        return result;
    }
}
