using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// Registry for relationship tier definitions.
/// Provides trust-to-tier mapping and tier lookup by ID.
/// </summary>
public class RelationshipTierRegistry : ObjectRegistry<RelationshipTierDefinition>
{
    /// <summary>
    /// Get the tier ID for a given trust value.
    /// Searches tiers in registration order, returns first tier where trust falls in range.
    /// </summary>
    public string GetTierFromTrust(float trust)
    {
        trust = Math.Clamp(trust, 0f, 1f);

        // Search registered tiers - they should be ordered by MinTrust
        foreach (var tierId in GetKeys())
        {
            var tier = Get(tierId);
            if (tier != null && trust >= tier.MinTrust && trust < tier.MaxTrust)
                return tierId;
        }

        // Fallback to last registered tier (usually "ally" or similar)
        var allKeys = GetKeys();
        if (allKeys.Count > 0)
            return allKeys[allKeys.Count - 1];

        return "unknown";
    }

    /// <summary>
    /// Get tier display name for a trust value.
    /// </summary>
    public string GetTierDisplayName(float trust)
    {
        var tierId = GetTierFromTrust(trust);
        var tier = Get(tierId);
        return tier?.DisplayName ?? tierId;
    }
}
