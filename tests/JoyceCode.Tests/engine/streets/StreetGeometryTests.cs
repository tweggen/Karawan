using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Nodes;
using engine.streets;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.streets;


/**
 * The geometry gate.
 *
 * Everything the generator emits has been pinned since WP-0. Everything the RENDERER
 * emits has not, and the elevation work so far has been able to lean on
 * StreetLevels.ElevationOf(0) being exactly zero to argue that the ground path cannot
 * have moved. Ramp geometry (WP-5a-ii) changes vertex emission itself, so that
 * argument stops working and this has to exist first.
 */
public class StreetGeometryTests
{
    private const string BaselineFile = "street-geometry.json";

    private readonly ITestOutputHelper _out;
    public StreetGeometryTests(ITestOutputHelper o) { _out = o; }


    /**
     * A subset of the network seeds. Geometry is far heavier per stroke than
     * generation, so the largest cluster is left out; seed017@2400 still emits tens of
     * thousands of vertices.
     */
    public static IEnumerable<object[]> Seeds => new List<object[]>
    {
        new object[] { "seed000",     500f  },
        new object[] { "seed011",     500f  },
        new object[] { "Yelukhdidru", 800f  },
        new object[] { "seed000",     1500f },

        /*
         * Reaches the "a and b ends overlapping" branch, which none of the others do.
         * Found by instrumenting that branch and scanning: it fires on very short
         * strokes, where the two junction fans meet. Degenerate paths like this are
         * exactly what a vertex-emission change breaks, so the gate would have had a
         * hole without it - a normal perturbed inside that branch went undetected
         * until this seed was added.
         */
        new object[] { "seed008",     500f  },
    };


    private static string _key(string idString, float size) => $"{idString}@{size:F0}";


    [Theory]
    [MemberData(nameof(Seeds))]
    public void GeometryIsRepeatableWithinAProcess(string idString, float size)
    {
        var first = StreetGeometryFingerprint.CanonicalLines(
            StreetGeometryHarness.Generate(idString, size));
        var second = StreetGeometryFingerprint.CanonicalLines(
            StreetGeometryHarness.Generate(idString, size));

        Assert.Equal(first.Length, second.Length);
        for (int i = 0; i < first.Length; ++i)
        {
            Assert.True(first[i] == second[i],
                $"{_key(idString, size)}: vertex {i} differs between two runs in one process.\n"
                + StreetNetworkFingerprint.Diff(first, second));
        }
    }


    /**
     * The gate proper. A refactor of the geometry code must leave this untouched.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void GeometryMatchesRecordedBaseline(string idString, float size)
    {
        string stamp = StreetNetworkFingerprint.EnvironmentStamp();
        string key = _key(idString, size);

        var mesh = StreetGeometryHarness.Generate(idString, size);
        string actual = StreetGeometryFingerprint.Of(mesh);

        if (StreetBaselines.WriteRequested)
        {
            var root = StreetBaselines.Load(BaselineFile);
            StreetBaselines.Record(root, stamp, key, JsonValue.Create(actual));
            StreetBaselines.Save(BaselineFile, root);
            return;
        }

        var baselines = StreetBaselines.EntriesFor(StreetBaselines.Load(BaselineFile), stamp);
        Assert.True(baselines != null,
            StreetBaselines.MissingBaselineMessage(stamp, BaselineFile));

        Assert.True(baselines!.TryGetPropertyValue(key, out var expectedNode) && expectedNode != null,
            $"No geometry baseline for '{key}' under environment '{stamp}'. Observed {actual}.");

        Assert.True(expectedNode!.GetValue<string>() == actual,
            $"{key}: emitted street geometry changed.\n"
            + $"  expected {expectedNode.GetValue<string>()}\n"
            + $"  actual   {actual}");
    }


    /**
     * A ground-only cluster is flat, and it sits where the operator says streets sit.
     * This is the invariant every elevation change so far has been justified by; now
     * it is checked rather than argued.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AGroundOnlyClusterIsEntirelyFlat(string idString, float size)
    {
        var clusterDesc = StreetHarness.MakeCluster(idString, size);
        float expected = clusterDesc.AverageHeight
            + global::engine.world.MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;

        var mesh = StreetGeometryHarness.Generate(idString, size);
        Assert.NotEmpty(mesh.Vertices);

        Assert.Equal(expected, StreetGeometryFingerprint.MinY(mesh), 3);
        Assert.Equal(expected, StreetGeometryFingerprint.MaxY(mesh), 3);
    }


    /**
     * And the reason the gate exists: a deck must actually come out one deck height up.
     *
     * Raising the levels of an already generated network keeps the plan geometry
     * identical, so the only thing that may differ is Y.
     */
    [Fact]
    public void RaisingEveryJunctionRaisesTheWholeSurfaceByOneDeck()
    {
        var ground = StreetGeometryHarness.Generate("seed000", 500f);
        var raised = StreetGeometryHarness.GenerateAtLevel("seed000", 500f, 1);

        Assert.Equal(ground.Vertices.Count, raised.Vertices.Count);

        Assert.Equal(
            StreetGeometryFingerprint.MaxY(ground) + StreetLevels.DeckHeight,
            StreetGeometryFingerprint.MaxY(raised), 3);

        for (int i = 0; i < ground.Vertices.Count; ++i)
        {
            Assert.Equal(ground.Vertices[i].X, raised.Vertices[i].X, 3);
            Assert.Equal(ground.Vertices[i].Z, raised.Vertices[i].Z, 3);
            Assert.Equal(ground.Vertices[i].Y + StreetLevels.DeckHeight, raised.Vertices[i].Y, 3);
        }
    }
}


