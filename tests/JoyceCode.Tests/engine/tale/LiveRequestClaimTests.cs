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
      { "id": "demand", "roles": ["extortionist"], "duration_minutes": 600, "location": "current",
        "interrupt_priority": 6,
        "postconditions_if": [
          { "condition": { "target_fear": { "max": 0.5 } }, "then": { }, "storylet_next": "threaten" }
        ] },
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
            string[] roles = { "thug", "bystander", "extortionist" };
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


    /** Two social ticks: the first only starts the window, the second lets everyone meet. */
    private static void _tick(TaleManager tm, DateTime t)
    {
        var evolver = new RuntimeSocialEvolver { P_Venue = 1f, MaxWindowMinutes = 600f };
        var npcs = tm.GetNpcsInCluster(Cluster);
        var spatial = tm.GetSpatialModel(Cluster);
        evolver.TickEncounters(Cluster, npcs, spatial, t);
        evolver.TickEncounters(Cluster, npcs, spatial, t.AddMinutes(30),
            (a, b, at) => tm.OnLiveEncounter(Cluster, a, b, at));
        tm.TickLiveSocial(t.AddMinutes(30), int.MaxValue);   // run every queued encounter
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
    public void ASocialTickEncounterClaimsAndSetsTheInterrupt()
    {
        var tm = _populated();
        var thug = tm.GetSchedule(Id(0));
        var bystander = tm.GetSchedule(Id(1));
        DateTime t = T0.AddHours(11);

        // The thug threatens while nobody is registered at the venue: unclaimed.
        tm.AdvanceNpc(thug.NpcId, t);
        var request = Assert.Single(tm.GetRequestPool(Cluster).Requests.Values);
        Assert.Null(request.ClaimerId);

        // The social tick lets thug and bystander meet (the extortionist is elsewhere);
        // the bystander claims and will flee.
        tm.GetSchedule(Id(2)).CurrentLocationId = -1;
        _tick(tm, t);
        Assert.Equal(bystander.NpcId, request.ClaimerId);
        Assert.Equal("flee", bystander.ArcStack.PendingInterruptStorylet);
    }


    [Fact]
    public void ASocialTickEncounterEscalatesAndInterruptsThePartner()
    {
        var tm = _populated();
        var extortionist = tm.GetSchedule(Id(2));
        var bystander = tm.GetSchedule(Id(1));
        DateTime t = T0.AddHours(11);
        Assert.Equal("demand", extortionist.CurrentStorylet);
        bystander.Properties["fear"] = 0.2f;

        _tick(tm, t);

        // Refused (fear <= 0.5): the extortionist's next storylet is forced to the threat ...
        Assert.Equal("threaten", extortionist.NextForcedStorylet);
        // ... and its priority-6 demand nests into the bystander's lower-priority loitering.
        Assert.Equal("demand", bystander.ArcStack.PendingInterruptStorylet);
        Assert.Equal(InterruptScope.Nest, bystander.ArcStack.PendingInterruptScope);

        Assert.Equal("threaten", tm.AdvanceNpc(extortionist.NpcId, t.AddMinutes(31)).Id);
    }


    [Fact]
    public void QueuedEncountersRunAFewPerTickInOrder()
    {
        var tm = _populated();
        var thug = tm.GetSchedule(Id(0));
        var bystander = tm.GetSchedule(Id(1));
        DateTime t = T0.AddHours(11);
        tm.AdvanceNpc(thug.NpcId, t);
        tm.TickLiveSocial(t);

        tm.OnLiveEncounter(Cluster, thug, thug, t);         // runs first, claims nothing
        tm.OnLiveEncounter(Cluster, thug, bystander, t);
        tm.TickLiveSocial(t, 1);
        Assert.Null(bystander.ArcStack.PendingInterruptStorylet);
        tm.TickLiveSocial(t, 1);
        Assert.Equal("flee", bystander.ArcStack.PendingInterruptStorylet);
    }


    [Fact]
    public void TheSocialFramePurgesExpiredRequestsAndResolvesTheDay()
    {
        var tm = _populated();
        tm.CapableRolesOf = type => type == "threat" ? new HashSet<string> { "bystander" } : new HashSet<string>();
        DateTime t = T0.AddHours(11);
        tm.TickLiveSocial(t);

        // A threat at 23:00, unclaimed: resolved abstractly at midnight, purged at its 09:00 timeout.
        tm.AdvanceNpc(Id(0), t);
        var pool = tm.GetRequestPool(Cluster);
        int first = Assert.Single(pool.Requests.Keys);

        tm.TickLiveSocial(t.AddMinutes(30));
        Assert.Single(pool.Requests);

        tm.TickLiveSocial(t.AddHours(1).AddMinutes(1));   // midnight passed: the day is resolved
        Assert.True(pool.HasSignal(first, "request_fulfilled"));

        tm.TickLiveSocial(t.AddHours(11));                // past the 600-minute timeout: purged
        Assert.Empty(pool.Requests);
    }


    [Fact]
    public void TheBackgroundTickAppliesATier3InterruptAtItsTimeAndLaterResumes()
    {
        var tm = _populated();
        var raised = new List<int>();
        tm.OnInterruptSet = id => raised.Add(id);
        var thug = tm.GetSchedule(Id(0));
        var bystander = tm.GetSchedule(Id(1));
        tm.GetSchedule(Id(2)).CurrentLocationId = -1;
        DateTime t = T0.AddHours(11);

        tm.AdvanceNpc(bystander.NpcId, t);                 // loiters for 600 minutes
        DateTime loiterEnd = bystander.CurrentEnd;
        tm.AdvanceNpc(thug.NpcId, t.AddMinutes(10));        // threatens; the bystander claims
        Assert.Equal("flee", bystander.ArcStack.PendingInterruptStorylet);
        Assert.Equal(new[] { bystander.NpcId }, raised);
        DateTime at = bystander.ArcStack.PendingInterruptAt;
        Assert.True(at >= t && at <= t.AddMinutes(15), $"interrupt set at {at}");   // the threat starts at +10 ±5 min

        // The next background tick steps it at the interrupt's time, long before loitering ends.
        tm.AdvanceAbstractSchedules(t.AddMinutes(16), int.MaxValue, int.MaxValue);
        Assert.Equal("flee", bystander.CurrentStorylet);
        Assert.False(bystander.ArcStack.HasPendingInterrupt);
        Assert.Equal(at, bystander.LastAdvanceTime);
        Assert.Single(bystander.ArcStack.PausedArcs);
        Assert.True(bystander.CurrentEnd < loiterEnd);

        // When the 20-minute flight ends, the loitering resumes.
        tm.AdvanceAbstractSchedules(t.AddMinutes(50), int.MaxValue, int.MaxValue);
        Assert.Equal("loiter", bystander.CurrentStorylet);
        Assert.Empty(bystander.ArcStack.PausedArcs);
    }


    [Fact]
    public void AnNpcOutsideAPopulatedClusterStillHonoursAPendingInterrupt()
    {
        var tm = _populated();
        var stray = new NpcSchedule
        {
            NpcId = NpcSchedule.MakeNpcId(99, 0), NpcIndex = 0, ClusterIndex = 99, Seed = 5, Role = "bystander",
            CurrentStorylet = "loiter", CurrentLocationId = -1,
            Properties = new Dictionary<string, float>(), Trust = new Dictionary<int, float>(),
        };
        tm.RegisterNpc(stray);
        stray.ArcStack.SetInterrupt("flee", InterruptScope.Nest, T0);
        Assert.Equal("flee", tm.AdvanceNpc(stray.NpcId, T0).Id);
    }


    /** The materialised side (TaleEntityStrategy) lives in nogame; its guards are pinned by source. */
    [Fact]
    public void AMaterialisedNpcIsNotInterruptedInAConversationWithThePlayer()
    {
        string root = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));
        string src = File.ReadAllText(Path.Combine(root, "nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs"));
        Assert.Contains("TaleNarrationBindings.Session?.NpcId == _npcId", src);

        int handler = src.IndexOf("private void _onInterruptEvent(", StringComparison.Ordinal);
        Assert.True(handler > 0);
        string body = src.Substring(handler, src.IndexOf("_advanceAndTravel();", handler, StringComparison.Ordinal) - handler);
        Assert.Contains("if (_inPlayerConversation()) return;", body);
        Assert.Contains("if (_isTier2) return;", body);
        Assert.Contains("Strategies[\"flee\"]", body);

        // The flee/recover return path takes a pending interrupt, with the same guards.
        Assert.Contains("if (_hasPendingInterrupt() && !_isTier2 && !_inPlayerConversation())", src);
        Assert.Contains("sm.Subscribe(EntityStrategy.InterruptEventPath(_entity), _onInterruptEvent);", src);
        Assert.Contains("sm.Unsubscribe(EntityStrategy.InterruptEventPath(_entity), _onInterruptEvent);", src);
    }


    [Theory]
    [InlineData(int.MaxValue, 9)]
    [InlineData(2, 2)]
    public void ACrowdedPlaceRollsAgainstAtMostTheCappedNumberOfNpcs(int cap, int expected)
    {
        var resolver = new EncounterResolver { P_Venue = 1f, MaxCandidatesPerCheck = cap };
        for (int i = 1; i <= 9; i++) resolver.RegisterPresence(i, 7, T0, T0.AddHours(1));
        resolver.RegisterPresence(0, 7, T0, T0.AddHours(1));
        var met = resolver.CheckEncounters(0, 7, T0, T0.AddHours(1), "social_venue", new Random(1));
        Assert.Equal(expected, met.Count);

        // The capped scan leaves everyone registered: the next arrival still sees them all.
        var again = resolver.CheckEncounters(42, 7, T0, T0.AddHours(1), "social_venue", new Random(2));
        Assert.Equal(Math.Min(cap, 10), again.Count);
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
