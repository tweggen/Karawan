using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * The regression net for the streets generator rework.
 *
 * WP-1 through WP-3 of docs/roadmap/proposed/STREETS-GENERATOR-REWORK-PLAN.md are pure
 * refactors: they must not change a single floating point operation or the order in
 * which random numbers are drawn. These tests are what makes that claim checkable. If
 * one of them fails during a refactor, a behaviour change was introduced — find it,
 * do not re-baseline.
 */
public class StreetDeterminismTests
{
    private const string BaselineFile = "street-fingerprints.json";

    /**
     * The same eight seeds with joyce.EnableGradeSeparation on, in V2 - which is the
     * fingerprint that distinguishes decks, and therefore the one WP-B3 will move.
     */
    private const string HeavyFirstBaselineFile = "street-fingerprints-gradesep.json";


    /**
     * Chosen from a survey of 180+ generated clusters so that between them these
     * exercise every branch the generator actually reaches:
     *
     *   seed000@500   small, no orphan bridging at all
     *   seed011@500   small, heaviest observed bridging (3 bridges)
     *   seed000@1500  mid, moderate bridging
     *   seed017@2400  the ONLY case found in 180 clusters that reaches the
     *                 >300 m multi-stroke corridor branch of _bridgeOrphanToMain
     *   Yelukhdidru@800 / @3000  the game's starting cluster name; 3000 is the largest
     *                 city the game builds
     *
     * ⚠️ This list used to claim that @3000 "exercises the maxGenerations = Size^2/1000
     * budget cut-off". IT DOES NOT, and neither does any other seed here: measured
     * 2026-09-05 with WP-B2, its _generationCounter finishes at 1886 against a budget of
     * 9000, seed017@2400 at 1034 of 5760 and seed000@1500 at 365 of 2250. Every one of
     * the eight leaves the drain by the queue running dry. The budget exit is covered by
     * HeavyFirstOrderingTests, which builds a generator whose budget genuinely binds.
     *   Yelukhdidru@400  small but non-degenerate
     *   Yelukhdidru@100  degenerate: the corner seeds sit at +/-Size/2.2, outside the
     *                 +/-(Size/2 - 20) bounds, so nothing is generated. Pinned on
     *                 purpose - this is the size the old diagnostic test used, and it
     *                 was silently measuring an empty network.
     */
    public static IEnumerable<object[]> Seeds => new List<object[]>
    {
        new object[] { "seed000",     500f  },
        new object[] { "seed011",     500f  },
        new object[] { "seed000",     1500f },
        new object[] { "seed017",     2400f },
        new object[] { "Yelukhdidru", 100f  },
        new object[] { "Yelukhdidru", 400f  },
        new object[] { "Yelukhdidru", 800f  },
        new object[] { "Yelukhdidru", 3000f },
    };


    private static string _key(string idString, float size)
        => $"{idString}@{size:F0}";


