using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools;

namespace engine.streets.generation;


/**
 * The inner edge of a block's pavement.
 *
 * A block floor is one triangle fan over the block's boundary ring, spanning kerb to kerb
 * with no interior vertices at all - between 3 and 16 vertices carrying a block up to
 * 150 m across. Every corner sits at its own junction's road height, which is what makes
 * the kerb meet the carriageway; but between two corners at different heights the surface
 * is a warped quad, and which way each triangle tilts is decided by the tessellator's
 * sweep rather than by anything geometric. Measured within a pavement's own width over the
 * generated cities on rolling ground, the fan falls 7.2 % ACROSS at the median against an
 * along-edge slope of 7.0 % - i.e. the surface is tilted diagonally at about 45 degrees to
 * the street - with a p95 of 15 % and a worst edge of 33 %. A real footway is built at 2 %.
 *
 * **The condition for level across, exactly.** A rim quad's surface has no cross-gradient
 * precisely when every one of its vertices carries the height the outer edge has at that
 * vertex's own projection onto it: all four heights then lie on the plane h = h0 + s*x,
 * whose gradient runs purely along the edge. Nothing else about the quad's shape matters.
 *
 * **Why the inset ring is per edge and not per corner.** The obvious construction - one
 * inset vertex per corner, at the mitre of its two edges, taking that corner's height - was
 * built and measured and it does not work. A mitre point sits one width from BOTH edge
 * lines, which puts it w*cot(t/2) along the leaving edge from the corner and the same
 * distance back along the arriving one, so the two cells it serves want two different
 * heights for it, differing by about twice the width times the slope. Give it the corner's
 * height and each cell keeps a cross-fall of s*cot(t/2), which at the MEDIAN block corner
 * of 90 degrees is s itself - no improvement whatsoever, and at a 40 degree corner nearly
 * three times worse than today. Measured on the real cities that construction moved the
 * median from 7.2 % to 6.7 %. Give it either cell's height instead and the surface cracks
 * open by 0.4 m at a 2 m pavement, 1.3 m at a 6 m one.
 *
 * The conflict is entirely due to two edges sharing a vertex, so the edges do not share
 * one. Each edge carries its own pair of inset points, both offset the full width, and two
 * neighbouring cells meet only at the outer corner - where the requirement is that both
 * name the corner's own height, which they trivially do. Every rim quad is then level
 * across EXACTLY, at every corner angle, with no crack anywhere.
 *
 * What it costs is that the pavement pinches back to the kerb over a ramp at each corner,
 * and within that ramp the surface is the block interior's rather than the rim's. The ramp
 * clears the corner's mitre and then adds a width, so it is a few metres against a median
 * edge of 66 m: it is the corner itself, which is where the crossing is anyway.
 *
 * Measured over the four baseline cities on rolling ground: 438 of 445 blocks and 79 of 82
 * carry a pavement, and the cross-fall on all 2823 measured edges is 0.0 % at every
 * percentile, against 7.5 % at the median and 63 % at the worst edge for the plain fan.
 *
 * **A block that cannot be inset keeps today's single ring.** Blocks are not convex:
 * 10-16 % of corners are sharper than 60 degrees and 7-15 % are reflex, so an inset can
 * fold through itself or land across the road. Rather than bevel, clamp or repair, InsetOf
 * checks the result and answers null, and the caller emits exactly the floor it emits
 * today. That is the same rule QuarterGenerator._createBuildings already uses when no
 * footprint remains after its own inset.
 */
