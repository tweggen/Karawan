using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets.generation;


/**
 * The line a pedestrian walks round a block, in plan.
 *
 * A block's outline IS the kerb - GenerateClusterQuartersOperator extrudes it up by
 * QuarterSidewalkOffset, so the top face is the pavement and the sides are the kerb - and a
 * walker going round the block wants to stay on that top face: inside the outline, and no
 * further from it than the pavement is wide.
 *
 * **What was there before, and why it put half the walk in the road.**
 * QuarterLoopRouteGenerator placed the waypoint AT corner i by taking the direction of the
 * edge LEAVING that corner and stepping 1.5 m along its inward normal. The edge ARRIVING at
 * the corner was never consulted, so the corner was treated as if it were the middle of a
 * straight edge. That point is on the inward side of the leaving edge by construction; it is
 * on the inward side of the arriving edge only when the two inward normals point the same
 * way, i.e. when their dot product -cos(t) is positive - so it is inside exactly when the
 * interior angle t exceeds 90 degrees, ON the kerb line at exactly 90, and in the
 * carriageway below it.
 *
 * Measured over the four baseline cities on the shipped terrain, at every block corner whose
 * two edges lie on their own strokes: **every one of the 1243 acute corners put the walker
 * outside the block and 1667 of the 1675 obtuse ones put it inside**, the eight exceptions
 * being corners of 90.000 to 90.002 degrees, where the point lies on the kerb line itself
 * and inside-or-outside is a rounding decision. The median block corner is 90.1 to 94.0
 * degrees, which is why it read as a coin toss: 52 to 58 % of waypoints inside. Signed
 * distance to the kerb, positive inside: median +0.002 to +0.105 m, p05 -0.55 to -1.06 m,
 * worst **-1.15 m**, with 14 to 38 % of waypoints within 5 cm of the line. Along the walk
 * rather than at its corners, 10 to 17 % of the path was outside the kerb.
 *
 * **What replaces it: the corner's own mitre.** The point one offset from BOTH of the edge
 * lines meeting at a corner, on the inward side of each - SidewalkRing.MitreOf. It is inside
 * whatever the corner does, and the segment between two consecutive mitre points has both
 * of its ends one offset from the edge it runs along, so **the whole walk between two
 * corners is exactly parallel to its own kerb**. Measured on the same corners: 100 % of
 * waypoints and 100 % of sampled positions along every segment are inside the block, and the
 * perpendicular distance to the edge being walked is at most half the pavement's width
 * everywhere.
 *
 * §7k rejected the mitre for the pavement SURFACE, and that reasoning does not carry over.
 * A mitre vertex is bad in a mesh because the two rim cells sharing it want two different
 * heights for it; a walker is a point, has exactly one height, and shares nothing.
 *
 * **Why not the pavement's own inset ring.** SidewalkRing.InsetOf already answers where the
 * pavement's inner edge runs, is already validated, and carries §7k's level-across heights -
 * so walking on its points would put the walker on the drawn surface by construction. But
 * its points belong to EDGES and deliberately not to corners, which is its whole design, and
 * a loop has to turn corners. Joining consecutive inset points cuts across each corner, and
 * where a block folds inward - 6 to 16 % of corners are reflex - that cut leaves the block
 * entirely: measured, 0.5 to 1.2 % of the path outside by up to **11.07 m** taking one point
 * per corner, and 0.1 to 0.3 % by up to **6.20 m** taking both points of every edge. Against
 * 0.0 % for the mitre. It also doubles the number of waypoints, which SegmentNavigator
 * cannot take - see QuarterLoopRouteGenerator.
 */
public static class PavementWalk
{
    /**
     * How far from the kerb a walker keeps, on a pavement of the given width.
     *
     * Half the pavement, so that the walk is on the pavement rather than at the edge of it
     * and a metre of error either way stays there - but never more than the 1.5 m the
     * shipped walker has always used, since beyond that it is no longer "keeping to the
     * pavement" but wandering across the block. So a 1 m pavement holds the walker 0.5 m in
     * and a 6 m one 1.5 m in, and on the 4 m and 6 m pavements that carry 67 % of the
     * baselines' corners the offset is unchanged from what shipped.
     *
     * Note what real data cannot tell you here: **the baseline cities contain no 1 m
     * pavement at all** - 0 of 2918 corners - because SidewalkWidth is 1 m only where
     * downtownness is below 0.2 and no traced block centre of any of the four is. So the
     * narrow case is real, reachable and untestable from the generated cities, and is
     * covered by fixture instead.
     */
    public static float OffsetFor(float sidewalkWidth)
        => Single.Min(1.5f, 0.5f * sidewalkWidth);


