using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.elevation;
using engine.streets;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.elevation;


/**
 * The pass that makes the ground agree with the roads (§2c).
 *
 * Three things are testable without a world and are tested here: that the paths on which
 * the pass must do nothing - a flat city, and a fragment nowhere near this one - hand the
 * terrain through bit for bit, that the write itself touches only the height, and that the
 * layer strings sort the way the cycle break needs them to.
 *
 * **What is NOT covered.** ElevationOperatorProcess on a terrain-following city calls
 * ClusterDesc.StrokeStore(), which generates a whole city and needs the I container, the
 * engine and the elevation cache; and the layer resolution it depends on lives in the
 * process-global elevation.Cache singleton, which a test may not register operators into
 * without leaking them into every other test in this assembly. So the grading arithmetic
 * is exercised through the extracted Grade, the field through StreetHeightFieldTests, and
 * the two seams that can only be stated as rules about the source are scanned for.
 */
public class ClusterConformElevationOperatorTests
{
    private const int N = 21;


    private sealed class FixedElevation : IElevationProvider
    {
        public ElevationSegment Segment;
        public int Calls;

        public ElevationSegment GetElevationSegmentBelow(in global::engine.geom.Rect2 rect2)
        {
            ++Calls;
            return Segment;
        }
    }


    private static global::engine.geom.Rect2 _fragmentRect()
    {
        global::engine.world.MetaGen.GetFragmentRect(0, 0, out var rect2);
        return rect2;
    }


    /**
     * A segment of terrain with a recognisable height at every sample, plus a biome and a
     * flag byte, so that anything the pass writes that it should not is visible.
     */
    private static ElevationSegment _terrain(float height)
    {
        var es = new ElevationSegment(N, N) { Rect2 = _fragmentRect() };

        for (int ez = 0; ez < N; ++ez)
        {
            for (int ex = 0; ex < N; ++ex)
            {
                es.Elevations[ez, ex] = new ElevationPixel
                {
                    Height = height + ex * 0.125f - ez * 0.0625f,
                    Biome = 1,
                    Flags1 = 7
                };
            }
        }

        return es;
    }


    /**
     * The same, at one height everywhere, for the tests that measure how far a sample has
     * been moved and would otherwise have to subtract the pattern back out.
     */
    private static ElevationSegment _flatTerrain(float height)
    {
        var es = new ElevationSegment(N, N) { Rect2 = _fragmentRect() };

        for (int ez = 0; ez < N; ++ez)
        {
            for (int ex = 0; ex < N; ++ex)
            {
                es.Elevations[ez, ex] = new ElevationPixel { Height = height, Biome = 1 };
            }
        }

        return es;
    }


    private static ElevationSegment _empty()
        => new ElevationSegment(N, N) { Rect2 = _fragmentRect() };


    private static StreetPoint _junction(int id, float x, float z)
    {
        var sp = new StreetPoint();
        sp.SetPos(x, z);
        sp.Id = id;
        return sp;
    }


    private static StreetHeightField _oneRoad(
        Vector2 from, Vector2 to, float height, float radius)
    {
        var a = _junction(1, from.X, from.Y);
        var b = _junction(2, to.X, to.Y);

        var stroke = new Stroke { A = a, B = b, Sid = 1, Weight = 1f };

        return StreetHeightField.Build(new[] { stroke }, _ => height, radius);
    }


    // ------------------------------------------------------- the flat default path


    /**
     * With the city flattened, the operator hands the terrain through untouched - every
     * field of every pixel, not merely the height.
     *
     * This is the proof rather than the claim that the default path is unaffected. It has
     * teeth for a second reason: the flat check sits BEFORE StrokeStore(), so an operator
     * that lost it would try to generate a whole city here, from a test with no I
     * container and no engine, and fail loudly.
     *
     * Belt and braces - GenerateClustersOperator does not register this operator at all
     * unless the flag is on, so in the shipped default world the layer does not exist.
     */
    [Fact]
    public void AFlatCityIsHandedThroughUnchanged()
    {
        var cd = StreetHarness.MakeCluster("conform-flat", 500f);
        cd.AverageHeight = 42f;
        cd.StreetHeightSource = new FlatStreetHeight(cd);

        var source = _terrain(100f);
        var provider = new FixedElevation { Segment = source };
        var target = _empty();

        var op = new ClusterConformElevationOperator(cd, "conform-flat");
        op.ElevationOperatorProcess(provider, target);

        Assert.Equal(1, provider.Calls);

        for (int ez = 0; ez < N; ++ez)
        {
            for (int ex = 0; ex < N; ++ex)
            {
                Assert.Equal(source.Elevations[ez, ex].Height, target.Elevations[ez, ex].Height);
                Assert.Equal(source.Elevations[ez, ex].Biome, target.Elevations[ez, ex].Biome);
                Assert.Equal(source.Elevations[ez, ex].Flags1, target.Elevations[ez, ex].Flags1);
            }
        }
    }


