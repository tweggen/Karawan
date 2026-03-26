using System;
using System.Collections.Generic;
using System.Numerics;
using engine.navigation;
using static engine.Logger;

namespace engine.tale;

public class NpcSchedule
{
    public int NpcId;
    public int Seed;
    public string Role; // "worker", "merchant", "socialite", "drifter", "authority"

    // Cluster population identity (for seed-based regeneration)
    public int ClusterIndex;
    public int NpcIndex;

    // Current state
    public int CurrentLocationId;
    public string CurrentStorylet;
    public DateTime CurrentStart;
    public DateTime CurrentEnd;
    public int ScheduleStep;

    // Assigned locations (from spatial model assignment)
    public int HomeLocationId;
    public int WorkplaceLocationId;
    public List<int> SocialVenueIds;

    // World positions (for live game fragment mapping, independent of SpatialModel)
    public Vector3 HomePosition;
    public Vector3 WorkplacePosition;
    public Vector3 CurrentWorldPosition;

    // Properties (0.0-1.0 range)
    public Dictionary<string, float> Properties;

    // Per-NPC trust relationships
    public Dictionary<int, float> Trust;

    // Group membership (-1 = no group)
    public int GroupId = -1;

    // Phase 5: Interrupt and escalation support
    public ArcStack ArcStack = new();
    public int LastEncounterPartnerId = -1;
    public string? NextForcedStorylet;

    // Phase 6: Deviation tracking
    public bool HasPlayerDeviation;

    // Phase D: Routing preferences
    /// <summary>
    /// Preferred transportation type for this NPC.
    /// </summary>
    public TransportationType PreferredTransportationType { get; set; } =
        TransportationType.Pedestrian;

    /// <summary>
    /// Routing preferences for multi-objective pathfinding (goal, urgency, weights).
    /// </summary>
    public RoutingPreferences RoutingPreferences { get; set; } = new();

    /// <summary>
    /// Next scheduled activity time.
    /// </summary>
    public DateTime? NextEventTime { get; set; }

    /// <summary>
    /// Is this NPC late for its next scheduled activity?
    /// </summary>
    public bool IsLate
    {
        get
        {
            if (NextEventTime == null)
                return false;
            return DateTime.Now > NextEventTime.Value;
        }
    }

    /// <summary>
    /// Tier 2: Has this NPC been noticed (materialized as Tier 1) by the player?
    /// Once true, persists for the session. NPCs keep map icons visible after dematerialization.
    /// </summary>
    public bool IsNoticedByPlayer { get; set; } = false;


    /// <summary>
    /// Encode cluster index + NPC index into a globally unique NPC ID.
    /// 20 bits cluster (up to 1M clusters), 12 bits NPC index (up to 4096 per cluster).
    /// </summary>
    public static int MakeNpcId(int clusterIndex, int npcIndex) => (clusterIndex << 12) | npcIndex;
    public static int GetClusterIndex(int npcId) => npcId >> 12;
    public static int GetNpcIndex(int npcId) => npcId & 0xFFF;


    /// <summary>
    /// Compute position as a pure function of schedule state.
    /// For Tier 3, returns the current location's entry position (door/shop front).
    /// </summary>
    public Vector3 PositionAt(DateTime gameTime, SpatialModel model)
    {
        if (model != null)
        {
            var loc = model.GetLocation(CurrentLocationId);
            if (loc != null)
                return loc.EntryPosition != Vector3.Zero ? loc.EntryPosition : loc.Position;
            Error($"No position found for time {gameTime} npc {NpcId}");
            return HomePosition;
        }

        return HomePosition;
    }
}
