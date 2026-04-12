using System.Collections.Generic;
using System.Linq;
using engine.tale.bake;
using Xunit;

namespace JoyceCode.Tests.engine.tale.bake;

/// <summary>
/// Tests for the D5 statistics builder. Pure data — no DI, no I/O. Each
/// test hand-builds a small Scenario with known properties and asserts the
/// computed PerScenarioStats matches.
/// </summary>
public class ScenarioStatisticsTests
{
    private static ScenarioNpc Npc(int rank, string role, int groupRank, float wealth, float morality)
    {
        return new ScenarioNpc
        {
            Rank = rank,
            Role = role,
            GroupRank = groupRank,
            Properties = new Dictionary<string, float>
            {
                { "morality", morality },
                { "wealth", wealth },
                { "fear", 0.0f },
                { "anger", 0.0f },
                { "reputation", 0.5f }
            }
        };
    }

    [Fact]
    public void From_EmptyScenario_ReturnsZeros()
    {
        var s = new Scenario { Category = "test", Index = 0, NpcCount = 0 };
        var stats = ScenarioStatisticsBuilder.From(s);
        Assert.Equal(0, stats.GroupCount);
        Assert.Equal(0, stats.RelationshipCount);
        Assert.Equal(0.0, stats.RelationshipDensity);
        Assert.Equal(0, stats.NpcsInAnyGroup);
        Assert.Equal(0.0, stats.GroupMembershipRatio);
        Assert.Empty(stats.RoleDistribution);
    }

    [Fact]
    public void From_GroupCounts_AreAccurate()
    {
        var s = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 4,
            Npcs =
            {
                Npc(0, "worker",  0, 0.5f, 0.5f),
                Npc(1, "worker",  0, 0.5f, 0.5f),
                Npc(2, "drifter", 1, 0.2f, 0.4f),
                Npc(3, "drifter", -1, 0.3f, 0.4f) // not in any group
            },
            Groups =
            {
                new ScenarioGroup { Rank = 0, Type = "trade", MemberRanks = new List<int> { 0, 1 } },
                new ScenarioGroup { Rank = 1, Type = "criminal", MemberRanks = new List<int> { 2 } }
            }
        };

        var stats = ScenarioStatisticsBuilder.From(s);

