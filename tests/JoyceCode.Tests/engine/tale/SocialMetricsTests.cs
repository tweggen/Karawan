using System;
using System.Collections.Generic;
using System.Linq;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// The shared social metrics (TALE S0 W6, KAR-448): calculator, churn history
/// and group matching. The DES side is in DesSocialMetricsTests.
/// </summary>
public class SocialMetricsTests
{
    private static NpcSchedule Npc(int id, int groupId = -1, Dictionary<int, float> trust = null) =>
        new()
        {
            NpcId = id,
            Role = "worker",
            GroupId = groupId,
            Trust = trust ?? new Dictionary<int, float>()
        };


    /**
     * Two groups of three, {1,2,3} as group 10 and {4,5,6} as group 20, plus
     * ungrouped 7. Trust, written out:
     *   1<->2 0.8/0.6 (asymmetric, avg 0.7)   1->3 0.9 one-sided
     *   4<->5 0.5/0.5                         5<->6 0.7/0.7
     *   3<->4 0.2/0.4 (inter, avg 0.3)        6->7 0.6 one-sided (7 ungrouped)
     * Player trust: 1 -> 0.5, 4 -> 0.3. The -1 entries are not pairs.
     */
    private static List<NpcSchedule> TwoGroupsOfThree() => new()
    {
        Npc(1, 10, new() { { 2, 0.8f }, { 3, 0.9f }, { -1, 0.5f } }),
        Npc(2, 10, new() { { 1, 0.6f } }),
        Npc(3, 10, new() { { 4, 0.2f } }),
        Npc(4, 20, new() { { 3, 0.4f }, { 5, 0.5f }, { -1, 0.3f } }),
        Npc(5, 20, new() { { 4, 0.5f }, { 6, 0.7f } }),
        Npc(6, 20, new() { { 5, 0.7f }, { 7, 0.6f } }),
        Npc(7)
    };


    private static Dictionary<int, RuntimeGroup> TableFor(List<NpcSchedule> npcs) => new()
    {
        [10] = new RuntimeGroup { GroupId = 10, Type = "criminal", MemberIds = new() { 1, 2, 3 } },
        [20] = new RuntimeGroup { GroupId = 20, Type = "social", MemberIds = new() { 4, 5, 6 } }
    };


    [Fact]
    public void PairsFromSchedulesKeepEachDirectionAndDropThePlayer()
    {
        var pairs = TrustPair.FromSchedules(TwoGroupsOfThree());
        Assert.Equal(6, pairs.Count);
        Assert.DoesNotContain(pairs, p => p.A < 0 || p.B < 0);

        var p12 = pairs.Single(p => p.A == 1 && p.B == 2);
        Assert.Equal(0.8f, p12.TrustAtoB);
        Assert.Equal(0.6f, p12.TrustBtoA);
        Assert.Equal(0.7f, p12.Average, 5);

        var p13 = pairs.Single(p => p.A == 1 && p.B == 3);
        Assert.Equal(0.9f, p13.TrustAtoB);
        Assert.Null(p13.TrustBtoA);
        Assert.Equal(0.9f, p13.Average, 5);

        // Recorded only by the higher id: still a pair, with A->B missing.
        var p67 = pairs.Single(p => p.A == 6 && p.B == 7);
        Assert.Equal(0.6f, p67.TrustAtoB);
        var p34 = pairs.Single(p => p.A == 3 && p.B == 4);
        Assert.Equal(0.3f, p34.Average, 5);
    }


    [Fact]
    public void CountsDensityAndStrongPairs()
    {
        var npcs = TwoGroupsOfThree();
        var m = SocialMetricsCalculator.Compute(npcs, TrustPair.FromSchedules(npcs), TableFor(npcs));

        Assert.Equal(7, m.NpcCount);
        Assert.Equal(2, m.GroupCount);
        Assert.Equal(1, m.GroupCountByType["criminal"]);
        Assert.Equal(1, m.GroupCountByType["social"]);
        Assert.Equal(6, m.NpcsInGroups);
        Assert.Equal(6.0 / 7, m.GroupMembershipRatio, 6);
        Assert.Equal(3, m.GroupSizeMin);
        Assert.Equal(3, m.GroupSizeMax);
        Assert.Equal(3.0, m.GroupSizeMedian);

        Assert.Equal(6, m.KnownPairs);
        Assert.Equal(6.0 / 21, m.TrustDensity, 6);      // 7 NPCs: 21 possible pairs
        // >= 0.6: 1-2 (0.7), 1-3 (0.9), 5-6 (0.7), 6-7 (0.6); not 4-5 (0.5), 3-4 (0.3)
        Assert.Equal(4, m.StrongPairs);
        Assert.Equal(0, m.GroupTableMismatch);
    }


