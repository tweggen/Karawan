using System;
using System.IO;
using System.Text.Json.Nodes;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * The shipped ruleset and the built-in defaults must stay the same thing.
 *
 * If they drift, the game and every test that runs without configuration generate
 * different cities, and the determinism fingerprints stop meaning what they claim.
 */
public class StreetGenConfigTests
{
    private static string _shippedPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Karawan.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, "could not locate the repository root above " + AppContext.BaseDirectory);
        return Path.Combine(dir.FullName, "models", "nogame.streets.json");
    }


    private static ExpansionRuleTable _parseShipped()
    {
        return StreetGenConfig.Parse(JsonNode.Parse(File.ReadAllText(_shippedPath())));
    }


    [Fact]
    public void AMissingRulesetYieldsTheDefaults()
    {
        var table = StreetGenConfig.Parse(null);

        Assert.Equal(4, table.Rules.Length);
        Assert.Equal(2, table.Groups.Length);
    }


    /**
     * AC-3.1. Not a spot check: every field of every rule and group.
     */
    [Fact]
    public void TheShippedRulesetIsValueIdenticalToTheDefaults()
    {
        var shipped = _parseShipped();
        var defaults = ExpansionRuleTable.Defaults();

        Assert.Equal(defaults.Rules.Length, shipped.Rules.Length);
        Assert.Equal(defaults.Groups.Length, shipped.Groups.Length);
        Assert.Equal(defaults.FallbackRule, shipped.FallbackRule);

        for (int i = 0; i < defaults.Groups.Length; ++i)
        {
            Assert.Equal(defaults.Groups[i].Name, shipped.Groups[i].Name);
            Assert.Equal(defaults.Groups[i].DecreaseProbability, shipped.Groups[i].DecreaseProbability);
            Assert.Equal(defaults.Groups[i].IncreaseProbability, shipped.Groups[i].IncreaseProbability);
        }

        for (int i = 0; i < defaults.Rules.Length; ++i)
        {
            var d = defaults.Rules[i];
            var s = shipped.Rules[i];

            Assert.Equal(d.Name, s.Name);
            Assert.Equal(d.Direction, s.Direction);
            Assert.Equal(d.WeightGroup, s.WeightGroup);
            Assert.Equal(d.KeepPrimary, s.KeepPrimary);
            Assert.Equal(d.IsFallback, s.IsFallback);
            Assert.Equal(d.Probability.Kind, s.Probability.Kind);
            Assert.Equal(d.Probability.A, s.Probability.A);
            Assert.Equal(d.Probability.B, s.Probability.B);
        }
    }


    /**
     * AC-3.1, the part that actually matters: running the generator with the shipped
     * ruleset must produce the very same networks as running it with the defaults.
     */
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void TheShippedRulesetGeneratesTheSameNetworksAsTheDefaults(string idString, float size)
    {
        var withDefaults = StreetNetworkFingerprint.CanonicalLines(
            StreetHarness.Generate(idString, size));
        var withShipped = StreetNetworkFingerprint.CanonicalLines(
            StreetHarness.Generate(idString, size, _parseShipped()));

        Assert.Equal(withDefaults.Length, withShipped.Length);
        for (int i = 0; i < withDefaults.Length; ++i)
        {
            Assert.True(withDefaults[i] == withShipped[i],
                StreetNetworkFingerprint.Diff(withDefaults, withShipped));
        }
    }


    /**
     * AC-3.5. Configuration JSON is case-insensitive throughout this codebase.
     */
    [Fact]
    public void PropertyNamesAreCaseInsensitive()
    {
        var table = StreetGenConfig.Parse(JsonNode.Parse("""
        {
          "WeightGroups": [ { "NAME": "g", "DecreaseProbability": 1, "increaseprobability": 2 } ],
          "RULES": [ { "Name": "r", "Direction": "Forward", "WeightGroup": "G",
                       "Probability": { "Kind": "Constant", "A": 128 },
                       "KeepPrimary": true, "IsFallback": true } ]
        }
        """));

        Assert.Single(table.Rules);
        Assert.Equal(StrokeDirection.Forward, table.Rules[0].Direction);
        Assert.Equal(0, table.FallbackRule);
    }


    /**
     * AC-3.3. Every one of these used to be a way to quietly reshape a city.
     */
    [Theory]
    [InlineData("""{ "weightGroups": [], "rules": [] }""", "no weight groups")]
    [InlineData("""
      { "weightGroups": [ { "name": "g", "decreaseProbability": 1, "increaseProbability": 2 } ],
        "rules": [ { "name": "r", "direction": "sideways", "weightGroup": "g",
                     "probability": { "kind": "constant", "a": 1 }, "keepPrimary": true, "isFallback": true } ] }
      """, "unknown direction")]
    [InlineData("""
      { "weightGroups": [ { "name": "g", "decreaseProbability": 1, "increaseProbability": 2 } ],
        "rules": [ { "name": "r", "direction": "forward", "weightGroup": "g",
                     "probability": { "kind": "quadratic", "a": 1, "b": 2 }, "keepPrimary": true, "isFallback": true } ] }
      """, "unknown probability shape")]
    [InlineData("""
      { "weightGroups": [ { "name": "g", "decreaseProbability": 1, "increaseProbability": 2 } ],
        "rules": [ { "name": "r", "direction": "forward", "weightGroup": "typo",
                     "probability": { "kind": "constant", "a": 1 }, "keepPrimary": true, "isFallback": true } ] }
      """, "undefined weight group")]
    [InlineData("""
      { "weightGroups": [ { "name": "g", "decreaseProbability": 1, "increaseProbability": 2 } ],
        "rules": [ { "name": "r", "direction": "forward", "weightGroup": "g",
                     "probability": { "kind": "constant", "a": 1 }, "keepPrimary": true } ] }
      """, "no fallback rule")]
    [InlineData("""
      { "weightGroups": [ { "name": "g", "decreaseProbability": 1, "increaseProbability": 2 } ],
        "rules": [ { "name": "a", "direction": "forward", "weightGroup": "g",
                     "probability": { "kind": "constant", "a": 1 }, "keepPrimary": true, "isFallback": true },
                   { "name": "b", "direction": "left", "weightGroup": "g",
                     "probability": { "kind": "constant", "a": 1 }, "keepPrimary": true, "isFallback": true } ] }
      """, "two fallback rules")]
    [InlineData("""
      { "weightGroups": [ { "name": "g", "decreaseProbability": 1, "increaseProbability": 2 } ],
        "rules": [ { "name": "r", "weightGroup": "g",
                     "probability": { "kind": "constant", "a": 1 }, "keepPrimary": true, "isFallback": true } ] }
      """, "missing direction")]
    public void AMalformedRulesetIsRefusedAtParseTime(string json, string why)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StreetGenConfig.Parse(JsonNode.Parse(json)));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message),
            $"the '{why}' case must explain itself");
    }
}
