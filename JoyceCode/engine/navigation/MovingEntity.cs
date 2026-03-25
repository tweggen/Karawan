using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.modules.satnav.desc;

namespace engine.navigation;

/// <summary>
/// An entity moving through pipes (car, NPC, etc.).
/// </summary>
public class MovingEntity
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Current position in world space.
    /// </summary>
    public Vector3 Position { get; set; }

    /// <summary>
    /// Current forward direction (unit vector).
    /// </summary>
    public Vector3 Direction { get; set; } = Vector3.UnitZ;

    /// <summary>
    /// Current movement speed (m/s).
    /// </summary>
    public float Speed { get; set; }

    /// <summary>
    /// The pipe this entity is currently in.
    /// Null if off-pipe (pushed by physics, etc.).
    /// </summary>
    public Pipe? CurrentPipe { get; set; }

    /// <summary>
    /// The full route this entity is following (from A*).
    /// </summary>
    public List<NavLane> Route { get; set; } = new();

    /// <summary>
    /// Current index in the route.
    /// </summary>
    public int RouteIndex { get; set; }

    /// <summary>
    /// Transportation type for this entity.
    /// </summary>
    public TransportationType TransportType { get; set; } = TransportationType.Pedestrian;

    /// <summary>
    /// Has this entity reached its destination?
    /// </summary>
    public bool HasReachedDestination
    {
        get => RouteIndex >= Route.Count;
    }

    public override string ToString()
        => $"MovingEntity(id={Id}, pos={Position}, pipe={CurrentPipe?.Id}, route={RouteIndex}/{Route.Count})";
}
