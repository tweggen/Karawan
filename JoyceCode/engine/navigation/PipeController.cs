using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace engine.navigation;

/// <summary>
/// Updates entity movement through pipes each frame.
/// </summary>
public class PipeController
{
    private PipeNetwork _network;
    private Dictionary<int, MovingEntity> _entities;
    private List<MovingEntity> _offPipeEntities;
    private List<(Vector3 position, float radius, ITemporalConstraint duration)> _activeObstructions;

    public PipeController(PipeNetwork network)
    {
        _network = network;
        _entities = new Dictionary<int, MovingEntity>();
        _offPipeEntities = new List<MovingEntity>();
        _activeObstructions = new List<(Vector3, float, ITemporalConstraint)>();
    }

    /// <summary>
    /// Update all entities' positions this frame.
    /// </summary>
    public void UpdateFrame(float deltaTime, DateTime currentTime)
    {
        // Check for expired obstructions
        UpdateObstructions(currentTime);

        // Move entities in pipes
        foreach (var pipe in _network.Pipes)
        {
            UpdatePipeEntities(pipe, deltaTime, currentTime);
        }

        // Update off-pipe entities (physics, manual control)
        UpdateOffPipeEntities(deltaTime, currentTime);
    }

    /// <summary>
    /// Register an obstruction on a pipe.
    /// Automatically creates a subdivision with appropriate speed function.
    /// </summary>
    public void RegisterObstruction(
        Vector3 position,
        float radius,
        ITemporalConstraint duration,
        string reason = "Obstruction")
    {
        _activeObstructions.Add((position, radius, duration));

        // Find affected pipe and add subdivision
        var pipe = _network.FindPipeContaining(position);
        if (pipe != null)
        {
            var speedFunc = SpeedFunctions.BrakingWave(
                position,
                DateTime.Now,
                normalSpeed: 10.0f);

            pipe.AddObstruction(position, radius, speedFunc, reason);
        }
    }

    /// <summary>
    /// Update obstruction states; remove expired ones.
    /// </summary>
    public void UpdateObstructions(DateTime currentTime)
    {
        var expired = new List<(Vector3, float, ITemporalConstraint)>();

        foreach (var (position, radius, duration) in _activeObstructions)
        {
            var state = duration.Query(currentTime);
            if (!state.CanAccess)
            {
                // Obstruction duration expired
                expired.Add((position, radius, duration));

                // Remove from all pipes
                foreach (var pipe in _network.Pipes)
                {
                    pipe.RemoveObstruction(position);
                }
            }
        }

        // Remove expired obstructions from tracking
        foreach (var obstruction in expired)
        {
            _activeObstructions.Remove(obstruction);
        }
    }

    /// <summary>
    /// Move all entities in a specific pipe.
    /// </summary>
    private void UpdatePipeEntities(Pipe pipe, float deltaTime, DateTime currentTime)
    {
        var entitiesToRemove = new List<MovingEntity>();

        foreach (var entity in pipe.Entities.ToList())
        {
            // Get current speed for this entity at this position
            var speed = pipe.GetSpeedAt(entity.Position, currentTime);

            if (speed > 0)
            {
                // Move entity forward
                var displacement = speed * deltaTime * entity.Direction;
                entity.Position += displacement;
                entity.Speed = speed;

                // Check if entity reached end of pipe
                if (Vector3.Distance(entity.Position, pipe.EndPosition) < 1.0f)
                {
                    // Mark for removal, will transition to next pipe
                    entitiesToRemove.Add(entity);
                }
            }
            else
            {
                // Speed is zero, entity waits
                entity.Speed = 0;
            }
        }

        // Handle pipe transitions (will implement in Phase C)
        foreach (var entity in entitiesToRemove)
        {
            pipe.Entities.Dequeue();
            TransitionEntityToNextPipe(entity, currentTime);
        }
    }

    /// <summary>
    /// Move entities that are currently off the pipe system.
    /// </summary>
    private void UpdateOffPipeEntities(float deltaTime, DateTime currentTime)
    {
        // Off-pipe entities can move freely or be controlled by physics
        // For now, just track them
    }

    /// <summary>
    /// Place an entity into the pipe system at the start of its route.
    /// </summary>
    public void PlaceEntity(MovingEntity entity)
    {
        if (entity.Route.Count == 0)
            return;

        var startLane = entity.Route[0];
        var startPipe = _network.FindPipeContaining(startLane.Start.Position);

        if (startPipe == null)
        {
            // No valid pipe for start position
            _offPipeEntities.Add(entity);
            return;
        }

        entity.Position = startLane.Start.Position;
        entity.Direction = Vector3.Normalize(startLane.End.Position - startLane.Start.Position);
        startPipe.Entities.Enqueue(entity);
        entity.CurrentPipe = startPipe;

        _entities[entity.Id] = entity;
    }

    /// <summary>
    /// Remove an entity from its pipe (pushed by physics, etc.).
    /// </summary>
    public void RemoveEntityFromPipe(MovingEntity entity)
    {
        if (entity.CurrentPipe == null)
            return;

        // Remove from pipe's queue carefully
        if (entity.CurrentPipe.Entities.Contains(entity))
        {
            var entities = entity.CurrentPipe.Entities.ToList();
            entities.Remove(entity);
            entity.CurrentPipe.Entities.Clear();
            foreach (var e in entities)
            {
                entity.CurrentPipe.Entities.Enqueue(e);
            }
        }

        entity.CurrentPipe = null;
        _offPipeEntities.Add(entity);
    }

    /// <summary>
    /// Re-enter an entity that was off-pipe.
    /// </summary>
    public void ReEnterPipe(MovingEntity entity, Vector3 position)
    {
        var pipe = _network.FindPipeContaining(position);
        if (pipe == null)
            return;

        entity.Position = position;
        entity.CurrentPipe = pipe;
        pipe.Entities.Enqueue(entity);

        _offPipeEntities.Remove(entity);
    }

    /// <summary>
    /// Move entity to the next pipe in its route.
    /// </summary>
    private void TransitionEntityToNextPipe(MovingEntity entity, DateTime currentTime)
    {
        // Increment route index
        entity.RouteIndex++;

        if (entity.HasReachedDestination)
        {
            // Entity has completed its route
            _entities.Remove(entity.Id);
            return;
        }

        // Find next pipe
        var nextLane = entity.Route[entity.RouteIndex];
        var nextPipe = _network.FindPipeContaining(nextLane.Start.Position);

        if (nextPipe != null)
        {
            entity.Position = nextLane.Start.Position;
            entity.Direction = Vector3.Normalize(nextLane.End.Position - nextLane.Start.Position);
            nextPipe.Entities.Enqueue(entity);
            entity.CurrentPipe = nextPipe;
        }
        else
        {
            // No pipe found, put off-pipe
            _offPipeEntities.Add(entity);
            entity.CurrentPipe = null;
        }
    }

    /// <summary>
    /// Get an entity by ID.
    /// </summary>
    public MovingEntity? GetEntity(int id)
    {
        _entities.TryGetValue(id, out var entity);
        return entity;
    }

    /// <summary>
    /// Get all off-pipe entities.
    /// </summary>
    public IEnumerable<MovingEntity> GetOffPipeEntities()
    {
        return _offPipeEntities;
    }

    /// <summary>
    /// Get all entities.
    /// </summary>
    public IEnumerable<MovingEntity> GetAllEntities()
    {
        return _entities.Values.Concat(_offPipeEntities);
    }
}
