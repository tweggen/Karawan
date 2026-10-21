using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace engine.tale;

/// <summary>
/// The save format of an NPC the player touched (GameState.TaleDeviations):
/// one JSON object per schedule. Moved unchanged out of TaleModule (KAR-468)
/// so the round trip is tested. Not saved: ArcStack, NextForcedStorylet,
/// LastAdvanceTime, routing preferences; FromJson marks the schedule as
/// deviated and touched now.
/// </summary>
public static class NpcScheduleJson
{
    /// <summary>The TaleDeviations string for these schedules: a JSON array.</summary>
    public static string WriteDeviations(IEnumerable<NpcSchedule> schedules)
    {
        var jArray = new JsonArray();
        foreach (var npc in schedules) jArray.Add(ToJson(npc));
        return jArray.ToJsonString();
    }


    /// <summary>The schedules in a TaleDeviations string; empty for null or "".</summary>
    public static List<NpcSchedule> ReadDeviations(string json)
    {
        var result = new List<NpcSchedule>();
        if (string.IsNullOrEmpty(json)) return result;
        var jArray = JsonNode.Parse(json)?.AsArray();
        if (jArray == null) return result;
        foreach (var jNode in jArray)
        {
            var schedule = FromJson(jNode);
            if (schedule != null) result.Add(schedule);
        }
        return result;
    }


    public static JsonObject ToJson(NpcSchedule npc)
    {
        var jo = new JsonObject
        {
            ["npcId"] = npc.NpcId,
            ["seed"] = npc.Seed,
            ["role"] = npc.Role,
            ["clusterIndex"] = npc.ClusterIndex,
            ["npcIndex"] = npc.NpcIndex,
            ["currentLocationId"] = npc.CurrentLocationId,
            ["currentStorylet"] = npc.CurrentStorylet,
            ["scheduleStep"] = npc.ScheduleStep,
            ["homeLocationId"] = npc.HomeLocationId,
            ["workplaceLocationId"] = npc.WorkplaceLocationId,
            ["groupId"] = npc.GroupId,
        };

        // Activity timing
        jo["currentStartTicks"] = npc.CurrentStart.Ticks;
        jo["currentEndTicks"] = npc.CurrentEnd.Ticks;

        // Transit state (critical for resuming walks on load)
        jo["isInTransit"] = npc.IsInTransit;
        if (npc.IsInTransit)
        {
            jo["transitFromLocationId"] = npc.TransitFromLocationId;
            jo["transitToLocationId"] = npc.TransitToLocationId;
            jo["transitStartTicks"] = npc.TransitStart.Ticks;
            jo["transitEndTicks"] = npc.TransitEnd.Ticks;
        }

        // Home/workplace positions
        jo["homePositionX"] = npc.HomePosition.X;
        jo["homePositionY"] = npc.HomePosition.Y;
        jo["homePositionZ"] = npc.HomePosition.Z;
        jo["workplacePositionX"] = npc.WorkplacePosition.X;
        jo["workplacePositionY"] = npc.WorkplacePosition.Y;
        jo["workplacePositionZ"] = npc.WorkplacePosition.Z;

        // Current world position (for fragment-accurate spawning)
        if (npc.CurrentWorldPosition != System.Numerics.Vector3.Zero)
        {
            jo["currentWorldPositionX"] = npc.CurrentWorldPosition.X;
            jo["currentWorldPositionY"] = npc.CurrentWorldPosition.Y;
            jo["currentWorldPositionZ"] = npc.CurrentWorldPosition.Z;
        }

        // Properties
        var jProps = new JsonObject();
        foreach (var kvp in npc.Properties)
        {
            jProps[kvp.Key] = kvp.Value;
        }
        jo["properties"] = jProps;

        // Trust
        var jTrust = new JsonObject();
        foreach (var kvp in npc.Trust)
        {
            jTrust[kvp.Key.ToString()] = kvp.Value;
        }
        jo["trust"] = jTrust;

        // Social venue IDs
        if (npc.SocialVenueIds != null)
        {
            var jVenues = new JsonArray();
            foreach (var v in npc.SocialVenueIds)
                jVenues.Add(v);
            jo["socialVenueIds"] = jVenues;
        }

        return jo;
    }


