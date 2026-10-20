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
/// TALE S0 W8 (KAR-451): the ImGui panel's snapshot. Nine NPCs in cluster 5:
/// group 1 = NPCs 0..3, group 2 = NPCs 4..5, group 3 = NPCs 6..7, NPC 8 in no
/// group. All share one social venue, which becomes every group's hangout.
/// </summary>
public class TaleDebugSnapshotTests
{
    private const int Cluster = 5;
    private const int NpcCount = 9;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);
    private static readonly Vector3 Home = new(10, 0, 10);
    private static readonly Vector3 Venue = new(40, 0, 10);

    private const string StoryletsJson = """
    [
      { "id": "idle_test", "roles": ["tester"], "duration_minutes": 30, "location": "current" },
      { "id": "wander", "roles": ["fallback_only"], "duration_minutes": 30, "location": "current" },
      { "id": "rest", "roles": ["fallback_only"], "duration_minutes": 60, "location": "current" }
    ]
    """;

    private static int Id(int npcIndex) => NpcSchedule.MakeNpcId(Cluster, npcIndex);

    private static int GroupOf(int npcIndex) => npcIndex switch
    {
        <= 3 => 1,
        <= 5 => 2,
        <= 7 => 3,
        _ => -1
    };

    private static SpatialModel MakeModel()
    {
        var model = new SpatialModel();
        model.Locations.Add(new Location { Id = 1, Type = "home", Position = Home, EntryPosition = Home });
        model.Locations.Add(new Location { Id = 2, Type = "social_venue", Position = Venue, EntryPosition = Venue });
        model.BuildIndex();
        return model;
    }

    private static TaleManager MakeManager()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tale-w8-" + Guid.NewGuid().ToString("N"));
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
        tm.GroupNameProvider = (cluster, groupId, type) => $"G{groupId}";
        return tm;
    }

    private static List<NpcSchedule> Generate(HashSet<int> skip)
    {
        var list = new List<NpcSchedule>();
        for (int i = 0; i < NpcCount; i++)
        {
            if (skip != null && skip.Contains(i)) continue;
            list.Add(new NpcSchedule
            {
                NpcId = Id(i), NpcIndex = i, ClusterIndex = Cluster, Seed = i, Role = "tester",
                HomeLocationId = 1, WorkplaceLocationId = 1, CurrentLocationId = 1,
                SocialVenueIds = new List<int> { 2 },
                HomePosition = Home, CurrentWorldPosition = Home,
                Properties = new Dictionary<string, float>(),
                Trust = new Dictionary<int, float>(),
            });
        }
        return list;
    }

    /*
     * Members of a group trust each other 0.7; groups 1 and 2 know each other
     * through NPCs 0 and 4 (0.2 / 0.4, mean 0.3).
     */
    private static List<ScenarioApplicator.AppliedGroup> Bake(List<NpcSchedule> fresh)
    {
        foreach (var npc in fresh)
        {
            npc.GroupId = GroupOf(npc.NpcIndex);
            foreach (var other in fresh)
            {
                if (other != npc && npc.GroupId >= 0 && GroupOf(other.NpcIndex) == npc.GroupId)
                    npc.Trust[other.NpcId] = 0.7f;
            }
        }
        fresh[0].Trust[Id(4)] = 0.2f;
        fresh[4].Trust[Id(0)] = 0.4f;

        return new[] { 1, 2, 3 }.Select(g => new ScenarioApplicator.AppliedGroup
        {
            GroupRank = g,
            Type = g == 1 ? "trade" : "social",
            RealMemberIds = fresh.Where(n => n.GroupId == g).Select(n => n.NpcId).ToList()
        }).ToList();
    }

    private static TaleManager Populated()
    {
        var tm = MakeManager();
        tm.PopulateClusterWith(Cluster, MakeModel(), T0, Generate, Bake);
        return tm;
    }


    [Fact]
    public void Groups_AreSortedBySize_ThenId_WithNameTypeAndHangout()
    {
        var tm = Populated();
        var snap = TaleDebugSnapshotBuilder.Build(tm, Cluster, T0, Home, -1);

        Assert.Equal(new[] { 1, 2, 3 }, snap.Groups.Select(g => g.GroupId));
        Assert.Equal(new[] { 4, 2, 2 }, snap.Groups.Select(g => g.MemberIds.Count));
        Assert.Equal("G1", snap.Groups[0].Name);
        Assert.Equal("trade", snap.Groups[0].Type);
        Assert.Equal(2, snap.Groups[0].HangoutLocationId);
        Assert.Equal(30f, snap.Groups[0].HangoutDistance!.Value, 3);
        Assert.Equal(NpcCount, snap.NpcCount);
        Assert.Null(snap.Selected);
    }


    [Fact]
    public void PlayerStanding_IsSortedByTrust_AndFeedsGroupStanding()
    {
        var tm = Populated();
        tm.GetSchedule(Id(1)).Trust[-1] = 0.2f;
        tm.GetSchedule(Id(5)).Trust[-1] = 0.8f;
        tm.GetSchedule(Id(2)).Trust[-1] = 0.5f;

        var snap = TaleDebugSnapshotBuilder.Build(tm, Cluster, T0, Home, -1);

        Assert.Equal(new[] { Id(5), Id(2), Id(1) }, snap.PlayerStanding.Select(s => s.NpcId));
        Assert.Equal(3, snap.KnownToPlayer);
        var g1 = snap.Groups.Single(g => g.GroupId == 1);
        Assert.Equal(2, g1.MembersWhoMetPlayer);
        Assert.Equal(0.35, g1.MeanPlayerTrust!.Value, 5);
        Assert.Null(snap.Groups.Single(g => g.GroupId == 3).MeanPlayerTrust);
    }


    [Fact]
    public void Matrix_EqualsTheSharedMetrics()
    {
        var tm = Populated();
        var snap = TaleDebugSnapshotBuilder.Build(tm, Cluster, T0, Home, -1);
        var metrics = tm.ComputeSocialMetrics(Cluster);

        Assert.Equal(metrics.ToLogLine(), snap.Metrics.ToLogLine());
        Assert.Equal(new[] { 1, 2, 3 }, snap.MatrixGroupIds);
        for (int i = 0; i < 3; i++)
        {
            Assert.Null(snap.InterGroupMatrix[i][i]);
            for (int j = 0; j < 3; j++)
            {
                if (i == j) continue;
                int a = snap.MatrixGroupIds[i], b = snap.MatrixGroupIds[j];
                string key = $"{Math.Min(a, b)}-{Math.Max(a, b)}";
                double? expected = metrics.InterGroupTrust.TryGetValue(key, out double v) ? v : null;
                Assert.Equal(expected, snap.InterGroupMatrix[i][j]);
            }
        }
        Assert.Equal(0.3, snap.InterGroupMatrix[0][1]!.Value, 5);
        Assert.Null(snap.InterGroupMatrix[0][2]);
    }


    [Fact]
    public void Matrix_TakesTheTwelveLargestGroups()
    {
        var ids = Enumerable.Range(1, 20).ToList();
        var metrics = new SocialMetrics();
        metrics.InterGroupTrust["3-17"] = 0.25;

        var matrix = TaleDebugSnapshotBuilder.Matrix(ids.Take(TaleDebugSnapshot.MatrixSize).ToList(), metrics);
        Assert.Equal(12, matrix.Count);
        Assert.All(matrix, row => Assert.Equal(12, row.Count));
        Assert.All(matrix.SelectMany(r => r), Assert.Null);

        var withPair = TaleDebugSnapshotBuilder.Matrix(new[] { 17, 3 }, metrics);
        Assert.Equal(0.25, withPair[0][1]);
        Assert.Equal(0.25, withPair[1][0]);
    }


    [Fact]
    public void SelectedNpc_CarriesScheduleTrustGroupAndFlags()
    {
        var tm = Populated();
        var npc = tm.GetSchedule(Id(0));
        npc.Properties["wealth"] = 0.4f;
        npc.Properties["fear"] = 0.1f;
        npc.Trust[-1] = 0.6f;
        for (int i = 0; i < 12; i++) npc.Trust[NpcSchedule.MakeNpcId(Cluster, 100 + i)] = 0.01f * i;
        tm.SetMaterialized(Id(0));

        var snap = TaleDebugSnapshotBuilder.Build(tm, Cluster, T0, Home, Id(0));
        var sel = snap.Selected;

        Assert.NotNull(sel);
        Assert.Equal(Id(0), sel.NpcId);
        Assert.Equal("tester", sel.Role);
        Assert.Equal(npc.CurrentStorylet, sel.CurrentStorylet);
        Assert.Equal("home", sel.CurrentLocationType);
        Assert.Equal(1, sel.GroupId);
        Assert.Equal("G1", sel.GroupName);
        Assert.Equal("trade", sel.GroupType);
        Assert.Equal(0.6f, sel.PlayerTrust);
        Assert.True(sel.IsMaterialized);
        Assert.True(sel.IsKnownToPlayer);
        Assert.False(sel.IsTier2);

        var keys = sel.Properties.Select(kv => kv.Key).ToList();
        Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal), keys);
        Assert.Contains(new KeyValuePair<string, float>("wealth", 0.4f), sel.Properties);

        // The player's entry is not among the NPC trust; strongest first.
        Assert.Equal(TaleDebugSnapshot.StrongestTrustCount, sel.StrongestTrust.Count);
        Assert.DoesNotContain(sel.StrongestTrust, kv => kv.Key == -1);
        Assert.Equal(0.7f, sel.StrongestTrust[0].Value);
        Assert.Equal(sel.StrongestTrust.Select(kv => kv.Value).OrderByDescending(v => v),
            sel.StrongestTrust.Select(kv => kv.Value));

        Assert.Null(TaleDebugSnapshotBuilder.Build(tm, Cluster, T0, Home, Id(77)).Selected);
    }


    [Fact]
    public void Snapshot_IsUnchanged_AfterTheManagerChanges()
    {
        var tm = Populated();
        tm.GetSchedule(Id(1)).Trust[-1] = 0.2f;
        var snap = TaleDebugSnapshotBuilder.Build(tm, Cluster, T0, Home, Id(1));
        string metricsBefore = snap.Metrics.ToLogLine();
        var membersBefore = snap.Groups.Select(g => string.Join(",", g.MemberIds)).ToList();
        var propsBefore = snap.Selected.Properties.Count;
        var trustBefore = snap.Selected.StrongestTrust.ToList();

        // Change everything the snapshot copied.
        var npc = tm.GetSchedule(Id(1));
        npc.Trust[-1] = 0.9f;
        npc.Trust[Id(8)] = 0.95f;
        npc.Properties["new_prop"] = 1f;
        npc.GroupId = -1;
        tm.GetGroup(Cluster, 1).MemberIds.Remove(Id(1));
        tm.GetGroup(Cluster, 2).Name = "renamed";
        tm.RedetectGroups(Cluster, new RuntimeSocialEvolver(), tm.GroupNameProvider);

        Assert.Equal(metricsBefore, snap.Metrics.ToLogLine());
        Assert.Equal(membersBefore, snap.Groups.Select(g => string.Join(",", g.MemberIds)));
        Assert.Equal("G2", snap.Groups.Single(g => g.GroupId == 2).Name);
        Assert.Equal(0.2f, snap.PlayerStanding.Single().Trust);
        Assert.Equal(0.2f, snap.Selected.PlayerTrust);
        Assert.Equal(1, snap.Selected.GroupId);
        Assert.Equal(propsBefore, snap.Selected.Properties.Count);
        Assert.Equal(trustBefore, snap.Selected.StrongestTrust);
    }


    [Fact]
    public void RedetectGroups_RewritesTheTable_AndIgnoresUnknownClusters()
    {
        var tm = Populated();
        Assert.Null(tm.RedetectGroups(99, new RuntimeSocialEvolver(), null));

        var stats = tm.RedetectGroups(Cluster, new RuntimeSocialEvolver(), tm.GroupNameProvider);
        Assert.NotNull(stats);
        Assert.Equal(0, tm.ComputeSocialMetrics(Cluster).GroupTableMismatch);
        Assert.Equal(tm.CopyGroupTable(Cluster).Keys.OrderBy(k => k),
            tm.GetSocialState(Cluster).Groups.Keys.OrderBy(k => k));
    }


    [Fact]
    public void EmptyCluster_GivesAnEmptySnapshot()
    {
        var tm = MakeManager();
        var snap = TaleDebugSnapshotBuilder.Build(tm, 3, T0, Vector3.Zero, -1);

        Assert.Equal(0, snap.NpcCount);
        Assert.Empty(snap.Groups);
        Assert.Empty(snap.MatrixGroupIds);
        Assert.Empty(snap.PlayerStanding);
        Assert.Empty(snap.ChurnPerDay);
        Assert.Empty(snap.PopulatedClusters);
    }
}
