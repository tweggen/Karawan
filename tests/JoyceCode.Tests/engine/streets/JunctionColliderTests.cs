using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * The slab under a junction cap.
 *
 * The Bepu call that consumes this needs a live simulation and is not covered here. What
 * is covered is the arithmetic, the decision of whether a slab is needed at all, and -
 * against real generated cities rather than a hand-built fixture - the claim the whole
 * change rests on: that there IS a gap between the branches of a junction, and that the
 * cap covers it.
 */
public class JunctionColliderTests
{
    private const float Thickness = 0.1f;


    /**
     * Is p inside the box the stroke's DeckCollider spans, in plan? That box runs
     * junction centre to junction centre at the carriageway width.
     */
    private static bool _isInsideStrokeBox(in Stroke stroke, Vector2 p)
    {
        Vector2 a = stroke.A.Pos;
        Vector2 ab = stroke.B.Pos - a;
        float length = ab.Length();
        if (length < 0.001f) return false;

        Vector2 dir = ab / length;
        Vector2 rel = p - a;

        float axial = Vector2.Dot(rel, dir);
        float lateral = Single.Abs(rel.X * dir.Y - rel.Y * dir.X);

        return axial >= 0f && axial <= length && lateral <= stroke.StreetWidth() / 2f;
    }


    /**
     * Is p inside the cap, tested over exactly the fan the mesh is triangulated as?
     */
    private static bool _isInsideCap(IReadOnlyList<Vector2> cap, Vector2 p)
    {
        int l = cap.Count;
        if (l < 3) return false;

        Vector2 centre = Vector2.Zero;
        foreach (var c in cap) centre += c;
        centre /= l;

        for (int k = 0; k < l; ++k)
        {
            Vector2 v0 = cap[k] - centre;
            Vector2 v1 = cap[(k + 1) % l] - centre;
            Vector2 vp = p - centre;

            float d = v0.X * v1.Y - v0.Y * v1.X;
            if (Single.Abs(d) < 1e-6f) continue;

            float s = (vp.X * v1.Y - vp.Y * v1.X) / d;
            float t = (v0.X * vp.Y - v0.Y * vp.X) / d;

            if (s >= 0f && t >= 0f && s + t <= 1f) return true;
        }

        return false;
    }


    /**
     * The cap the collider actually covers, read back from the emitted prism rather than
     * from the section array it was built out of - so a collider that covered less than
     * the cap would be seen here.
     */
    private static List<Vector2> _capOf(in StreetPoint sp)
    {
        var collider = JunctionCollider.For(
            sp.GetSectionArray(), Vector3.Zero, 100f, Thickness);

        var plan = new List<Vector2>();
        for (int i = 0; i < collider.Points.Count; i += 2)
        {
            plan.Add(new Vector2(collider.Points[i].X, collider.Points[i].Z));
        }

        return plan;
    }


    /**
     * A flat city's fragment floor plane already covers every junction on the ground,
     * so nothing may be emitted there - which is what keeps the default game's physics
     * world exactly what it was. Asserted over whole generated cities rather than over a
     * fixture, because "no slab anywhere" is the claim being made.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    public void AFlatCityGetsNoJunctionSlabsAtAll(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);
        int nCapped = 0;

        foreach (var sp in store.GetStreetPoints())
        {
            Assert.Equal(0, sp.Level);
            Assert.False(JunctionCollider.IsNeededFor(sp, groundIsFlat: true),
                $"junction {sp.Id} would add a static to a flat city");

            if (sp.GetSectionArray().Count >= 3) ++nCapped;
        }

        Assert.True(nCapped > 0, "no junction in this city has a cap - the city is wrong");
    }


    /**
     * Once the city follows its terrain there is no floor plane, so every junction that
     * has a cap needs its own slab.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    public void EveryCappedJunctionNeedsASlabOnceTheGroundIsNotFlat(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);
        int nCapped = 0;

        foreach (var sp in store.GetStreetPoints())
        {
            bool hasCap = sp.GetSectionArray().Count >= 3;
            Assert.Equal(hasCap, JunctionCollider.IsNeededFor(sp, groundIsFlat: false));
            if (hasCap) ++nCapped;
        }

        Assert.True(nCapped > 0);
    }


    /**
     * A raised junction escapes the floor plane even in a flat city, exactly as a raised
     * stroke does.
     */
    [Fact]
    public void ARaisedJunctionNeedsASlabEvenInAFlatCity()
    {
        var store = StreetHarness.Generate("seed000", 500f);
        var sp = store.GetStreetPoints().First(p => p.GetSectionArray().Count >= 3);

        Assert.False(JunctionCollider.IsNeededFor(sp, groundIsFlat: true));

        sp.Level = 1;
        Assert.True(JunctionCollider.IsNeededFor(sp, groundIsFlat: true));
    }


