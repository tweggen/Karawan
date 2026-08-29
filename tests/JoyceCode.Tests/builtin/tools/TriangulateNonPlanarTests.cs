using System;
using System.Linq;
using System.Numerics;
using engine.joyce;
using Xunit;

namespace JoyceCode.Tests.builtin.tools;


/**
 * What the shared triangulator does with a polygon that is not flat.
 *
 * Worth pinning, because the answer decided a design. City blocks stand on a tilted
 * PLANE rather than following the terrain across their interior, and one of the reasons
 * a plane was safe to choose is that even if a later change makes a block's corners
 * genuinely non-coplanar, the geometry behind it does not fall apart: LibTess projects
 * the contour to a plane to sweep it, but carries every vertex's own height through
 * untouched.
 *
 * The measurement, not the assumption - it was run before the design was settled.
 */
public class TriangulateNonPlanarTests
{
    private static readonly Vector3[] _ell =
    {
        new(0f, 10f, 0f),
        new(100f, 14f, 0f),
        new(100f, 16f, 40f),
        new(40f, 13f, 40f),
        new(40f, 12f, 90f),
        new(0f, 9f, 90f),
    };


    /**
     * Every input vertex comes back at its own height, and no vertices are invented.
     *
     * A concave outline on purpose: a convex one can be fanned without the sweep ever
     * having to decide anything, so it would not exercise the case at all.
     */
    [Fact]
    public void ANonPlanarOutlineKeepsEveryVertexHeight()
    {
        var mesh = Mesh.CreateNormalsListInstance("nonplanar-ell");
        global::builtin.tools.Triangulate.ToMesh(_ell, Vector3.Zero, Vector2.One / 64f, mesh);

        Assert.Equal(_ell.Length, mesh.Vertices.Count);
        Assert.Equal((_ell.Length - 2) * 3, mesh.Indices.Count);

        foreach (var input in _ell)
        {
            Assert.Contains(mesh.Vertices, v =>
                Single.Abs(v.X - input.X) < 1e-3f
                && Single.Abs(v.Y - input.Y) < 1e-3f
                && Single.Abs(v.Z - input.Z) < 1e-3f);
        }
    }


    /**
     * Naming the sweep normal explicitly - which is what PairedNormals does - does not
     * change the result. Left in because "it worked with the normal we happened to pass"
     * would be a thin thing to have built a decision on.
     */
    [Fact]
    public void TheSweepNormalDoesNotChangeTheHeights()
    {
        var implicitNormal = Mesh.CreateNormalsListInstance("nonplanar-implicit");
        var explicitNormal = Mesh.CreateNormalsListInstance("nonplanar-explicit");

        global::builtin.tools.Triangulate.ToMesh(
            _ell, Vector3.Zero, Vector2.One / 64f, implicitNormal);
        global::builtin.tools.Triangulate.ToMesh(
            _ell, Vector3.UnitY, Vector2.One / 64f, explicitNormal);

        Assert.Equal(implicitNormal.Vertices.Count, explicitNormal.Vertices.Count);
        for (int i = 0; i < implicitNormal.Vertices.Count; ++i)
        {
            Assert.Equal(implicitNormal.Vertices[i].Y, explicitNormal.Vertices[i].Y, 4);
        }
    }
}
