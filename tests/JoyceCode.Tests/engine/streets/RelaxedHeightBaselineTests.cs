using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B3a.2 — the height model may not move the city that ships.
 *
 * `joyce.DisableClusterFlattening` defaults to true, so the terrain following city IS
 * the shipped city and every relaxed junction height is content. WP-B3a teaches
 * GradeRelaxer about boundary conditions and GradePolicy about ramps; neither may
 * change a single float of a city that contains no structure - which is every city the
 * shipped ruleset builds.
 *
 * Recorded rather than argued. "No generated city contains a Ramp, therefore nothing
 * can have changed" is exactly the containment reasoning §7 of the Phase B plan warns
 * about: it is an argument about the code, and the thing being gated is the output.
 * The fingerprint below is the output - every junction's relaxed height and every
 * stroke's permitted grade, as exact float BITS rather than as printed decimals, over
 * the same eight seeds StreetDeterminismTests pins - and it was recorded at
 * a135898e, before a line of WP-B3a existed.
 *
 * Per environment, for the reason StreetBaselines documents: floating point results are
 * only reproducible for a given runtime and architecture.
 */
public class RelaxedHeightBaselineTests
{
    private const string BaselineFile = "street-relaxed-heights.json";


    private static string _key(string idString, float size)
        => $"{idString}@{size:F0}";


    /**
     * Exact bits, not a rounded decimal.
     *
     * A relaxed height printed to six places would hide precisely the class of change
     * this gate exists to catch: an arithmetic reordering that moves a junction by an
     * ulp, which is invisible in a city and is not invisible in a fingerprint recorded
     * downstream of it.
     */
    private static string _bits(float f)
        => BitConverter.SingleToInt32Bits(f).ToString("X8", CultureInfo.InvariantCulture);


    /**
     * Keyed on plan position rather than on junction id, exactly as
     * StreetNetworkFingerprint is and for the same reason: an id is an artefact of what
     * else allocated points earlier in the process.
     */
    internal static string[] CanonicalLines(
        global::engine.world.ClusterDesc clusterDesc, StrokeStore store)
    {
        var source = ShippedTerrain.StreetHeightsOf(clusterDesc, store);
        var policy = new GradePolicy();

        var lines = new List<string>();

        foreach (var sp in store.GetStreetPoints())
        {
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "P|{0:F3},{1:F3}|{2}", sp.Pos.X, sp.Pos.Y, _bits(source.GroundHeightAt(sp))));
        }

        foreach (var s in store.GetStrokes())
        {
            string a = string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F3}", s.A.Pos.X, s.A.Pos.Y);
            string b = string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F3}", s.B.Pos.X, s.B.Pos.Y);
            string p = string.CompareOrdinal(a, b) <= 0 ? a : b;
            string q = string.CompareOrdinal(a, b) <= 0 ? b : a;

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "S|{0}|{1}|{2}", p, q, _bits(policy.MaxGradeFor(s))));
        }

        lines.Sort(StringComparer.Ordinal);
        return lines.ToArray();
    }


    private static string _fingerprint(
        global::engine.world.ClusterDesc clusterDesc, StrokeStore store)
    {
        var lines = CanonicalLines(clusterDesc, store);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

        return string.Format(CultureInfo.InvariantCulture,
            "n={0},s={1},h={2}",
            store.GetStreetPoints().Count, store.GetStrokes().Count,
            Convert.ToHexString(hash).Substring(0, 16));
    }


    /**
     * The gate. Every relaxed height and every permitted grade of eight cities on the
     * shipped terrain, bit for bit.
     */
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void TheRelaxedHeightsOfAShippedCityAreUnchanged(string idString, float size)
    {
        string stamp = StreetNetworkFingerprint.EnvironmentStamp();
        string key = _key(idString, size);

        var clusterDesc = StreetHarness.MakeCluster(idString, size);
        var store = StreetHarness.Generate(idString, size);

        string actual = _fingerprint(clusterDesc, store);

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

        Assert.True(
            baselines!.TryGetPropertyValue(key, out var expectedNode) && expectedNode != null,
            $"No relaxed height baseline for seed '{key}' under environment '{stamp}'. "
            + $"Observed {actual}.");

        string expected = expectedNode!.GetValue<string>();
        Assert.True(expected == actual,
            $"{key}: the relaxed street heights moved.\n"
            + $"  expected {expected}\n"
            + $"  actual   {actual}\n"
            + "The terrain following city is the shipped city. Nothing in WP-B3a may "
            + "move a junction of a city that contains no structure - find the cause, "
            + "do not re-record.");
    }


    /**
     * Environment independent, and therefore always meaningful: the relaxation of a
     * given city is the same twice in one process.
     */
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void RelaxedHeightsAreRepeatableWithinAProcess(string idString, float size)
    {
        var first = CanonicalLines(
            StreetHarness.MakeCluster(idString, size), StreetHarness.Generate(idString, size));
        var second = CanonicalLines(
            StreetHarness.MakeCluster(idString, size), StreetHarness.Generate(idString, size));

        Assert.Equal(first, second);
    }


    /**
     * The other half of B3a.2, stated as a property rather than as a hash: a Street's
     * permitted grade is the weight interpolation and nothing else.
     *
     * Written out here in full, in a different file from the policy, so that a Kind
     * branch which accidentally caught Street or ConnectorBridge fails on something
     * that says WHY rather than only on a changed hash. ConnectorBridge is named
     * explicitly: one to three of them exist in every shipped city and a rule phrased
     * against "not a Street" would change the default city (§0.7 of the plan).
     */
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void EveryStrokeOfAShippedCityIsGradedByItsWeightAlone(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);
        var policy = new GradePolicy();

        foreach (var s in store.GetStrokes())
        {
            Assert.True(
                s.Kind == StrokeKind.Street || s.Kind == StrokeKind.ConnectorBridge,
                $"{_key(idString, size)}: a flag-off city produced a {s.Kind}");

            float span = policy.WeightMax - policy.WeightMin;
            float t = Single.Clamp((s.Weight - policy.WeightMin) / span, 0f, 1f);
            float expected = policy.MaxGradeAtMinWeight
                             + t * (policy.MaxGradeAtMaxWeight - policy.MaxGradeAtMinWeight);

            Assert.Equal(expected, policy.MaxGradeFor(s));
        }
    }
}
