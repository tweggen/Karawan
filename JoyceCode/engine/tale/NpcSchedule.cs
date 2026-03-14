using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.tale;

public class NpcSchedule
{
    public int NpcId;
    public int Seed;
    public string Role; // "Worker", "Merchant", "Socialite", "Drifter", "Authority"

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


    /// <summary>
    /// Compute position as a pure function of schedule state.
    /// For Tier 3, returns the current location position.
    /// </summary>
    public Vector3 PositionAt(DateTime gameTime, SpatialModel model)
    {
        var loc = model.GetLocation(CurrentLocationId);
        return loc?.Position ?? Vector3.Zero;
    }
}