    /**
     * A junction of two arms has a section array of two points and a fan built around
     * their own midpoint, so both of its triangles are degenerate - there is no cap and
     * nothing to cover. The two strokes hand their surfaces over to each other there and
     * their boxes overlap across the junction.
     */
    [Fact]
    public void AJunctionWithNoCapGetsNoSlab()
    {
        var store = StreetHarness.Generate("Yelukhdidru", 800f);

        var twoArmed = store.GetStreetPoints()
            .Where(p => 2 == p.GetSectionArray().Count)
            .ToList();

        Assert.True(twoArmed.Count > 0, "expected some two-arm junctions in this city");

        foreach (var sp in twoArmed)
        {
            Assert.False(JunctionCollider.IsNeededFor(sp, groundIsFlat: false));
            Assert.False(JunctionCollider.For(sp.GetSectionArray(), Vector3.Zero, 20f, Thickness)
                .IsUsable);
        }
    }


    /**
     * The TOP face is the road, as with DeckCollider, and the cap lands where the mesh
     * puts it: cluster origin plus the section point, with the junction's one height.
     */
    [Fact]
    public void TheTopFaceIsTheRoadAndTheCapLandsWhereTheMeshPutsIt()
    {
        var section = new List<Vector2>
        {
            new(10f, 0f), new(-5f, 8f), new(-5f, -8f)
        };
        Vector3 origin = new(1000f, 77f, -2000f);

        var c = JunctionCollider.For(section, origin, 42f, Thickness);

        Assert.True(c.IsUsable);
        Assert.Equal(6, c.Points.Count);

        for (int k = 0; k < section.Count; ++k)
        {
            Vector3 top = c.Points[2 * k];
            Vector3 bottom = c.Points[2 * k + 1];

            Assert.Equal(origin.X + section[k].X, top.X, 4);
            Assert.Equal(origin.Z + section[k].Y, top.Z, 4);
            Assert.Equal(42f, top.Y, 4);

            Assert.Equal(top.X, bottom.X, 4);
            Assert.Equal(top.Z, bottom.Z, 4);
            Assert.Equal(42f - Thickness, bottom.Y, 4);
        }

        /*
         * The origin's own Y is the cluster's, not the junction's, and must not reach
         * the slab: the junction is one node with one height and that height is the
         * relaxed street's.
         */
        Assert.All(c.Points, p => Assert.True(p.Y <= 42f && p.Y >= 42f - Thickness));
    }


    /**
     * A cap of no area cannot be stood on and gives the hull builder nothing to work
     * with, so it is refused before it reaches Bepu.
     */
    [Fact]
    public void ACollapsedCapIsRefused()
    {
        var collinear = new List<Vector2> { new(0f, 0f), new(5f, 0f), new(10f, 0f) };
        Assert.False(JunctionCollider.For(collinear, Vector3.Zero, 10f, Thickness).IsUsable);

        var tiny = new List<Vector2> { new(0f, 0f), new(0.1f, 0f), new(0f, 0.1f) };
        Assert.False(JunctionCollider.For(tiny, Vector3.Zero, 10f, Thickness).IsUsable);
    }