    [Fact]
    public void IntraAndInterGroupTrust()
    {
        var npcs = TwoGroupsOfThree();
        var m = SocialMetricsCalculator.Compute(npcs, TrustPair.FromSchedules(npcs), TableFor(npcs));

        // intra: 1-2 0.7, 1-3 0.9, 4-5 0.5, 5-6 0.7; 6-7 does not count (7 ungrouped)
        Assert.Equal((0.7 + 0.9 + 0.5 + 0.7) / 4, m.MeanIntraGroupTrust.Value, 5);
        Assert.Equal(0.3, m.MeanInterGroupTrust.Value, 5);
        Assert.Equal(0.3, m.InterGroupTrust["10-20"], 5);
        Assert.Single(m.InterGroupTrust);
    }


    [Fact]
    public void PlayerTrustIsCountedOnItsOwn()
    {
        var npcs = TwoGroupsOfThree();
        var m = SocialMetricsCalculator.Compute(npcs, TrustPair.FromSchedules(npcs));

        Assert.Equal(2, m.MetPlayer);
        Assert.Equal(2.0 / 7, m.MetPlayerShare, 6);
        Assert.Equal(0.4, m.MeanPlayerTrust.Value, 5);
    }


    [Fact]
    public void WithoutTheTableTypesAreUnknownAndNothingMismatches()
    {
        var npcs = TwoGroupsOfThree();
        var m = SocialMetricsCalculator.Compute(npcs, TrustPair.FromSchedules(npcs));
        Assert.Equal(2, m.GroupCountByType["unknown"]);
        Assert.Equal(0, m.GroupTableMismatch);
    }


    [Fact]
    public void RosterMismatchCountsEachNpcOnce()
    {
        var npcs = TwoGroupsOfThree();
        var table = TableFor(npcs);
        // 3 moved to group 20 in its schedule but the table still lists it in 10,
        // and 7's schedule names a group the table does not have.
        npcs.Single(n => n.NpcId == 3).GroupId = 20;
        npcs.Single(n => n.NpcId == 7).GroupId = 99;

        var m = SocialMetricsCalculator.Compute(npcs, TrustPair.FromSchedules(npcs), table);
        Assert.Equal(2, m.GroupTableMismatch);
    }


    [Fact]
    public void BakeInfoGivesMatchRatioAndCoverage()
    {
        var bake = new SocialBakeInfo
        {
            Category = "mid", Index = 2, RealNpcCount = 80, ScenarioNpcCount = 100, MatchedNpcCount = 60
        };
        Assert.Equal(0.75, bake.MatchRatio, 6);
        Assert.Equal(0.6, bake.Coverage, 6);

        var npcs = TwoGroupsOfThree();
        var d = SocialMetricsCalculator.Compute(npcs, TrustPair.FromSchedules(npcs), bake: bake).ToDictionary();
        var jBake = Assert.IsType<Dictionary<string, object>>(d["bake"]);
        Assert.Equal("mid/2", jBake["scenario"]);
        Assert.Equal(0.75, jBake["match_ratio"]);

        Assert.Equal(0.0, new SocialBakeInfo().MatchRatio);
    }


    [Fact]
    public void EmptyClusterHasNoMeans()
    {
        var m = SocialMetricsCalculator.Compute(new List<NpcSchedule>(), new List<TrustPair>());
        Assert.Equal(0, m.GroupCount);
        Assert.Equal(0.0, m.TrustDensity);
        Assert.Null(m.MeanIntraGroupTrust);
        Assert.Null(m.MeanPlayerTrust);
        Assert.Null(m.MembershipChurnPerDay);
        Assert.Contains("npcs=0", m.ToLogLine());
    }


    // --- history -------------------------------------------------------------

    private static readonly DateTime D0 = new(2024, 3, 1);

