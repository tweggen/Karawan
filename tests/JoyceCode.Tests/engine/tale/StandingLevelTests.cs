using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S1-f (KAR-460): the player's standing level per cluster — anonymous,
/// recognised, connected, entangled — read from trust and group standing,
/// with hysteresis.
/// </summary>
public class StandingLevelTests
{
    private const int Cluster = 3;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);
    private static readonly StandingThresholds Default = new();

    private static NpcSchedule Npc(int index, int groupId = -1, int cluster = Cluster) => new()
    {
        NpcId = NpcSchedule.MakeNpcId(cluster, index), ClusterIndex = cluster, NpcIndex = index, GroupId = groupId,
        Properties = new Dictionary<string, float> { ["fear"] = 0.1f, ["anger"] = 0.1f },
        Trust = new Dictionary<int, float>()
    };


    /* Ten ungrouped NPCs and a group of four (ids 10..13, group 5) in cluster 3. */
    private static (TaleManager Tm, List<NpcSchedule> Loners, List<NpcSchedule> Group) City()
    {
        var tm = ShippedInteractions.Manager();
        var loners = Enumerable.Range(0, 10).Select(i => Npc(i)).ToList();
        var group = Enumerable.Range(10, 4).Select(i => Npc(i, groupId: 5)).ToList();
        foreach (var n in loners.Concat(group)) tm.RegisterNpc(n);
        tm.GetOrCreateSocialState(Cluster).Groups[5] = new RuntimeGroup
        {
            GroupId = 5, MemberIds = group.Select(m => m.NpcId).ToList()
        };
        return (tm, loners, group);
    }


    [Fact]
    public void Nobody_KnowsThePlayer_Anonymous()
    {
        var (tm, _, _) = City();
        Assert.Equal(StandingLevel.Anonymous, tm.GetPlayerStanding(Cluster).Level);
        Assert.Equal(StandingLevel.Anonymous, tm.GetPlayerStanding(42).Level);      // a cluster with nothing
    }


    [Theory]
    [InlineData("help")]
    [InlineData("assault")]           // notoriety counts too (owner, 2026-10-18)
    public void ThreeNpcs_HelpedOrHit_MakeThePlayerRecognised(string id)
    {
        var (tm, loners, _) = City();

        tm.ApplyPlayerInteraction(loners[0].NpcId, id, T0);
        tm.ApplyPlayerInteraction(loners[1].NpcId, id, T0);
        Assert.Equal(StandingLevel.Anonymous, tm.GetPlayerStanding(Cluster).Level);

        tm.ApplyPlayerInteraction(loners[2].NpcId, id, T0);
        var reading = tm.GetPlayerStanding(Cluster);
        Assert.Equal(StandingLevel.Recognised, reading.Level);
        Assert.Equal(3, reading.Inputs.KnownEnter);
        Assert.Equal("recognised", reading.Name);
    }


    [Fact]
    public void Chatting_WithManyNpcs_IsNotYetBeingRecognised()
    {
        var (tm, loners, _) = City();
        foreach (var n in loners) tm.ApplyPlayerInteraction(n.NpcId, "chat", T0);
        Assert.Equal(StandingLevel.Anonymous, tm.GetPlayerStanding(Cluster).Level);
    }


    [Fact]
    public void HelpingTwoMembersOfAGroup_MakesThePlayerConnected()
    {
        var (tm, _, group) = City();

        tm.ApplyPlayerInteraction(group[0].NpcId, "help", T0);
        Assert.NotEqual(StandingLevel.Connected, tm.GetPlayerStanding(Cluster).Level);    // one member only

        tm.ApplyPlayerInteraction(group[1].NpcId, "help", T0);
        var reading = tm.GetPlayerStanding(Cluster);
        Assert.Equal(StandingLevel.Connected, reading.Level);                             // gs = 2·0.4/3 ≈ 0.27
        Assert.Equal(1, reading.Inputs.GroupsEnter);
    }


    [Fact]
    public void OneHitSeenByAGroupmate_ConnectsThePlayerTheBadWay()
    {
        var (tm, _, group) = City();
        var positions = group.Select((m, i) => (m.NpcId, new Vector3(5f * i, 0, 0))).ToList();

        var report = tm.ReportPlayerAction(group[0].NpcId, "assault", Vector3.Zero, T0, positions.Take(2));
        Assert.Single(report.Witnesses);

        var reading = tm.GetPlayerStanding(Cluster);
        Assert.Equal(StandingLevel.Connected, reading.Level);
        Assert.True(tm.GetGroupStanding(Cluster, 5).Standing <= -Default.ConnectedStanding);
    }


    [Fact]
    public void Hysteresis_ALevelIsKeptUntilTheStayConditionFails()
    {
        var recognisedEnough = new StandingInputs(KnownEnter: 2, KnownStay: 2, 0, 0, 0, 0);
        Assert.Equal(StandingLevel.Recognised, PlayerStandingLevel.Evaluate(recognisedEnough, StandingLevel.Recognised, Default));
        Assert.Equal(StandingLevel.Anonymous, PlayerStandingLevel.Evaluate(recognisedEnough, null, Default));
        Assert.Equal(StandingLevel.Anonymous, PlayerStandingLevel.Evaluate(recognisedEnough, StandingLevel.Anonymous, Default));
        Assert.Equal(StandingLevel.Anonymous,
            PlayerStandingLevel.Evaluate(new StandingInputs(1, 1, 0, 0, 0, 0), StandingLevel.Recognised, Default));

        var weakGroup = new StandingInputs(0, 0, GroupsEnter: 0, GroupsStay: 1, 0, 0);
        Assert.Equal(StandingLevel.Connected, PlayerStandingLevel.Evaluate(weakGroup, StandingLevel.Connected, Default));
        Assert.Equal(StandingLevel.Anonymous, PlayerStandingLevel.Evaluate(weakGroup, null, Default));

        // Losing "connected" falls back to "recognised" if that still holds.
        Assert.Equal(StandingLevel.Recognised,
            PlayerStandingLevel.Evaluate(new StandingInputs(0, 2, 0, 0, 0, 0), StandingLevel.Connected, Default));
    }


    [Fact]
    public void Hysteresis_ThroughTheManager()
    {
        var (tm, loners, _) = City();
        foreach (var n in loners.Take(3)) n.Trust[PlayerTrust.Key] = 0.31f;
        tm.ApplyPlayerInteraction(loners[9].NpcId, "chat", T0);          // bumps the trust version
        Assert.Equal(StandingLevel.Recognised, tm.GetPlayerStanding(Cluster).Level);

        // One drops below the enter line but stays above the stay line (0.26): still recognised.
        loners[0].Trust[PlayerTrust.Key] = 0.28f;
        tm.ApplyPlayerInteraction(loners[9].NpcId, "chat", T0.AddHours(2));
        Assert.Equal(StandingLevel.Recognised, tm.GetPlayerStanding(Cluster).Level);

        // A fresh manager with the same trust (as after a load) applies only the enter conditions.
        var (fresh, freshLoners, _) = City();
        for (int i = 0; i < 3; i++) freshLoners[i].Trust[PlayerTrust.Key] = loners[i].Trust[PlayerTrust.Key];
        Assert.Equal(StandingLevel.Anonymous, fresh.GetPlayerStanding(Cluster).Level);
    }


    [Fact]
    public void ClustersAreIndependent()
    {
        var (tm, loners, _) = City();
        var elsewhere = Enumerable.Range(0, 3).Select(i => Npc(i, cluster: 4)).ToList();
        foreach (var n in elsewhere) tm.RegisterNpc(n);

        foreach (var n in loners.Take(3)) tm.ApplyPlayerInteraction(n.NpcId, "help", T0);

        Assert.Equal(StandingLevel.Recognised, tm.GetPlayerStanding(Cluster).Level);
        Assert.Equal(StandingLevel.Anonymous, tm.GetPlayerStanding(4).Level);
    }


    [Fact]
    public void Entangled_IsUnreachableInS1()
    {
        var (tm, loners, group) = City();
        foreach (var n in loners.Concat(group)) tm.ApplyPlayerInteraction(n.NpcId, "help", T0);

        var reading = tm.GetPlayerStanding(Cluster);
        Assert.Equal(0, reading.Inputs.FavourDirections);
        Assert.Equal(0, reading.Inputs.RivalGroups);
        Assert.NotEqual(StandingLevel.Entangled, reading.Level);
        // Even a remembered "entangled" falls without favours and rivals.
        Assert.NotEqual(StandingLevel.Entangled, PlayerStandingLevel.Evaluate(reading.Inputs, StandingLevel.Entangled, Default));
        Assert.Equal(StandingLevel.Entangled,
            PlayerStandingLevel.Evaluate(new StandingInputs(0, 0, 0, 0, 2, 2), null, Default));
    }


    [Fact]
    public void TheReadingIsCached_UntilTrustChanges()
    {
        var (tm, loners, _) = City();
        Assert.Equal(StandingLevel.Anonymous, tm.GetPlayerStanding(Cluster).Level);

        foreach (var n in loners.Take(3)) n.Trust[PlayerTrust.Key] = 0.5f;     // written behind the writer's back
        Assert.Equal(StandingLevel.Anonymous, tm.GetPlayerStanding(Cluster).Level);   // still the cached reading

        tm.ApplyPlayerInteraction(loners[9].NpcId, "chat", T0);                 // a real write invalidates it
        Assert.Equal(StandingLevel.Recognised, tm.GetPlayerStanding(Cluster).Level);
    }


    [Fact]
    public void Publisher_ReportsTransitionsOnly()
    {
        var p = new StandingPublisher();
        Assert.True(p.Changed(StandingLevel.Anonymous));
        Assert.False(p.Changed(StandingLevel.Anonymous));
        Assert.True(p.Changed(StandingLevel.Recognised));
        Assert.False(p.Changed(StandingLevel.Recognised));
        Assert.True(p.Changed(StandingLevel.Anonymous));
    }
}