    /**
     * The reported defect, measured against real cities rather than assumed.
     *
     * Half the story is the plain gap: each stroke's box spans junction centre to
     * junction centre at the carriageway width, so between two neighbouring branches
     * there is a wedge that no box reaches at all, and over it the hover probe's ray
     * finds nothing built and falls back to the terrain.
     *
     * That half is smaller than it looks and it is worth having the number written down,
     * because it is the number that says a cap collider is not the whole answer. Sampling
     * the cap on a grid over every junction of the baselines, the fraction of the cap
     * covered by NO stroke box is 0.1 % at the median - the boxes of three or more
     * branches really do overlap across most of a junction - but half the junctions have
     * some, and the worst junction of the 3000 m city has 92 m2 of cap standing on
     * nothing. The other half of the story is what the boxes put there where they DO
     * reach, which the next test covers.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    public void TheCapCoversWhatTheStrokeBoxesLeaveStandingOnNothing(
        string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);
        var strokes = store.GetStrokes();

        int nWithGap = 0;
        int nCapped = 0;

        foreach (var sp in store.GetStreetPoints())
        {
            var section = sp.GetSectionArray();
            if (section.Count < 3) continue;
            ++nCapped;

            var cap = _capOf(sp);

            Vector2 centre = Vector2.Zero;
            foreach (var s in section) centre += s;
            centre /= section.Count;

            bool hasGap = false;

            for (int k = 0; k < section.Count && !hasGap; ++k)
            {
                Vector2 v0 = section[k] - centre;
                Vector2 v1 = section[(k + 1) % section.Count] - centre;

                const int N = 12;
                for (int i = 0; i <= N && !hasGap; ++i)
                for (int j = 0; j + i <= N; ++j)
                {
                    Vector2 probe = centre + v0 * ((float)i / N) + v1 * ((float)j / N);
                    if (strokes.Any(s => _isInsideStrokeBox(s, probe))) continue;

                    hasGap = true;
                    Assert.True(_isInsideCap(cap, probe),
                        $"junction {sp.Id}: {probe} is covered by no stroke box and not by "
                        + "the cap either - the gap between the branches is still open");
                    break;
                }
            }

            if (hasGap) ++nWithGap;
        }

        Assert.True(nCapped > 0);
        Assert.True(nWithGap >= nCapped / 4,
            $"only {nWithGap} of {nCapped} capped junctions stand partly on nothing - if the "
            + "stroke boxes really do cover the junctions there is nothing here to fix, and "
            + "this test has stopped testing anything");
    }


    /**
     * The other half, and the one that matches the report - a ship stuck on the surface
     * BETWEEN the branches rather than falling through it.
     *
     * A stroke's DeckCollider is tilted across its whole length, junction centre to
     * junction centre. The road MESH is not: _shearOntoSlope holds each end flat over its
     * junction's footprint and spreads the rise over the carriageway between, precisely
     * so that the flat cap and the road meet. So inside a junction the collider climbs
     * while the picture is level, and where two branches of different slope overlap they
     * put a ridge across the middle of a junction that nothing renders.
     *
     * The size of it is the axial reach of the cap times the grade: measured over the
     * baselines the cap reaches 7.6 - 10.6 m into its own strokes' boxes at the median
     * and 28.4 m at the worst, and GradePolicy allows 5 % to an arterial and 14 % to an
     * alley, so 0.4 m to 4 m of invisible step.
     *
     * The cap slab is flat, at the junction's one height - the height the mesh is drawn
     * at - so wherever a box has climbed above the road it is the cap the ship stands on.
     */
    [Fact]
    public void TheCapIsFlatWhereTheStrokeBoxesThroughItAreNot()
    {
        /*
         * One junction, two branches of opposite slope, a 6 % grade either way.
         */
        Vector3 junction = new(0f, 20f, 0f);
        const float grade = 0.06f;
        const float reach = 10f;

        var rising = DeckCollider.For(
            junction, new Vector3(100f, 20f + 100f * grade, 0f), 16f, Thickness);
        var falling = DeckCollider.For(
            junction, new Vector3(0f, 20f - 100f * grade, 100f), 16f, Thickness);

        float risingTop = _topOf(rising, new Vector2(reach, 0f));
        float fallingTop = _topOf(falling, new Vector2(0f, reach));

        Assert.True(risingTop - 20f > 0.5f,
            $"the rising box should stand {reach * grade:F2} m proud inside the junction, "
            + $"got {risingTop - 20f:F2}");
        Assert.True(20f - fallingTop > 0.5f,
            $"the falling box should sink inside the junction, got {fallingTop - 20f:F2}");

        var section = new List<Vector2> { new(8f, 8f), new(-11f, 0f), new(0f, -11f) };
        var cap = JunctionCollider.For(section, Vector3.Zero, junction.Y, Thickness);

        Assert.True(cap.IsUsable);
        Assert.All(cap.Points, p => Assert.True(
            p.Y == junction.Y || p.Y == junction.Y - Thickness,
            $"the cap must be flat at the junction's one height, got {p.Y}"));
    }


