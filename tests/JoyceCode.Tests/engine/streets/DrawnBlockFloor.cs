using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.tools;
using engine.streets;
using engine.streets.generation;
using engine.world;

namespace JoyceCode.Tests.engine.streets;


/**
 * A block's floor as the operator emits it, readable at a plan position.
 *
 * The floor's own TRIANGLES rather than the ring it was built from, because what a walker
 * stands on and what a player sees is the surface - §7j found a case where the ring was
 * right and the surface was not, and §7k a case where the surface between two right
 * vertices was tilted the wrong way.
 *
 * ⚠️ NOT engine.streets.generation.BlockFloor, and it was renamed away from that name
 * rather than folded into it (WP-O3, §7w). This reads the WHOLE mesh the operator emits -
 * ExtrudePoly.BuildGeom, walls included - and picks the cap out of it by matching vertex
 * positions, so it is an independent reading of what is drawn. The production class asks
 * ExtrudePoly.BuildCap for the cap directly, and every gate that says a building's base is
 * under its own floor is asserted against THIS one, so the two are checked against each
 * other rather than one being the other's mirror.
 */
internal static class DrawnBlockFloor
{
    /**
     * The cap triangles of one block's floor, in cluster relative coordinates, raised by
     * QuarterSidewalkOffset - i.e. the pavement.
     */
    internal static List<(Vector3 a, Vector3 b, Vector3 c)> CapOf(
        IList<Vector3> outline, IList<CapInsetEdge> inset)
    {
        var path = new List<Vector3> { new(0f, MetaGen.QuarterSidewalkOffset, 0f) };
        var mesh = new global::engine.joyce.Mesh("floor");
        new ExtrudePoly(outline, path, 27, 10000f, false, false, true)
        {
            CapInsetEdges = inset
        }.BuildGeom(mesh);

        float up = MetaGen.QuarterSidewalkOffset;
        var wanted = new List<Vector3>();
        foreach (var v in outline) wanted.Add(v + new Vector3(0f, up, 0f));
        if (null != inset)
        {
            foreach (var e in inset)
            {
                wanted.Add(e.Start + new Vector3(0f, up, 0f));
                wanted.Add(e.End + new Vector3(0f, up, 0f));
            }
        }

        bool IsCap(Vector3 v) => wanted.Any(w => (w - v).Length() < 1e-3f);

        var tris = new List<(Vector3, Vector3, Vector3)>();
        for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            Vector3 a = mesh.Vertices[(int)mesh.Indices[i]];
            Vector3 b = mesh.Vertices[(int)mesh.Indices[i + 1]];
            Vector3 c = mesh.Vertices[(int)mesh.Indices[i + 2]];
            if (IsCap(a) && IsCap(b) && IsCap(c)) tris.Add((a, b, c));
        }

        return tris;
    }


    internal static List<(Vector3 a, Vector3 b, Vector3 c)> CapOf(Quarter q)
    {
        var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
        if (outline.Count < 3) return new();

        return CapOf(outline, GenerateClusterQuartersOperator.PavementInsetOf(q, outline));
    }


    /**
     * The floor's height at a plan position, read barycentrically off its own triangles, or
     * null where the cap does not cover the point.
     */
    internal static float? SurfaceAt(
        List<(Vector3 a, Vector3 b, Vector3 c)> tris, in Vector2 p)
    {
        foreach (var (a, b, c) in tris)
        {
            Vector2 pa = new(a.X, a.Z), pb = new(b.X, b.Z), pc = new(c.X, c.Z);

            float d = (pb.Y - pc.Y) * (pa.X - pc.X) + (pc.X - pb.X) * (pa.Y - pc.Y);
            if (Single.Abs(d) < 1e-9f) continue;

            float l1 = ((pb.Y - pc.Y) * (p.X - pc.X) + (pc.X - pb.X) * (p.Y - pc.Y)) / d;
            float l2 = ((pc.Y - pa.Y) * (p.X - pc.X) + (pa.X - pc.X) * (p.Y - pc.Y)) / d;
            float l3 = 1f - l1 - l2;
            if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;

            return l1 * a.Y + l2 * b.Y + l3 * c.Y;
        }

        return null;
    }


    /**
     * Is this plan position inside the ring, by the crossing count.
     */
    internal static bool Contains(IList<Vector2> poly, in Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y)
                && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y)
                / (poly[j].Y - poly[i].Y) + poly[i].X)
            {
                inside = !inside;
            }
        }

        return inside;
    }


    /**
     * The lowest and highest the drawn floor gets over a plan polygon, EXACTLY.
     *
     * Written here, over the triangles read back out of the mesh the operator emits, so
     * that BuildingFooting's answer can be asserted as an identity against something that
     * shares no expression with it. The enumeration is the standard one for a function that
     * is affine on each triangle - the extremes over a region are at the corners of that
     * region's intersection with one of them, so at a corner of the polygon, at a corner of
     * a triangle inside the polygon, or where the two boundaries cross.
     *
     * ⚠️ THE CROSSINGS ARE NOT DECORATION. Measured on seed000/1500, the lowest point of
     * the floor over a footprint is on the footprint's own boundary BETWEEN two of its
     * corners more often than not, and a grid over the interior misses it by up to 0.14 m.
     */
    internal static (float Lo, float Hi) BoundsOver(
        List<(Vector3 a, Vector3 b, Vector3 c)> tris, IList<Vector2> poly)
    {
        float lo = Single.MaxValue, hi = Single.MinValue;

        void Take(float h)
        {
            lo = Single.Min(lo, h);
            hi = Single.Max(hi, h);
        }

        foreach (var p in poly)
        {
            float? h = SurfaceAt(tris, p);
            if (h.HasValue) Take(h.Value);
        }

        foreach (var (a, b, c) in tris)
        {
            foreach (var v in new[] { a, b, c })
            {
                if (Contains(poly, new Vector2(v.X, v.Z))) Take(v.Y);
            }

            foreach (var (u, w) in new[] { (a, b), (b, c), (c, a) })
            {
                Vector2 uu = new(u.X, u.Z), ww = new(w.X, w.Z);
                for (int i = 0; i < poly.Count; ++i)
                {
                    if (_crossing(uu, ww, poly[i], poly[(i + 1) % poly.Count], out float t))
                    {
                        Take(u.Y + t * (w.Y - u.Y));
                    }
                }
            }
        }

        return (lo, hi);
    }


    private static bool _crossing(
        in Vector2 a, in Vector2 b, in Vector2 c, in Vector2 d, out float t)
    {
        t = 0f;

        Vector2 r = b - a, s = d - c;
        float denom = r.X * s.Y - r.Y * s.X;
        if (Single.Abs(denom) < 1e-12f) return false;

        Vector2 ca = c - a;
        float tt = (ca.X * s.Y - ca.Y * s.X) / denom;
        float uu = (ca.X * r.Y - ca.Y * r.X) / denom;

        if (tt < 0f || tt > 1f || uu < 0f || uu > 1f) return false;

        t = tt;
        return true;
    }


    /**
     * A percentile of a sample, for reporting a distribution in a failure message rather
     * than a single worst case that says nothing about how common it is.
     */
    internal static float Percentile(List<float> v, float f)
    {
        var s = new List<float>(v);
        s.Sort();
        return s[Math.Clamp((int)(f * s.Count), 0, s.Count - 1)];
    }
}