    /**
     * A fragment nowhere near the city is handed through without the city's street graph
     * ever being asked for.
     *
     * The "first layer below" search does test whether an operator's AABB intersects, but
     * the TOP layer's operator is run for every fragment in the world with no such test -
     * see the disabled intersection check in Cache.ElevationCacheGetAt - and this is the
     * top layer in any world with no intercity network above it. Without the guard the
     * first fragment loaded anywhere would generate a whole city to grade ground a
     * kilometre away.
     *
     * Uses a NON-flat height source deliberately, so the flat short circuit cannot be what
     * is passing this test; and reaching StrokeStore() from here would need the I
     * container and fail.
     */
    [Fact]
    public void AFragmentNowhereNearTheCityIsHandedThroughUnchanged()
    {
        var cd = StreetHarness.MakeCluster("conform-far", 500f);
        cd.Pos = new Vector3(20000f, 0f, 20000f);
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => 0.05f * x);

        var source = _terrain(100f);
        var provider = new FixedElevation { Segment = source };
        var target = _empty();

        var op = new ClusterConformElevationOperator(cd, "conform-far");
        op.ElevationOperatorProcess(provider, target);

        for (int ez = 0; ez < N; ++ez)
        {
            for (int ex = 0; ex < N; ++ex)
            {
                Assert.Equal(source.Elevations[ez, ex].Height, target.Elevations[ez, ex].Height);
                Assert.Equal(source.Elevations[ez, ex].Biome, target.Elevations[ez, ex].Biome);
            }
        }
    }


    // ----------------------------------------------------------------- the write


    /**
     * The pass rewrites the height and nothing else.
     *
     * ClusterBaseElevationOperator writes Biome = 1 across the city rectangle below this
     * one, and that still means "this is city" whatever shape the ground is given. Losing
     * it would change which biome the terrain renders and populates as, a long way from
     * anything that looks like a height bug.
     */
    [Fact]
    public void OnlyTheHeightIsWritten()
    {
        var source = _terrain(0f);
        var target = _empty();

        var field = _oneRoad(new Vector2(-200f, 0f), new Vector2(200f, 0f), 55f, 60f);
        ClusterConformElevationOperator.Grade(field, Vector2.Zero, source, target);

        bool moved = false;

        for (int ez = 0; ez < N; ++ez)
        {
            for (int ex = 0; ex < N; ++ex)
            {
                Assert.Equal(1, target.Elevations[ez, ex].Biome);
                Assert.Equal(7, target.Elevations[ez, ex].Flags1);

                if (Math.Abs(target.Elevations[ez, ex].Height
                             - source.Elevations[ez, ex].Height) > 0.001f)
                {
                    moved = true;
                }
            }
        }

        Assert.True(moved, "the road must have moved some ground, or this proves nothing");
    }


    /**
     * A sample on a road takes that road's height exactly.
     *
     * The road here runs along z = 0 across the middle of the fragment, so the middle row
     * of samples sits on it and must come out at the street height rather than merely
     * near it.
     */
    [Fact]
    public void TheGroundUnderARoadIsTheRoadsHeight()
    {
        var source = _terrain(0f);
        var target = _empty();

        var field = _oneRoad(new Vector2(-200f, 0f), new Vector2(200f, 0f), 55f, 60f);
        ClusterConformElevationOperator.Grade(field, Vector2.Zero, source, target);

        /*
         * The fragment spans -200..200 in both axes with 21 samples, so row 10 is z = 0.
         */
        for (int ex = 0; ex < N; ++ex)
        {
            Assert.Equal(55f, target.Elevations[10, ex].Height, 3);
        }
    }


    /**
     * The ground EASES back to the terrain away from a road; it is not stamped flat out
     * to the radius and then dropped off a cliff.
     *
     * The falloff is the whole difference between grading a site and cutting a corridor,
     * and this is the assertion that says the write site uses it. It was missing from the
     * first version of these tests, and a mutation proved it: writing the street height
     * outright wherever the field answered at all - no blend, no influence - passed every
     * other test here, because a sample ON the road, a sample out of range and a sample
     * whose biome must survive are all still right. What it produced was a plateau three
     * cells wide along every street with a 20 m wall around it, which is precisely the
     * terracing the corridor idea was rejected for.
     *
     * Stated as a shape - between the two heights, falling with distance, exactly the
     * terrain once past the radius - rather than by naming the kernel's values, which
     * would only restate StreetHeightField here.
     */
    [Fact]
    public void TheGroundEasesBackToTheTerrainAwayFromTheRoad()
    {
        const float terrain = 10f;
        const float road = 110f;

        var source = _flatTerrain(terrain);
        var target = _empty();

        var field = _oneRoad(new Vector2(-200f, 0f), new Vector2(200f, 0f), road, 60f);
        ClusterConformElevationOperator.Grade(field, Vector2.Zero, source, target);

        /*
         * Row 10 is z = 0 and sits on the road; each further row is one cell - 20 m -
         * away, so rows 11 and 12 are inside the 60 m radius and row 13 is on it.
         */
        Assert.Equal(road, target.Elevations[10, 5].Height, 3);

        float previous = road;
        foreach (int ez in new[] { 11, 12 })
        {
            float h = target.Elevations[ez, 5].Height;

            Assert.True(h > terrain && h < previous,
                $"row {ez} at {h} is not easing from the road back toward the terrain");
            previous = h;
        }

        Assert.Equal(terrain, target.Elevations[13, 5].Height);
        Assert.Equal(terrain, target.Elevations[20, 5].Height);
    }


    /**
     * Ground beyond the influence of every street is copied through exactly, so the pass
     * grades a city site and does not level the countryside around it.
     */
    [Fact]
    public void GroundOutOfRangeIsCopiedThroughExactly()
    {
        var source = _terrain(30f);
        var target = _empty();

        /*
         * A road well south of the fragment, further away than the radius from every
         * sample in it.
         */
        var field = _oneRoad(new Vector2(-200f, -400f), new Vector2(200f, -400f), 999f, 60f);
        ClusterConformElevationOperator.Grade(field, Vector2.Zero, source, target);

        for (int ez = 0; ez < N; ++ez)
        {
            for (int ex = 0; ex < N; ++ex)
            {
                Assert.Equal(source.Elevations[ez, ex].Height, target.Elevations[ez, ex].Height);
            }
        }
    }


    /**
     * Samples are placed where CacheEntry.GetElevationPixelAt reads them back from.
     *
     * A segment carries GroundResolution + 1 samples spanning FragmentSize, so the step is
     * FragmentSize / GroundResolution and the LAST sample sits on the far edge. The two
     * sibling elevation operators divide the span by the sample COUNT instead, which
     * places every sample about five percent short and restarts the error at each
     * fragment; they get away with it because each writes a constant inside a rectangle.
     * Here it would put a step in the graded ground along every fragment seam.
     *
     * Stated by putting a road exactly on the fragment's far edge: with the correct
     * spacing the last column is on the road and takes its height exactly, and with the
     * sibling operators' spacing it is 19 m away and does not.
     */
    [Fact]
    public void SamplesLandWhereTheyAreReadBackFrom()
    {
        var rect = _fragmentRect();

        var source = _terrain(0f);
        var target = _empty();

        var field = _oneRoad(
            new Vector2(rect.B.X, rect.A.Y), new Vector2(rect.B.X, rect.B.Y), 77f, 60f);
        ClusterConformElevationOperator.Grade(field, Vector2.Zero, source, target);

        Assert.Equal(77f, target.Elevations[10, N - 1].Height, 3);

        /*
         * And the same on the other axis, since the two steps are computed separately and
         * one of them being right proves nothing about the other.
         */
        var targetZ = _empty();
        var fieldZ = _oneRoad(
            new Vector2(rect.A.X, rect.B.Y), new Vector2(rect.B.X, rect.B.Y), 77f, 60f);
        ClusterConformElevationOperator.Grade(fieldZ, Vector2.Zero, source, targetZ);

        Assert.Equal(77f, targetZ.Elevations[N - 1, 10].Height, 3);
    }


    /**
     * The field is in cluster relative coordinates and an elevation segment is not, so the
     * origin has to be subtracted - and a version that forgot would grade the right shape
     * in the wrong place, which looks like terrain and reads like a bug somewhere else.
     */
    [Fact]
    public void TheFieldIsPlacedAtTheClustersOrigin()
    {
        var source = _terrain(0f);
        var target = _empty();

        Vector2 v2Origin = new(1000f, -600f);

        /*
         * Cluster relative road along z = 0, i.e. world z = -600, which is outside this
         * fragment entirely.
         */
        var field = _oneRoad(new Vector2(-400f, 0f), new Vector2(400f, 0f), 55f, 60f);
        ClusterConformElevationOperator.Grade(field, v2Origin, source, target);

        for (int ez = 0; ez < N; ++ez)
        {
            for (int ex = 0; ex < N; ++ex)
            {
                Assert.Equal(source.Elevations[ez, ex].Height, target.Elevations[ez, ex].Height);
            }
        }
    }


    // ---------------------------------------------------------------- the layers


    /**
     * The layer strings sort the way the cycle break needs them to.
     *
     * elevation.Cache picks "the first factory strictly below this layer" by comparing the
     * prefixed factory ids in a sorted list, so the whole arrangement rests on three
     * string comparisons:
     *
     *   - a conforming registration sorts ABOVE the layer TerrainStreetHeight samples, so
     *     streets read terrain that no city has conformed;
     *   - the flattening layer sorts BELOW it, so that is what they read instead;
     *   - the intercity layer sorts ABOVE the conforming ones, so an intercity track's
     *     absolute height still has the last word over the city terrain it crosses.
     *
     * The "elevation-factory-" prefix is duplicated from Cache._createFactoryId, which is
     * private; it is a constant prefix on every id, and comparing the layers both with and
     * without it says the ordering does not depend on that duplication being right.
     */
    [Fact]
    public void TheConformingLayerSitsBetweenFlatteningAndIntercity()
    {
        const string prefix = "elevation-factory-";

        string flatten = Cache.LAYER_BASE + "/000100/flattenCluster/somekey-city";
        string fillGrid = Cache.LAYER_BASE + "/000002/fillGrid";
        string sampled = ClusterConformElevationOperator.Layer;
        string conform = ClusterConformElevationOperator.Layer + "/conformCluster/somekey-city";
        string intercity = Cache.LAYER_BASE + "/000200/intercityTrails/line";

        foreach (string p in new[] { "", prefix })
        {
            Assert.True(String.Compare(p + fillGrid, p + sampled) < 0,
                "the base terrain must be below the layer street heights are sampled at");
            Assert.True(String.Compare(p + flatten, p + sampled) < 0,
                "flattening must be below the layer street heights are sampled at");
            Assert.True(String.Compare(p + conform, p + sampled) > 0,
                "conforming must be above the layer street heights are sampled at, "
                + "or streets read their own answer back");
            Assert.True(String.Compare(p + intercity, p + conform) > 0,
                "the intercity network must stay above the city terrain it crosses");
        }
    }


    /**
     * Street heights are sampled BELOW the conforming layer, and that single argument is
     * the whole reason there is no cycle here.
     *
     * A source scan, because reaching TerrainStreetHeight for real needs the elevation
     * cache and a generated world. What it pins is that the sample does not silently go
     * back to the default parameter - GetHeightAt's layer defaults to TOP_LAYER, so
     * dropping the argument compiles, runs, and makes the terrain and the streets chase
     * each other with nothing in the log.
     */
    [Fact]
    public void StreetHeightsAreSampledBelowTheConformingLayer()
    {
        string path = global::engine.GameRoot.PathTo("JoyceCode")
                      + "/engine/streets/TerrainStreetHeight.cs";
        Assert.True(File.Exists(path), $"could not find the height source at {path}");

        string source = File.ReadAllText(path);

        Assert.Contains("Loader.GetHeightAt(", source);
        Assert.Contains("ClusterConformElevationOperator.Layer", source);

        /*
         * The token, not the phrase - the comment above the call says "the top layer" in
         * words on purpose, so that this stays a statement about what the file does.
         */
        Assert.DoesNotContain("TOP_LAYER", source);
    }


    /**
     * The conforming operator is registered only where the city keeps its terrain.
     *
     * Also a source scan: reaching the registration needs a world being generated. The
     * operator would do nothing on the flat path anyway - that is the test above - but a
     * layer that is not registered cannot be reached by anything at all, including by the
     * "first layer below" search every other operator runs, and that is what keeps the
     * default world's layer stack exactly what it has always been.
     */
    [Fact]
    public void TheConformingOperatorIsOnlyRegisteredForATerrainFollowingCity()
    {
        string path = global::engine.GameRoot.PathTo("JoyceCode")
                      + "/engine/world/GenerateClustersOperator.cs";
        Assert.True(File.Exists(path), $"could not find the operator source at {path}");

        string[] lines = File.ReadAllLines(path);

        int registration = Enumerable.Range(0, lines.Length)
            .FirstOrDefault(
                i => lines[i].Contains("ClusterConformElevationOperator.Layer"), -1);

        Assert.True(registration >= 0,
            "GenerateClustersOperator no longer registers the conforming operator");

        /*
         * The guard sits within a few lines above the registration; a comment explaining
         * it is between them.
         */
        bool guarded = Enumerable.Range(Math.Max(0, registration - 16), 16)
            .Any(i => lines[i].Contains("StreetHeightSources.FollowsTerrain"));

        Assert.True(guarded,
            "the conforming operator must only be registered when cities follow terrain");
    }
}
