using System;

namespace engine.tale;

/// <summary>
/// Defines a relationship tier with trust range thresholds.
/// Game authors configure tiers in /relationships/tiers JSON config.
/// </summary>
public class RelationshipTierDefinition
{
    /// <summary>Unique identifier (e.g., "stranger", "friend", "ally").</summary>
    public string Id { get; set; }

    /// <summary>Display name for UI (e.g., "Close Friend").</summary>
    public string DisplayName { get; set; }

    /// <summary>Minimum trust value to enter this tier (inclusive).</summary>
    public float MinTrust { get; set; }

    /// <summary>Maximum trust value in this tier (exclusive, or 1.0 for final tier).</summary>
    public float MaxTrust { get; set; }
}
