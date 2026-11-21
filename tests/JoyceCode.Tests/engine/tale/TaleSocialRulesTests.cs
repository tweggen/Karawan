using System;
using System.Collections.Generic;
using System.IO;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-486 (TALE S2b-a): the rules DesSimulation runs - selection order, requests, claims -
 * as one component the live game will call too. The DES's own behaviour is pinned by the
 * TALE suite and by a re-bake that reproduced all 13 shipped scenarios byte for byte; these
 * pin the component directly.
 */
public class TaleSocialRulesTests
{
    private const string Json = """
    [
      { "id": "routine", "roles": [], "location": "home", "duration_minutes": 30, "weight": 1.0 },
      { "id": "forced", "roles": [], "location": "home", "duration_minutes": 30, "weight": 0.0 },
      { "id": "interrupt", "roles": [], "location": "current", "duration_minutes": 10, "weight": 0.0 },
      { "id": "threaten", "roles": [], "location": "current", "duration_minutes": 5, "weight": 0.0,
        "postconditions": { "request": { "type": "threat", "location": "current", "urgency": 0.9, "timeout_minutes": 10 } } },
      { "id": "flee", "roles": [], "claim_trigger": { "request_type": "threat", "role_match": [] },
        "location": "random_street", "duration_minutes": 20 },
      { "id": "wander", "roles": [], "location": "random_street", "duration_minutes": 30, "weight": 0.0, "tags": ["fallback"] },
      { "id": "rest", "roles": [], "location": "home", "duration_minutes": 30, "weight": 0.0, "tags": ["fallback"] }
    ]
    """;

    private static readonly DateTime T0 = new(2024, 1, 1, 12, 0, 0);


    private static TaleSocialRules _rules(out StoryletLibrary library, out InteractionPool pool)
    {
        var dir = Path.Combine(Path.GetTempPath(), "tale-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "test.json"), Json);
            library = new StoryletLibrary();
            library.LoadFromDirectory(dir);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
        pool = new InteractionPool();
        return new TaleSocialRules(library, new StoryletSelector(library), pool, new NullEventLogger(), null);
    }


    private static NpcSchedule _npc(int id)
    {
        var npc = new NpcSchedule
        {
            NpcId = id, Seed = id, Role = "worker", CurrentStorylet = "routine",
            CurrentStart = T0.AddMinutes(-10), CurrentEnd = T0.AddMinutes(20),
            Properties = new Dictionary<string, float>()
        };
        StoryletSelector.EnsurePropertiesInitialized(npc);
        return npc;
    }


    [Fact]
    public void SelectionGoesForcedThenInterruptThenResumeThenRoutine()
    {
        var rules = _rules(out _, out _);
        var npc = _npc(1);
        npc.NextForcedStorylet = "forced";
        npc.ArcStack.SetInterrupt("interrupt", InterruptScope.Nest);

        var (n1, s1) = rules.SelectNext(npc, "routine", T0, 0);
        Assert.Equal(("forced", SelectionSource.Forced), (n1.Id, s1));
        Assert.Null(npc.NextForcedStorylet);

        var (n2, s2) = rules.SelectNext(npc, "forced", T0, 0);
        Assert.Equal(("interrupt", SelectionSource.InterruptNest), (n2.Id, s2));
        Assert.Single(npc.ArcStack.PausedArcs);

        var (n3, s3) = rules.SelectNext(npc, "interrupt", T0, 0);
        Assert.Equal(("forced", SelectionSource.Resumed), (n3.Id, s3));

        var (n4, s4) = rules.SelectNext(npc, "forced", T0, 0);
        Assert.Equal(("routine", SelectionSource.Routine), (n4.Id, s4));
    }


    [Fact]
    public void AReplaceInterruptPausesNothing()
    {
        var rules = _rules(out _, out _);
        var npc = _npc(1);
        npc.ArcStack.SetInterrupt("interrupt", InterruptScope.Replace);
        var (next, source) = rules.SelectNext(npc, "routine", T0, 0);
        Assert.Equal(("interrupt", SelectionSource.InterruptReplace), (next.Id, source));
        Assert.Empty(npc.ArcStack.PausedArcs);
    }


    [Fact]
    public void AThreatIsClaimedAndTheClaimerFleesAsANestInterrupt()
    {
        var rules = _rules(out var library, out var pool);
        var threatener = _npc(1);
        var bystander = _npc(2);

        rules.EmitRequest(threatener, library.GetById("threaten"), 7, T0, 0);
        Assert.Single(pool.GetActiveRequests(T0));

        rules.ClaimRequests(bystander, T0.AddMinutes(1), 0);
        Assert.Equal("flee", bystander.ArcStack.PendingInterruptStorylet);
        Assert.Equal(InterruptScope.Nest, bystander.ArcStack.PendingInterruptScope);

        // A claimed request is claimed once.
        var third = _npc(3);
        rules.ClaimRequests(third, T0.AddMinutes(2), 0);
        Assert.Null(third.ArcStack.PendingInterruptStorylet);
    }


    [Fact]
    public void AnExpiredRequestIsNotClaimed()
    {
        var rules = _rules(out var library, out _);
        rules.EmitRequest(_npc(1), library.GetById("threaten"), 7, T0, 0);
        var late = _npc(2);
        rules.ClaimRequests(late, T0.AddMinutes(11), 0);
        Assert.Null(late.ArcStack.PendingInterruptStorylet);
    }


    [Fact]
    public void TheDesRunsItsRulesThroughTheComponent()
    {
        string root = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));
        string des = File.ReadAllText(Path.Combine(root, "JoyceCode/engine/tale/DesSimulation.cs"));
        Assert.Contains("_rules.SelectNext(npc, previousStorylet, _clock, day)", des);
        Assert.Contains("_rules.EmitRequest(npc, next, destination, _clock, day)", des);
        Assert.Contains("_rules.ClaimRequests(schedA, encounterTime, day)", des);
        Assert.Contains("_rules.ClaimRequests(schedB, encounterTime, day)", des);
        Assert.Contains("_rules.ApplyEncounterConditionals(schedA, schedB, encounterTime, day)", des);
        Assert.Contains("_rules.ResolveDaily(dayBoundary, completedDay, _npcs.Values, GetCapableRoles, _rng)", des);
        Assert.DoesNotContain("private void CheckAndClaimRequests", des);
    }
}
