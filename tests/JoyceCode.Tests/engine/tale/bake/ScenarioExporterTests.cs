using System.Collections.Generic;
using System.IO;
using engine.tale.bake;
using Xunit;

namespace JoyceCode.Tests.engine.tale.bake;

/// <summary>
/// Round-trip and structural checks for ScenarioExporter. The runtime
/// ScenarioLibrary deserializes Phase D1 bake artifacts via
/// ReadFromStream, so any drift between the Write/Read pair silently breaks
/// the on-disk loader path.
/// </summary>
public class ScenarioExporterTests
{
    private static Scenario MakeSampleScenario()
    {
        return new Scenario
        {
            Version = 1,
            Category = "test",
            Index = 2,
            Seed = 12345,
            NpcCount = 3,
            SimulationDays = 30,
            Npcs =
            {
                new ScenarioNpc
                {
                    Rank = 0, Role = "worker", GroupRank = 0,
                    Properties = new Dictionary<string, float>
                    {
                        { "morality", 0.8f },
                        { "wealth", 0.6f },
                        { "fear", 0.1f },
                        { "anger", 0.05f },
                        { "reputation", 0.5f }
                    }
                },
                new ScenarioNpc
                {
                    Rank = 1, Role = "drifter", GroupRank = -1,
                    Properties = new Dictionary<string, float>
                    {
                        { "morality", 0.3f },
                        { "wealth", 0.1f },
                        { "fear", 0.4f },
                        { "anger", 0.2f },
                        { "reputation", 0.2f }
                    }
                },
                new ScenarioNpc
                {
                    Rank = 2, Role = "worker", GroupRank = 0,
                    Properties = new Dictionary<string, float>
                    {
                        { "morality", 0.7f },
                        { "wealth", 0.5f },
                        { "fear", 0.0f },
                        { "anger", 0.0f },
                        { "reputation", 0.6f }
                    }
                }
            },
            Groups =
            {
                new ScenarioGroup
                {
                    Rank = 0, Type = "trade",
                    MemberRanks = new List<int> { 0, 2 }
                }
            },
            Relationships =
            {
                new ScenarioRelationship
                {
                    FromRank = 0, ToRank = 2,
                    TrustAtoB = 0.85f, TrustBtoA = 0.78f,
                    InteractionCount = 42
                }
            },
            Histograms =
            {
                ["morality"] = new ScenarioHistogram
                {
                    Bins = new List<float> { 0.5f, 1.0f },
                    Counts = new List<int> { 1, 2 }
                }
            }
        };
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = MakeSampleScenario();

        // Round-trip via a temp file (the writer takes a path, not a stream).
        Scenario restored;
        string tmp = Path.Combine(Path.GetTempPath(), $"scenario_test_{System.Guid.NewGuid():N}.json");
        try
        {
            ScenarioExporter.WriteToFile(original, tmp);
            using var reread = File.OpenRead(tmp);
            restored = ScenarioExporter.ReadFromStream(reread);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }

        Assert.NotNull(restored);
        Assert.Equal(original.Version, restored.Version);
        Assert.Equal(original.Category, restored.Category);
        Assert.Equal(original.Index, restored.Index);
        Assert.Equal(original.Seed, restored.Seed);
        Assert.Equal(original.NpcCount, restored.NpcCount);
        Assert.Equal(original.SimulationDays, restored.SimulationDays);

        // NPCs
        Assert.Equal(original.Npcs.Count, restored.Npcs.Count);
        for (int i = 0; i < original.Npcs.Count; i++)
        {
            Assert.Equal(original.Npcs[i].Rank, restored.Npcs[i].Rank);
            Assert.Equal(original.Npcs[i].Role, restored.Npcs[i].Role);
            Assert.Equal(original.Npcs[i].GroupRank, restored.Npcs[i].GroupRank);
            foreach (var key in original.Npcs[i].Properties.Keys)
            {
                Assert.Equal(original.Npcs[i].Properties[key], restored.Npcs[i].Properties[key], 5);
            }
        }

        // Groups
        Assert.Equal(original.Groups.Count, restored.Groups.Count);
        Assert.Equal(original.Groups[0].Rank, restored.Groups[0].Rank);
        Assert.Equal(original.Groups[0].Type, restored.Groups[0].Type);
        Assert.Equal(original.Groups[0].MemberRanks, restored.Groups[0].MemberRanks);

        // Relationships
        Assert.Equal(original.Relationships.Count, restored.Relationships.Count);
        Assert.Equal(original.Relationships[0].FromRank, restored.Relationships[0].FromRank);
        Assert.Equal(original.Relationships[0].ToRank, restored.Relationships[0].ToRank);
        Assert.Equal(original.Relationships[0].TrustAtoB, restored.Relationships[0].TrustAtoB, 5);
        Assert.Equal(original.Relationships[0].TrustBtoA, restored.Relationships[0].TrustBtoA, 5);
        Assert.Equal(original.Relationships[0].InteractionCount, restored.Relationships[0].InteractionCount);

        // Histograms
        Assert.Equal(original.Histograms.Count, restored.Histograms.Count);
        Assert.True(restored.Histograms.ContainsKey("morality"));
        Assert.Equal(original.Histograms["morality"].Bins, restored.Histograms["morality"].Bins);
        Assert.Equal(original.Histograms["morality"].Counts, restored.Histograms["morality"].Counts);
    }

    [Fact]
    public void WriteToFile_ProducesNonEmptyArtifact()
    {
        var s = MakeSampleScenario();
        string tmp = Path.Combine(Path.GetTempPath(), $"scenario_test_{System.Guid.NewGuid():N}.json");
        try
        {
            ScenarioExporter.WriteToFile(s, tmp);
            Assert.True(File.Exists(tmp));
            Assert.True(new FileInfo(tmp).Length > 0);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
