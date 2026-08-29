using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * What the streets want the ground to do, and how far out they want it.
 *
 * StreetHeightField is the pure half of the corridor-conforming pass (§2c): the kernel
 * and the positional "how high is the street network near here" query, with no terrain,
 * no elevation cache and no engine, so both can be tested directly. The operator that
 * uses it needs a booted engine and is covered only where it can be - see
 * ClusterConformElevationOperatorTests.
 */
public class StreetHeightFieldTests
{
    private const float Radius = 60f;

    private static int _nextId = 1;


    private static StreetPoint _junction(float x, float z)
    {
        var sp = new StreetPoint();
        sp.SetPos(x, z);
        sp.Id = _nextId++;
        return sp;
    }


    private static Stroke _stroke(int sid, StreetPoint a, StreetPoint b)
    {
        var stroke = new Stroke();
        stroke.A = a;
        stroke.B = b;
        stroke.Sid = sid;
        stroke.Weight = 1f;
        return stroke;
    }


    /**
     * One straight stroke running along +x from the origin, climbing from 0 to 10.
     */
    private static (StreetHeightField Field, StreetPoint A, StreetPoint B) _oneRoad()
    {
        var a = _junction(0f, 0f);
        var b = _junction(100f, 0f);
        var heights = new Dictionary<int, float> { [a.Id] = 0f, [b.Id] = 10f };

        var field = StreetHeightField.Build(
            new[] { _stroke(1, a, b) }, sp => heights[sp.Id], Radius);

        return (field, a, b);
    }


    // ---------------------------------------------------------------- the kernel


    [Fact]
    public void TheKernelIsOneOnTheRoadAndZeroAtTheRadius()
    {
        Assert.Equal(1f, StreetHeightField.Falloff(0f, Radius));
        Assert.Equal(0f, StreetHeightField.Falloff(Radius, Radius));
        Assert.Equal(0f, StreetHeightField.Falloff(Radius * 10f, Radius));
        Assert.Equal(0.5f, StreetHeightField.Falloff(Radius / 2f, Radius), 5);
    }


    /**
     * The kernel leaves off FLAT at both ends, which is the whole reason it is a
     * smoothstep rather than a linear ramp.
     *
     * At the far end that means the graded ground meets untouched terrain tangentially
     * instead of creasing along a circle around every street; at the near end it means a
     * road sits in a flat pad rather than on the apex of a tent. On a 20 m grid a crease
     * lands on a single row of samples and reads as exactly the terracing this pass
     * exists to avoid.
     *
     * Stated as: one percent of the way in from either end, the kernel has moved far LESS
     * than one percent. A linear ramp moves exactly one percent and fails this.
     */
    [Fact]
    public void TheKernelLeavesOffFlatAtBothEnds()
    {
        float nearRoad = StreetHeightField.Falloff(0.01f * Radius, Radius);
        float nearEdge = StreetHeightField.Falloff(0.99f * Radius, Radius);

        Assert.True(nearRoad > 0.999f, $"kernel drops too fast off the road: {nearRoad}");
        Assert.True(nearEdge < 0.001f, $"kernel is still climbing at the edge: {nearEdge}");
    }


    [Fact]
    public void TheKernelFallsMonotonically()
    {
        float previous = Single.PositiveInfinity;
        for (int i = 0; i <= 100; ++i)
        {
            float w = StreetHeightField.Falloff(i * Radius / 100f, Radius);
            Assert.InRange(w, 0f, 1f);
            Assert.True(w <= previous, $"kernel rose again at {i}% of the radius");
            previous = w;
        }
    }


    /**
     * The radius is a property of the elevation GRID, and that is the finding this whole
     * pass turns on: MetaGen.GroundResolution = 20 over MetaGen.FragmentSize = 400 is one
     * sample every 20 m, while a street is 8-22 m wide. A corridor is about one cell
     * across and cannot be cut without terracing, so the ground is graded over a few
     * cells instead.
     *
     * Pinned here so that a future change to the grid has to come past this test and read
     * the reason rather than inheriting a number.
     */
    [Fact]
    public void TheRadiusIsMeasuredInElevationCells()
    {
        float cell = global::engine.world.MetaGen.FragmentSize
                     / global::engine.world.MetaGen.GroundResolution;

        Assert.Equal(20f, cell, 4);
        Assert.Equal(3f, StreetHeightField.RadiusInCells);
        Assert.Equal(60f, StreetHeightField.DefaultRadius, 4);

        /*
         * At least two cells, or there are no samples inside the falloff to carry a ramp
         * and what comes out is the step a corridor would have given.
         */
        Assert.True(StreetHeightField.DefaultRadius >= 2f * cell);
    }


    // ----------------------------------------------------------------- the blend