        Assert.Equal(2, stats.GroupCount);
        Assert.Equal(1, stats.GroupCountByType["trade"]);
        Assert.Equal(1, stats.GroupCountByType["criminal"]);
        Assert.Equal(2, stats.LargestGroupSize);
        Assert.Equal(1, stats.SmallestGroupSize);
        Assert.Equal(1.5, stats.MeanGroupSize, 5);
        Assert.Equal(3, stats.NpcsInAnyGroup);
        Assert.Equal(0.75, stats.GroupMembershipRatio, 5);
    }

    [Fact]
    public void From_RelationshipDensity_IsRatioOfActualToMaxPairs()
    {
        // 4 NPCs → max 4*3/2 = 6 undirected pairs. 3 relationships → density = 0.5.
        var s = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 4,
            Npcs = { Npc(0, "worker", -1, 0.5f, 0.5f), Npc(1, "worker", -1, 0.5f, 0.5f),
                     Npc(2, "worker", -1, 0.5f, 0.5f), Npc(3, "worker", -1, 0.5f, 0.5f) },
            Relationships =
            {
                new ScenarioRelationship { FromRank = 0, ToRank = 1, TrustAtoB = 0.7f, TrustBtoA = 0.6f, InteractionCount = 10 },
                new ScenarioRelationship { FromRank = 0, ToRank = 2, TrustAtoB = 0.5f, TrustBtoA = 0.5f, InteractionCount = 8 },
                new ScenarioRelationship { FromRank = 1, ToRank = 3, TrustAtoB = 0.3f, TrustBtoA = 0.4f, InteractionCount = 6 }
            }
        };

        var stats = ScenarioStatisticsBuilder.From(s);

        Assert.Equal(3, stats.RelationshipCount);
        Assert.Equal(0.5, stats.RelationshipDensity, 5);
        Assert.Equal(0.5, stats.MeanTrustAtoB, 5); // (0.7 + 0.5 + 0.3) / 3
        Assert.Equal(0.5, stats.MeanTrustBtoA, 5); // (0.6 + 0.5 + 0.4) / 3
        Assert.Equal(8.0, stats.MeanInteractionCount, 5);
    }

    [Fact]
    public void From_RoleDistribution_CountsByRole()
    {
        var s = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 5,
            Npcs =
            {
                Npc(0, "worker",   -1, 0.5f, 0.5f),
                Npc(1, "worker",   -1, 0.5f, 0.5f),
                Npc(2, "worker",   -1, 0.5f, 0.5f),
                Npc(3, "drifter",  -1, 0.2f, 0.4f),
                Npc(4, "merchant", -1, 0.7f, 0.6f)
            }
        };

        var stats = ScenarioStatisticsBuilder.From(s);

        Assert.Equal(3, stats.RoleDistribution["worker"]);
        Assert.Equal(1, stats.RoleDistribution["drifter"]);
        Assert.Equal(1, stats.RoleDistribution["merchant"]);
    }

    [Fact]
    public void From_PropertyStats_MeanAndExtremes()
    {
        var s = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 4,
            Npcs =
            {
                Npc(0, "worker", -1, wealth: 0.0f, morality: 0.5f),
                Npc(1, "worker", -1, wealth: 0.5f, morality: 0.5f),
                Npc(2, "worker", -1, wealth: 1.0f, morality: 0.5f),
                Npc(3, "worker", -1, wealth: 0.5f, morality: 0.5f)
            }
        };

        var stats = ScenarioStatisticsBuilder.From(s);
        var wealth = stats.PropertyStats["wealth"];

        Assert.Equal(0.5, wealth.Mean, 5);
        Assert.Equal(0.0, wealth.Min, 5);
        Assert.Equal(1.0, wealth.Max, 5);
        Assert.Equal(0.25, wealth.FractionAtFloor, 5);  // one of four <= 0.05
        Assert.Equal(0.25, wealth.FractionAtCeiling, 5); // one of four >= 0.95
    }

    [Fact]
    public void From_DeterministicAcrossCalls()
    {
        var s = new Scenario
        {
            Category = "test", Index = 7, Seed = 42, NpcCount = 4,
            Npcs =
            {
                Npc(0, "worker", 0, 0.6f, 0.7f),
                Npc(1, "drifter", 0, 0.2f, 0.4f),
                Npc(2, "merchant", -1, 0.8f, 0.6f),
                Npc(3, "worker", -1, 0.5f, 0.5f)
            },
            Groups =
            {
                new ScenarioGroup { Rank = 0, Type = "trade", MemberRanks = new List<int> { 0, 1 } }
            },
            Relationships =
            {
                new ScenarioRelationship { FromRank = 0, ToRank = 1, TrustAtoB = 0.85f, TrustBtoA = 0.85f, InteractionCount = 50 }
            }
        };

        var a = ScenarioStatisticsBuilder.From(s);
        var b = ScenarioStatisticsBuilder.From(s);

        Assert.Equal(a.GroupCount, b.GroupCount);
        Assert.Equal(a.RelationshipCount, b.RelationshipCount);
        Assert.Equal(a.MeanTrustAtoB, b.MeanTrustAtoB, 5);
        Assert.Equal(a.PropertyStats["wealth"].Mean, b.PropertyStats["wealth"].Mean, 5);
    }

    [Fact]
    public void BuildReport_AggregatesByCategory()
    {
        var perScenario = new List<PerScenarioStats>
        {
            new() { Category = "small", Index = 0, GroupCount = 2, RelationshipDensity = 0.30, GroupMembershipRatio = 0.6 },
            new() { Category = "small", Index = 1, GroupCount = 4, RelationshipDensity = 0.50, GroupMembershipRatio = 0.8 },
            new() { Category = "large", Index = 0, GroupCount = 12, RelationshipDensity = 0.10, GroupMembershipRatio = 0.4 }
        };

        var report = ScenarioStatisticsBuilder.BuildReport(perScenario);

        Assert.Equal(3, report.TotalScenarios);
        Assert.Equal(2, report.Categories.Count);

        var small = report.Categories["small"];
        Assert.Equal(2, small.ScenarioCount);
        Assert.Equal(3.0, small.MeanGroupCount, 5);  // (2 + 4) / 2
        Assert.Equal(0.40, small.MeanRelationshipDensity, 5); // (0.30 + 0.50) / 2
        Assert.Equal(0.70, small.MeanGroupMembershipRatio, 5); // (0.6 + 0.8) / 2

        var large = report.Categories["large"];
        Assert.Equal(1, large.ScenarioCount);
        Assert.Equal(12.0, large.MeanGroupCount, 5);
    }
}