    /**
     * Environment-independent and therefore always meaningful: generating the same
     * cluster twice in one process must produce the same network. Catches any
     * dependence on static mutable state, hash ordering or allocation addresses.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void GenerationIsRepeatableWithinAProcess(string idString, float size)
    {
        var first = StreetNetworkFingerprint.CanonicalLines(StreetHarness.Generate(idString, size));
        var second = StreetNetworkFingerprint.CanonicalLines(StreetHarness.Generate(idString, size));

        Assert.True(
            first.Length == second.Length,
            $"{_key(idString, size)}: stroke count differs between two runs in the same " +
            $"process ({first.Length} vs {second.Length}).");

        for (int i = 0; i < first.Length; ++i)
        {
            Assert.True(first[i] == second[i],
                $"{_key(idString, size)}: networks differ between two runs in the same process.\n" +
                StreetNetworkFingerprint.Diff(first, second));
        }
    }


    /**
     * Cross-process stability, and the actual refactor gate: the network must match
     * the committed baseline for this environment.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void GenerationMatchesRecordedBaseline(string idString, float size)
    {
        string stamp = StreetNetworkFingerprint.EnvironmentStamp();
        string key = _key(idString, size);

        var store = StreetHarness.Generate(idString, size);
        string actual = StreetNetworkFingerprint.V1(store);

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
            $"No baseline for seed '{key}' under environment '{stamp}'. " +
            $"Observed {actual}. If this seed is new, regenerate the baseline file.");

        string expected = expectedNode!.GetValue<string>();
        Assert.True(expected == actual,
            $"{key}: street network changed.\n" +
            $"  expected {expected}\n" +
            $"  actual   {actual}\n" +
            $"This is a behaviour change, not a flake. During a pure refactor (WP-1..WP-3) " +
            $"find the cause; do not re-baseline.");
    }


    /**
     * WP-B2.3, first half: the heavy-first drain is repeatable within one process.
     *
     * Environment independent and therefore always meaningful. The ordering scans the
     * pending list and breaks ties by push position, so nothing about it depends on
     * hash order or allocation addresses - which is exactly the claim being made.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void HeavyFirstGenerationIsRepeatableWithinAProcess(string idString, float size)
    {
        var first = StreetNetworkFingerprint.CanonicalLinesV2(
            StreetHarness.GenerateHeavyFirst(idString, size));
        var second = StreetNetworkFingerprint.CanonicalLinesV2(
            StreetHarness.GenerateHeavyFirst(idString, size));

        Assert.True(first.Length == second.Length,
            $"{_key(idString, size)}: stroke count differs between two heavy-first runs " +
            $"in the same process ({first.Length} vs {second.Length}).");

        for (int i = 0; i < first.Length; ++i)
        {
            Assert.True(first[i] == second[i],
                $"{_key(idString, size)}: heavy-first networks differ between two runs in " +
                $"the same process.\n" +
                StreetNetworkFingerprint.Diff(first, second));
        }
    }


    /**
     * WP-B2.3, second half: a recorded V2 baseline per seed, with the flag ON.
     *
     * The flag-off baselines above say the ordering did not leak into the default city.
     * They say nothing at all about the city the ordering builds, which is what WP-B3
     * will place structures in - so it gets its own recorded file rather than being
     * left as "whatever comes out".
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void HeavyFirstGenerationMatchesRecordedBaseline(string idString, float size)
    {
        string stamp = StreetNetworkFingerprint.EnvironmentStamp();
        string key = _key(idString, size);

        var store = StreetHarness.GenerateHeavyFirst(idString, size);
        string actual = StreetNetworkFingerprint.V2(store);

        if (StreetBaselines.WriteRequested)
        {
            var root = StreetBaselines.Load(HeavyFirstBaselineFile);
            StreetBaselines.Record(root, stamp, key, JsonValue.Create(actual));
            StreetBaselines.Save(HeavyFirstBaselineFile, root);
            return;
        }

        var baselines = StreetBaselines.EntriesFor(
            StreetBaselines.Load(HeavyFirstBaselineFile), stamp);
        Assert.True(baselines != null,
            StreetBaselines.MissingBaselineMessage(stamp, HeavyFirstBaselineFile));

        Assert.True(baselines!.TryGetPropertyValue(key, out var expectedNode) && expectedNode != null,
            $"No heavy-first baseline for seed '{key}' under environment '{stamp}'. " +
            $"Observed {actual}. If this seed is new, regenerate the baseline file.");

        string expected = expectedNode!.GetValue<string>();
        Assert.True(expected == actual,
            $"{key}: the heavy-first street network changed.\n" +
            $"  expected {expected}\n" +
            $"  actual   {actual}\n" +
            $"Nothing in WP-B2 or WP-B3 is allowed to move this by accident either.");
    }


    /**
     * Sanity properties that hold for every generated cluster, independent of any
     * baseline. These keep working when the baseline is intentionally regenerated.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void GeneratedNetworkIsStructurallySane(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);
        var points = store.GetStreetPoints();
        var strokes = store.GetStrokes();

        foreach (var stroke in strokes)
        {
            Assert.True(stroke.A.InStore,
                $"{_key(idString, size)}: stroke {stroke.Sid} endpoint A is not in the store.");
            Assert.True(stroke.B.InStore,
                $"{_key(idString, size)}: stroke {stroke.Sid} endpoint B is not in the store.");
            Assert.True(stroke.A != stroke.B,
                $"{_key(idString, size)}: stroke {stroke.Sid} is degenerate (A == B).");
        }

        /*
         * Orphan bridging runs at the end of Generate(), so a finished cluster is
         * expected to be a single connected component (or empty).
         */
        int components = StreetHarness.CountComponents(store);
        Assert.True(components <= 1,
            $"{_key(idString, size)}: {components} disconnected components survived " +
            $"orphan bridging ({points.Count} points, {strokes.Count} strokes).");
    }
}