    private static List<NpcSchedule> Members(params (int id, int group)[] m) =>
        m.Select(x => Npc(x.id, x.group)).ToList();


    [Fact]
    public void FirstRollOnlyStarts()
    {
        var h = new SocialMetricsHistory();
        Assert.Null(h.RollDay(D0, Members((1, 1), (2, 1))));
        Assert.True(h.IsStarted);
        Assert.Empty(h.Samples);
    }


    [Fact]
    public void RenumberingEveryGroupIsNoChurn()
    {
        var h = new SocialMetricsHistory();
        h.Start(D0, Members((1, 1), (2, 1), (3, 1), (4, 2), (5, 2), (6, 2), (7, -1)));
        var s = h.RollDay(D0.AddDays(1), Members((1, 7), (2, 7), (3, 7), (4, 3), (5, 3), (6, 3), (7, -1))).Value;

        Assert.Equal(0, s.MembershipChanges);
        Assert.Equal(0, s.GroupsCreated);
        Assert.Equal(0, s.GroupsDissolved);
        Assert.Equal(2, s.Groups);
        Assert.Equal(6, s.NpcsInGroups);
    }


    [Fact]
    public void OneMoveIsOneChange()
    {
        var h = new SocialMetricsHistory();
        h.Start(D0, Members((1, 1), (2, 1), (3, 1), (4, 1), (5, 2), (6, 2), (7, 2)));
        // 4 moves to the other group, and both groups are renumbered too.
        var s = h.RollDay(D0.AddDays(1), Members((1, 8), (2, 8), (3, 8), (4, 9), (5, 9), (6, 9), (7, 9))).Value;

        Assert.Equal(1, s.MembershipChanges);
        Assert.Equal(0, s.GroupsCreated);
        Assert.Equal(0, s.GroupsDissolved);
    }


    [Fact]
    public void DissolveAndCreate()
    {
        var h = new SocialMetricsHistory();
        h.Start(D0, Members((1, 1), (2, 1), (3, 1), (4, -1), (5, -1), (6, -1)));
        var s = h.RollDay(D0.AddDays(1), Members((1, -1), (2, -1), (3, -1), (4, 5), (5, 5), (6, 5))).Value;

        Assert.Equal(6, s.MembershipChanges);   // three left a dissolved group, three joined a new one
        Assert.Equal(1, s.GroupsCreated);
        Assert.Equal(1, s.GroupsDissolved);
    }


    [Fact]
    public void JoiningAndLeavingAGroupAreChanges()
    {
        var h = new SocialMetricsHistory();
        h.Start(D0, Members((1, 1), (2, 1), (3, 1), (4, 1), (5, -1)));
        var s = h.RollDay(D0.AddDays(1), Members((1, 1), (2, 1), (3, 1), (4, -1), (5, 1))).Value;
        Assert.Equal(2, s.MembershipChanges);
        Assert.Equal(0, s.GroupsCreated + s.GroupsDissolved);
    }


    [Fact]
    public void HistoryKeepsTheLast120DaysAndFeedsTheMeans()
    {
        var h = new SocialMetricsHistory();
        var a = Members((1, 1), (2, 1), (3, 1), (4, -1));
        var b = Members((1, 1), (2, 1), (3, 1), (4, 1));
        h.Start(D0, a);
        for (int day = 1; day <= 130; day++)
            h.RollDay(D0.AddDays(day), day % 2 == 1 ? b : a);

        Assert.Equal(SocialMetricsHistory.Capacity, h.Samples.Count);
        Assert.All(h.Samples, s => Assert.Equal(1, s.MembershipChanges));

        var m = SocialMetricsCalculator.Compute(a, new List<TrustPair>(), history: h);
        Assert.Equal(120, m.HistoryDays);
        Assert.Equal(1.0, m.MembershipChurnPerDay);
        Assert.Equal(0.0, m.GroupsCreatedPerDay);
    }


    // --- matching ------------------------------------------------------------

