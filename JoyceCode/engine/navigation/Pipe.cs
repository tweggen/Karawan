using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.modules.satnav.desc;

namespace engine.navigation;

/// <summary>
/// A flow container for entities moving through connected NavLanes.
/// All entities in a pipe experience the same movement constraints.
/// </summary>
public class Pipe
{
    /// <summary>
    /// Unique identifier within the network.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The NavLanes that make up this pipe.
    /// </summary>
    public List<NavLane> NavLanes { get; set; } = new();

    /// <summary>
    /// Starting position (beginning of first NavLane).
    /// </summary>
    public Vector3 StartPosition { get; set; }

    /// <summary>
    /// Ending position (end of last NavLane).
    /// </summary>
    public Vector3 EndPosition { get; set; }

    /// <summary>
    /// Total length of all NavLanes in this pipe.
    /// </summary>
    public float Length { get; set; }

    /// <summary>
    /// Entities currently in this pipe.
    /// </summary>
    public Queue<MovingEntity> Entities { get; set; } = new();

    /// <summary>
    /// Speed function: f(position, time) → speed in m/s.
    /// Null means use default speed calculation.
    /// </summary>
    public Func<Vector3, DateTime, float>? SpeedFunction { get; set; }

    /// <summary>
    /// Global temporal constraint on this pipe (e.g., traffic light).
    /// Null means no constraint.
    /// </summary>
    public ITemporalConstraint? GlobalConstraint { get; set; }

    /// <summary>
    /// Maximum entities this pipe can hold.
    /// </summary>
    public int MaxCapacity { get; set; } = int.MaxValue;

    /// <summary>
    /// Current number of entities in this pipe.
    /// </summary>
    public int CurrentOccupancy => Entities.Count;

    /// <summary>
    /// Transportation type this pipe supports.
    /// </summary>
    public TransportationType SupportedType { get; set; }

    /// <summary>
    /// Calculate and set length based on NavLanes.
    /// </summary>
    public void ComputeLength()
    {
        Length = NavLanes.Sum(lane => Vector3.Distance(lane.Start.Position, lane.End.Position));
    }

    /// <summary>
    /// Get the movement speed at a specific position and time.
    /// </summary>
    public float GetSpeedAt(Vector3 position, DateTime currentTime)
    {
        // Check global constraint
        if (GlobalConstraint != null)
        {
            var state = GlobalConstraint.Query(currentTime);
            if (!state.CanAccess)
                return 0.0f;  // Blocked
        }

        // Apply speed function if present
        if (SpeedFunction != null)
            return SpeedFunction(position, currentTime);

        // Default: return typical speed for this transport type
        return GetDefaultSpeedForType(SupportedType);
    }

    private float GetDefaultSpeedForType(TransportationType type)
    {
        return type switch
        {
            TransportationType.Pedestrian => 1.5f,
            TransportationType.Car => 13.4f,
            TransportationType.Bicycle => 5.0f,
            TransportationType.Bus => 11.0f,
            _ => 1.5f
        };
    }

    /// <summary>
    /// Check if a position is within this pipe's bounds.
    /// </summary>
    public bool ContainsPosition(Vector3 position)
    {
        // Simplified: check if within start/end bounds
        // Future: more sophisticated spatial query
        var distance = Vector3.Distance(position, StartPosition) +
                      Vector3.Distance(position, EndPosition);
        return distance <= (Length * 1.1f);  // 10% tolerance
    }

    public override string ToString()
        => $"Pipe(id={Id}, lanes={NavLanes.Count}, length={Length:F1}m, entities={CurrentOccupancy})";
}
