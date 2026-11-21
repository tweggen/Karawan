using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-487 (TALE S2b-b): requests, claims and interrupts run in the live game. A populated
 * cluster has its own request pool and encounter model; an NPC's advance emits the request
 * its storylet carries, and an arrival next to someone who can answer it is claimed at once
 * and starts the claimer's storylet as an interrupt.
 */
public class LiveRequestClaimTests
{
    private const int Cluster = 3;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);

    private const string StoryletsJson = """
    [
      { "id": "threaten", "roles": ["thug"], "duration_minutes": 600, "location": "current",
        "postconditions": { "request": { "type": "threat", "location": "current", "urgency": 0.9, "timeout_minutes": 600 } } },
      { "id": "loiter", "roles": ["bystander"], "duration_minutes": 600, "location": "current" },
      { "id": "flee", "roles": ["bystander"], "claim_trigger": { "request_type": "threat", "role_match": [] },
        "duration_minutes": 20, "location": "current" },
      { "id": "wander", "roles": ["fallback_only"], "duration_minutes": 30, "location": "current", "tags": ["fallback"] },
      { "id": "rest", "roles": ["fallback_only"], "duration_minutes": 60, "location": "current", "tags": ["fallback"] }
    ]
    """;

    private static int Id(int i) => NpcSchedule.MakeNpcId(Cluster, i);


    private static TaleManager _populated()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tale-live-" + Guid.NewGuid().ToString("N"));
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

        var model = new SpatialModel();
        model.Locations.Add(new Location { Id = 1, Type = "social_venue", Position = new Vector3(10, 0, 10) });
        model.BuildIndex();

        tm.PopulateClusterWith(Cluster, model, T0, _ =>
        {
            var list = new List<NpcSchedule>();
            string[] roles = { "thug", "bystander" };
            for (int i = 0; i < roles.Length; i++)
            {
                list.Add(new NpcSchedule
                {
                    NpcId = Id(i), NpcIndex = i, ClusterIndex = Cluster, Seed = i, Role = roles[i],
                    HomeLocationId = 1, WorkplaceLocationId = 1, CurrentLocationId = 1,
                    HomePosition = new Vector3(10, 0, 10), CurrentWorldPosition = new Vector3(10, 0, 10),
                    Properties = new Dictionary<string, float>(), Trust = new Dictionary<int, float>(),
                });
            }
            return list;
        });

        // Certain encounters, so the test pins the rules rather than a probability.
        tm.GetLiveEncounters(Cluster).P_Venue = 1f;
        return tm;
    }


    [Fact]
    public void TheWarmUpEmitsNoRequest()
    {
        var tm = _populated();
        Assert.Equal("threaten", tm.GetSchedule(Id(0)).CurrentStorylet);
        Assert.Empty(tm.GetRequestPool(Cluster).Requests);
    }


    [Fact]
    public void AThreatIsClaimedOnArrivalAndTheBystanderFleesThenResumes()
    {
        var tm = _populated();
        var thug = tm.GetSchedule(Id(0));
        var bystander = tm.GetSchedule(Id(1));
        DateTime t = T0.AddHours(11);

        // The bystander arrives first and stands there; nothing to claim yet.
        Assert.Equal("loiter", tm.AdvanceNpc(bystander.NpcId, t).Id);
        Assert.Null(bystander.ArcStack.PendingInterruptStorylet);

        // The thug arrives, threatens, and the bystander already there claims it at once.
        Assert.Equal("threaten", tm.AdvanceNpc(thug.NpcId, t).Id);
        var request = Assert.Single(tm.GetRequestPool(Cluster).Requests.Values);
        Assert.Equal("threat", request.Type);
        Assert.Equal(bystander.NpcId, request.ClaimerId);
        Assert.Equal("flee", bystander.ArcStack.PendingInterruptStorylet);
        Assert.Equal(InterruptScope.Nest, bystander.ArcStack.PendingInterruptScope);

        // Its next advance runs the interrupt and pauses the loitering, which then resumes.
        Assert.Equal("flee", tm.AdvanceNpc(bystander.NpcId, t.AddMinutes(1)).Id);
        Assert.Single(bystander.ArcStack.PausedArcs);
        Assert.Equal("loiter", tm.AdvanceNpc(bystander.NpcId, t.AddMinutes(25)).Id);
        Assert.Empty(bystander.ArcStack.PausedArcs);
    }


    [Fact]
    public void DepopulatingDropsTheLiveState()
    {
        var tm = _populated();
        tm.AdvanceNpc(Id(0), T0.AddHours(11));
        Assert.NotNull(tm.GetRequestPool(Cluster));
        tm.DepopulateCluster(Cluster);
        Assert.Null(tm.GetRequestPool(Cluster));
        Assert.Null(tm.GetLiveEncounters(Cluster));
    }
}
