using System.Collections.Generic;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S1-a (KAR-454): the player-trust baseline and its one writer.
/// </summary>
public class PlayerTrustTests
{
    private static NpcSchedule Npc(int index = 2) => new()
    {
        NpcId = NpcSchedule.MakeNpcId(3, index), ClusterIndex = 3, NpcIndex = index,
        Properties = new Dictionary<string, float>(), Trust = new Dictionary<int, float>()
    };


    [Fact]
    public void NoEntry_MeansUnknown_AndReadsAsFirstContact()
    {
        var npc = Npc();
        Assert.False(PlayerTrust.Knows(npc));
        Assert.Equal(PlayerTrust.FirstContact, PlayerTrust.Get(npc));
        Assert.False(PlayerTrust.Knows(new NpcSchedule()));
        Assert.Equal(PlayerTrust.FirstContact, PlayerTrust.Get(new NpcSchedule()));
    }


    [Fact]
    public void FirstContact_IsTheStrangerAcquaintanceBoundary_NotFriend()
    {
        // nogame.relationship-tiers.json: stranger < 0.15 <= acquaintance < 0.4 <= friend.
        Assert.Equal(0.15f, PlayerTrust.FirstContact);
    }


    [Fact]
    public void Apply_StartsAtFirstContact_ThenAddsTheChange()
    {
        var tm = new TaleManager();
        var npc = Npc();

        Assert.Equal(0.25f, tm.ApplyPlayerTrust(npc, 0.10f), 4);
        Assert.True(PlayerTrust.Knows(npc));
        Assert.Equal(0.35f, tm.ApplyPlayerTrust(npc, 0.10f), 4);
        Assert.Equal(0.35f, PlayerTrust.Get(npc), 4);
    }


    [Theory]
    [InlineData(0.9f, 0.4f, 1f)]
    [InlineData(0.1f, -0.4f, 0f)]
    public void Apply_ClampsBothWays(float start, float delta, float expected)
    {
        var npc = Npc();
        npc.Trust[-1] = start;
        Assert.Equal(expected, new TaleManager().ApplyPlayerTrust(npc, delta));
    }


    [Fact]
    public void Apply_MarksTheContact_AndBumpsTheVersion()
    {
        var tm = new TaleManager();
        var npc = Npc(index: 5);
        int before = tm.PlayerTrustVersion;

        tm.ApplyPlayerTrust(npc, 0f);

        Assert.True(npc.HasPlayerDeviation);
        Assert.Contains(5, tm.GetDeviationSkipMask(3));
        Assert.Equal(before + 1, tm.PlayerTrustVersion);
        Assert.Equal(PlayerTrust.FirstContact, npc.Trust[-1]);
        Assert.Equal(0, tm.UnflaggedPlayerContacts);
    }
}
