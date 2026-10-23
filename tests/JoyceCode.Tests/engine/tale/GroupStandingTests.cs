using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using engine.narration;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S1-e (KAR-458): a group's standing with the player, from its members'
/// regard, and the greeting it buys from members who never met the player.
/// </summary>
public class GroupStandingTests
{
    private const int Cluster = 3;
    private const int GroupId = 5;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);

    private static NpcSchedule Npc(int index, int groupId = GroupId) => new()
    {
        NpcId = NpcSchedule.MakeNpcId(Cluster, index), ClusterIndex = Cluster, NpcIndex = index, GroupId = groupId,
        Properties = new Dictionary<string, float> { ["fear"] = 0.1f, ["anger"] = 0.1f },
        Trust = new Dictionary<int, float>()
    };

    private static List<NpcSchedule> Group(int size) => Enumerable.Range(0, size).Select(i => Npc(i)).ToList();


    /* A TaleManager with the shipped interactions and one group of these members. */
    private static TaleManager Manager(List<NpcSchedule> members)
    {
        var tm = ShippedInteractions.Manager();
        foreach (var m in members) tm.RegisterNpc(m);
        tm.GetOrCreateSocialState(Cluster).Groups[GroupId] = new RuntimeGroup
        {
            GroupId = GroupId, MemberIds = members.Select(m => m.NpcId).OrderBy(id => id).ToList()
        };
        return tm;
    }


    #region formula

    [Theory]
    [InlineData(0.15f, 0f)]
    [InlineData(0f, -1f)]
    [InlineData(0.075f, -0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(0.575f, 0.5f)]
    public void Regard_IsCentredOnFirstContact(float trust, float regard)
    {
        Assert.Equal(regard, PlayerStandingCalculator.Regard(trust), 4);
    }


    [Fact]
    public void NobodyKnowsThePlayer_StandingIsZero()
    {
        Assert.Equal(GroupStanding.None, PlayerStandingCalculator.ForGroup(Group(5)));
        Assert.Equal(GroupStanding.None, PlayerStandingCalculator.ForGroup(new List<NpcSchedule>()));
        Assert.Equal(GroupStanding.None, PlayerStandingCalculator.ForGroup(null));
    }


    [Fact]
    public void OneHelp_OnOneMember_IsAboutPlusZeroPointTwo_WhateverTheGroupSize()
    {
        foreach (int size in new[] { 2, 4, 12 })
        {
            var members = Group(size);
            var tm = Manager(members);
            tm.ApplyPlayerInteraction(members[0].NpcId, "help", T0);

            var s = tm.GetGroupStanding(members[1].NpcId);
            Assert.Equal(0.2f, s.Standing, 3);     // r = 0.4, ŵ = 1, K = 1
            Assert.Equal(1, s.Known);
        }
    }


    [Fact]
    public void OneHit_WeighsAboutAsMuchAsOneHelp_TheOtherWay()
    {
        var members = Group(4);
        var tm = Manager(members);
        tm.ApplyPlayerInteraction(members[0].NpcId, "assault", T0);

        Assert.Equal(-0.5f, tm.GetGroupStanding(members[1].NpcId).Standing, 3);   // t = 0, r = −1
    }


    [Fact]
    public void AChat_BarelyMovesIt()
    {
        var members = Group(4);
        var tm = Manager(members);
        tm.ApplyPlayerInteraction(members[0].NpcId, "chat", T0);

        float s = tm.GetGroupStanding(members[1].NpcId).Standing;
        Assert.InRange(s, 0.005f, 0.015f);
    }


    [Fact]
    public void ACentralMember_CountsMoreThanAPeripheralOne()
    {
        // Everyone trusts member 0 a lot, nobody trusts member 3.
        List<NpcSchedule> Trusting()
        {
            var g = Group(4);
            foreach (var m in g.Skip(1)) m.Trust[g[0].NpcId] = 0.9f;
            return g;
        }

        var central = Trusting();
        central[0].Trust[PlayerTrust.Key] = 0.49f;
        var peripheral = Trusting();
        peripheral[3].Trust[PlayerTrust.Key] = 0.49f;

        float c = PlayerStandingCalculator.ForGroup(central).Standing;
        float p = PlayerStandingCalculator.ForGroup(peripheral).Standing;
        Assert.True(c > 0.2f && p < 0.2f && p > 0f, $"central {c}, peripheral {p}");
    }


    [Fact]
    public void MissingSchedules_AreSkipped_AndTheSpeakerIsNotCountedAsKnown()
    {
        var members = Group(3);
        members[0].Trust[PlayerTrust.Key] = 0.49f;
        var withGaps = new List<NpcSchedule> { members[0], null, members[1], null, members[2] };

        var s = PlayerStandingCalculator.ForGroup(withGaps, excludeId: members[0].NpcId);
        Assert.Equal(0.2f, s.Standing, 3);
        Assert.Equal(0, s.Known);
        Assert.Equal(1, PlayerStandingCalculator.ForGroup(withGaps).Known);
    }


    [Fact]
    public void NpcTrustInOtherNpcs_IsNotTrustInThePlayer()
    {
        var members = Group(3);
        members[0].Trust[members[1].NpcId] = 0.95f;      // a strong NPC–NPC tie, no player entry

        Assert.Equal(GroupStanding.None, PlayerStandingCalculator.ForGroup(members));
    }


    [Fact]
    public void Ungrouped_HasNoStanding()
    {
        var loner = Npc(9, groupId: -1);
        var tm = ShippedInteractions.Manager();
        tm.RegisterNpc(loner);
        Assert.Equal(GroupStanding.None, tm.GetGroupStanding(loner.NpcId));
    }

    #endregion


    #region greeting

    /* Follows the entry node's conditional goto, as NarrationRunner would, with these props. */
    private static string EntryTarget(string file, Dictionary<string, object> props)
    {
        var script = JsonNode.Parse(File.ReadAllText(
            Path.Combine(ShippedInteractions.RepoRoot(), "models", "tale", "conversations", file)))!;
        var evaluator = new NarrationConditionEvaluator(key => props.GetValueOrDefault(key));
        foreach (var branch in script["nodes"]!["entry"]!["flow"]![0]!["goto"]!.AsArray())
        {
            if (branch!["else"] != null) return (string)branch["else"];
            if (evaluator.Evaluate((string)branch["if"])) return (string)branch["goto"];
        }
        return null;
    }


    private static Dictionary<string, object> PropsOf(TaleManager tm, NpcSchedule npc)
    {
        var s = tm.GetGroupStanding(npc.NpcId);
        return new Dictionary<string, object>
        {
            ["npc.met_player"] = PlayerTrust.Knows(npc) ? "true" : "false",
            ["npc.trust_player"] = PlayerTrust.Get(npc),
            ["npc.group_standing"] = s.Standing,
            ["npc.group_known"] = (float)s.Known,
            ["npc.has_group"] = "true",
            ["npc.hunger"] = 0.3f, ["npc.anger"] = 0.3f, ["npc.fatigue"] = 0.3f,
        };
    }


    [Theory]
    [InlineData("tale.tag.routine.json")]
    [InlineData("tale.generic.json")]
    public void HelpingOneMember_MakesAnotherGreetWarmly_AHitMakesThemCold(string file)
    {
        var helped = Group(4);
        var tmHelp = Manager(helped);
        Assert.NotEqual("group_warm", EntryTarget(file, PropsOf(tmHelp, helped[1])));

        tmHelp.ApplyPlayerInteraction(helped[0].NpcId, "help", T0);
        Assert.Equal("group_warm", EntryTarget(file, PropsOf(tmHelp, helped[1])));

        var hit = Group(4);
        var tmHit = Manager(hit);
        tmHit.ApplyPlayerInteraction(hit[0].NpcId, "assault", T0);
        Assert.Equal("group_cold", EntryTarget(file, PropsOf(tmHit, hit[1])));

        // Whoever the player hit personally stays hostile.
        Assert.Equal("hostile", EntryTarget(file,
            new Dictionary<string, object>(PropsOf(tmHit, hit[0])) { ["npc.player_fact.hit_me"] = 1f }));
    }


    [Fact]
    public void SomeoneWhoKnowsThePlayer_GetsNoGroupGreeting()
    {
        var members = Group(4);
        var tm = Manager(members);
        tm.ApplyPlayerInteraction(members[0].NpcId, "help", T0);
        tm.ApplyPlayerInteraction(members[1].NpcId, "chat", T0);

        Assert.Equal("default", EntryTarget("tale.tag.routine.json", PropsOf(tm, members[1])));
    }

    #endregion
}
