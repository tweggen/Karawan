using System;
using System.Numerics;

namespace engine.navigation;

/// <summary>
/// A dynamic subdivision within a pipe.
/// Represents a region with different speed characteristics
/// (obstruction, congestion, etc.).
/// </summary>
public class PipeSubdivision
{
    /// <summary>
    /// Starting position of this subdivision.
    /// </summary>
    public Vector3 StartPosition { get; set; }

    /// <summary>
    /// Ending position of this subdivision.
    /// </summary>
    public Vector3 EndPosition { get; set; }

    /// <summary>
    /// Length of this subdivision (computed).
    /// </summary>
    public float Length => Vector3.Distance(StartPosition, EndPosition);

    /// <summary>
    /// Speed function for this subdivision.
    /// f(position, time) → speed in m/s.
    /// </summary>
    public Func<Vector3, DateTime, float>? LocalSpeedFunction { get; set; }

    /// <summary>
    /// Description of what caused this subdivision (for debugging).
    /// </summary>
    public string? Reason { get; set; } = "Obstruction";

    /// <summary>
    /// When this subdivision was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Check if a position is within this subdivision.
    /// </summary>
    public bool ContainsPosition(Vector3 position)
    {
        // Simplified: check if between start and end
        var distToStart = Vector3.Distance(position, StartPosition);
        var distToEnd = Vector3.Distance(position, EndPosition);
        var totalDist = Length;

        return (distToStart + distToEnd) <= (totalDist * 1.05f);  // 5% tolerance
    }

    /// <summary>
    /// Get speed at a specific position and time.
    /// </summary>
    public float GetSpeed(Vector3 position, DateTime currentTime)
    {
        if (LocalSpeedFunction == null)
            return 1.0f;  // Default slow speed

        return LocalSpeedFunction(position, currentTime);
    }

    public override string ToString()
        => $"PipeSubdivision({Reason}, len={Length:F1}m)";
}
