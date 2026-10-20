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

    // Phase 6: Deviation tracking. Set only through TaleManager.MarkPlayerContact
    // (or RegisterNpc for schedules loaded from a save): the save includes only
    // flagged NPCs, and the flag also puts the NPC into its cluster's skip mask.
    public bool HasPlayerDeviation;

    /// <summary>
    /// TALE S0 W3: the player has touched this NPC — it is flagged, holds a
    /// trust value toward the player (Trust[-1]) or remembers a player_fact.*.
    /// Such a schedule is never removed from TaleManager. Normally equal to
    /// HasPlayerDeviation; TaleManager warns and flags a schedule where the
    /// two disagree.
    /// </summary>
    public bool IsKnownToPlayer
    {
        get
        {
            if (HasPlayerDeviation) return true;
            if (Trust != null && Trust.ContainsKey(-1)) return true;
            if (Properties != null)
            {
                foreach (var key in Properties.Keys)
                {
                    if (key.StartsWith("player_fact.", StringComparison.Ordinal)) return true;
                }
            }
            return false;
        }
    }

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
    /// UTC timestamp of the last time the player initiated a conversation with this NPC.
    /// DateTime.MinValue = never talked to.
    ///
    /// Read by TaleManager.HasRecentConversation. Nothing in the shipped game
    /// decides on it any more: the Tier-2 / forget path that used it was dead
    /// code (TALE NPCs are never despawned by distance) and was removed in
    /// S0 W3 (KAR-447); an NPC the player knows is never forgotten.
    ///
    /// NOT a cooldown — conversations are not gated on this. Stamped by
    /// TaleManager.RecordConversation (called from Tale*Behavior.OnAction). On
    /// load, set to UtcNow.
    /// </summary>
    public DateTime LastConversationTime = DateTime.MinValue;

    // Transit phase: NPC is walking between locations
    /// <summary>
    /// Is this NPC currently in transit (walking between locations)?
    /// </summary>
    public bool IsInTransit;

    /// <summary>
    /// Source location ID for current transit phase.
    /// </summary>
    public int TransitFromLocationId;

    /// <summary>
    /// Destination location ID for current transit phase.
    /// </summary>
    public int TransitToLocationId;

    /// <summary>
    /// When the transit phase started.
    /// </summary>
    public DateTime TransitStart;

    /// <summary>
    /// When the transit phase ends (NPC arrives at destination).
    /// Conceptually equal to CurrentStart, but without time randomization jitter.
    /// </summary>
    public DateTime TransitEnd;


    /// <summary>
    /// Encode cluster index + NPC index into a globally unique NPC ID.
    /// 20 bits cluster (up to 1M clusters), 12 bits NPC index (up to 4096 per cluster).
    /// </summary>
    public static int MakeNpcId(int clusterIndex, int npcIndex) => (clusterIndex << 12) | npcIndex;
    public static int GetClusterIndex(int npcId) => npcId >> 12;
    public static int GetNpcIndex(int npcId) => npcId & 0xFFF;


    /// <summary>
    /// Compute position as a pure function of schedule state.
    /// If in transit, interpolates linearly between source and destination entry positions.
    /// Otherwise, returns the current location's entry position (door/shop front).
    /// </summary>
    public Vector3 PositionAt(DateTime gameTime, SpatialModel model)
    {
        if (model != null)
        {
            // Check if NPC is in transit and gameTime falls within transit window
            if (IsInTransit && gameTime >= TransitStart && gameTime < TransitEnd
                && (TransitEnd - TransitStart).TotalSeconds > 0)
            {
                // Interpolate position between transit source and destination
                float t = (float)(gameTime - TransitStart).TotalSeconds
                        / (float)(TransitEnd - TransitStart).TotalSeconds;
                var fromLoc = model.GetLocation(TransitFromLocationId);
                var toLoc = model.GetLocation(TransitToLocationId);
                if (fromLoc != null && toLoc != null)
                {
                    // EntryPositionFor spreads NPCs bound to the same junction
                    // across its sidewalk corners instead of stacking them.
                    var fromPos = fromLoc.EntryPositionFor(NpcId);
                    var toPos = toLoc.EntryPositionFor(NpcId);
                    return Vector3.Lerp(fromPos, toPos, Math.Clamp(t, 0f, 1f));
                }
            }

            // Not in transit: return current location position
            var loc = model.GetLocation(CurrentLocationId);
            if (loc != null)
                return loc.EntryPositionFor(NpcId);
            Error($"No position found for time {gameTime} npc {NpcId}");
            return HomePosition;
        }

        return HomePosition;
    }
}
