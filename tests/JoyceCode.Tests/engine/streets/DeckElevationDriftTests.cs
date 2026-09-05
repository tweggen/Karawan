using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B1.6 and B1.7.
 *
 * B1.6 is mostly other tests' job: StreetDeterminismTests pins V1 against the recorded
 * fingerprint for eight seeds, StreetGeometryTests pins street-geometry.json and
 * StreetCostTests the allocation ceiling. What is missing from that set is V2, which
 * has no baseline of its own - so rather than record a second file, the property below
 * shows that V2 is DETERMINED by V1 in a flag-off city: every stroke is on level 0, so
 * the only thing V2 adds is a constant suffix.
 *
 * B1.7 pins the deck-elevation expression. It is already the single one; this makes a
 * second copy fail rather than merely be regrettable.
 */
public class DeckElevationDriftTests
{
    /**
     * Nothing a flag-off generator produces is above or below the ground, and nothing
     * it produces is a structure.
     *
     * Note the two kinds named explicitly. ConnectorBridge strokes exist in shipped
     * flat cities - one to three per city - so "every stroke is a Street" would be
     * false and "no stroke is a Street" would be the wrong rule.
     */
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void AFlagOffCityIsEntirelyOnTheGround(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);

        foreach (var stroke in store.GetStrokes())
        {
            Assert.Equal((sbyte)0, stroke.Level);
            Assert.False(StrokeKinds.IsStructure(stroke.Kind),
                $"{idString}@{size}: a flag-off city produced a {stroke.Kind}");
            Assert.True(
                stroke.Kind == StrokeKind.Street || stroke.Kind == StrokeKind.ConnectorBridge,
                $"{idString}@{size}: unexpected stroke kind {stroke.Kind}");
        }

