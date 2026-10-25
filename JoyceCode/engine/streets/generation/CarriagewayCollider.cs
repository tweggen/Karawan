using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets.generation;


/**
 * The colliders that stand in for one stroke's carriageway in the physics world: convex
 * prisms over the carriageway the road MESH draws, at the heights the mesh gives it.
 *
 * It used to be one box from junction centre to junction centre, tilted by the stroke's
 * grade (the former DeckCollider.For). The mesh is not built that way: RoadSurface holds
 * each end flat at its junction's one height along the section chord where the
 * carriageway leaves the cap, and each kerb rises linearly from its A to its B corner.
 * So inside a junction the box and the picture parted company by the cap's axial reach
 * times the grade - up to several metres:
 *
 * - on a branch RISING away from the junction the box stood proud of the flat cap, an
 *   invisible ridge across the middle of the junction;
 * - on a branch FALLING away from it the box sank below the cap, and the cap's own slab
 *   (JunctionCollider), flat at the junction's height, stood up out of the road where the
 *   carriageway leaves it. A ship climbing that branch into the junction drove into the
 *   slab's edge (KAR-465): measured over the seventy shipped cities, 2 812 street ends and
 *   48 ramp feet with an edge over 1 m, the worst 2.3 m.
 *
 * Built on the mesh's own corners, the carriageway's colliders begin exactly at the
 * section chord at the junction's height, where the cap slab begins at that same height,
 * and reach into no junction at all - so neither the ridge nor the edge can exist.
 *
 * ## Why slices
 *
 * Every vertex the mesh emits lies on one of the two kerbs, and the mesh joins a left and
 * a right kerb point at the same axial position. Where an end is oblique the two kerbs
 * have different lengths and therefore different slopes, so the drawn surface is twisted,
 * and the top of ONE convex hull over the four corners bridges above it - measured up to
 * 1.8 m. The carriageway is therefore cut across into axial slices, each its own prism,
 * short enough that a slice's length times the two kerbs' slope difference stays under
 * MaxTwist; where the kerbs climb alike the road is planar and needs no cut. Measured
 * over the seventy shipped cities (2026-10-25): 5.0 prisms per stroke on average (the
 * two end wedges of an oblique stroke are slices of their own), the top within 4.7 cm
 * of the drawn road at the 99th percentile and 0.17 m at worst, never below it. Every slice vertex lies on the
 * carriageway's boundary with the height the mesh gives that boundary, so no slice can
 * dip below the drawn road.
 *
 * Kept as a plain computation, separate from the Bepu call that consumes it, so the part
 * with the arithmetic in it can be tested without a physics simulation.
 */
