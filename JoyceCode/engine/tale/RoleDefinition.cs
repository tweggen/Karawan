using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// Metadata for a single NPC role. Loaded from game configuration.
/// Encapsulates all role-specific behavior: location preferences, properties, capabilities.
/// </summary>
public class RoleDefinition
{
    /// <summary>Unique identifier (e.g., "worker", "merchant").</summary>
    public string Id { get; set; }

    /// <summary>Display name (e.g., "Factory Worker").</summary>
    public string DisplayName { get; set; }

    /// <summary>Default spawn probability (0.0–1.0). Normalized against all roles.</summary>
    public float DefaultWeight { get; set; }

    /// <summary>Hour of day when this role typically wakes (0–23.99). Used for initial scheduling.</summary>
    public float BaseWakeHour { get; set; }

    /// <summary>
    /// Location type preferences. Maps location type name → list of preferred types.
    /// Example: "workplace" → ["workplace", "shop"] means this role prefers workplace or shop for work.
    /// </summary>
    public Dictionary<string, List<string>> LocationPreferences { get; set; } = new();

    /// <summary>
    /// Property ranges for initial NPC generation. Each property maps to [min, max].
    /// Used in deterministic generation from seed.
    /// </summary>
    public Dictionary<string, (float Min, float Max)> PropertyRanges { get; set; } = new();

    /// <summary>
    /// Request types this role can fulfill (e.g., ["food_delivery", "trade_service"]).
    /// Used for interaction request claiming and abstract resolution.
    /// </summary>
    public List<string> FulfillableRequestTypes { get; set; } = new();

    /// <summary>
    /// Optional marker role for group classification.
    /// If "Authority" is in group, classified as "patrol_unit"; if both "merchant" + "trader", classified as "trade".
    /// </summary>
    public string GroupClassificationMarker { get; set; }

    /// <summary>
    /// Optional special interaction type when this role encounters others (e.g., "patrol_check" for Authority).
    /// </summary>
    public string SpecialInteractionType { get; set; }
}
