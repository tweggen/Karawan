using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.joyce;
using Xunit;

namespace JoyceCode.Tests.builtin.tools;


/**
 * The caps ExtrudePoly puts on an extrusion, and which way they look.
 *
 * A cap is perpendicular to the extrusion by construction, so the extrusion direction IS
 * its plane and the tessellator is told so. It used to be told only when the caller also
 * wanted per vertex normals - the two were one argument - and everywhere else LibTess
 * guessed, which is how the city's pavements went missing (QuarterFloorFacingTests).
 *
 * Exercised here with a NON-vertical extrusion on purpose. The city block is extruded
 * straight up, so hard coding UnitY as the cap's plane would pass every test about
 * pavements and quietly leave the rooftop powerlines - the other caller without paired
 * normals - guessing exactly as before.
 *
 * ExtrudePoly.BuildGeom became reachable from a test at all on 2026-08-30: the
 * constructor used to resolve engine.physics.API out of the container, so the geometry
 * half of the class could not be built without an engine behind it. The lookup now sits
 * in BuildStaticPhys, which is the only half that needs it.
 */
public class ExtrudePolyCapTests
{
    /**
     * The powerline of nogame.cities.HouseInstanceGenerator, reduced to its geometry: a
     * 1 m square section swept along an arbitrarily oriented axis, capped at both ends.
     */
    private static Mesh _sweep(Quaternion rotation, float length, bool addFloor)
    {
        Vector3 vd = Vector3.Transform(Vector3.UnitX, rotation);
        Vector3 vr = Vector3.Transform(Vector3.UnitY, rotation);
        Vector3 vt = Vector3.Cross(vd, vr);

        vd *= length;
        vr *= 0.5f;
        vt *= 0.5f;

        var poly = new List<Vector3> { vr - vt, vr + vt, -vr + vt, -vr - vt };
        var path = new List<Vector3> { vd };

        var mesh = new Mesh("sweep");
        new global::builtin.tools.ExtrudePoly(poly, path, 27, 100f, false, addFloor, true)
            .BuildGeom(mesh);

        return mesh;
    }


    public static IEnumerable<object[]> Orientations()
    {
        var rnd = new Random(20260830);

        for (int i = 0; i < 24; ++i)
        {
            yield return new object[]
            {
                (float)rnd.NextDouble() * 6.2831853f,
                (float)(rnd.NextDouble() - 0.5) * 2.4f,
                (float)(rnd.NextDouble() - 0.5) * 2.4f
            };
        }
    }


    /**
     * The far cap looks along the extrusion and the near cap looks back down it, whatever
     * direction the extrusion runs in.
     *
     * A cap triangle is one whose three vertices are all on the same ring; there are only
     * two rings on a single-row extrusion, so that separates the caps from the sides
     * without counting on how many triangles come first.
     */
    [Theory]
    [MemberData(nameof(Orientations))]
    public void BothCapsLookOutOfTheExtrusion(float yaw, float pitch, float roll)
    {
        var rotation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
        Vector3 vu = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, rotation));

        var mesh = _sweep(rotation, 6f, true);

        int nFar = 0, nNear = 0;

        for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            Vector3 a = mesh.Vertices[(int)mesh.Indices[i]];
            Vector3 b = mesh.Vertices[(int)mesh.Indices[i + 1]];
            Vector3 c = mesh.Vertices[(int)mesh.Indices[i + 2]];

            /*
             * How far along the extrusion each corner is. A cap has all three at the same
             * end; a side spans both.
             */
            float da = Vector3.Dot(a, vu), db = Vector3.Dot(b, vu), dc = Vector3.Dot(c, vu);
            float lo = Single.Min(da, Single.Min(db, dc));
            float hi = Single.Max(da, Single.Max(db, dc));
            if (hi - lo > 0.1f) continue;

            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.LengthSquared() < 1e-8f) continue;

            if (hi > 3f)
            {
                Assert.True(Vector3.Dot(n, vu) > 0f,
                    "the far cap faces back into the extrusion");
                ++nFar;
            }
            else
            {
                Assert.True(Vector3.Dot(n, vu) < 0f,
                    "the near cap faces into the extrusion");
                ++nNear;
            }
        }

        Assert.Equal(2, nFar);
        Assert.Equal(2, nNear);
    }


    /**
     * The cap is where the extrusion put it: the far ring is the polygon plus the whole
     * path vector, exactly.
     *
     * Here because naming the plane is a claim about winding only, and a change that
     * moved a cap while pointing it the right way would satisfy everything above.
     */
    [Fact]
    public void TheFarCapSitsAtTheEndOfThePath()
    {
        var rotation = Quaternion.CreateFromYawPitchRoll(0.7f, 0.3f, -0.2f);
        Vector3 vd = Vector3.Transform(Vector3.UnitX, rotation) * 6f;
        Vector3 vr = Vector3.Transform(Vector3.UnitY, rotation) * 0.5f;
        Vector3 vt = Vector3.Cross(Vector3.Transform(Vector3.UnitX, rotation),
            Vector3.Transform(Vector3.UnitY, rotation)) * 0.5f;

        var mesh = _sweep(rotation, 6f, false);

        foreach (var corner in new[] { vr - vt, vr + vt, -vr + vt, -vr - vt })
        {
            Assert.Contains(mesh.Vertices, v => (v - (corner + vd)).Length() < 1e-3f);
        }
    }
}
