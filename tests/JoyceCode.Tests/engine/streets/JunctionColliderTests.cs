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
     * The TOP face is the road, as with CarriagewayCollider, and the cap lands where the mesh
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
}
