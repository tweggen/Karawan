using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.tale;
using JoyceCode.Tests.engine.tale.bake;
using Xunit;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-490 (TALE S2b-e) acceptance, live, with the shipped storylets: a drifter demanding
 * protection meets a target who is not afraid enough (fear <= 0.5), so the refusal forces
 * threaten_harder; that issues a threat; a bystander who meets the drifter claims it and
 * flees (flee_from_threat) as a Nest interrupt, which the background tick applies; and the
 * TALE panel's snapshot shows the request and the interruption.
 */
public class LiveEscalationAcceptanceTests : IClassFixture<ScenarioCompilerTests.RegistryFixture>
{
    public LiveEscalationAcceptanceTests(ScenarioCompilerTests.RegistryFixture _) { }

    private const int Cluster = 8;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);
    private static int Id(int i) => NpcSchedule.MakeNpcId(Cluster, i);


    private static TaleManager _populated()
    {
        string tale = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), "..", "models", "tale"));
        var library = new StoryletLibrary();
        library.LoadFromDirectory(tale);
        var tm = new TaleManager();
        tm.Initialize(library);

        var model = new SpatialModel();
        model.Locations.Add(new Location { Id = 1, Type = "social_venue", Position = new Vector3(10, 0, 10) });
        model.Locations.Add(new Location { Id = 2, Type = "street_segment", Position = new Vector3(60, 0, 10) });
        model.BuildIndex();

        tm.PopulateClusterWith(Cluster, model, T0, _ =>
        {
            string[] roles = { "drifter", "worker", "worker" };
            return roles.Select((role, i) => new NpcSchedule
            {
                NpcId = Id(i), NpcIndex = i, ClusterIndex = Cluster, Seed = 100 + i, Role = role,
                HomeLocationId = 1, WorkplaceLocationId = 1, CurrentLocationId = 1,
                SocialVenueIds = new List<int> { 1 },
                HomePosition = new Vector3(10, 0, 10), CurrentWorldPosition = new Vector3(10, 0, 10),
                Properties = new Dictionary<string, float>(), Trust = new Dictionary<int, float>(),
            }).ToList();
        });
        return tm;
    }


    [Fact]
    public void ARefusedProtectionDemandBecomesAThreatAndABystanderFlees()
    {
        var tm = _populated();
        var drifter = tm.GetSchedule(Id(0));
        var target = tm.GetSchedule(Id(1));
        var bystander = tm.GetSchedule(Id(2));
        DateTime t = T0.AddHours(2);

        drifter.CurrentStorylet = "demand_protection";
        drifter.Properties["wealth"] = 0.7f;
        drifter.Properties["anger"] = 0.3f;
        target.Properties["fear"] = 0.2f;
        string bystanderWas = bystander.CurrentStorylet;

        // 1. The drifter meets the target (the social tick's encounter): refused, escalate.
        tm.OnLiveEncounter(Cluster, drifter, target, t);
        tm.TickLiveSocial(t, int.MaxValue);
        Assert.Equal("threaten_harder", drifter.NextForcedStorylet);
        Assert.Equal(0.5f, drifter.Properties["anger"], 3);

        // 2. Its next storylet is the forced threat, which issues a threat request.
        Assert.Equal("threaten_harder", tm.AdvanceNpc(drifter.NpcId, t.AddMinutes(1)).Id);
        var threat = Assert.Single(tm.GetRequestPool(Cluster).Requests.Values, r => r.Type == "threat");
        Assert.Equal(drifter.NpcId, threat.RequesterId);
        Assert.Null(threat.ClaimerId);

        // 3. The bystander meets the drifter and claims the threat: flee, as a Nest.
        tm.OnLiveEncounter(Cluster, bystander, drifter, t.AddMinutes(2));
        tm.TickLiveSocial(t.AddMinutes(2), int.MaxValue);
        Assert.Equal(bystander.NpcId, threat.ClaimerId);
        Assert.Equal("flee_from_threat", bystander.ArcStack.PendingInterruptStorylet);
        Assert.Equal(InterruptScope.Nest, bystander.ArcStack.PendingInterruptScope);

        // 4. The panel shows the request and the pending interrupt ...
        var before = TaleDebugSnapshotBuilder.Build(tm, Cluster, t.AddMinutes(2), Vector3.Zero, bystander.NpcId);
        var shown = Assert.Single(before.OpenRequests, r => r.Type == "threat");
        Assert.Equal(bystander.NpcId, shown.ClaimerId);
        Assert.Contains(before.Interruptions, i => i.NpcId == bystander.NpcId && i.PendingInterrupt == "flee_from_threat");
        Assert.Equal("flee_from_threat", before.Selected.PendingInterrupt);
        Assert.Equal("Nest", before.Selected.PendingInterruptScope);

        // 5. ... the background tick runs the flight, pausing what the bystander did ...
        tm.AdvanceAbstractSchedules(t.AddMinutes(3), int.MaxValue, int.MaxValue);
        Assert.Equal("flee_from_threat", bystander.CurrentStorylet);
        Assert.Equal(2, bystander.CurrentLocationId);              // random_street
        var after = TaleDebugSnapshotBuilder.Build(tm, Cluster, t.AddMinutes(3), Vector3.Zero, bystander.NpcId);
        Assert.Equal(new[] { bystanderWas }, after.Selected.PausedStorylets);
        Assert.Null(after.Selected.PendingInterrupt);

        // 6. ... and resumes it when the flight is over.
        tm.AdvanceAbstractSchedules(t.AddMinutes(60), int.MaxValue, int.MaxValue);
        Assert.Empty(bystander.ArcStack.PausedArcs);
        Assert.NotEqual("flee_from_threat", bystander.CurrentStorylet);
    }
}