public static class SidewalkRing
{
    /**
     * The pavement's inner edge, or null if this block cannot carry one.
     *
     * One entry per EDGE - entry i belongs to the edge from outer[i] to outer[i+1] - each
     * carrying that edge's two inset points. See builtin.tools.CapInsetEdge.
     *
     * @param outer
     *     The block's boundary ring, in the order it is traced, with each vertex at the
     *     height of the road it meets. X and Z are the plan; Y is what the inset points
     *     interpolate, which is what makes the rim level across.
     * @param width
     *     Pavement width in metres.
     */
    public static List<CapInsetEdge> InsetOf(in IList<Vector3> outer, float width)
    {
        if (null == outer || outer.Count < 3 || !(width > 0f) || !Single.IsFinite(width))
        {
            return null;
        }

        int n = outer.Count;

        /*
         * Plan directions and lengths of every edge. A zero-length edge has no direction and
         * no inward side, so there is nothing to offset against and the block is left alone.
         */
        var dirs = new Vector2[n];
        var lens = new float[n];
        for (int i = 0; i < n; ++i)
        {
            Vector2 d = _plan(outer[(i + 1) % n]) - _plan(outer[i]);
            float l = d.Length();
            if (!(l > 1e-4f))
            {
                return null;
            }

            dirs[i] = d / l;
            lens[i] = l;
        }

        /*
         * Which side is inward. Derived from the ring's own signed area rather than assumed
         * from how the generator happens to trace blocks, so that this cannot silently
         * produce an OUTset ring - a pavement down the middle of the road - if the tracing
         * order ever changes.
         */
        float area2 = _area2(outer);
        if (0f == area2)
        {
            return null;
        }

        bool isCcw = area2 > 0f;

        /*
         * How far along each edge, from each of its ends, the pavement reaches full width.
         *
         * Not simply one width. The two edges meeting at a corner both reach full width
         * somewhere, and if they do so before the corner's mitre point their inset points
         * cross over each other and the interior ring folds - which at a 90 degree corner,
         * the median one, happens at exactly one width. So the ramp clears the mitre first
         * and then adds a width on top, which puts each edge's inset point safely inside the
         * corner rather than on top of its neighbour's.
         */
        var ramps = new float[n];
        for (int i = 0; i < n; ++i)
        {
            ramps[i] = width + _mitreReach(dirs[(i + n - 1) % n], dirs[i], isCcw, width);
        }

        var edges = new List<CapInsetEdge>(n);
        for (int i = 0; i < n; ++i)
        {
            Vector2 o0 = _plan(outer[i]);
            Vector2 d = dirs[i];
            Vector2 nrm = _inward(d, isCcw);
            float l = lens[i];

            float rStart = ramps[i];
            float rEnd = ramps[(i + 1) % n];

            /*
             * A block whose edges are too short to carry both of their ramps has no room
             * for a pavement of this width at all, and is left with today's plain fan.
             */
            if (rStart + rEnd > 0.9f * l)
            {
                return null;
            }

            Vector3 start = _at(outer, i, o0 + rStart * d + width * nrm, rStart / l);
            Vector3 end = _at(outer, i, o0 + (l - rEnd) * d + width * nrm, (l - rEnd) / l);

            if (!Single.IsFinite(start.X) || !Single.IsFinite(start.Z)
                || !Single.IsFinite(end.X) || !Single.IsFinite(end.Z))
            {
                return null;
            }

            edges.Add(new CapInsetEdge(start, end));
        }

        return _isUsable(outer, edges, width) ? edges : null;
    }


    /**
     * An inset point, at the height the outer edge has directly across from it.
     *
     * The interpolation is the whole construction: a rim quad is level across exactly when
     * each of its vertices carries the outer edge's height at its own projection, and since
     * both of an edge's inset points project back onto that same edge, that is simply the
     * edge's own linear height at the same parameter.
     */
    private static Vector3 _at(in IList<Vector3> outer, int i, in Vector2 plan, float t)
    {
        int n = outer.Count;
        float h = outer[i].Y + t * (outer[(i + 1) % n].Y - outer[i].Y);

        return new Vector3(plan.X, h, plan.Y);
    }


    /**
     * How far along either of its edges a corner's mitre point sits from the corner.
     *
     * The mitre is the point one width in from BOTH edge lines, and it is offset from the
     * corner along each of them by width*cot(t/2) for an interior angle t. That is the
     * distance an edge's own inset has to clear before it can be sure of being inside the
     * corner rather than across it. Clamped, because cot blows up at a near-straight corner
     * where the answer does not matter - the two edges are then almost one line and their
     * insets are almost collinear too.
     */
    private static float _mitreReach(
        in Vector2 dPrev, in Vector2 dNext, bool isCcw, float width)
    {
        Vector2 nPrev = _inward(dPrev, isCcw);
        Vector2 nNext = _inward(dNext, isCcw);

        float denom = 1f + Vector2.Dot(nPrev, nNext);
        if (denom < 1e-4f)
        {
            return 3f * width;
        }

        Vector2 m = width * (nPrev + nNext) / denom;
        float reach = Single.Abs(Vector2.Dot(m, dNext));

        return Single.Min(reach, 3f * width);
    }