internal readonly struct CarriagewayCollider
{
    /** Shortest slice a strongly twisted carriageway is cut into, in metres. */
    internal const float MinSliceLength = 0.25f;

    /**
     * The slice length times the two kerbs' slope difference is held to this, in metres:
     * how far the twisted road may part from one slice's flat top.
     */
    internal const float MaxTwist = 0.2f;

    /** Corners closer than this along the stroke share a slice instead of each getting a cut. */
    internal const float MinCutSpacing = 0.5f;


    /**
     * One prism per slice, its points in world space: for every corner of the slice's
     * plan polygon the top point (the road) then the bottom point (skirt).
     */
    internal readonly IReadOnlyList<IReadOnlyList<Vector3>> Slices;


    internal bool IsUsable => Slices.Count > 0;


    private CarriagewayCollider(IReadOnlyList<IReadOnlyList<Vector3>> slices)
    {
        Slices = slices;
    }


    private readonly record struct Corner(Vector2 Plan, float Height);


    /**
     * @param origin, unit
     *     A point on the stroke's axis and its direction, in cluster-local plan
     *     coordinates - the stroke's A position and Stroke.Unit.
     * @param leftA, rightA, leftB, rightB
     *     The carriageway's four corners in cluster-local plan coordinates - what
     *     RoadSurface.TryCornersOf hands the mesh, so the two cannot disagree.
     * @param v3Origin
     *     Where the cluster's origin lands in world space. Y is ignored.
     * @param heightA, heightB
     *     The road height at the A and the B junction - RoadSurface.HeightAtJunction,
     *     the same numbers the mesh and the junction caps are built at.
     * @param thickness
     *     How deep each prism is. Only its top face matters for driving on.
     */
    internal static CarriagewayCollider For(
        in Vector2 origin, in Vector2 unit,
        in Vector2 leftA, in Vector2 rightA, in Vector2 leftB, in Vector2 rightB,
        in Vector3 v3Origin, float heightA, float heightB, float thickness)
    {
        /*
         * The carriageway in drawing order, each corner at its junction's height. Every
         * point of its boundary then has the height the mesh gives it by interpolating
         * along the edge it lies on: the kerbs rise linearly, the two section chords are
         * level at their junction's height.
         */
        var quad = new List<Corner>
        {
            new(leftA, heightA), new(rightA, heightA), new(rightB, heightB), new(leftB, heightB)
        };

        Vector2 o = origin, u = unit;
        float Axial(Vector2 p) => Vector2.Dot(p - o, u);

        float dMin = Single.MaxValue, dMax = Single.MinValue;
        var breaks = new List<float>();
        foreach (var c in quad)
        {
            float d = Axial(c.Plan);
            dMin = Single.Min(dMin, d);
            dMax = Single.Max(dMax, d);
            breaks.Add(d);
        }

        var slices = new List<IReadOnlyList<Vector3>>();
        if (!(dMax - dMin > 0.01f))
        {
            return new CarriagewayCollider(slices);
        }

        /*
         * Cut at every corner's axial position - where a kerb or a chord begins or ends -
         * and evenly in between so that no slice is longer than sliceLength.
         */
        /*
         * Ends that overlap - the A section chord reaching past the B chord, as on a very
         * short stroke between two wide junctions - are what the mesh draws as one quad
         * over the four section points (its "a and b ends overlapping" case). One prism
         * over the same four corners is exactly that.
         */
        if (Single.Max(Axial(leftA), Axial(rightA)) >= Single.Min(Axial(leftB), Axial(rightB)))
        {
            if (_area(quad) >= 1e-4f) slices.Add(_prism(quad, v3Origin, thickness));
            return new CarriagewayCollider(slices);
        }

        /*
         * How long a slice may be: the twist inside one is its length times the two kerbs'
         * slope difference, so a strongly twisted carriageway (very oblique ends, a short
         * stroke) is cut finer. Where both kerbs climb alike the drawn road is a plane and
         * one prism between the corner cuts is exact - the common case, and the one that
         * keeps the count of statics near one per stroke.
         */
        float slopeLeft = (heightB - heightA) / Single.Max(0.01f, Axial(leftB) - Axial(leftA));
        float slopeRight = (heightB - heightA) / Single.Max(0.01f, Axial(rightB) - Axial(rightA));
        float slopeDifference = Single.Abs(slopeLeft - slopeRight);
        float sliceLength = slopeDifference > 1e-6f
            ? Single.Max(MaxTwist / slopeDifference, MinSliceLength)
            : Single.PositiveInfinity;

        breaks.Sort();
        var cuts = new List<float> { breaks[0] };
        for (int i = 1; i < breaks.Count; ++i)
        {
            float from = cuts[^1], to = breaks[i];

            /*
             * A corner this close to the previous cut gets no cut of its own: it stays a
             * vertex of the slice that holds it, and no sliver too thin to make a hull
             * out of is ever produced - a dropped sliver would take its corner, and the
             * seam with the cap, with it.
             */
            if (to - from < MinCutSpacing && i < breaks.Count - 1) continue;
            if (to - from < MinCutSpacing)
            {
                if (cuts.Count > 1) cuts[^1] = to;
                else cuts.Add(to);
                continue;
            }

            int n = Single.IsPositiveInfinity(sliceLength)
                ? 1
                : (int)Single.Ceiling((to - from) / sliceLength);
            for (int k = 1; k <= n; ++k) cuts.Add(from + (to - from) * k / n);
        }

        for (int i = 0; i + 1 < cuts.Count; ++i)
        {
            var poly = _clip(quad, Axial, cuts[i], keepAbove: true);
            poly = _clip(poly, Axial, cuts[i + 1], keepAbove: false);
            if (poly.Count < 3 || _area(poly) < 1e-4f) continue;

            slices.Add(_prism(poly, v3Origin, thickness));
        }

        return new CarriagewayCollider(slices);
    }


    private static Vector3[] _prism(List<Corner> poly, in Vector3 v3Origin, float thickness)
    {
        var points = new Vector3[2 * poly.Count];
        for (int k = 0; k < poly.Count; ++k)
        {
            float x = v3Origin.X + poly[k].Plan.X;
            float z = v3Origin.Z + poly[k].Plan.Y;
            points[2 * k] = new Vector3(x, poly[k].Height, z);
            points[2 * k + 1] = new Vector3(x, poly[k].Height - thickness, z);
        }
        return points;
    }


    /*
     * One Sutherland-Hodgman step against the line axial == cut. A point made on an edge
     * takes its height by interpolating along that edge - which is the height the mesh
     * gives that edge.
     */
    private static List<Corner> _clip(List<Corner> poly, Func<Vector2, float> axial, float cut, bool keepAbove)
    {
        var result = new List<Corner>();
        bool Inside(Corner c) => keepAbove ? axial(c.Plan) >= cut - 1e-4f : axial(c.Plan) <= cut + 1e-4f;

        for (int k = 0; k < poly.Count; ++k)
        {
            Corner cur = poly[k], next = poly[(k + 1) % poly.Count];
            bool inCur = Inside(cur), inNext = Inside(next);
            if (inCur) result.Add(cur);
            if (inCur != inNext)
            {
                float a = axial(cur.Plan), b = axial(next.Plan);
                float t = Single.Clamp((cut - a) / (b - a), 0f, 1f);
                result.Add(new Corner(Vector2.Lerp(cur.Plan, next.Plan, t), Single.Lerp(cur.Height, next.Height, t)));
            }
        }
        return result;
    }


    private static float _area(List<Corner> poly)
    {
        float twice = 0f;
        for (int k = 0; k < poly.Count; ++k)
        {
            Vector2 p = poly[k].Plan, q = poly[(k + 1) % poly.Count].Plan;
            twice += p.X * q.Y - q.X * p.Y;
        }
        return Single.Abs(twice) * 0.5f;
    }
}