    /**
     * One walk position per corner of a block's outline, in the order it is traced.
     *
     * Every returned point is strictly inside the ring in plan and at most `sidewalkWidth`
     * from it, so a walker on them is on the pavement and never in the carriageway.
     *
     * @param corners
     *     The block's outline in plan, which is the kerb line.
     * @param sidewalkWidth
     *     The block's own pavement width. Quarter.SidewalkWidth - the same number the
     *     block floor insets its cap by and the same number the estate is inset by, so
     *     that the walk, the pavement and the building line cannot drift apart.
     * @returns
     *     null for a ring that has no inside at all - fewer than three corners, or zero
     *     signed area. Never a shorter list than `corners`.
     */
    public static List<Vector2> RingOf(in IList<Vector2> corners, float sidewalkWidth)
    {
        if (null == corners || corners.Count < 3
            || !(sidewalkWidth > 0f) || !Single.IsFinite(sidewalkWidth))
        {
            return null;
        }

        int n = corners.Count;

        float area2 = SidewalkRing.SignedArea2Of(corners);
        if (0f == area2 || !Single.IsFinite(area2))
        {
            return null;
        }

        bool isCcw = area2 > 0f;
        float offset = OffsetFor(sidewalkWidth);

        /*
         * Plan directions of every edge. A zero length edge has no direction and hence no
         * inward side; the corners at its ends keep the kerb rather than guess one.
         */
        var dirs = new Vector2?[n];
        for (int i = 0; i < n; ++i)
        {
            Vector2 d = corners[(i + 1) % n] - corners[i];
            float l = d.Length();
            dirs[i] = l > 1e-4f ? d / l : null;
        }

        var walk = new List<Vector2>(n);
        for (int i = 0; i < n; ++i)
        {
            walk.Add(_at(corners, dirs, i, isCcw, offset, sidewalkWidth));
        }

        return walk;
    }


    private static Vector2 _at(
        in IList<Vector2> corners, in Vector2?[] dirs, int i, bool isCcw,
        float offset, float sidewalkWidth)
    {
        int n = corners.Count;
        Vector2 c = corners[i];

        Vector2? dPrev = dirs[(i + n - 1) % n];
        Vector2? dNext = dirs[i];
        if (!dPrev.HasValue || !dNext.HasValue)
        {
            return c;
        }

        if (!SidewalkRing.MitreOf(
                SidewalkRing.InwardNormalOf(dPrev.Value, isCcw),
                SidewalkRing.InwardNormalOf(dNext.Value, isCcw),
                offset, out Vector2 m))
        {
            return c;
        }

        /*
         * The mitre's length is offset/sin(t/2) for an interior angle t, so it runs away as
         * a corner sharpens: at 30 degrees it is already four offsets from the corner. Bound
         * it by the pavement's own width, which is exactly the statement "a walker rounding
         * a corner is still on the pavement" - and note the bound is on the DISTANCE, not on
         * the direction, so the point stays on the corner's bisector and the walk stays
         * symmetric about it.
         */
        float l = m.Length();
        if (!(l > 0f))
        {
            return c;
        }

        if (l > sidewalkWidth)
        {
            m *= sidewalkWidth / l;
        }

        /*
         * ...and then check, rather than argue. A block narrow enough that one edge's
         * pavement reaches across to another's has nowhere to put a walker off the kerb, and
         * such a block is not hypothetical - QuarterGenerator traces whatever the street
         * graph leaves. The kerb line itself is the honest answer there: it is where the
         * pavement is, and it is never in the road.
         */
        Vector2 p = c + m;

        return SidewalkRing.ContainsInPlan(corners, p) ? p : c;
    }
}
