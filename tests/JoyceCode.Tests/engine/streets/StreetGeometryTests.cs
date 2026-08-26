using System.Collections.Generic;
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