    public static NpcSchedule FromJson(JsonNode jNode)
    {
        if (jNode is not JsonObject jo) return null;

        var npc = new NpcSchedule
        {
            NpcId = jo["npcId"]?.GetValue<int>() ?? 0,
            Seed = jo["seed"]?.GetValue<int>() ?? 0,
            Role = jo["role"]?.GetValue<string>() ?? "worker",
            ClusterIndex = jo["clusterIndex"]?.GetValue<int>() ?? 0,
            NpcIndex = jo["npcIndex"]?.GetValue<int>() ?? 0,
            CurrentLocationId = jo["currentLocationId"]?.GetValue<int>() ?? 0,
            CurrentStorylet = jo["currentStorylet"]?.GetValue<string>(),
            ScheduleStep = jo["scheduleStep"]?.GetValue<int>() ?? 0,
            HomeLocationId = jo["homeLocationId"]?.GetValue<int>() ?? 0,
            WorkplaceLocationId = jo["workplaceLocationId"]?.GetValue<int>() ?? 0,
            GroupId = jo["groupId"]?.GetValue<int>() ?? -1,
            HasPlayerDeviation = true,
            // Treat load as a touch so saved deviated NPCs aren't immediately
            // aged out by the dematerialization decay path on first traversal.
            LastConversationTime = DateTime.UtcNow,
        };

        // Activity timing
        npc.CurrentStart = new DateTime(jo["currentStartTicks"]?.GetValue<long>() ?? 0);
        npc.CurrentEnd = new DateTime(jo["currentEndTicks"]?.GetValue<long>() ?? 0);

        // Transit state (restore for walking NPCs)
        npc.IsInTransit = jo["isInTransit"]?.GetValue<bool>() ?? false;
        if (npc.IsInTransit)
        {
            npc.TransitFromLocationId = jo["transitFromLocationId"]?.GetValue<int>() ?? 0;
            npc.TransitToLocationId = jo["transitToLocationId"]?.GetValue<int>() ?? 0;
            npc.TransitStart = new DateTime(jo["transitStartTicks"]?.GetValue<long>() ?? 0);
            npc.TransitEnd = new DateTime(jo["transitEndTicks"]?.GetValue<long>() ?? 0);
        }

        // Positions
        npc.HomePosition = new System.Numerics.Vector3(
            jo["homePositionX"]?.GetValue<float>() ?? 0f,
            jo["homePositionY"]?.GetValue<float>() ?? 0f,
            jo["homePositionZ"]?.GetValue<float>() ?? 0f);
        npc.WorkplacePosition = new System.Numerics.Vector3(
            jo["workplacePositionX"]?.GetValue<float>() ?? 0f,
            jo["workplacePositionY"]?.GetValue<float>() ?? 0f,
            jo["workplacePositionZ"]?.GetValue<float>() ?? 0f);
        npc.CurrentWorldPosition = new System.Numerics.Vector3(
            jo["currentWorldPositionX"]?.GetValue<float>() ?? 0f,
            jo["currentWorldPositionY"]?.GetValue<float>() ?? 0f,
            jo["currentWorldPositionZ"]?.GetValue<float>() ?? 0f);

        // Properties
        npc.Properties = new System.Collections.Generic.Dictionary<string, float>();
        if (jo["properties"] is JsonObject jProps)
        {
            foreach (var kvp in jProps)
            {
                npc.Properties[kvp.Key] = kvp.Value?.GetValue<float>() ?? 0.5f;
            }
        }

        // Ensure all expected properties are initialized (backfill any missing from older saves)
        StoryletSelector.EnsurePropertiesInitialized(npc);

        // Trust
        npc.Trust = new System.Collections.Generic.Dictionary<int, float>();
        if (jo["trust"] is JsonObject jTrust)
        {
            foreach (var kvp in jTrust)
            {
                if (int.TryParse(kvp.Key, out int trustId))
                {
                    npc.Trust[trustId] = kvp.Value?.GetValue<float>() ?? 0f;
                }
            }
        }

        // Social venues
        npc.SocialVenueIds = new System.Collections.Generic.List<int>();
        if (jo["socialVenueIds"] is JsonArray jVenues)
        {
            foreach (var v in jVenues)
            {
                npc.SocialVenueIds.Add(v?.GetValue<int>() ?? 0);
            }
        }

        return npc;
    }
}
