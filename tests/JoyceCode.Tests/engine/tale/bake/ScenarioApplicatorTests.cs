using System.Collections.Generic;
using System.Linq;
using engine.tale;
using engine.tale.bake;
using Xunit;

namespace JoyceCode.Tests.engine.tale.bake;

/// <summary>
/// ScenarioApplicator unit tests. The applicator is the part of the
/// TALE-SOCIAL pipeline that physically rewrites real cluster NPCs from
/// baked scenario data, so its determinism and edge-case handling are the
/// linchpin of the seedability story. Each test builds a small synthetic
/// Scenario + NpcSchedule list inline — no DI, no engine boot, no I/O.
/// </summary>
public class ScenarioApplicatorTests
{
    private static NpcSchedule MakeNpc(int id, string role, float wealth, float morality)
    {
        return new NpcSchedule
        {
            NpcId = id,
            Seed = id,
            Role = role,
            Properties = new Dictionary<string, float>
            {
                { "morality", morality },
                { "wealth", wealth },
                { "fear", 0f },
                { "anger", 0f },
                { "reputation", 0.5f }
            },
            Trust = new Dictionary<int, float>()
        };
    }

    private static ScenarioNpc MakeScenarioNpc(int rank, string role, float wealth, float morality, int groupRank = -1)
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
                { "fear", 0.1f },
                { "anger", 0.2f },
                { "reputation", 0.3f }
            }
        };
    }

    [Fact]
    public void Apply_NullScenario_ReturnsEmptyResult()
    {
        var applicator = new ScenarioApplicator();
        var realNpcs = new List<NpcSchedule> { MakeNpc(1, "worker", 0.5f, 0.5f) };
        var result = applicator.Apply(null, realNpcs);
        Assert.Equal(0, result.MatchedNpcCount);
        Assert.Equal(0, result.RelationshipsApplied);
    }

    [Fact]
    public void Apply_EmptyRealNpcs_ReturnsEmptyResult()
    {
        var applicator = new ScenarioApplicator();
        var scenario = new Scenario { Category = "test", Index = 0 };
        var result = applicator.Apply(scenario, new List<NpcSchedule>());
        Assert.Equal(0, result.MatchedNpcCount);
    }

    [Fact]
    public void Apply_SimplePair_ReportsMatchAndCopiesWealth()
    {
        var scenario = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 1,
            Npcs = { MakeScenarioNpc(0, "worker", wealth: 0.9f, morality: 0.8f) }
        };
        var realNpcs = new List<NpcSchedule>
        {
            MakeNpc(100, "worker", wealth: 0.1f, morality: 0.2f)
        };

        var result = new ScenarioApplicator().Apply(scenario, realNpcs);

        Assert.Equal(1, result.MatchedNpcCount);
        Assert.Equal(0, result.UnmatchedScenarioRanks);
        Assert.Equal(0, result.UnmatchedRealNpcs);
        Assert.Equal(0.9f, realNpcs[0].Properties["wealth"], 5);
        Assert.Equal(0.8f, realNpcs[0].Properties["morality"], 5);
        // Property-by-property coverage of all five overwritten fields lives
        // in Apply_OverwritesAllSocialProperties below.
    }

    [Fact]
    public void Apply_OverwritesAllSocialProperties()
    {
        var scenario = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 1,
            Npcs = { MakeScenarioNpc(0, "worker", wealth: 0.9f, morality: 0.8f) }
        };
        var realNpcs = new List<NpcSchedule>
        {
            MakeNpc(100, "worker", wealth: 0.1f, morality: 0.2f)
        };

        new ScenarioApplicator().Apply(scenario, realNpcs);

        // All five overwritten properties (morality, wealth, fear, anger, reputation)
        // should now hold the scenario's values, not the real NPC's pre-existing ones.
        Assert.Equal(0.9f, realNpcs[0].Properties["wealth"], 5);
        Assert.Equal(0.8f, realNpcs[0].Properties["morality"], 5);
        Assert.Equal(0.1f, realNpcs[0].Properties["fear"], 5);
        Assert.Equal(0.2f, realNpcs[0].Properties["anger"], 5);
        Assert.Equal(0.3f, realNpcs[0].Properties["reputation"], 5);
    }

    [Fact]
    public void Apply_PairsByWealthDescendingWithinRole()
    {
        // Three scenario workers (high/mid/low wealth), three real workers
        // (high/mid/low wealth in REVERSE order). After matching, the
        // highest-wealth scenario should land on the highest-wealth real,
        // regardless of NpcId order.
        var scenario = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 3,
            Npcs =
            {
                MakeScenarioNpc(0, "worker", wealth: 0.1f, morality: 0.5f, groupRank: 5),
                MakeScenarioNpc(1, "worker", wealth: 0.9f, morality: 0.5f, groupRank: 6),
                MakeScenarioNpc(2, "worker", wealth: 0.5f, morality: 0.5f, groupRank: 7)
            }
        };
        // Real NPCs created with NpcIds 100/101/102 but reverse-sorted wealth.
        var realNpcs = new List<NpcSchedule>
        {
            MakeNpc(100, "worker", wealth: 0.2f, morality: 0.5f),  // lowest
            MakeNpc(101, "worker", wealth: 0.8f, morality: 0.5f),  // highest
            MakeNpc(102, "worker", wealth: 0.5f, morality: 0.5f)   // middle
        };

        new ScenarioApplicator().Apply(scenario, realNpcs);

        // The richest real (NpcId 101) should now have the richest scenario's
        // GroupRank (6, from rank 1 with wealth 0.9).
        var richest = realNpcs.First(n => n.NpcId == 101);
        Assert.Equal(6, richest.GroupId);

        // The poorest real (NpcId 100) should have GroupId 5 (rank 0, wealth 0.1).
        var poorest = realNpcs.First(n => n.NpcId == 100);
        Assert.Equal(5, poorest.GroupId);

        // The middle real (NpcId 102) should have GroupId 7 (rank 2, wealth 0.5).
        var middle = realNpcs.First(n => n.NpcId == 102);
        Assert.Equal(7, middle.GroupId);
    }

    [Fact]
    public void Apply_GroupRankMinusOne_LeavesGroupIdUnset()
    {
        var scenario = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 1,
            Npcs = { MakeScenarioNpc(0, "worker", 0.5f, 0.5f, groupRank: -1) }
        };
        var realNpcs = new List<NpcSchedule> { MakeNpc(100, "worker", 0.5f, 0.5f) };

        new ScenarioApplicator().Apply(scenario, realNpcs);

        Assert.Equal(-1, realNpcs[0].GroupId); // default, untouched
    }

    [Fact]
    public void Apply_RelationshipBothDirections()
    {
        var scenario = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 2,
            Npcs =
            {
                MakeScenarioNpc(0, "worker", 0.9f, 0.5f),
                MakeScenarioNpc(1, "worker", 0.1f, 0.5f)
            },
            Relationships =
            {
                new ScenarioRelationship
                {
                    FromRank = 0, ToRank = 1,
                    TrustAtoB = 0.85f, TrustBtoA = 0.42f,
                    InteractionCount = 99
                }
            }
        };
        var realNpcs = new List<NpcSchedule>
        {
            MakeNpc(100, "worker", wealth: 0.9f, morality: 0.5f), // matches rank 0
            MakeNpc(101, "worker", wealth: 0.1f, morality: 0.5f)  // matches rank 1
        };

        var result = new ScenarioApplicator().Apply(scenario, realNpcs);

        Assert.Equal(1, result.RelationshipsApplied);
        Assert.Equal(0, result.RelationshipsSkipped);
        Assert.Equal(0.85f, realNpcs[0].Trust[101], 5);
        Assert.Equal(0.42f, realNpcs[1].Trust[100], 5);
    }

    [Fact]
    public void Apply_ScenarioRoleOverflow_DropsExtraRanks()
    {
        // Scenario has 3 drifters, real cluster has 2. The third scenario
        // drifter should be reported as unmatched, no exception, the two
        // matched pairs should still get their properties copied.
        var scenario = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 3,
            Npcs =
            {
                MakeScenarioNpc(0, "drifter", 0.8f, 0.4f),
                MakeScenarioNpc(1, "drifter", 0.5f, 0.4f),
                MakeScenarioNpc(2, "drifter", 0.2f, 0.4f)
            }
        };
        var realNpcs = new List<NpcSchedule>
        {
            MakeNpc(200, "drifter", wealth: 0.6f, morality: 0.5f),
            MakeNpc(201, "drifter", wealth: 0.3f, morality: 0.5f)
        };

        var result = new ScenarioApplicator().Apply(scenario, realNpcs);

        Assert.Equal(2, result.MatchedNpcCount);
        Assert.Equal(1, result.UnmatchedScenarioRanks);
        Assert.Equal(0, result.UnmatchedRealNpcs);
        Assert.Equal(2, result.MatchedByRole["drifter"]);
    }

    [Fact]
    public void Apply_RealRoleOverflow_LeavesUnmatchedRealNpcs()
    {
        // Scenario has 1 worker, real cluster has 3. Two real workers should
        // be reported as unmatched and keep their original properties.
        var scenario = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 1,
            Npcs = { MakeScenarioNpc(0, "worker", 0.9f, 0.8f) }
        };
        var realNpcs = new List<NpcSchedule>
        {
            MakeNpc(100, "worker", wealth: 0.5f, morality: 0.5f),
            MakeNpc(101, "worker", wealth: 0.4f, morality: 0.4f),
            MakeNpc(102, "worker", wealth: 0.3f, morality: 0.3f)
        };

        var result = new ScenarioApplicator().Apply(scenario, realNpcs);

        Assert.Equal(1, result.MatchedNpcCount);
        Assert.Equal(0, result.UnmatchedScenarioRanks);
        Assert.Equal(2, result.UnmatchedRealNpcs);
        // The richest real worker (NpcId 100) gets the scenario worker's
        // properties; the other two keep their originals.
        var richest = realNpcs.First(n => n.NpcId == 100);
        Assert.Equal(0.9f, richest.Properties["wealth"], 5);
        var unmatched = realNpcs.First(n => n.NpcId == 102);
        Assert.Equal(0.3f, unmatched.Properties["wealth"], 5);
    }

    [Fact]
    public void Apply_RelationshipCrossingUnmatchedRank_IsSkipped()
    {
        // Scenario has 3 workers, real cluster has 2. The relationship
        // (rank 0 ↔ rank 2) crosses the unmatched rank 2 and should be
        // skipped + counted; (rank 0 ↔ rank 1) is fully matched and applied.
        var scenario = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 3,
            Npcs =
            {
                MakeScenarioNpc(0, "worker", 0.9f, 0.5f),
                MakeScenarioNpc(1, "worker", 0.5f, 0.5f),
                MakeScenarioNpc(2, "worker", 0.1f, 0.5f)
            },
            Relationships =
            {
                new ScenarioRelationship { FromRank = 0, ToRank = 1, TrustAtoB = 0.7f, TrustBtoA = 0.6f },
                new ScenarioRelationship { FromRank = 0, ToRank = 2, TrustAtoB = 0.5f, TrustBtoA = 0.4f }
            }
        };
        var realNpcs = new List<NpcSchedule>
        {
            MakeNpc(100, "worker", wealth: 0.8f, morality: 0.5f),
            MakeNpc(101, "worker", wealth: 0.4f, morality: 0.5f)
        };

        var result = new ScenarioApplicator().Apply(scenario, realNpcs);

        Assert.Equal(1, result.RelationshipsApplied);
        Assert.Equal(1, result.RelationshipsSkipped);
    }

    [Fact]
    public void Apply_DeterministicAcrossCalls()
    {
        // Same inputs → same NPC state. The "seedability validation" the
        // D4 plan is built around: if Apply ever picks up an unseeded
        // dependency (e.g. iterates a non-deterministic dictionary), this
        // test catches it.
        var scenario = new Scenario
        {
            Category = "test", Index = 0, NpcCount = 4,
            Npcs =
            {
                MakeScenarioNpc(0, "worker", 0.9f, 0.8f, groupRank: 1),
                MakeScenarioNpc(1, "drifter", 0.2f, 0.4f, groupRank: 2),
                MakeScenarioNpc(2, "worker", 0.5f, 0.6f, groupRank: 1),
                MakeScenarioNpc(3, "merchant", 0.7f, 0.7f, groupRank: 3)
            },
            Relationships =
            {
                new ScenarioRelationship { FromRank = 0, ToRank = 2, TrustAtoB = 0.85f, TrustBtoA = 0.85f }
            }
        };

        // Build two parallel populations.
        var realNpcsA = new List<NpcSchedule>
        {
            MakeNpc(100, "worker", 0.5f, 0.5f),
            MakeNpc(101, "drifter", 0.3f, 0.4f),
            MakeNpc(102, "worker", 0.7f, 0.6f),
            MakeNpc(103, "merchant", 0.6f, 0.6f)
        };
        var realNpcsB = new List<NpcSchedule>
        {
            MakeNpc(100, "worker", 0.5f, 0.5f),
            MakeNpc(101, "drifter", 0.3f, 0.4f),
            MakeNpc(102, "worker", 0.7f, 0.6f),
            MakeNpc(103, "merchant", 0.6f, 0.6f)
        };

        new ScenarioApplicator().Apply(scenario, realNpcsA);
        new ScenarioApplicator().Apply(scenario, realNpcsB);

        for (int i = 0; i < realNpcsA.Count; i++)
        {
            Assert.Equal(realNpcsA[i].GroupId, realNpcsB[i].GroupId);
            Assert.Equal(realNpcsA[i].Properties["wealth"], realNpcsB[i].Properties["wealth"], 5);
            Assert.Equal(realNpcsA[i].Properties["morality"], realNpcsB[i].Properties["morality"], 5);
            Assert.Equal(realNpcsA[i].Trust.Count, realNpcsB[i].Trust.Count);
        }
    }
}
