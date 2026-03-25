using System;
using builtin.modules.satnav.desc;
using engine.navigation;

namespace engine.tale;

/// <summary>
/// Routing preferences for an NPC, affecting A* cost calculation.
/// </summary>
public class RoutingPreferences
{
    /// <summary>
    /// The primary routing goal.
    /// </summary>
    public NpcGoal Goal { get; set; } = NpcGoal.Fast;

    /// <summary>
    /// For OnTime goal: when does the NPC need to arrive?
    /// </summary>
    public DateTime? DeadlineTime { get; set; }

    /// <summary>
    /// For Scenic goal: weight for scenic attribute (0-1).
    /// Higher = more preference for scenic routes.
    /// </summary>
    public float SceneryWeight { get; set; } = 0.5f;

    /// <summary>
    /// For Safe goal: weight for safety/traffic avoidance (0-1).
    /// Higher = more avoidance of high-traffic areas.
    /// </summary>
    public float SafetyWeight { get; set; } = 0.5f;

    /// <summary>
    /// Urgency level (0-1).
    /// 0 = relaxed, 1 = very urgent
    /// Used to modulate routing preference.
    /// </summary>
    public float Urgency { get; set; } = 0.0f;

    /// <summary>
    /// Compute a cost multiplier for a specific lane.
    /// Values > 1.0 make the lane less desirable.
    /// </summary>
    public float ComputeCostMultiplier(NavLane lane, TransportationType type)
    {
        // Base cost (distance/speed) is already in the lane
        // This multiplier adjusts it based on goal

        return Goal switch
        {
            NpcGoal.Fast =>
                1.0f,  // No adjustment

            NpcGoal.OnTime =>
                ComputeOnTimeMultiplier(lane, type),

            NpcGoal.Scenic =>
                ComputeScenicMultiplier(lane),

            NpcGoal.Safe =>
                ComputeSafetyMultiplier(lane),

            NpcGoal.Custom =>
                1.0f,

            _ => 1.0f
        };
    }

    /// <summary>
    /// For OnTime goal: prefer routes that avoid high-wait situations.
    /// Increase cost of lanes with long expected waits.
    /// </summary>
    private float ComputeOnTimeMultiplier(NavLane lane, TransportationType type)
    {
        if (DeadlineTime == null || Urgency < 0.5f)
            return 1.0f;  // Not urgent

        // Penalize lanes with temporal constraints (traffic lights, etc.)
        if (lane.Constraint != null)
        {
            var state = lane.QueryConstraint(DateTime.Now);
            var untilChange = state.UntilChange.TotalSeconds;

            // If light is red and will take a while to change, penalize heavily
            if (!state.CanAccess && untilChange > 30)
                return 1.5f;  // 50% cost increase
            else if (!state.CanAccess)
                return 1.2f;  // 20% cost increase
        }

        return 1.0f;
    }

    /// <summary>
    /// For Scenic goal: prefer lanes with high scenic score.
    /// </summary>
    private float ComputeScenicMultiplier(NavLane lane)
    {
        // Lane.ScenicScore (0-1, not defined in current NavLane but can be added)
        // For now, assume lanes near parks/water have higher scores
        // This is a placeholder; actual implementation depends on lane data

        // Example: prefer lanes with scenic=0.7+ (parks, waterfronts)
        // For simplicity, we'll assume some lanes have scenic properties
        // For now, return neutral; implement scenic scoring later
        return 1.0f;
    }

    /// <summary>
    /// For Safe goal: avoid high-traffic areas.
    /// Penalize lanes in busy intersections, highways.
    /// </summary>
    private float ComputeSafetyMultiplier(NavLane lane)
    {
        // Lane.TrafficDensity (0-1, not defined in current NavLane but can be added)
        // For now, assume busy lanes have higher density
        // This is a placeholder; actual implementation depends on observed traffic

        // Example: avoid lanes with traffic_density > 0.7
        // For simplicity, return neutral; implement traffic density tracking later

        return 1.0f;
    }

    /// <summary>
    /// Check if this NPC is late (current time > deadline).
    /// </summary>
    public bool IsLate => DeadlineTime.HasValue &&
                          DateTime.Now > DeadlineTime.Value;

    /// <summary>
    /// Get urgency based on time until deadline.
    /// </summary>
    public void UpdateUrgency(DateTime currentTime)
    {
        if (!DeadlineTime.HasValue)
        {
            Urgency = 0.0f;
            return;
        }

        var timeRemaining = (DeadlineTime.Value - currentTime).TotalMinutes;

        if (timeRemaining < 0)
            Urgency = 1.0f;  // Already late
        else if (timeRemaining < 5)
            Urgency = 0.9f;  // Very urgent
        else if (timeRemaining < 15)
            Urgency = 0.7f;  // Urgent
        else if (timeRemaining < 30)
            Urgency = 0.5f;  // Moderately urgent
        else
            Urgency = 0.2f;  // Relaxed
    }

    public override string ToString()
        => $"RoutingPreferences({Goal}, urgency={Urgency:F1})";
}
