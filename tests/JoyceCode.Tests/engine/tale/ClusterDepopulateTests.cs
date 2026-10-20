using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.tale;
using engine.tale.bake;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S0 W4 (KAR-434): leaving a cluster keeps its social state. Populating
/// goes through the PopulateClusterWith seam, so no ClusterDesc is needed;
/// the generator stands in for TalePopulationGenerator and honours the skip
/// mask, and the "bake" stands in for the scenario applicator.
/// </summary>
public class ClusterDepopulateTests
{
    private const int Cluster = 4;
    private const int NpcCount = 6;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);

    private const string StoryletsJson = """
    [
      { "id": "idle_test", "roles": ["tester"], "duration_minutes": 30, "location": "current" },
      { "id": "form_test", "roles": ["founder"], "duration_minutes": 10, "location": "current",
        "postconditions": { "group": "form" } },
      { "id": "wander", "roles": ["fallback_only"], "duration_minutes": 30, "location": "current" },
      { "id": "rest", "roles": ["fallback_only"], "duration_minutes": 60, "location": "current" }
    ]
    """;

    private static int Id(int npcIndex) => NpcSchedule.MakeNpcId(Cluster, npcIndex);

    private static SpatialModel MakeModel()
    {
        var model = new SpatialModel();
        model.Locations.Add(new Location { Id = 1, Type = "home", Position = new Vector3(10, 0, 10) });
        model.BuildIndex();
        return model;
    }

    private static TaleManager MakeManager()
    {
        // LoadFromString alone builds no id index (the loaders do), and
        // without it AdvanceNpc cannot see a storylet's group verb.
        var dir = Path.Combine(Path.GetTempPath(), "tale-w4-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var library = new StoryletLibrary();
        try
        {
            File.WriteAllText(Path.Combine(dir, "test.json"), StoryletsJson);
            library.LoadFromDirectory(dir);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
        var tm = new TaleManager();
        tm.Initialize(library);
        return tm;
    }

    /*
     * Fresh schedules for every index not in the skip mask, as the real
     * generator makes them. Founders already trust each other, so the
     * form_test verb would succeed if it fired.
     */
    private static Func<HashSet<int>, List<NpcSchedule>> Generator(string role = "tester") => skip =>
    {
        var list = new List<NpcSchedule>();
        for (int i = 0; i < NpcCount; i++)
        {
            if (skip != null && skip.Contains(i)) continue;
            var npc = new NpcSchedule
            {
                NpcId = Id(i), NpcIndex = i, ClusterIndex = Cluster, Seed = i, Role = role,
                HomeLocationId = 1, WorkplaceLocationId = 1, CurrentLocationId = 1,
                HomePosition = new Vector3(10, 0, 10), CurrentWorldPosition = new Vector3(10, 0, 10),
                Properties = new Dictionary<string, float>(),
                Trust = new Dictionary<int, float>(),
            };
            if (role == "founder")
            {
                for (int j = 0; j < NpcCount; j++)
                    if (j != i) npc.Trust[Id(j)] = 0.9f;
            }
            list.Add(npc);
        }
        return list;
    };

    /*
     * The bake: NPCs 0..2 form group 1 and trust each other.
     */
    private static List<ScenarioApplicator.AppliedGroup> Bake(List<NpcSchedule> fresh)
    {
        var members = new List<int>();
        foreach (var npc in fresh)
        {
            if (npc.NpcIndex > 2) continue;
            npc.GroupId = 1;
            for (int j = 0; j <= 2; j++)
                if (j != npc.NpcIndex) npc.Trust[Id(j)] = 0.5f;
            members.Add(npc.NpcId);
        }
        return new List<ScenarioApplicator.AppliedGroup>
        {
            new() { GroupRank = 1, Type = "social", RealMemberIds = members }
        };
    }

    private static void Populate(TaleManager tm, Func<HashSet<int>, List<NpcSchedule>> generate = null)
        => tm.PopulateClusterWith(Cluster, MakeModel(), T0, generate ?? Generator(), Bake);


    [Fact]
    public void LeaveAndReturn_RestoresTrustGroupAndFacts()
    {
        var tm = MakeManager();
        Populate(tm);
        Assert.True(tm.IsClusterPopulated(Cluster));

        var known = tm.GetSchedule(Id(0));
        known.Trust[-1] = 0.6f;
        known.Properties["player_fact.helped"] = 1f;
        tm.MarkPlayerContact(known);

        var primary = tm.GetSchedule(Id(1));
        primary.Trust[Id(2)] = 0.9f;              // evolved since the bake
        tm.SetMaterialized(Id(1));

        tm.DepopulateCluster(Cluster);

        Assert.False(tm.IsClusterPopulated(Cluster));
        Assert.Null(tm.GetSchedule(Id(1)));
        Assert.Same(known, tm.GetSchedule(Id(0)));
        Assert.False(tm.IsMaterialized(Id(1)));
        Assert.Contains(0, tm.GetDeviationSkipMask(Cluster));

        Populate(tm);

        // The known NPC is the same object, with its trust and facts.
        Assert.Same(known, tm.GetSchedule(Id(0)));
        Assert.Equal(0.6f, known.Trust[-1]);
        Assert.Equal(1f, known.Properties["player_fact.helped"]);
        Assert.Equal(1, known.GroupId);

        // The primary NPC is regenerated, with its evolved trust and group back.
        var back = tm.GetSchedule(Id(1));
        Assert.NotSame(primary, back);
        Assert.Equal(0.9f, back.Trust[Id(2)]);
        Assert.Equal(1, back.GroupId);
        Assert.Equal(NpcCount, tm.GetNpcsInCluster(Cluster).Count);

        // Table and schedules agree.
        Assert.Equal(0, tm.ComputeSocialMetrics(Cluster).GroupTableMismatch);
        Assert.Equal(new[] { Id(0), Id(1), Id(2) }, tm.GetGroup(Cluster, 1).MemberIds.OrderBy(i => i));
    }


    [Fact]
    public void Depopulate_KeepsAnyKnownToPlayer()
    {
        var tm = MakeManager();
        Populate(tm);

        var flagged = tm.GetSchedule(Id(0));
        tm.MarkPlayerContact(flagged);
        var trusted = tm.GetSchedule(Id(1));
        trusted.Trust[-1] = 0.2f;                 // written without MarkPlayerContact
        var told = tm.GetSchedule(Id(2));
        told.Properties["player_fact.owes"] = 1f; // likewise

        tm.DepopulateCluster(Cluster);

        Assert.Same(flagged, tm.GetSchedule(Id(0)));
        Assert.Same(trusted, tm.GetSchedule(Id(1)));
        Assert.Same(told, tm.GetSchedule(Id(2)));
        Assert.Null(tm.GetSchedule(Id(3)));
        Assert.Equal(2, tm.UnflaggedPlayerContacts);
        var mask = tm.GetDeviationSkipMask(Cluster);
        Assert.True(new[] { 0, 1, 2 }.All(mask.Contains));
        Assert.Equal(3, tm.GetNpcsInCluster(Cluster).Count);
    }


    [Fact]
    public void WarmUp_FiresNoGroupVerb_SoTheBakeIsSeeded()
    {
        var tm = MakeManager();
        Populate(tm, Generator("founder"));

        var social = tm.GetSocialState(Cluster);
        Assert.Single(social.Groups);
        var group = social.Groups[1];
        Assert.False(group.Authored);
        Assert.Equal(new[] { Id(0), Id(1), Id(2) }, group.MemberIds.OrderBy(i => i));
        Assert.Equal(0, tm.ComputeSocialMetrics(Cluster).GroupTableMismatch);
    }


    [Fact]
    public void ReturnVisit_DoesNotReseedDissolvedGroups()
    {
        var tm = MakeManager();
        Populate(tm);

        // The group dissolved while the player was here.
        var social = tm.GetSocialState(Cluster);
        social.Groups.Clear();
        foreach (var npc in tm.GetNpcsInCluster(Cluster)) npc.GroupId = -1;

        tm.DepopulateCluster(Cluster);
        Populate(tm);

        Assert.Empty(tm.GetSocialState(Cluster).Groups);
        Assert.All(tm.GetNpcsInCluster(Cluster), npc => Assert.Equal(-1, npc.GroupId));
        Assert.Equal(0, tm.ComputeSocialMetrics(Cluster).GroupTableMismatch);
    }
}
