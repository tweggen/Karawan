using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace builtin.tools.Lindenmayer;

/// <summary>
/// JSON DTO for an L-system definition.
/// </summary>
public class LSystemDefinition
{
    /// <summary>
    /// Unique name for this L-system.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Optional description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Type of L-system: "standard" (default) or "parametric" (requires C# logic).
    /// Parametric L-systems use Config for parameters but keep complex logic in C#.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "standard";

    /// <summary>
    /// Configuration for parametric L-systems (houses, complex structures).
    /// </summary>
    [JsonPropertyName("config")]
    public LSystemConfig? Config { get; set; }

    /// <summary>
    /// The initial state (seed) of the L-system.
    /// </summary>
    [JsonPropertyName("seed")]
    public SeedDefinition Seed { get; set; } = new();

    /// <summary>
    /// Transformation rules applied iteratively.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<RuleDefinition> Rules { get; set; } = new();

    /// <summary>
    /// Macro rules applied once at the end to convert to turtle commands.
    /// </summary>
    [JsonPropertyName("macros")]
    public List<RuleDefinition> Macros { get; set; } = new();
}


/// <summary>
/// Configuration for parametric L-systems that require C# logic.
/// </summary>
public class LSystemConfig
{
    /// <summary>
    /// Height of one story in meters.
    /// </summary>
    [JsonPropertyName("storyHeight")]
    public float StoryHeight { get; set; } = 3.0f;

    /// <summary>
    /// Minimum number of stories before segmentation is considered.
    /// </summary>
    [JsonPropertyName("minSegmentStories")]
    public int MinSegmentStories { get; set; } = 4;

    /// <summary>
    /// Amount to shrink polygon when creating upper segments.
    /// </summary>
    [JsonPropertyName("shrinkAmount")]
    public float ShrinkAmount { get; set; } = 2.0f;

    /// <summary>
    /// Probability of segmenting a buildable into base + upper.
    /// </summary>
    [JsonPropertyName("segmentProbability")]
    public float SegmentProbability { get; set; } = 0.8f;

    /// <summary>
    /// Probability of further segmenting upper parts.
    /// </summary>
    [JsonPropertyName("upperSegmentProbability")]
    public float UpperSegmentProbability { get; set; } = 0.9f;

    /// <summary>
    /// Probability of keeping as single block.
    /// </summary>
    [JsonPropertyName("singleBlockProbability")]
    public float SingleBlockProbability { get; set; } = 0.1f;

    /// <summary>
    /// Material names for different parts.
    /// </summary>
    [JsonPropertyName("materials")]
    public Dictionary<string, string> Materials { get; set; } = new();
}


/// <summary>
/// JSON DTO for the seed (initial state).
/// </summary>
public class SeedDefinition
{
    /// <summary>
    /// The initial parts.
    /// </summary>
    [JsonPropertyName("parts")]
    public List<PartDefinition> Parts { get; set; } = new();
}


/// <summary>
/// JSON DTO for a rule (transformation or macro).
/// </summary>
public class RuleDefinition
{
    /// <summary>
    /// The part name to match (e.g., "stem(r,l)").
    /// </summary>
    [JsonPropertyName("match")]
    public string Match { get; set; } = "";

    /// <summary>
    /// Probability of this rule being applied (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("probability")]
    public float Probability { get; set; } = 1.0f;

    /// <summary>
    /// Condition expression that must evaluate to true for rule to apply.
    /// Example: "$r > 0.02 && $l > 0.1"
    /// </summary>
    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    /// <summary>
    /// The parts to replace the matched part with.
    /// </summary>
    [JsonPropertyName("transform")]
    public List<PartDefinition> Transform { get; set; } = new();
}


/// <summary>
/// JSON DTO for a part (turtle command).
/// </summary>
public class PartDefinition
{
    /// <summary>
    /// The part name (e.g., "stem(r,l)", "rotate(d,x,y,z)", "push()").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Parameters for this part. Values can be literals or expression strings.
    /// Example: { "r": 0.1, "l": "$l * 0.8" }
    /// </summary>
    [JsonPropertyName("params")]
    public Dictionary<string, object>? Params { get; set; }
}


/// <summary>
/// Container for multiple L-system definitions.
/// </summary>
public class LSystemCatalog
{
    /// <summary>
    /// Collection of L-system definitions.
    /// </summary>
    [JsonPropertyName("lsystems")]
    public List<LSystemDefinition> LSystems { get; set; } = new();
}
