using System;
using System.Numerics;

namespace engine.streets.generation;


/**
 * Where the carriageway edges of two adjacent arms of a junction cross.
 *
 * A junction's section array holds one point per adjacent pair of arms, and that point is
 * the MITRE: the one point that is half a street width inward of the prev arm's centre line
 * and half a street width inward of the curr arm's, on the side of each that faces into the
 * wedge between them. Everything a city is built out of hangs off it - it is the corner of
 * the junction cap, the corner of the block that stands there (§7i), the end of the
 * carriageway (§7o), and through the block outline the estate, the building footprint and
 * its shops.
 *
 * **Why this is not a call to Line.IntersectInfinite.** The mitre used to be computed by
 * intersecting the two offset lines in absolute world coordinates, by Cramer's rule on
 * homogeneous line coordinates whose constant term is `A.Y*B.X - A.X*B.Y`. On a cluster
 * 3 km across that term is of order 2e6 and the numerator of the solve is a difference of
 * two products of order 2e8 - so when the two lines are nearly parallel and the determinant
 * falls to a few thousandths, every significant digit of the answer is cancelled away. The
 * intersection then comes back tens of metres off BOTH of the lines it is supposed to lie
 * on. Measured over the four baseline cities plus four hairpin-dense seeds, that is where
 * every one of §7o's off-line block corners comes from - up to 27.3 m at `seed027`/1500 and
 * 10.8 m at `Yelukhdidru`/3000, all of them at an arm-to-arm angle of 179.9999-180.0001
 * degrees.
 *
 * The construction below never forms an absolute coordinate. It works entirely in unit
 * normals and half widths, so its accuracy does not depend on where in the world the
 * junction is.
 *
 * **The two halves.** Write the wedge angle between the arms as t (measured from the prev
 * arm counterclockwise to the curr arm, so t is in (0, 2pi)), the average half width as S
 * and half the difference of the two half widths as D. The mitre is then
 *
 *     S/sin(t/2) along the bisector   +   D/cos(t/2) across it
 *
 * and the two terms fail in two completely different places. The first blows up only as
 * t -> 0, a hairpin - two arms leaving the junction in nearly the same direction. The
 * second blows up only as t -> pi, a straight-through junction, and then only if the two
 * arms are of DIFFERENT width, where two parallel offset lines at different offsets have no
 * common point at all. Neither is a numerical artefact: both are the honest answer that the
 * mitre is at infinity, which is what a mitre limit is for.
 *
 * ⚠️ **The straight-through case is the common one, and the code this replaced had it the
 * right way round.** Its comment said the far-out intersection means *"these are pretty
 * in-line streets"* and that is exactly what it is: over five generated cities every single
 * one of the 46 corners that took its distance fallback was at 179.9999-180.2983 degrees,
 * and not one was a hairpin. What the fallback could not do is notice the cases where the
 * cancelled intersection happened to land NEAR the junction, since it tested only how far
 * away the answer was.
 *
 * @see SidewalkRing.MitreOf, which is the same expression for the equal-width case and is
 *     reused here rather than written a second time.
 */
public static class SectionMitre
{
    /**
     * How far a section point may stand from its junction, in multiples of the average half
     * street width, before the mitre is cut back to that bound.
     *
     * Replaces an absolute `dist2 > 4000f` - 63.24 m - which is a mitre limit of 6.7 on a
     * narrow street and 13 on a wide one, i.e. no bound in practice on the wide streets
     * where an over-long corner does the most damage. Relative is also what ClipperOffset
     * does with `JoinType.jtMiter` twenty lines away in QuarterGenerator._createBuildings,
     * whose own default limit is 2.
     *
     * The value is 3 rather than Clipper's 2 because a section point is not a cosmetic
     * corner: it is one end of a carriageway, and cutting it back moves the block, the
     * estate, the building and its shops. Measured over eight generated cities and 6084
     * section points, 3 clamps 15 corners while 2 clamps 725 - the first is every genuinely
     * degenerate corner in the corpus and the second is one corner in eight.
     */
    public const float MitreLimit = 3f;


