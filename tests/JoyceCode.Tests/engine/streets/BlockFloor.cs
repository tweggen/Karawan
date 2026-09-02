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
 */
internal static class BlockFloor
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
