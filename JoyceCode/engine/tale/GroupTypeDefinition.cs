using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// Defines a group type with classification rules.
/// Game authors configure group types in /groups/types JSON config.
/// </summary>
public class GroupTypeDefinition
{
    /// <summary>Unique identifier (e.g., "patrol_unit", "criminal", "trade").</summary>
    public string Id { get; set; }

    /// <summary>Display name for UI (e.g., "Criminal Syndicate").</summary>
    public string DisplayName { get; set; }

    /// <summary>Classification rules that determine if a group matches this type.</summary>
    public List<GroupClassificationRule> Rules { get; set; } = new();

    /// <summary>Priority for classification (higher = checked first, useful for overlapping rules).</summary>
    public int Priority { get; set; } = 0;
}

/// <summary>
/// A single classification rule within a group type definition.
/// Rules can check properties like authority count, wealth, morality, etc.
/// </summary>
public class GroupClassificationRule
{
    /// <summary>
    /// Rule type: "authority_threshold", "property_threshold", etc.
    /// Determines how Parameters are interpreted.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Parameters for the rule (varies by Type).
    /// Examples:
    ///   - authority_threshold: { "minimum_ratio": 0.5 }
    ///   - property_threshold: { "property": "wealth", "operator": "less_than", "value": 0.3 }
    /// </summary>
    public Dictionary<string, float> Parameters { get; set; } = new();
}
