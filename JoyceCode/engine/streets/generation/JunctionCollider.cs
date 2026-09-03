using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets.generation;


/**
 * The slab that stands in for one junction cap in the physics world.
 *
 * A stroke's DeckCollider spans junction centre to junction centre, so the boxes of the
 * streets meeting at a junction all reach its middle and overlap there. What none of
 * them covers is the area BETWEEN the branches - the outer corners of the cap that
 * GenerateClusterStreetsOperator._generateJunction fills with a fan. Over that wedge the
 * hover probe's ray finds nothing built, falls back to the terrain height, and the ship
 * is commanded down onto the edges of the converging stroke boxes.
 *
 * So the cap gets a slab of its own, under the same two conditions the stroke colliders
 * are emitted under: a flat city's fragment floor plane already covers every junction on
 * the ground, and only a raised deck escapes it.
 *
 * **The shape is the cap itself, not a disc over it.** A junction is one node with one
 * height, so a horizontal disc of the cap's own radius is the obvious candidate and needs
 * no orientation at all. Measured over the generated baselines, the circumscribed disc is
 * 2.75x the cap's plan area at the median (a three-arm cap is a triangle, and a triangle
 * is 2.42x its own circumcircle by construction), 5.3x at the 99th percentile, and 34x at
 * the worst junction in a 3000 m city - where two nearly collinear strokes push a section
 * point 42 m out and the disc becomes an 85 m pancake. That surplus is an INVISIBLE
 * apron: a slab at street height reaching several metres past the road into ground the
 * terrain-following city may have put well below it, which is the artefact this change
 * exists to remove rather than to introduce. A convex hull over the cap's own corners has
 * no surplus and costs one hull per junction per fragment, about thirteen for the largest
 * city in the baselines.
 *
 * Kept as a plain computation, separate from the Bepu call that consumes it, so the part
 * with the arithmetic in it can be tested without a physics simulation.
 */
internal readonly struct JunctionCollider
{
    /**
     * Below this the cap is not a surface anybody can stand on and the hull builder has
     * nothing to work with. Real caps are 50 - 300 m2.
     */
    internal const float MinArea = 0.1f;


    /**
     * The prism's corners in world space, top face first at each section point: 2N
     * points for an N-corner cap. The TOP face is the road, as with DeckCollider, so
     * everything below is skirt.
     */
    internal readonly IReadOnlyList<Vector3> Points;

    /**
     * Plan area of the cap, summed over the same fan the mesh is triangulated as.
     */
    internal readonly float Area;


    internal bool IsUsable => Points.Count >= 6 && Area >= MinArea;


    private JunctionCollider(IReadOnlyList<Vector3> points, float area)
    {
        Points = points;
        Area = area;
    }


    /**
     * Does this junction need a slab of its own?
     *
     * The same two escapes from the fragment floor plane that DeckCollider.IsNeededFor
     * tests, plus one condition of its own: there has to BE a cap.
     *
     * A junction of fewer than three arms has no cap with any area. The mesh emits a fan
     * over two section points and their own midpoint, so both of its triangles are
     * degenerate - which is also why _generateJunction's guard is written as "fewer than
     * two points" and still produces nothing visible at two. Nothing is missing there
     * either: two strokes meeting head on hand their surfaces over to each other at the
     * junction, and their boxes overlap across it.
     *
     * @param groundIsFlat
     *     Whether the whole city sits at one height - IStreetHeightSource.IsFlat.
     */
    internal static bool IsNeededFor(in StreetPoint sp, bool groundIsFlat)
    {
        if (sp.GetSectionArray().Count < 3)
        {
            return false;
        }

        return !groundIsFlat || 0 != sp.Level;
    }


    /**
     * @param sectionPoints
     *     StreetPoint.GetSectionArray(), in cluster-local plan coordinates - the same
     *     list the cap's mesh is built from, so the two cannot disagree about the shape.
     * @param v3Origin
     *     Where the cluster's origin lands in world space. Y is ignored.
     * @param surfaceHeight
     *     World height of the cap. A junction is one node, so it has exactly one.
     * @param thickness
     *     How deep the slab is. Only its top face matters for driving on.
     */
    internal static JunctionCollider For(
        IList<Vector2> sectionPoints, in Vector3 v3Origin, float surfaceHeight, float thickness)
    {
        int l = null == sectionPoints ? 0 : sectionPoints.Count;
        if (l < 3)
        {
            return new JunctionCollider(Array.Empty<Vector3>(), 0f);
        }

        Vector2 centre = Vector2.Zero;
        foreach (var p in sectionPoints)
        {
            centre += p;
        }

        centre /= l;

        /*
         * Summed over the fan rather than as a signed shoelace, because the fan is what
         * the mesh draws and an absolute sum cannot cancel a concave corner away into
         * "no cap here".
         */
        float area = 0f;
        for (int k = 0; k < l; ++k)
        {
            Vector2 e0 = sectionPoints[k] - centre;
            Vector2 e1 = sectionPoints[(k + 1) % l] - centre;
            area += Single.Abs(e0.X * e1.Y - e0.Y * e1.X) * 0.5f;
        }

        var points = new Vector3[2 * l];
        for (int k = 0; k < l; ++k)
        {
            float x = v3Origin.X + sectionPoints[k].X;
            float z = v3Origin.Z + sectionPoints[k].Y;

            points[2 * k] = new Vector3(x, surfaceHeight, z);
            points[2 * k + 1] = new Vector3(x, surfaceHeight - thickness, z);
        }

        return new JunctionCollider(points, area);
    }
}
