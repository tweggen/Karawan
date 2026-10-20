using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// The save format of NPCs the player touched (Phase 6 scripts 42, 43, 46, 49,
/// moved here in KAR-468 together with the code, out of TaleModule).
/// </summary>
public class NpcScheduleJsonTests
{
    private static NpcSchedule Deviated() => new()
    {
        NpcId = NpcSchedule.MakeNpcId(2, 17), Seed = 99, Role = "merchant", ClusterIndex = 2, NpcIndex = 17,
        CurrentLocationId = 4, CurrentStorylet = "serve_customers", ScheduleStep = 12,
        HomeLocationId = 3, WorkplaceLocationId = 4, GroupId = 6,
        CurrentStart = new DateTime(2024, 1, 3, 9, 0, 0), CurrentEnd = new DateTime(2024, 1, 3, 11, 30, 0),
        IsInTransit = true, TransitFromLocationId = 3, TransitToLocationId = 4,
        TransitStart = new DateTime(2024, 1, 3, 8, 40, 0), TransitEnd = new DateTime(2024, 1, 3, 9, 0, 0),
        HomePosition = new Vector3(1.5f, 2.25f, -3f), WorkplacePosition = new Vector3(10f, 2.25f, 4f),
        CurrentWorldPosition = new Vector3(5f, 2.25f, 0.5f),
        Properties = new Dictionary<string, float> { ["anger"] = 0.9f, ["wealth"] = 0.1f, ["player_fact.helped"] = 1f },
        Trust = new Dictionary<int, float> { [-1] = 0.35f, [NpcSchedule.MakeNpcId(2, 3)] = 0.8f },
        SocialVenueIds = new List<int> { 7, 9 },
        HasPlayerDeviation = true,
    };


    [Fact]
    public void RoundTrip_KeepsWhatIsSaved()   // 46
    {
        var npc = Deviated();
        var back = NpcScheduleJson.ReadDeviations(NpcScheduleJson.WriteDeviations(new[] { npc })).Single();

        Assert.Equal(npc.NpcId, back.NpcId);
        Assert.Equal(npc.Role, back.Role);
        Assert.Equal(npc.CurrentStorylet, back.CurrentStorylet);
        Assert.Equal(npc.ScheduleStep, back.ScheduleStep);
        Assert.Equal(npc.GroupId, back.GroupId);
        Assert.Equal(npc.CurrentStart, back.CurrentStart);
        Assert.Equal(npc.CurrentEnd, back.CurrentEnd);
        Assert.True(back.IsInTransit);
        Assert.Equal(npc.TransitToLocationId, back.TransitToLocationId);
        Assert.Equal(npc.TransitEnd, back.TransitEnd);
        Assert.Equal(npc.HomePosition, back.HomePosition);
        Assert.Equal(npc.WorkplacePosition, back.WorkplacePosition);
        Assert.Equal(npc.CurrentWorldPosition, back.CurrentWorldPosition);
        Assert.Equal(0.35f, back.Trust[-1]);
        Assert.Equal(0.8f, back.Trust[NpcSchedule.MakeNpcId(2, 3)]);
        Assert.Equal(0.9f, back.Properties["anger"]);
        Assert.Equal(1f, back.Properties["player_fact.helped"]);
        Assert.Equal(new List<int> { 7, 9 }, back.SocialVenueIds);
        Assert.True(back.HasPlayerDeviation);
        Assert.True(back.IsKnownToPlayer);

        // Stable: a second round trip writes the same JSON.
        Assert.Equal(NpcScheduleJson.ToJson(back).ToJsonString(),
            NpcScheduleJson.ToJson(NpcScheduleJson.FromJson(NpcScheduleJson.ToJson(back))).ToJsonString());
    }


    [Fact]
    public void NoDeviations_WritesAnEmptyArray_AndEmptyReadsAsNone()   // 43
    {
        Assert.Equal("[]", NpcScheduleJson.WriteDeviations(Array.Empty<NpcSchedule>()));
        Assert.Empty(NpcScheduleJson.ReadDeviations("[]"));
        Assert.Empty(NpcScheduleJson.ReadDeviations(""));
        Assert.Empty(NpcScheduleJson.ReadDeviations(null));
    }


    [Fact]
    public void OnlyDeviatedNpcsAreSaved_SoTheSaveScalesWithThem()   // 42, 49
    {
        var tm = new TaleManager();
        tm.Initialize(new StoryletLibrary());
        for (int i = 0; i < 200; i++)
            tm.RegisterNpc(new NpcSchedule
            {
                NpcId = NpcSchedule.MakeNpcId(2, i), ClusterIndex = 2, NpcIndex = i, Role = "worker",
                Properties = new Dictionary<string, float>(), Trust = new Dictionary<int, float>()
            });
        tm.MarkPlayerContact(tm.GetSchedule(NpcSchedule.MakeNpcId(2, 5)));
        tm.MarkPlayerContact(tm.GetSchedule(NpcSchedule.MakeNpcId(2, 77)));

        string json = NpcScheduleJson.WriteDeviations(tm.GetAllDeviatedNpcs());
        var ids = JsonNode.Parse(json)!.AsArray().Select(n => n!["npcId"]!.GetValue<int>()).OrderBy(i => i);

        Assert.Equal(new[] { NpcSchedule.MakeNpcId(2, 5), NpcSchedule.MakeNpcId(2, 77) }, ids);
    }


    [Fact]
    public void AnOldSaveWithoutNewProperties_IsBackfilled()
    {
        var jo = NpcScheduleJson.ToJson(Deviated());
        jo["properties"] = new JsonObject { ["anger"] = 0.9f };
        var back = NpcScheduleJson.FromJson(jo);
        Assert.Equal(0.9f, back.Properties["anger"]);
        Assert.True(back.Properties.Count > 1);
    }
}
