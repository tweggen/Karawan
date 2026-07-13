using System.Collections.Generic;
using System.Linq;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// GetGroupHangout tests (TALE-SOCIAL Phase E5). The hangout is the modal
/// first social venue among a group's members — the venue where the player
/// actually SEES a faction together. Computed lazily, cached on the group,
/// and consumed by ResolveSocialVenue's co-location bias.
/// </summary>
public class GroupHangoutTests
{
    private static NpcSchedule MakeNpc(int id, params int[] venues)
    {
        return new NpcSchedule
        {
            NpcId = id,
            Seed = id,
            Role = "worker",
            ClusterIndex = 1,
            GroupId = 5,
            SocialVenueIds = venues.ToList(),
            Properties = new Dictionary<string, float>(),
            Trust = new Dictionary<int, float>()
        };
    }

    private static TaleManager MakeManagerWithGroup(NpcSchedule[] npcs, out RuntimeGroup group)
    {
        var tm = new TaleManager();
        tm.Initialize(new StoryletLibrary());
        foreach (var npc in npcs) tm.RegisterNpc(npc);

        group = new RuntimeGroup
        {
            GroupId = 5,
            Type = "social",
            Name = "The Corner Regulars",
            MemberIds = npcs.Select(n => n.NpcId).ToList()
        };
        tm.GetOrCreateSocialState(1).Groups[5] = group;
        return tm;
    }

    [Fact]
    public void GetGroupHangout_PicksModalFirstVenue()
    {
        var npcs = new[]
        {
            MakeNpc(1, 700, 900),
            MakeNpc(2, 700, 901),
            MakeNpc(3, 800, 700)
        };
        var tm = MakeManagerWithGroup(npcs, out var group);

        Assert.Equal(700, tm.GetGroupHangout(1, 5)); // first venues 700,700,800
        Assert.Equal(700, group.HangoutLocationId);  // cached on the group
    }

    [Fact]
    public void GetGroupHangout_CachedValueIsStable()
    {
        var npcs = new[] { MakeNpc(1, 700), MakeNpc(2, 700), MakeNpc(3, 800) };
        var tm = MakeManagerWithGroup(npcs, out _);

        Assert.Equal(700, tm.GetGroupHangout(1, 5));

        // Members changing their venue preference must not move the hangout —
        // factions keep their bar.
        npcs[0].SocialVenueIds = new List<int> { 999 };
        npcs[1].SocialVenueIds = new List<int> { 999 };
        Assert.Equal(700, tm.GetGroupHangout(1, 5));
    }

    [Fact]
    public void GetGroupHangout_TieBreaksToLowestVenueId()
    {
        var npcs = new[] { MakeNpc(1, 900), MakeNpc(2, 700) };
        var tm = MakeManagerWithGroup(npcs, out _);

        Assert.Equal(700, tm.GetGroupHangout(1, 5)); // 1 vote each → lowest id
    }

    [Fact]
    public void GetGroupHangout_UnknownStateOrGroup_ReturnsMinusOne()
    {
        var tm = new TaleManager();
        tm.Initialize(new StoryletLibrary());
        Assert.Equal(-1, tm.GetGroupHangout(1, 5)); // no social state at all

        var npcs = new[] { MakeNpc(1, 700) };
        tm = MakeManagerWithGroup(npcs, out _);
        Assert.Equal(-1, tm.GetGroupHangout(1, 99)); // unknown group
        Assert.Equal(-1, tm.GetGroupHangout(2, 5));  // unknown cluster
    }

    [Fact]
    public void GetGroupHangout_MembersWithoutVenues_ReturnsMinusOne()
    {
        var npcs = new[] { MakeNpc(1), MakeNpc(2) }; // no venues at all
        var tm = MakeManagerWithGroup(npcs, out _);
        Assert.Equal(-1, tm.GetGroupHangout(1, 5));
    }
}
