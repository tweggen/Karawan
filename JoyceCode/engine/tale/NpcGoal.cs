namespace engine.tale;

/// <summary>
/// The primary goal affecting an NPC's routing decisions.
/// </summary>
public enum NpcGoal
{
    /// <summary>
    /// Minimize travel time (shortest path).
    /// Default for most NPCs.
    /// </summary>
    Fast,

    /// <summary>
    /// Arrive by a specific deadline (e.g., work by 9am).
    /// Prefers routes that allow time for delays.
    /// </summary>
    OnTime,

    /// <summary>
    /// Prefer scenic or pleasant routes.
    /// Leisure NPCs, people exploring.
    /// </summary>
    Scenic,

    /// <summary>
    /// Avoid dangerous or high-traffic areas.
    /// Cautious NPCs, people with anxiety.
    /// </summary>
    Safe,

    /// <summary>
    /// Custom goal (defined by subclass or property).
    /// For future extension.
    /// </summary>
    Custom
}
