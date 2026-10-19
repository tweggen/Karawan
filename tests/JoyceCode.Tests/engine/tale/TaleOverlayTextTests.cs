using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// The per-NPC debug overlay text (TALE S0 W7, KAR-449).
/// </summary>
public class TaleOverlayTextTests
{
    private static NpcSchedule MakeNpc(string role = "merchant", string storylet = "open_shop",
        int groupId = -1, Dictionary<int, float> trust = null)
    {
        return new NpcSchedule
        {
            NpcId = 7,
            Role = role,
            CurrentStorylet = storylet,
            GroupId = groupId,
            Trust = trust ?? new Dictionary<int, float>()
        };
    }


    [Fact]
    public void UngroupedNpcThePlayerNeverMet()
    {
        Assert.Equal("merchant | open_shop\nno group | trust -",
            TaleOverlayText.Format(MakeNpc(), null, false));
    }


    [Fact]
    public void Tier2IsMarkedOnTheFirstLine()
    {
        Assert.Equal("merchant | open_shop [T2]\nno group | trust -",
            TaleOverlayText.Format(MakeNpc(), null, true));
    }


    [Fact]
    public void GroupShowsIdAndName()
    {
        var group = new RuntimeGroup { GroupId = 3, Name = "The Rusted Syndicate" };
        Assert.Equal("drifter | loiter\ng3 The Rusted Syndicate | trust -",
            TaleOverlayText.Format(MakeNpc("drifter", "loiter", 3), group, false));
    }


    [Fact]
    public void UnnamedGroupShowsIdOnly()
    {
        var group = new RuntimeGroup { GroupId = 3, Name = "" };
        Assert.EndsWith("\ng3 | trust -", TaleOverlayText.Format(MakeNpc(groupId: 3), group, false));
    }


    [Fact]
    public void GroupIdWithoutRuntimeGroupStillShowsTheId()
    {
        Assert.EndsWith("\ng5 | trust -", TaleOverlayText.Format(MakeNpc(groupId: 5), null, false));
    }


    [Fact]
    public void PlayerTrustIsTheMinusOneEntry()
    {
        var npc = MakeNpc(trust: new Dictionary<int, float> { { 12, 0.9f }, { -1, 0.5234f } });
        Assert.EndsWith("| trust 0.52", TaleOverlayText.Format(npc, null, false));
    }


    [Fact]
    public void OtherNpcsTrustIsNotPlayerTrust()
    {
        var npc = MakeNpc(trust: new Dictionary<int, float> { { 12, 0.9f } });
        Assert.EndsWith("| trust -", TaleOverlayText.Format(npc, null, false));
    }


    [Fact]
    public void TrustUsesInvariantCulture()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            var npc = MakeNpc(trust: new Dictionary<int, float> { { -1, 0.25f } });
            Assert.EndsWith("| trust 0.25", TaleOverlayText.Format(npc, null, false));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }


    [Fact]
    public void MissingFieldsDoNotThrow()
    {
        var npc = new NpcSchedule { Role = null, CurrentStorylet = null, Trust = null };
        Assert.Equal("? | -\nno group | trust -", TaleOverlayText.Format(npc, null, false));
        Assert.Equal("", TaleOverlayText.Format(null, null, false));
    }
}