    /**
     * No influence means the terrain is returned exactly. Everything outside a city
     * depends on it, and it comes out of the arithmetic rather than out of a guard:
     * multiplying by zero is exact and adding zero changes nothing.
     */
    [Fact]
    public void NoInfluenceLeavesTheTerrainExactlyAsItWas()
    {
        const float terrain = 123.456789f;

        Assert.Equal(terrain, StreetHeightField.Blend(terrain, -9999f, 0f));
    }


    /**
     * Full influence means the STREET height exactly, and this one does need a guard.
     * a + 1 * (b - a) is not b once a and b are far apart, and a sample sitting on a road
     * has to come out at the road's own height - a road standing on ground that is nearly
     * but not quite its own is the thing this pass exists to remove.
     */
    [Fact]
    public void FullInfluenceGivesTheStreetHeightExactly()
    {
        const float street = 87.65432f;
        const float terrain = -9999f;

        Assert.Equal(street, StreetHeightField.Blend(terrain, street, 1f));
        Assert.Equal(street, StreetHeightField.Blend(terrain, street, 1.5f));

        /*
         * And the naive expression really does miss, or the guard above would be
         * decoration and this test would pass without it.
         */
        Assert.NotEqual(street, terrain + 1f * (street - terrain));
    }


    [Fact]
    public void PartialInfluenceMovesPartOfTheWay()
    {
        Assert.Equal(15f, StreetHeightField.Blend(10f, 20f, 0.5f), 4);
        Assert.Equal(12.5f, StreetHeightField.Blend(10f, 20f, 0.25f), 4);
    }


    // ----------------------------------------------------------------- the query


    /**
     * On a road, the ground is that road's height there - which for a climbing stroke
     * means interpolated along it, not either end's value.
     */
    [Fact]
    public void APointOnAStrokeTakesThatStrokesHeightThere()
    {
        var (field, _, _) = _oneRoad();

        Assert.True(field.TryHeightAt(new Vector2(0f, 0f), out float atA, out float wA));
        Assert.Equal(0f, atA, 4);
        Assert.Equal(1f, wA, 4);

        Assert.True(field.TryHeightAt(new Vector2(50f, 0f), out float mid, out float wMid));
        Assert.Equal(5f, mid, 4);
        Assert.Equal(1f, wMid, 4);

        Assert.True(field.TryHeightAt(new Vector2(100f, 0f), out float atB, out float wB));
        Assert.Equal(10f, atB, 4);
        Assert.Equal(1f, wB, 4);
    }


    /**
     * Beside a road the height is still the road's height at the NEAREST point on it, and
     * only the influence falls off.
     *
     * Two separate claims, and separating them is the point: a version that took the
     * distance into the height as well would leave the ground sloping away from every
     * road for reasons that have nothing to do with the road's own gradient.
     */
    [Fact]
    public void BesideAStrokeTheHeightIsTakenAtTheNearestPointOnIt()
    {
        var (field, _, _) = _oneRoad();

        foreach (float offset in new[] { 5f, 20f, 45f })
        {
            Assert.True(field.TryHeightAt(new Vector2(50f, offset), out float h, out float w));
            Assert.Equal(5f, h, 4);
            Assert.Equal(StreetHeightField.Falloff(offset, Radius), w, 5);
        }
    }


    /**
     * Past a stroke's end the nearest point is that end, so the height stops climbing
     * rather than being extrapolated off into the sky.
     */
    [Fact]
    public void BeyondAStrokesEndTheHeightHoldsAtThatEnd()
    {
        var (field, _, _) = _oneRoad();

        Assert.True(field.TryHeightAt(new Vector2(-30f, 0f), out float before, out _));
        Assert.Equal(0f, before, 4);

        Assert.True(field.TryHeightAt(new Vector2(130f, 0f), out float after, out _));
        Assert.Equal(10f, after, 4);
    }


    /**
     * Out of range, the streets have nothing to say and the terrain is left alone. This
     * is what keeps the pass local to the city site instead of levelling the countryside.
     */
    [Fact]
    public void FarFromEveryStrokeTheStreetsSayNothing()
    {
        var (field, _, _) = _oneRoad();

        Assert.False(field.TryHeightAt(new Vector2(50f, Radius), out _, out float w));
        Assert.Equal(0f, w);
        Assert.False(field.TryHeightAt(new Vector2(50f, 400f), out _, out _));
        Assert.False(field.TryHeightAt(new Vector2(-500f, 0f), out _, out _));
    }