    /**
     * The slab stands exactly where the cap is drawn, over ground that is not flat.
     *
     * This is the assertion the first round of this work was missing, and the mutation
     * that got through without it was the one that matters: reading the city AVERAGE
     * rather than the junction's own relaxed height. That compiles, leaves a flat city
     * bit for bit identical - so every other test here still passes - and floats a
     * pancake at the mean height over every junction of a terrain-following city, which
     * is worse than the gap it was meant to close. ClusterGroundHeightTests cannot catch
     * it either: the operator is already on that allow list for its flat floor plane, and
     * the list is per file.
     *
     * So the collider is compared against the MESH, which is built by a separate
     * expression in _generateJunction. Either one drifting fails this.
     */
    [Fact]
    public void TheSlabStandsWhereTheCapIsDrawnOverGroundThatIsNotFlat()
    {
        const string idString = "seed000";
        const float size = 500f;

        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 137f;
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => 20f + 0.05f * x - 0.03f * z);

        var store = StreetHarness.Generate(idString, size);
        int nChecked = 0;
        var heights = new HashSet<float>();
        var levels = new HashSet<sbyte>();

        foreach (var sp in store.GetStreetPoints())
        {
            if (!JunctionCollider.IsNeededFor(sp, groundIsFlat: false)) continue;

            /*
             * Every other one onto a raised deck. A generated city is all on the ground,
             * so without this a collider that simply forgot the deck elevation would come
             * out right everywhere and never be noticed.
             */
            sp.Level = (sbyte)(nChecked % 2);
            levels.Add(sp.Level);

            var mesh = StreetGeometryHarness.GenerateJunctionsFor(cd, store, new[] { sp });
            Assert.True(mesh.Vertices.Count > 0);

            float meshHeight = mesh.Vertices[0].Y;
            Assert.All(mesh.Vertices, v => Assert.Equal(meshHeight, v.Y, 4));

            var cap = JunctionCollider.For(
                sp.GetSectionArray(),
                Vector3.Zero,
                RoadSurface.HeightAtJunction(cd.StreetHeightSource, sp),
                Thickness);

            Assert.True(cap.IsUsable);
            for (int i = 0; i < cap.Points.Count; i += 2)
            {
                Assert.Equal(meshHeight, cap.Points[i].Y, 4);
            }

            heights.Add(meshHeight);
            ++nChecked;
        }

        Assert.True(nChecked > 0);
        Assert.True(heights.Count > 1,
            "every junction of this city came out at the same height, so a slab at the "
            + "city average would pass this too - pick a source that actually slopes");
        Assert.Equal(2, levels.Count);
    }


    /**
     * Height of a deck collider's top face over a point in plan.
     */
    private static float _topOf(in DeckCollider c, Vector2 plan)
    {
        Vector3 up = Vector3.Transform(Vector3.UnitY, c.Orientation);
        Vector3 surface = c.Position + up * (Thickness / 2f);

        /*
         * The top face is a plane through surface with normal up.
         */
        return surface.Y - (up.X * (plan.X - surface.X) + up.Z * (plan.Y - surface.Z)) / up.Y;
    }
}