    private static Vector2 _plan(in Vector3 v) => new(v.X, v.Z);


    private static float _cross(in Vector2 a, in Vector2 b) => a.X * b.Y - a.Y * b.X;


    /**
     * The unit normal pointing into the polygon for an edge running in direction d.
     */
    private static Vector2 _inward(in Vector2 d, bool isCcw)
        => isCcw ? new Vector2(-d.Y, d.X) : new Vector2(d.Y, -d.X);


    private static float _area2(in IList<Vector3> ring)
    {
        int n = ring.Count;
        float area2 = 0f;
        for (int i = 0; i < n; ++i)
        {
            Vector2 a = _plan(ring[i]), b = _plan(ring[(i + 1) % n]);
            area2 += a.X * b.Y - b.X * a.Y;
        }

        return area2;
    }


    /**
     * The interior polygon the rim leaves behind, in traversal order.
     *
     * The rim cells meet the kerb again at every corner, so the interior's boundary runs
     * corner, inset, inset, corner, inset, inset - reaching out to the block's outline at
     * each corner and back in along each edge. That reach is the pinch: within one pavement
     * width of a corner the surface is the interior's, and at the corner itself the two
     * agree exactly, since both take the corner's own height.
     */
    public static List<Vector3> InteriorRingOf(
        in IList<Vector3> outer, in IList<CapInsetEdge> edges)
    {
        var ring = new List<Vector3>(3 * edges.Count);
        for (int i = 0; i < edges.Count; ++i)
        {
            ring.Add(outer[i]);
            ring.Add(edges[i].Start);
            ring.Add(edges[i].End);
        }

        return ring;
    }


    /**
     * Is what we produced actually a pavement?
     *
     * Three ways an inset goes wrong on a real block, all of them reachable on the
     * generated cities, and all of them a strip drawn somewhere other than along the kerb:
     *
     *   - a point lands outside the block, i.e. across the road;
     *   - the interior ring turns inside out, so the "interior" it bounds is the outside;
     *   - the interior ring crosses itself, which a local per-edge offset cannot notice -
     *     this is what happens once a block is narrower than twice its pavement.
     *
     * The self-intersection scan is every pair of non-adjacent edges of that ring. Blocks
     * have at most 16 corners, hence at most 48 ring vertices, so it is a few hundred
     * segment tests once per block at generation time.
     */
    private static bool _isUsable(
        in IList<Vector3> outer, in List<CapInsetEdge> edges, float width)
    {
        var ring = InteriorRingOf(outer, edges);
        int m = ring.Count;

        if (m < 3)
        {
            return false;
        }

        float outerArea2 = _area2(outer);
        float ringArea2 = _area2(ring);
        if (0f == ringArea2 || (ringArea2 > 0f) != (outerArea2 > 0f))
        {
            return false;
        }

        foreach (var e in edges)
        {
            if (!_containsInPlan(outer, _plan(e.Start))
                || !_containsInPlan(outer, _plan(e.End)))
            {
                return false;
            }
        }

        for (int i = 0; i < m; ++i)
        {
            for (int j = i + 2; j < m; ++j)
            {
                if (0 == i && m - 1 == j) continue;

                if (_segmentsCross(
                        _plan(ring[i]), _plan(ring[(i + 1) % m]),
                        _plan(ring[j]), _plan(ring[(j + 1) % m])))
                {
                    return false;
                }
            }
        }

        return true;
    }


    /**
     * Crossing-number point in polygon, in plan.
     */
    private static bool _containsInPlan(in IList<Vector3> poly, in Vector2 p)
    {
        int n = poly.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = _plan(poly[i]), b = _plan(poly[j]);
            if (a.Y > p.Y != b.Y > p.Y
                && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }


    /**
     * Do the open segments a0a1 and b0b1 properly cross?
     *
     * Touching at an endpoint does not count: consecutive segments of the ring share one,
     * and they are excluded by the caller anyway - what matters here is two parts of the
     * boundary passing through each other's interior.
     */
    private static bool _segmentsCross(
        in Vector2 a0, in Vector2 a1, in Vector2 b0, in Vector2 b1)
    {
        Vector2 da = a1 - a0, db = b1 - b0;

        float d1 = _cross(da, b0 - a0);
        float d2 = _cross(da, b1 - a0);
        float d3 = _cross(db, a0 - b0);
        float d4 = _cross(db, a1 - b0);

        return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
    }
}