        foreach (var sp in store.GetStreetPoints())
        {
            Assert.Equal((sbyte)0, sp.Level);
            Assert.Equal(0f, sp.LevelElevation);
        }
    }


    /**
     * ...and therefore V2 carries exactly what V1 carries. V1 has a recorded baseline
     * per environment; this is what makes that baseline cover V2 as well, without a
     * second file to re-record.
     */
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void V2AddsNothingToAFlagOffCity(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);

        var v1 = StreetNetworkFingerprint.CanonicalLines(store);

        /*
         * V2's line is V1's line with the level appended, so on the ground the two sets
         * differ by a constant suffix and by nothing else.
         */
        var expected = v1.Select(l => l + "|L0").OrderBy(l => l, StringComparer.Ordinal).ToArray();

        var actual = store.GetStrokes()
            .Select(s =>
            {
                var a = s.A.Pos;
                var b = s.B.Pos;
                string pa = $"{a.X:F3},{a.Y:F3}";
                string pb = $"{b.X:F3},{b.Y:F3}";
                string p = string.CompareOrdinal(pa, pb) <= 0 ? pa : pb;
                string q = string.CompareOrdinal(pa, pb) <= 0 ? pb : pa;
                return $"{p}|{q}|{s.Weight:F3}|{(s.IsPrimary ? 1 : 0)}|L{s.Level}";
            })
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }


    /*
     * ------------------------------------------------------------------ B1.7 ------
     */

    /**
     * Files permitted to name StreetLevels.DeckHeight, and why.
     *
     * Deck elevation is one expression - StreetLevels.ElevationOf - reached through
     * StreetPoint.LevelElevation by all four of its consumers. The failure mode this
     * guards against is the one §7m and §7o both hit in other guises: a second site
     * computes the same quantity from the same parts, the two agree while the parts are
     * zero, and they part company the day they stop being.
     */
    private static readonly Dictionary<string, string> _mayNameDeckHeight = new()
    {
        ["JoyceCode/engine/streets/StreetLevels.cs"] =
            "declares it and is the one expression that multiplies by it",
    };


    /**
     * Sites that read a junction's deck elevation, and must do it through
     * LevelElevation rather than by multiplying a level by anything.
     */
    private static readonly string[] _elevationConsumers =
    {
        "JoyceCode/engine/streets/generation/RoadSurface.cs",
        "JoyceCode/builtin/modules/satnav/GenerateNavMapOperator.cs",
        "JoyceCode/engine/tale/SpatialModel.cs",
        "JoyceCode/engine/Placer.cs",
    };


    private static string _repoRoot()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        return Path.GetFullPath(Path.Combine(root, ".."));
    }


    [Fact]
    public void OnlyOneExpressionTurnsADeckLevelIntoAHeight()
    {
        string root = _repoRoot();

        var found = new[] { "JoyceCode", "nogameCode" }
            .SelectMany(dir => Directory.EnumerateFiles(
                Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
            .Where(f => File.ReadAllText(f).Contains("DeckHeight"))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var unexpected = found.Where(f => !_mayNameDeckHeight.ContainsKey(f)).ToList();

        Assert.True(0 == unexpected.Count,
            "these name StreetLevels.DeckHeight and are not on the known list:\n  "
            + String.Join("\n  ", unexpected)
            + "\n\nHow high a deck stands above the ground is StreetLevels.ElevationOf, "
            + "reached through StreetPoint.LevelElevation. A second expression for it is "
            + "how two sites come to disagree about where a bridge is - and they will "
            + "agree perfectly until the first city with a level on it.");

        var stale = _mayNameDeckHeight.Keys.Where(f => !found.Contains(f)).ToList();

        Assert.True(0 == stale.Count,
            "these are on the known list but no longer name DeckHeight - remove them:\n  "
            + String.Join("\n  ", stale));
    }


    [Fact]
    public void EveryDeckElevationConsumerReadsItOffTheJunction()
    {
        string root = _repoRoot();

        foreach (var relative in _elevationConsumers)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"{relative} has moved or gone");

            string text = File.ReadAllText(path);

            Assert.Contains(".LevelElevation", text);

            /*
             * And the other direction, which is what a presence scan alone cannot say:
             * a site that computed the height itself would still contain the word if
             * the old call were left beside it in a comment.
             */
            Assert.DoesNotContain("DeckHeight", text);
        }
    }


    /**
     * The behaviour behind the scan: LevelElevation IS ElevationOf, over the whole
     * range a level can take. A scan can only see names; this is what says the name
     * still means what it says.
     */
    [Fact]
    public void AJunctionsElevationIsTheOneExpressionForItsLevel()
    {
        for (int level = -4; level <= 4; ++level)
        {
            var sp = new StreetPoint() { ClusterId = 0, Level = (sbyte)level };
            Assert.Equal(StreetLevels.ElevationOf((sbyte)level), sp.LevelElevation);
        }
    }


    /*
     * ------------------------------------------------- the flag is injected --------
     */

    /**
     * Where the grade-separation setting may be read.
     *
     * It is a process global, so a Generator that consulted it directly could not be
     * driven both ways in one test run - the setting would leak sideways into whatever
     * else was generating. Exactly the reasoning recorded on
     * StreetHeightSources.FollowsTerrain, and exactly the reason a mutation that moves
     * the read INTO the generator has to fail something.
     */
    private static readonly Dictionary<string, string> _mayReadTheFlag = new()
    {
        ["JoyceCode/engine/streets/StreetLevels.cs"] = "declares the setting and reads it",
        ["JoyceCode/engine/world/ClusterDesc.cs"] =
            "does the one read and hands the answer to the Generator as a value",
    };


    [Fact]
    public void TheGradeSeparationSettingIsReadInExactlyOnePlace()
    {
        string root = _repoRoot();

        var found = new[] { "JoyceCode", "nogameCode" }
            .SelectMany(dir => Directory.EnumerateFiles(
                Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
            .Where(f =>
            {
                string text = File.ReadAllText(f);
                return text.Contains("GradeSeparation.IsEnabled")
                       || text.Contains("EnableGradeSeparation\"")
                       || text.Contains("GradeSeparation.Setting");
            })
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var unexpected = found.Where(f => !_mayReadTheFlag.ContainsKey(f)).ToList();

        Assert.True(0 == unexpected.Count,
            "these read the grade-separation setting and are not on the known list:\n  "
            + String.Join("\n  ", unexpected)
            + "\n\nThe setting is read once, in ClusterDesc._generateStrokes, and passed "
            + "to Generator.EnableGradeSeparation as a value. Reading it deeper in makes "
            + "the generator untestable both ways in one process.");

        var stale = _mayReadTheFlag.Keys.Where(f => !found.Contains(f)).ToList();

        Assert.True(0 == stale.Count,
            "these are on the known list but no longer read the setting - remove them:\n  "
            + String.Join("\n  ", stale));
    }


    [Fact]
    public void AGeneratorIsGroundOnlyUntilItIsToldOtherwise()
    {
        Assert.False(new Generator().EnableGradeSeparation);
    }
}