    [Fact]
    public void MatchingNeedsHalfOfTheOldRosterAndClaimsOnce()
    {
        var old = new List<(int, IReadOnlyCollection<int>)>
        {
            (1, new List<int> { 1, 2, 3, 4 }),
            (2, new List<int> { 5, 6 })
        };
        var result = GroupMatching.Match(new List<IReadOnlyCollection<int>>
        {
            new List<int> { 1, 2 },        // 2 of 4: continues group 1
            new List<int> { 3, 4, 9 },     // group 1 already claimed
            new List<int> { 6, 7, 8 }      // 1 of 2: continues group 2
        }, old);

        Assert.Equal(new[] { 1, -1, 2 }, result);

        var tooSmall = GroupMatching.Match(new List<IReadOnlyCollection<int>> { new List<int> { 1, 9 } }, old);
        Assert.Equal(new[] { -1 }, tooSmall);     // 1 of 4 is not half
    }


    [Fact]
    public void MatchingTiesGoToTheLowerId()
    {
        var old = new List<(int, IReadOnlyCollection<int>)>
        {
            (7, new List<int> { 1, 2 }),
            (3, new List<int> { 3, 4 })
        };
        var result = GroupMatching.Match(new List<IReadOnlyCollection<int>> { new List<int> { 1, 3 } }, old);
        Assert.Equal(new[] { 3 }, result);
    }


    // --- DES rate counters ------------------------------------------------------

    [Fact]
    public void CompletionRateIsTheShareOfOwnRoutine()
    {
        var c = new MetricsCollector();
        c.RegisterNpc("worker");
        Assert.Null(c.RoutineCompletionRate);

        c.OnSelection("worker", SelectionSource.Routine);
        c.OnSelection("worker", SelectionSource.Routine);
        c.OnSelection("worker", SelectionSource.Resumed);
        c.OnSelection("worker", SelectionSource.Forced);
        c.OnSelection("worker", SelectionSource.InterruptNest);
        Assert.Equal(0.6, c.RoutineCompletionRate.Value, 6);
        Assert.Equal(2, c.SelectionsBySource[SelectionSource.Routine]);
    }


    [Fact]
    public void RequestsAreCountedById()
    {
        var c = new MetricsCollector();
        Assert.Null(c.RequestFulfillmentRate);
        c.OnRequestEmitted(1);
        c.OnRequestEmitted(2);
        c.OnRequestFulfilled(1);
        c.OnRequestFulfilled(1);     // abstract resolution signals the same request again
        c.OnRequestFulfilled(77);    // never emitted here
        Assert.Equal(2, c.RequestsEmitted);
        Assert.Equal(1, c.RequestsFulfilled);
        Assert.Equal(0.5, c.RequestFulfillmentRate.Value, 6);
    }


    [Fact]
    public void FinalMetricsCarryTheCountersNotLiterals()
    {
        var c = new MetricsCollector();
        c.RegisterNpc("worker");
        c.RegisterNpc("merchant");
        c.OnSelection("worker", SelectionSource.Routine);
        c.OnSelection("worker", SelectionSource.InterruptReplace);
        c.OnSelection("merchant", SelectionSource.Routine);
        c.OnInterrupt(1, "worker");
        c.OnInterrupt(1, "worker");
        c.OnDayEnd();
        c.OnDayEnd();
        c.OnRequestEmitted(5);

        var npcs = new Dictionary<int, NpcSchedule>
        {
            [1] = new() { NpcId = 1, Role = "worker", Properties = new() },
            [2] = new() { NpcId = 2, Role = "merchant", Properties = new() }
        };
        var social = SocialMetricsCalculator.Compute(npcs.Values.ToList(), new List<TrustPair>());
        var d = c.ComputeFinalMetrics(npcs, new RelationshipTracker(), 2, 0, new EncounterResolver(),
            new SpatialModel(), social);

        Assert.Equal(Math.Round(2.0 / 3, 3), d["routine_completion_rate"]);
        Assert.Equal(0.0, d["request_fulfillment_rate"]);
        var ipd = Assert.IsType<Dictionary<string, object>>(d["interrupts_per_day"]);
        Assert.Equal(1.0, ipd["cluster_mean"]);        // 2 interrupts on day 1, 0 on day 2
        Assert.Equal(0.5, ipd["mean"]);                // per NPC-day: two NPCs
        var roles = Assert.IsType<Dictionary<string, object>>(d["role_breakdown"]);
        var worker = Assert.IsType<Dictionary<string, object>>(roles["worker"]);
        Assert.Equal(0.5, worker["completion_rate"]);
        Assert.Equal(2.0, worker["mean_interrupts"]);
        Assert.True(d.ContainsKey("social"));
    }
}
