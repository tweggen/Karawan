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
    /// Dynamic subdivisions (obstructions, slow zones, etc.).
    /// </summary>
    public List<PipeSubdivision>? Subdivisions { get; set; }

    /// <summary>
    /// Calculate and set length based on NavLanes.
    /// </summary>
    public void ComputeLength()
    {
        Length = NavLanes.Sum(lane => Vector3.Distance(lane.Start.Position, lane.End.Position));
    }

    /// <summary>
    /// Add an obstruction that creates a subdivision.
    /// </summary>
    public void AddObstruction(
        Vector3 position,
        float radius,
        Func<Vector3, DateTime, float> speedFunction,
        string reason = "Obstruction")
    {
        Subdivisions ??= new List<PipeSubdivision>();

        var subdivision = new PipeSubdivision
        {
            StartPosition = position - new Vector3(radius, 0, radius),
            EndPosition = position + new Vector3(radius, 0, radius),
            LocalSpeedFunction = speedFunction,
            Reason = reason,
            CreatedAt = DateTime.Now
        };

        Subdivisions.Add(subdivision);
    }

    /// <summary>
    /// Remove an obstruction by position.
    /// </summary>
    public void RemoveObstruction(Vector3 position, float tolerance = 1.0f)
    {
        if (Subdivisions == null)
            return;

        Subdivisions.RemoveAll(s =>
            Vector3.Distance(s.StartPosition, position) < tolerance ||
            Vector3.Distance(s.EndPosition, position) < tolerance);
    }

    /// <summary>
    /// Check if pipe has active subdivisions.
    /// </summary>
    public bool HasSubdivisions => Subdivisions?.Count > 0;

    /// <summary>
    /// Clear all subdivisions (e.g., when obstruction clears).
    /// </summary>
    public void ClearSubdivisions()
    {
        Subdivisions?.Clear();
    }

    /// <summary>
    /// Get the movement speed at a specific position and time.
    /// Takes into account global constraints (traffic lights, etc.) and subdivisions.
    /// </summary>
    public float GetSpeedAt(Vector3 position, DateTime currentTime)
    {
        // Step 1: Check global constraint (e.g., traffic light)
        if (GlobalConstraint != null)
        {
            var state = GlobalConstraint.Query(currentTime);
            if (!state.CanAccess)
                return 0.0f;  // Red light: stop completely
        }

        // Step 2: Check local constraints (subdivisions)
        var subdivision = FindSubdivisionAt(position);
        if (subdivision != null)
            return subdivision.GetSpeed(position, currentTime);

        // Step 3: Apply pipe-wide speed function
        if (SpeedFunction != null)
            return SpeedFunction(position, currentTime);

        // Default: standard speed for this type
        return GetDefaultSpeedForType(SupportedType);
    }

    private PipeSubdivision? FindSubdivisionAt(Vector3 position)
    {
        return Subdivisions?.FirstOrDefault(s => s.ContainsPosition(position));
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