    /**
     * The section point between two adjacent arms, as an offset FROM the junction.
     *
     * @param dPrev
     *     Unit direction of the previous arm, pointing OUT of the junction.
     * @param dCurr
     *     Unit direction of the current arm, pointing out of the junction. The pair is read
     *     in the counterclockwise order StreetPoint's angle array holds, so the wedge this
     *     answers about is the one swept counterclockwise from dPrev to dCurr.
     * @param halfWidthPrev
     *     Half the previous arm's street width.
     * @param halfWidthCurr
     *     Half the current arm's street width.
     * @param mitreLimit
     *     Bound on each of the two terms, in multiples of the average half width.
     * @param isClamped
     *     True when the mitre was cut back, i.e. when the returned point is NOT on both
     *     carriageway edges. Callers that care about that property have to ask.
     */
    public static Vector2 OffsetOf(
        in Vector2 dPrev, in Vector2 dCurr,
        float halfWidthPrev, float halfWidthCurr,
        float mitreLimit,
        out bool isClamped)
    {
        isClamped = false;

        /*
         * The inward normals of the two offset lines - the direction each of them has to be
         * moved to reach into the wedge. Rotating the prev arm's outward direction by +90
         * degrees and the curr arm's by -90 puts both of them there, which is what makes the
         * two-normal form below symmetric in the two arms.
         */
        Vector2 nPrev = new(-dPrev.Y, dPrev.X);
        Vector2 nCurr = new(dCurr.Y, -dCurr.X);

        float aver = 0.5f * (halfWidthPrev + halfWidthCurr);
        float diff = 0.5f * (halfWidthPrev - halfWidthCurr);
        float limit = mitreLimit * aver;

        /*
         * Along the bisector: the equal-width mitre, S/sin(t/2) out. This is exactly
         * SidewalkRing.MitreOf, whose denominator 1 + nPrev.nCurr is 2sin^2(t/2) - so it is
         * well conditioned everywhere except the hairpin, which is the one place the answer
         * really is unbounded.
         */
        Vector2 along;
        if (SidewalkRing.MitreOf(nPrev, nCurr, aver, out Vector2 m))
        {
            if (m.LengthSquared() > limit * limit)
            {
                along = Vector2.Normalize(m) * limit;
                isClamped = true;
            }
            else
            {
                along = m;
            }
        }
        else
        {
            /*
             * A hairpin: the two arms leave in the same direction and there is no finite
             * mitre. The bisector is still the right direction and is still the sum of the
             * two normals - it is only its LENGTH that has gone away - so take it at the
             * bound. If even that has cancelled to nothing the two arms are the same ray and
             * any direction into the wedge will do; the curr arm's own normal is the answer
             * the parallel case gives and is used here for the same reason.
             */
            Vector2 sum = nPrev + nCurr;
            float l = sum.Length();
            along = limit * (l > 1e-6f ? sum / l : nCurr);
            isClamped = true;
        }

        /*
         * Across the bisector: what a difference in the two arms' widths shifts the corner
         * along the street, D/cos(t/2). Exactly zero for two arms of equal width, which is
         * most of them, and unbounded at a straight-through junction whose two arms are of
         * different width - where the two offset lines are parallel and a whole street width
         * apart, so there is no corner and the honest answer is to leave the point square
         * off the junction and let the carriageway step.
         */
        Vector2 across = Vector2.Zero;
        if (0f != diff)
        {
            float denom = 1f - Vector2.Dot(nPrev, nCurr);
            if (denom > 1e-4f)
            {
                across = diff * (nPrev - nCurr) / denom;
                if (across.LengthSquared() > limit * limit)
                {
                    across = Vector2.Normalize(across) * limit;
                    isClamped = true;
                }
            }
            else
            {
                isClamped = true;
            }
        }

        /*
         * No final "and if that is still not finite" guard. There was one and it was
         * unreachable AND ineffective, which mutation testing said out loud by leaving it
         * alive: every path above already ends in a finite vector for any finite input, and
         * for a NaN direction the guard's own fallback is built from the same NaN. Both arm
         * directions come from Stroke.Unit, which throws rather than returning a zero-length
         * one.
         */
        return along + across;
    }
}
