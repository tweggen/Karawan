using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// The storylet group verb fires through the public AdvanceNpc (KAR-433, S0
/// W1): finishing a "group": "form" storylet founds a group. Drop the verb
/// call in AdvanceNpc and this fails.
/// </summary>
public class AdvanceNpcGroupVerbTests
{
    private const int Cluster = 6;
    private const int NpcCount = 4;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);

    private const string StoryletsJson = """
    [
      { "id": "idle_test", "roles": ["founder"], "duration_minutes": 30, "location": "current" },
      { "id": "form_test", "roles": ["nobody"], "duration_minutes": 10, "location": "current",
        "postconditions": { "group": "form" } },
      { "id": "wander", "roles": ["fallback_only"], "duration_minutes": 30, "location": "current" },
      { "id": "rest", "roles": ["fallback_only"], "duration_minutes": 60, "location": "current" }
    ]
    """;

    private static int Id(int i) => NpcSchedule.MakeNpcId(Cluster, i);

    private static TaleManager MakeManager()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tale-w1-" + Guid.NewGuid().ToString("N"));
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

    private static List<NpcSchedule> Generate(HashSet<int> skip)
    {
        var list = new List<NpcSchedule>();
        for (int i = 0; i < NpcCount; i++)
        {
            var npc = new NpcSchedule
            {
                NpcId = Id(i), NpcIndex = i, ClusterIndex = Cluster, Seed = i, Role = "founder",
                HomeLocationId = 1, WorkplaceLocationId = 1, CurrentLocationId = 1,
                HomePosition = new Vector3(10, 0, 10), CurrentWorldPosition = new Vector3(10, 0, 10),
                Properties = new Dictionary<string, float>(),
                Trust = new Dictionary<int, float>(),
            };
            for (int j = 0; j < NpcCount; j++)
                if (j != i) npc.Trust[Id(j)] = 0.9f;
            list.Add(npc);
        }
        return list;
    }


    [Fact]
    public void FinishingAFormStorylet_FoundsAGroup()
    {
        var tm = MakeManager();
        var model = new SpatialModel();
        model.Locations.Add(new Location { Id = 1, Type = "home", Position = new Vector3(10, 0, 10) });
        model.BuildIndex();
        tm.PopulateClusterWith(Cluster, model, T0, Generate);
        Assert.Empty(tm.GetSocialState(Cluster)?.Groups ?? new Dictionary<int, RuntimeGroup>());

        var founder = tm.GetSchedule(Id(0));
        founder.CurrentStorylet = "form_test";
        founder.CurrentEnd = T0.AddHours(1);
        tm.AdvanceNpc(Id(0), T0.AddHours(1));

        var group = Assert.Single(tm.GetSocialState(Cluster).Groups.Values);
        Assert.True(group.Authored);
        Assert.Contains(Id(0), group.MemberIds);
        Assert.True(group.MemberIds.Count >= 3);
        Assert.Equal(group.GroupId, founder.GroupId);
    }
}
