using System.Collections.Generic;
using System.Text.Json.Nodes;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// Save-game round-trip tests for ClusterSocialState (TALE-SOCIAL Phase E6).
/// Pure JsonObject build/parse — no engine boot, mirroring how
/// TaleModule._onBeforeSaveGame / _onAfterLoadGame use ToJson / FromJson.
/// </summary>
public class ClusterSocialStateSerializationTests
{
    private static ClusterSocialState MakeState()
    {
        var social = new ClusterSocialState { ClusterIndex = 3, NextGroupId = 42 };
        social.Groups[7] = new RuntimeGroup
        {
            GroupId = 7,
            Type = "criminal",
            Name = "The Rusted Vipers",
            HangoutLocationId = 512,
            Authored = true,
            MemberIds = new List<int> { 10, 11, 12 }
        };
        social.Groups[9] = new RuntimeGroup
        {
            GroupId = 9,
            Type = "social",
            Name = "The Corner Regulars",
            MemberIds = new List<int> { 20, 21, 22, 23 }
        };
        social.Snapshots[10] = new NpcSocialSnapshot
        {
            GroupId = 7,
            Trust = new Dictionary<int, float> { [11] = 0.8f, [-1] = 0.35f },
            SocialProps = new Dictionary<string, float> { ["morality"] = 0.15f, ["fear"] = 0.4f }
        };
        social.Snapshots[20] = new NpcSocialSnapshot { GroupId = 9 };
        return social;
    }

    [Fact]
    public void ToJson_FromJson_RoundTripsEverything()
    {
        var original = MakeState();

        // Serialize → string → parse → deserialize, exactly like the
        // GameState.TaleSocialState path.
        string json = original.ToJson().ToJsonString();
        var restored = ClusterSocialState.FromJson(JsonNode.Parse(json));

        Assert.NotNull(restored);
        Assert.Equal(3, restored.ClusterIndex);
        Assert.Equal(42, restored.NextGroupId);

        Assert.Equal(2, restored.Groups.Count);
        var gang = restored.Groups[7];
        Assert.Equal("criminal", gang.Type);
        Assert.Equal("The Rusted Vipers", gang.Name);
        Assert.Equal(512, gang.HangoutLocationId);
        Assert.True(gang.Authored);
        Assert.Equal(new List<int> { 10, 11, 12 }, gang.MemberIds);

        var circle = restored.Groups[9];
        Assert.False(circle.Authored);
        Assert.Equal(-1, circle.HangoutLocationId);

        Assert.Equal(2, restored.Snapshots.Count);
        var snap = restored.Snapshots[10];
        Assert.Equal(7, snap.GroupId);
        Assert.Equal(0.8f, snap.Trust[11], 5);
        Assert.Equal(0.35f, snap.Trust[-1], 5); // player trust survives
        Assert.Equal(0.15f, snap.SocialProps["morality"], 5);
        Assert.Equal(0.4f, snap.SocialProps["fear"], 5);
    }

    [Fact]
    public void RoundTrip_RestoresOntoFreshSchedule()
    {
        var original = MakeState();
        string json = original.ToJson().ToJsonString();
        var restored = ClusterSocialState.FromJson(JsonNode.Parse(json));

        var npc = new NpcSchedule
        {
            NpcId = 10,
            Properties = new Dictionary<string, float> { ["morality"] = 0.9f },
            Trust = new Dictionary<int, float> { [99] = 0.5f }
        };
        Assert.True(restored.RestoreNpc(npc));
        Assert.Equal(7, npc.GroupId);
        Assert.Equal(0.8f, npc.Trust[11], 5);
        Assert.False(npc.Trust.ContainsKey(99));
        Assert.Equal(0.15f, npc.Properties["morality"], 5);
    }

    [Fact]
    public void FromJson_MalformedInput_ReturnsNull()
    {
        Assert.Null(ClusterSocialState.FromJson(JsonNode.Parse("[1,2,3]")));
        Assert.Null(ClusterSocialState.FromJson(JsonNode.Parse("{\"nextGroupId\": 5}"))); // no clusterIndex
    }

    [Fact]
    public void ToJson_EmptyState_RoundTrips()
    {
        var social = new ClusterSocialState { ClusterIndex = 1 };
        var restored = ClusterSocialState.FromJson(
            JsonNode.Parse(social.ToJson().ToJsonString()));
        Assert.NotNull(restored);
        Assert.Empty(restored.Groups);
        Assert.Empty(restored.Snapshots);
    }
}
