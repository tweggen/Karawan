using System;
using System.Collections.Generic;
using System.Linq;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// GroupActions verb tests (TALE-SOCIAL Phase E4). No GroupTypeRegistry is
/// registered here, so formed groups classify as "social" — classification
/// itself is covered by GroupTypeRegistryTests. No IEventLogger either; the
/// gang_formed emission path is exercised by the TALE regression suite.
/// </summary>
public class GroupActionsTests
{
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);

    private static NpcSchedule MakeNpc(int id, int groupId = -1)
    {
        return new NpcSchedule
        {
            NpcId = id,
            Seed = id,
            Role = "drifter",
            ClusterIndex = 1,
            GroupId = groupId,
            Properties = new Dictionary<string, float>
            {
                { "wealth", 0.1f }, { "morality", 0.2f }, { "reputation", 0.3f },
                { "anger", 0f }, { "fear", 0.1f }
            },
            Trust = new Dictionary<int, float>()
        };
    }

    private static void Bond(Dictionary<int, NpcSchedule> npcs, int a, int b, float trust)
    {
        npcs[a].Trust[b] = trust;
        npcs[b].Trust[a] = trust;
    }

    private static int Apply(string action, NpcSchedule self,
        Dictionary<int, NpcSchedule> npcs, ClusterSocialState social)
        => GroupActions.Apply(action, self, npcs, social,
            (c, g, t) => $"gen-{g}", logger: null, day: 1, gameTime: T0);

    [Fact]
    public void Form_CreatesAuthoredGroupFromTrustedNeighbors()
    {
        var npcs = Enumerable.Range(1, 5).Select(i => MakeNpc(i)).ToDictionary(n => n.NpcId);
        Bond(npcs, 1, 2, 0.6f);
        Bond(npcs, 1, 3, 0.5f);
        Bond(npcs, 1, 4, 0.4f);
        // Npc 5: too little trust to be recruited.
        Bond(npcs, 1, 5, 0.1f);

        var social = new ClusterSocialState { ClusterIndex = 1, NextGroupId = 7 };
        int groupId = Apply("form", npcs[1], npcs, social);

        Assert.Equal(7, groupId);
        Assert.Equal(8, social.NextGroupId);
        var group = Assert.Single(social.Groups).Value;
        Assert.True(group.Authored);
        Assert.Equal("gen-7", group.Name);
        Assert.Equal(new List<int> { 1, 2, 3, 4 }, group.MemberIds);
        Assert.Equal(7, npcs[1].GroupId);
        Assert.Equal(7, npcs[4].GroupId);
        Assert.Equal(-1, npcs[5].GroupId);
    }

    [Fact]
    public void Form_NoOpsWithoutEnoughTrustedPartners()
    {
        var npcs = Enumerable.Range(1, 3).Select(i => MakeNpc(i)).ToDictionary(n => n.NpcId);
        Bond(npcs, 1, 2, 0.6f); // only ONE trusted partner; form needs 2+

        var social = new ClusterSocialState { ClusterIndex = 1 };
        Assert.Equal(-1, Apply("form", npcs[1], npcs, social));
        Assert.Empty(social.Groups);
        Assert.Equal(-1, npcs[1].GroupId);
    }

    [Fact]
    public void Form_NoOpsWhenAlreadyGrouped()
    {
        var npcs = Enumerable.Range(1, 4).Select(i => MakeNpc(i)).ToDictionary(n => n.NpcId);
        npcs[1].GroupId = 3;
        Bond(npcs, 1, 2, 0.9f);
        Bond(npcs, 1, 3, 0.9f);

        var social = new ClusterSocialState { ClusterIndex = 1 };
        Assert.Equal(-1, Apply("form", npcs[1], npcs, social));
    }

    [Fact]
    public void Form_SkipsAlreadyGroupedCandidates()
    {
        var npcs = Enumerable.Range(1, 4).Select(i => MakeNpc(i)).ToDictionary(n => n.NpcId);
        Bond(npcs, 1, 2, 0.9f);
        Bond(npcs, 1, 3, 0.9f);
        Bond(npcs, 1, 4, 0.9f);
        npcs[2].GroupId = 99; // spoken for

        var social = new ClusterSocialState { ClusterIndex = 1, NextGroupId = 1 };
        int groupId = Apply("form", npcs[1], npcs, social);

        Assert.Equal(1, groupId);
        Assert.Equal(new List<int> { 1, 3, 4 }, social.Groups[1].MemberIds);
        Assert.Equal(99, npcs[2].GroupId); // untouched
    }

    [Fact]
    public void Join_AdoptsGroupOfMostTrustedMember()
    {
        var npcs = Enumerable.Range(1, 4).Select(i => MakeNpc(i)).ToDictionary(n => n.NpcId);
        var social = new ClusterSocialState { ClusterIndex = 1, NextGroupId = 10 };
        social.Groups[5] = new RuntimeGroup
        {
            GroupId = 5, Type = "criminal", Name = "The Stray Dogs",
            MemberIds = new List<int> { 2, 3 }
        };
        npcs[2].GroupId = 5;
        npcs[3].GroupId = 5;
        Bond(npcs, 1, 2, 0.4f);
        Bond(npcs, 1, 3, 0.7f); // most trusted

        int groupId = Apply("join", npcs[1], npcs, social);

        Assert.Equal(5, groupId);
        Assert.Equal(5, npcs[1].GroupId);
        Assert.Equal(new List<int> { 1, 2, 3 }, social.Groups[5].MemberIds);
    }

    [Fact]
    public void Join_NoOpsBelowTrustThreshold()
    {
        var npcs = Enumerable.Range(1, 2).Select(i => MakeNpc(i)).ToDictionary(n => n.NpcId);
        var social = new ClusterSocialState { ClusterIndex = 1 };
        social.Groups[5] = new RuntimeGroup { GroupId = 5, MemberIds = new List<int> { 2 } };
        npcs[2].GroupId = 5;
        Bond(npcs, 1, 2, 0.1f); // below JoinTrustThreshold

        Assert.Equal(-1, Apply("join", npcs[1], npcs, social));
        Assert.Equal(-1, npcs[1].GroupId);
    }

    [Fact]
    public void Leave_RemovesFromRoster()
    {
        var npcs = Enumerable.Range(1, 3).Select(i => MakeNpc(i, groupId: 5)).ToDictionary(n => n.NpcId);
        var social = new ClusterSocialState { ClusterIndex = 1 };
        social.Groups[5] = new RuntimeGroup
        {
            GroupId = 5, MemberIds = new List<int> { 1, 2, 3 }
        };

        Assert.Equal(5, Apply("leave", npcs[1], npcs, social));
        Assert.Equal(-1, npcs[1].GroupId);
        Assert.Equal(new List<int> { 2, 3 }, social.Groups[5].MemberIds);
        Assert.Equal(5, npcs[2].GroupId);
    }

    [Fact]
    public void Leave_DissolvesGroupBelowTwoMembers()
    {
        var npcs = Enumerable.Range(1, 2).Select(i => MakeNpc(i, groupId: 5)).ToDictionary(n => n.NpcId);
        var social = new ClusterSocialState { ClusterIndex = 1 };
        social.Groups[5] = new RuntimeGroup
        {
            GroupId = 5, MemberIds = new List<int> { 1, 2 }
        };

        Apply("leave", npcs[1], npcs, social);

        Assert.Empty(social.Groups);
        Assert.Equal(-1, npcs[1].GroupId);
        Assert.Equal(-1, npcs[2].GroupId); // last member ungrouped on dissolve
    }

    [Fact]
    public void UnknownAction_NoOps()
    {
        var npcs = new Dictionary<int, NpcSchedule> { [1] = MakeNpc(1) };
        var social = new ClusterSocialState();
        Assert.Equal(-1, Apply("explode", npcs[1], npcs, social));
    }
}