/**
 * Ramp surfaces: the one piece of street geometry that is not flat.
 */
public class RampGeometryTests
{
    private const float ClusterSize = 1000f;

    private static (global::engine.world.ClusterDesc Cluster, StrokeStore Store, List<Stroke> Chain) _overpass()
    {
        var clusterDesc = StreetHarness.MakeCluster("ramps", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var from = new StreetPoint() { ClusterId = 0, Level = 0 };
        from.SetPos(0f, 0f);
        var to = new StreetPoint() { ClusterId = 0, Level = 0 };
        to.SetPos(240f, 0f);

        var chain = new global::engine.streets.generation.OverpassBuilder(0).Build(
            from, to, StrokeKind.Bridge, rampFraction: 0.25f, weight: 1f);

        new global::engine.streets.generation.NetworkBuilder(store).CommitChain(chain);
        return (clusterDesc, store, chain);
    }


    private static float _streetBase(global::engine.world.ClusterDesc cd)
        => cd.AverageHeight + global::engine.world.MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;


    /**
     * A ramp starts on the ground and finishes a deck up, and does so continuously
     * rather than as two flat platforms.
     */
    [Fact]
    public void ARampSurfaceClimbsFromOneDeckToTheNext()
    {
        var (cd, store, chain) = _overpass();
        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { chain[0] });

        Assert.NotEmpty(mesh.Vertices);

        float ground = _streetBase(cd);
        float deck = ground + StreetLevels.DeckHeight;

        Assert.Equal(ground, StreetGeometryFingerprint.MinY(mesh), 2);
        Assert.Equal(deck, StreetGeometryFingerprint.MaxY(mesh), 2);

        /*
         * Deliberately no assertion about vertices part way up: a straight ramp is
         * emitted as a single quad, so its only vertices are at the two ends and the
         * slope lives in the interpolation between them. That the surface really is a
         * slope, rather than a step, is what the linearity test below establishes -
         * before this change the same two ends both sat at ground height.
         */
        Assert.True(StreetGeometryFingerprint.MaxY(mesh) - StreetGeometryFingerprint.MinY(mesh)
                    > StreetLevels.DeckHeight - 0.1f,
            "the surface must span a whole deck height");
    }


    /**
     * Height follows distance along the ramp. Checked against the ramp's own endpoints
     * rather than against a recomputed slope, so the test does not just restate the
     * implementation.
     */
    [Fact]
    public void HeightAlongARampIsProportionalToDistanceAlongIt()
    {
        var (cd, store, chain) = _overpass();
        var ramp = chain[0];
        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { ramp });

        float ground = _streetBase(cd);
        float rise = StreetLevels.DeckHeight;
        float runX = ramp.B.Pos.X - ramp.A.Pos.X;

        foreach (var v in mesh.Vertices)
        {
            float along = Single.Clamp((v.X - ramp.A.Pos.X) / runX, 0f, 1f);
            Assert.Equal(ground + along * rise, v.Y, 1);
        }
    }


    /**
     * A sloped surface needs a sloped normal, or it lights as though it were flat.
     */
    [Fact]
    public void ARampSurfaceIsNotLitAsThoughItWereFlat()
    {
        var (cd, store, chain) = _overpass();
        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { chain[0] });

        Assert.All(mesh.Normals, n =>
        {
            Assert.True(n.Y > 0f, "a road surface still faces upwards");
            Assert.True(Single.Abs(n.X) > 0.01f || Single.Abs(n.Z) > 0.01f,
                $"a climbing surface must not have a straight-up normal, got {n}");
            Assert.Equal(1f, n.Length(), 3);
        });

        /*
         * The ramp climbs towards +X, so its normal leans back towards -X.
         */
        Assert.All(mesh.Normals, n => Assert.True(n.X < 0f,
            $"normal should lean against the climb, got {n}"));
    }


    /**
     * The deck between the two ramps is flat, and lit as such.
     */
    [Fact]
    public void TheDeckBetweenTheRampsStaysFlat()
    {
        var (cd, store, chain) = _overpass();
        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { chain[1] });

        float deck = _streetBase(cd) + StreetLevels.DeckHeight;

        Assert.Equal(deck, StreetGeometryFingerprint.MinY(mesh), 2);
        Assert.Equal(deck, StreetGeometryFingerprint.MaxY(mesh), 2);
        Assert.All(mesh.Normals, n => Assert.Equal(Vector3.UnitY, n));
    }


    /**
     * And the descent leans the other way, so the two ramps are not accidentally
     * identical.
     */
    [Fact]
    public void TheDescendingRampLeansTheOtherWay()
    {
        var (cd, store, chain) = _overpass();
        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { chain[2] });

        Assert.All(mesh.Normals, n => Assert.True(n.X > 0f,
            $"a descending ramp leans the other way, got {n}"));
    }
}
