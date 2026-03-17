using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// Metadata for a single NPC interaction type. Loaded from game configuration.
/// Encapsulates trust impact, selection conditions, and categorization.
/// </summary>
public class InteractionTypeDefinition
{
    /// <summary>Unique identifier (e.g., "greet", "rob").</summary>
    public string Id { get; set; }

    /// <summary>Display name for UI/logging (e.g., "Greeting", "Robbery").</summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Trust delta applied when this interaction occurs.
    /// Positive = increases trust, Negative = decreases trust, Zero = neutral.
    /// </summary>
    public float TrustDelta { get; set; }

    /// <summary>
    /// Category for metrics and classification.
    /// Values: "positive", "negative", "conflict", "authority", "special".
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Priority order for selection. Higher numbers are checked first.
    /// Used to determine order of condition checking in DetermineInteractionType().
    /// Example: Authority interactions priority=100, defaults priority=10.
    /// </summary>
    public int SelectionPriority { get; set; }

    /// <summary>
    /// Selection condition class name. Must implement IInteractionCondition.
    /// Example: "engine.tale.conditions.TrustBasedCondition"
    /// If null, this type is never automatically selected (manual-only).
    /// </summary>
    public string ConditionClassName { get; set; }

    /// <summary>
    /// Condition parameters as a flat property dictionary.
    /// Parsed by the condition class. Examples:
    /// - "minTrust": 0.2, "maxTrust": 0.5
    /// - "minMorality": 0.3, "minDesperation": 0.6
    /// - "probability": 0.4
    /// </summary>
    public Dictionary<string, float> ConditionParameters { get; set; } = new();
}
