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
 *
 * **And the measurement was too narrow, which cost a round.** The first version of this
 * file concluded that naming the sweep normal "does not change the result" on the
 * strength of the heights coming back equal. The heights are equal. The WINDING is not,
 * and a back-facing triangle is culled by GL and simply is not drawn - which is how half
 * a hillside city's pavements went missing while every test here passed. Whatever else is
 * checked about a triangulation, check which way it points.
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
        global::builtin.tools.Triangulate.ToMesh(_ell, Vector3.UnitY, Vector3.UnitY, Vector2.One / 64f, mesh);

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
     * The sweep normal decides which way the faces point, and nothing else.
     *
     * Both halves matter. Every triangle faces the way the caller asked - that is the
     * whole reason the plane is an argument - and every vertex still lands at its own
     * height whichever way it was asked, which is the property the block design was
     * settled on.
     */
    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void TheFacesFollowTheSweepNormal(float sign)
    {
        Vector3 plane = sign * Vector3.UnitY;

        var mesh = new Mesh($"nonplanar-{sign}");
        global::builtin.tools.Triangulate.ToMesh(
            _ell, plane, Vector3.Zero, Vector2.One / 64f, mesh);

        Assert.Equal((_ell.Length - 2) * 3, mesh.Indices.Count);

        for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            Vector3 a = mesh.Vertices[(int)mesh.Indices[i]];
            Vector3 b = mesh.Vertices[(int)mesh.Indices[i + 1]];
            Vector3 c = mesh.Vertices[(int)mesh.Indices[i + 2]];

            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), plane) > 0f,
                $"triangle {i / 3} faces away from the plane it was tessellated in");
        }

        foreach (var input in _ell)
        {
            Assert.Contains(mesh.Vertices, v => (v - input).Length() < 1e-3f);
        }
    }


    /**
     * `clockwise` turns the whole thing round, which is how ExtrudePoly gets a floor that
     * looks down out of the same call that gives it a ceiling that looks up.
     */
    [Fact]
    public void TheClockwiseFlagReversesTheFacing()
    {
        var mesh = new Mesh("nonplanar-cw");
        global::builtin.tools.Triangulate.ToMesh(
            _ell, Vector3.UnitY, Vector3.Zero, Vector2.One / 64f, mesh, true);

        for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            Vector3 a = mesh.Vertices[(int)mesh.Indices[i]];
            Vector3 b = mesh.Vertices[(int)mesh.Indices[i + 1]];
            Vector3 c = mesh.Vertices[(int)mesh.Indices[i + 2]];

            Assert.True(Vector3.Cross(b - a, c - a).Y < 0f,
                $"triangle {i / 3} still faces up despite being asked for clockwise");
        }
    }


    /**
     * Refusing to guess is the fix, so refusing has to be tested.
     *
     * A zero plane is what the block floor passed for years, and it is why its pavements
     * went missing. Accepting it "for compatibility" would leave the trap set for the
     * next caller.
     */
    [Fact]
    public void AZeroSweepNormalIsRefused()
    {
        var mesh = Mesh.CreateNormalsListInstance("nonplanar-zero");

        Assert.Throws<ArgumentException>(() =>
            global::builtin.tools.Triangulate.ToMesh(
                _ell, Vector3.Zero, Vector3.UnitY, Vector2.One / 64f, mesh));
    }


    /**
     * The sweep plane and the per vertex normal are separate arguments, and only the
     * second decides whether normals are written.
     *
     * They were one parameter, and that is the whole of the bug: the only caller that
     * wanted no vertex normals was thereby also the only caller that let the tessellator
     * guess its plane.
     */
    [Fact]
    public void TheVertexNormalIsIndependentOfTheSweepPlane()
    {
        var withNormals = Mesh.CreateNormalsListInstance("with");
        global::builtin.tools.Triangulate.ToMesh(
            _ell, Vector3.UnitY, Vector3.UnitY, Vector2.One / 64f, withNormals);

        var without = new Mesh("without");
        global::builtin.tools.Triangulate.ToMesh(
            _ell, Vector3.UnitY, Vector3.Zero, Vector2.One / 64f, without);

        Assert.Equal(_ell.Length, withNormals.Normals.Count);

        Assert.Equal(withNormals.Vertices.Count, without.Vertices.Count);
        for (int i = 0; i < withNormals.Vertices.Count; ++i)
        {
            Assert.Equal(withNormals.Vertices[i], without.Vertices[i]);
        }

        for (int i = 0; i < withNormals.Indices.Count; ++i)
        {
            Assert.Equal(withNormals.Indices[i], without.Indices[i]);
        }
    }
}