    /**
     * Where two streets at different heights are both in range, the ground takes a
     * weighted mean of them - and crossing the line where the nearer of the two changes
     * must not step.
     *
     * A nearest-stroke-wins query gives the same answer everywhere else and fails exactly
     * here, with a seam running down the middle of every block. Asserted as continuity
     * across the midline rather than by naming the expected mean, so this stays a
     * statement about the surface and not a restatement of the arithmetic.
     */
    [Fact]
    public void BetweenTwoStreetsTheGroundIsContinuous()
    {
        var a = _junction(0f, 0f);
        var b = _junction(200f, 0f);
        var c = _junction(0f, 40f);
        var d = _junction(200f, 40f);

        var heights = new Dictionary<int, float>
        {
            [a.Id] = 0f, [b.Id] = 0f,
            [c.Id] = 8f, [d.Id] = 8f
        };

        var field = StreetHeightField.Build(
            new[] { _stroke(1, a, b), _stroke(2, c, d) }, sp => heights[sp.Id], Radius);

        Assert.True(field.TryHeightAt(new Vector2(100f, 19.9f), out float below, out float wBelow));
        Assert.True(field.TryHeightAt(new Vector2(100f, 20.1f), out float above, out _));

        /*
         * The influence is the LARGEST single kernel weight and not the sum of them. A
         * sum exceeds one wherever streets are dense, and clamping it back would put a
         * hard edge along wherever the clamp starts biting - so it is stated here, where
         * two streets are in range at once and the two readings differ.
         */
        Assert.Equal(StreetHeightField.Falloff(19.9f, Radius), wBelow, 5);
        Assert.InRange(wBelow, 0f, 1f);

        Assert.InRange(below, 0f, 8f);
        Assert.InRange(above, 0f, 8f);
        Assert.True(Math.Abs(above - below) < 0.05f,
            $"the ground steps across the midline between two streets: {below} to {above}");

        /*
         * And it really is between them rather than pinned to either, which is what
         * distinguishes a mean from "the lower one wins".
         */
        Assert.True(below > 0.5f && below < 7.5f, $"midline height {below} is not a blend");
    }


    /**
     * The answer may not depend on the order the strokes arrive in.
     *
     * Cities are regenerated from a seed rather than stored and elevation fragments are
     * recomputed on demand, so a fragment computed after a different set of neighbours
     * must come back bit for bit identical. Asserted with exact equality, since the thing
     * at risk is floating point addition order and an approximate assertion cannot see
     * it.
     */
    [Fact]
    public void TheFieldDoesNotDependOnTheOrderStrokesArriveIn()
    {
        var junctions = Enumerable.Range(0, 8)
            .Select(i => _junction(40f * i, 25f * (i % 3)))
            .ToList();

        var heights = junctions
            .Select((sp, i) => (sp, i))
            .ToDictionary(e => e.sp.Id, e => 3.7f * e.i);

        var strokes = Enumerable.Range(0, junctions.Count - 1)
            .Select(i => _stroke(100 - i, junctions[i], junctions[i + 1]))
            .ToList();

        var forward = StreetHeightField.Build(strokes, sp => heights[sp.Id], Radius);
        var backward = StreetHeightField.Build(
            Enumerable.Reverse(strokes).ToList(), sp => heights[sp.Id], Radius);

        for (int i = 0; i <= 40; ++i)
        {
            Vector2 p = new(-20f + 8f * i, 12f + 1.3f * i);

            bool okForward = forward.TryHeightAt(p, out float hForward, out float wForward);
            bool okBackward = backward.TryHeightAt(p, out float hBackward, out float wBackward);

            Assert.Equal(okForward, okBackward);
            Assert.Equal(hForward, hBackward);
            Assert.Equal(wForward, wBackward);
        }
    }


    /**
     * A junction is one node, so it has one height - and the field asks for it once even
     * though every stroke meeting there wants it.
     *
     * Not merely a saving: IStreetHeightSource is allowed to be expensive (TerrainStreetHeight
     * samples the elevation cache) and, more to the point, asking twice is a second chance
     * to get a different answer at a point two streets share.
     */
    [Fact]
    public void AJunctionIsAskedForItsHeightOnce()
    {
        var a = _junction(0f, 0f);
        var shared = _junction(100f, 0f);
        var c = _junction(100f, 100f);

        var asked = new List<int>();

        StreetHeightField.Build(
            new[] { _stroke(1, a, shared), _stroke(2, shared, c) },
            sp =>
            {
                asked.Add(sp.Id);
                return 0f;
            },
            Radius);

        Assert.Equal(3, asked.Count);
        Assert.Equal(3, asked.Distinct().Count());
    }


    /**
     * A stroke that has lost an endpoint carries no height and is dropped rather than
     * throwing from inside an elevation operator, where the exception would be swallowed
     * by the cache and reported as a blank fragment.
     */
    [Fact]
    public void AStrokeMissingAnEndpointIsNotInTheField()
    {
        var a = _junction(0f, 0f);
        var b = _junction(100f, 0f);

        var broken = new Stroke { A = a, Sid = 2 };

        var field = StreetHeightField.Build(
            new[] { _stroke(1, a, b), broken }, _ => 0f, Radius);

        Assert.Equal(1, field.SpanCount);
    }


    [Fact]
    public void AnEmptyNetworkGradesNothing()
    {
        var field = StreetHeightField.Build(Array.Empty<Stroke>(), _ => 0f, Radius);

        Assert.Equal(0, field.SpanCount);
        Assert.False(field.TryHeightAt(Vector2.Zero, out _, out _));
    }
}
