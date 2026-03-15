using System;
using System.Collections.Generic;
using System.Numerics;

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


    /// <summary>
    /// Encode cluster index + NPC index into a globally unique NPC ID.
    /// 20 bits cluster (up to 1M clusters), 12 bits NPC index (up to 4096 per cluster).
    /// </summary>
    public static int MakeNpcId(int clusterIndex, int npcIndex) => (clusterIndex << 12) | npcIndex;
    public static int GetClusterIndex(int npcId) => npcId >> 12;
    public static int GetNpcIndex(int npcId) => npcId & 0xFFF;


    /// <summary>
    /// Compute position as a pure function of schedule state.
    /// For Tier 3, returns the current location position.
    /// </summary>
    public Vector3 PositionAt(DateTime gameTime, SpatialModel model)
    {
        if (model != null)
        {
            var loc = model.GetLocation(CurrentLocationId);
            return loc?.Position ?? HomePosition;
        }

        return HomePosition;
    }
}
