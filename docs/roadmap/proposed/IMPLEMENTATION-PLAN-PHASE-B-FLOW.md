# Phase B Implementation Plan: Flow — Pipes & Basic Movement

**Phase:** B (Flow)
**Duration:** ~3-4 days (actual: 1 day)
**Status:** ✅ COMPLETE (2026-03-25)
**Commit:** 10935509
**Depends On:** Phase A ✅
**Enables:** Phase C (ready), Phase D (pending Phase C)

---

## Overview

Phase B implements the pipe system for flow-based entity movement. Pipes act as containers for entities moving through connected NavLanes with uniform flow properties.

**Objectives:**
1. Implement temporal constraint system (foundation for traffic lights, future features)
2. Create Pipe class as flow container
3. Build PipeNetwork to organize pipes by transportation type
4. Implement PipeController for basic frame-by-frame movement
5. Integrate entities into pipes with basic speed control

**Success Criteria:**
- Pipes correctly model NavLane segments
- Entities move smoothly through pipes at constant speed
- Speed functions can be queried
- Citizens integrate with pipe system
- No regressions in Phase 7B tests

---

## Task B1: Temporal Constraint System

### Files to Create

**`JoyceCode/engine/navigation/TemporalConstraintState.cs`**

```csharp
using System;

namespace JoyceCode.engine.navigation
{
    /// <summary>
    /// State of a temporal constraint at a specific moment in time.
    /// </summary>
    public record TemporalConstraintState(bool CanAccess, TimeSpan UntilChange)
    {
        /// <summary>
        /// Can entities access the constrained resource right now?
        /// </summary>
        public bool CanAccess { get; } = CanAccess;

        /// <summary>
        /// How long until the state changes? (minimum time to recheck)
        /// </summary>
        public TimeSpan UntilChange { get; } = UntilChange;
    }
}
```

**`JoyceCode/engine/navigation/ITemporalConstraint.cs`**

```csharp
using System;

namespace JoyceCode.engine.navigation
{
    /// <summary>
    /// Interface for time-dependent access control.
    /// Can be used for traffic lights, elevators, doors, transit, etc.
    /// </summary>
    public interface ITemporalConstraint
    {
        /// <summary>
        /// Query the constraint state at a given time.
        /// </summary>
        TemporalConstraintState Query(DateTime currentTime);
    }
}
```

**`JoyceCode/engine/navigation/CyclicConstraint.cs`**

```csharp
using System;

namespace JoyceCode.engine.navigation
{
    /// <summary>
    /// A repeating temporal constraint (e.g., traffic light cycle).
    /// </summary>
    public class CyclicConstraint : ITemporalConstraint
    {
        /// <summary>
        /// Length of one complete cycle (seconds).
        /// </summary>
        public double CycleSeconds { get; set; } = 60.0;

        /// <summary>
        /// When the active phase starts within the cycle (seconds).
        /// </summary>
        public double ActivePhaseStart { get; set; } = 0.0;

        /// <summary>
        /// Duration of the active phase (seconds).
        /// </summary>
        public double ActivePhaseDuration { get; set; } = 30.0;

        /// <summary>
        /// Query the constraint state at a specific time.
        /// </summary>
        public TemporalConstraintState Query(DateTime currentTime)
        {
            var cyclePosition = currentTime.TotalSeconds % CycleSeconds;

            var isActive = cyclePosition >= ActivePhaseStart &&
                          cyclePosition < (ActivePhaseStart + ActivePhaseDuration);

            // Calculate time until next state change
            double untilChange;
            if (isActive)
            {
                // In active phase: time until deactivation
                untilChange = (ActivePhaseStart + ActivePhaseDuration) - cyclePosition;
            }
            else
            {
                // In inactive phase: time until next activation
                untilChange = (CycleSeconds - cyclePosition) + ActivePhaseStart;
            }

            return new TemporalConstraintState(isActive, TimeSpan.FromSeconds(untilChange));
        }

        /// <summary>
        /// Get a text representation (for debugging).
        /// </summary>
        public override string ToString()
            => $"CyclicConstraint(cycle={CycleSeconds}s, active={ActivePhaseStart}-{ActivePhaseStart + ActivePhaseDuration}s)";
    }
}
```

### Tests to Write

**`tests/JoyceCode.Tests/engine/navigation/TemporalConstraintTests.cs`**

```csharp
using System;
using Xunit;
using JoyceCode.engine.navigation;

namespace JoyceCode.Tests.engine.navigation
{
    public class CyclicConstraintTests
    {
        [Fact]
        public void Query_GreenPhase_ReturnsCanAccessTrue()
        {
            var constraint = new CyclicConstraint
            {
                CycleSeconds = 60,
                ActivePhaseStart = 0,
                ActivePhaseDuration = 30
            };

            var time = DateTime.UnixEpoch.AddSeconds(15);
            var state = constraint.Query(time);

            Assert.True(state.CanAccess);
        }

        [Fact]
        public void Query_RedPhase_ReturnsCanAccessFalse()
        {
            var constraint = new CyclicConstraint
            {
                CycleSeconds = 60,
                ActivePhaseStart = 0,
                ActivePhaseDuration = 30
            };

            var time = DateTime.UnixEpoch.AddSeconds(45);
            var state = constraint.Query(time);

            Assert.False(state.CanAccess);
        }

        [Fact]
        public void Query_ReturnsCorrectTimeUntilChange()
        {
            var constraint = new CyclicConstraint
            {
                CycleSeconds = 60,
                ActivePhaseStart = 0,
                ActivePhaseDuration = 30
            };

            // At t=15s: green until 30s = 15s remaining
            var time = DateTime.UnixEpoch.AddSeconds(15);
            var state = constraint.Query(time);

            Assert.Equal(15, state.UntilChange.TotalSeconds, precision: 0.1);
        }

        [Fact]
        public void Query_Cycle_RepeatsCorrectly()
        {
            var constraint = new CyclicConstraint
            {
                CycleSeconds = 60,
                ActivePhaseStart = 0,
                ActivePhaseDuration = 30
            };

            // t=15s: green
            // t=75s (15+60): also green (second cycle)
            var state1 = constraint.Query(DateTime.UnixEpoch.AddSeconds(15));
            var state2 = constraint.Query(DateTime.UnixEpoch.AddSeconds(75));

            Assert.True(state1.CanAccess);
            Assert.True(state2.CanAccess);
        }

        [Fact]
        public void Query_OffsetCycle_WorksCorrectly()
        {
            var constraint = new CyclicConstraint
            {
                CycleSeconds = 60,
                ActivePhaseStart = 20,      // Green starts at 20s
                ActivePhaseDuration = 20    // Green until 40s
            };

            var greenTime = DateTime.UnixEpoch.AddSeconds(30);
            var redTime = DateTime.UnixEpoch.AddSeconds(50);

            Assert.True(constraint.Query(greenTime).CanAccess);
            Assert.False(constraint.Query(redTime).CanAccess);
        }
    }
}
```

### Checklist

- [ ] Create TemporalConstraintState.cs
- [ ] Create ITemporalConstraint.cs
- [ ] Create CyclicConstraint.cs
- [ ] Write and pass TemporalConstraintTests
- [ ] Compile JoyceCode project without errors

---

## Task B2: NavLane Constraint Extension

### Files to Modify

**`JoyceCode/engine/navigation/NavLane.cs`**

Add constraint support:

```csharp
/// <summary>
/// Temporal constraint on this lane (e.g., traffic light).
/// Null means no constraint (always accessible).
/// </summary>
public ITemporalConstraint? Constraint { get; set; }

/// <summary>
/// Query the constraint state at a specific time.
/// </summary>
public TemporalConstraintState QueryConstraint(DateTime currentTime)
{
    if (Constraint == null)
        return new TemporalConstraintState(true, TimeSpan.MaxValue);

    return Constraint.Query(currentTime);
}
```

### Checklist

- [ ] Add Constraint property to NavLane
- [ ] Add QueryConstraint() method
- [ ] Compile without errors
- [ ] Verify existing tests still pass

---

## Task B3: Pipe Core

### Files to Create

**`JoyceCode/engine/navigation/MovingEntity.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;

namespace JoyceCode.engine.navigation
{
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
}
```

**`JoyceCode/engine/navigation/Pipe.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoyceCode.engine.navigation
{
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
            Length = NavLanes.Sum(lane => Vector3.Distance(lane.From, lane.To));
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
}
```

**`JoyceCode/engine/navigation/PipeNetwork.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoyceCode.engine.navigation
{
    /// <summary>
    /// Collection of pipes for a specific transportation type.
    /// </summary>
    public class PipeNetwork
    {
        /// <summary>
        /// All pipes in this network.
        /// </summary>
        public List<Pipe> Pipes { get; set; } = new();

        /// <summary>
        /// Transportation type this network serves.
        /// </summary>
        public TransportationType SupportedType { get; set; }

        /// <summary>
        /// Find the pipe containing a specific position.
        /// </summary>
        public Pipe? FindPipeContaining(Vector3 position)
        {
            return Pipes.FirstOrDefault(pipe => pipe.ContainsPosition(position));
        }

        /// <summary>
        /// Find all pipes that could be connected at a junction.
        /// </summary>
        public List<Pipe> FindOutgoingPipes(Pipe fromPipe)
        {
            // Find pipes that start where this pipe ends
            var outgoing = Pipes
                .Where(p => p != fromPipe &&
                           Vector3.Distance(p.StartPosition, fromPipe.EndPosition) < 1.0f)
                .ToList();

            return outgoing;
        }

        /// <summary>
        /// Get total entity count across all pipes.
        /// </summary>
        public int GetTotalEntityCount()
        {
            return Pipes.Sum(p => p.CurrentOccupancy);
        }

        public override string ToString()
            => $"PipeNetwork({SupportedType}, pipes={Pipes.Count}, entities={GetTotalEntityCount()})";
    }
}
```

### Tests to Write

**`tests/JoyceCode.Tests/engine/navigation/PipeTests.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using JoyceCode.engine.navigation;

namespace JoyceCode.Tests.engine.navigation
{
    public class PipeTests
    {
        [Fact]
        public void Pipe_ComputeLength_CalculatesCorrectly()
        {
            var lane1 = new NavLane { From = Vector3.Zero, To = new Vector3(10, 0, 0) };
            var lane2 = new NavLane { From = new Vector3(10, 0, 0), To = new Vector3(20, 0, 0) };

            var pipe = new Pipe
            {
                NavLanes = new List<NavLane> { lane1, lane2 }
            };
            pipe.ComputeLength();

            Assert.Equal(20, pipe.Length, precision: 0.1f);
        }

        [Fact]
        public void Pipe_GetSpeedAt_WithoutConstraint_ReturnsSpeedFunctionValue()
        {
            var pipe = new Pipe
            {
                SupportedType = TransportationType.Car,
                SpeedFunction = (pos, time) => 5.0f
            };

            var speed = pipe.GetSpeedAt(Vector3.Zero, DateTime.Now);

            Assert.Equal(5.0f, speed);
        }

        [Fact]
        public void Pipe_GetSpeedAt_WithBlockingConstraint_ReturnsZero()
        {
            var constraint = new CyclicConstraint
            {
                CycleSeconds = 60,
                ActivePhaseStart = 0,
                ActivePhaseDuration = 30
            };

            var pipe = new Pipe
            {
                SupportedType = TransportationType.Car,
                GlobalConstraint = constraint,
                SpeedFunction = (pos, time) => 5.0f
            };

            // Query at time when constraint is blocked (t=45s)
            var time = DateTime.UnixEpoch.AddSeconds(45);
            var speed = pipe.GetSpeedAt(Vector3.Zero, time);

            Assert.Equal(0.0f, speed);
        }

        [Fact]
        public void PipeNetwork_FindPipeContaining_ReturnsCorrectPipe()
        {
            var pipe1 = new Pipe
            {
                Id = 1,
                StartPosition = Vector3.Zero,
                EndPosition = new Vector3(100, 0, 0),
                Length = 100
            };

            var pipe2 = new Pipe
            {
                Id = 2,
                StartPosition = new Vector3(100, 0, 0),
                EndPosition = new Vector3(200, 0, 0),
                Length = 100
            };

            var network = new PipeNetwork
            {
                Pipes = new List<Pipe> { pipe1, pipe2 }
            };

            var found = network.FindPipeContaining(new Vector3(50, 0, 0));

            Assert.NotNull(found);
            Assert.Equal(1, found.Id);
        }

        [Fact]
        public void PipeNetwork_FindOutgoingPipes_ReturnsConnected()
        {
            var pipe1 = new Pipe { Id = 1, EndPosition = new Vector3(100, 0, 0) };
            var pipe2 = new Pipe { Id = 2, StartPosition = new Vector3(100, 0, 0) };

            var network = new PipeNetwork
            {
                Pipes = new List<Pipe> { pipe1, pipe2 }
            };

            var outgoing = network.FindOutgoingPipes(pipe1);

            Assert.Contains(pipe2, outgoing);
        }
    }
}
```

### Checklist

- [ ] Create MovingEntity.cs
- [ ] Create Pipe.cs
- [ ] Create PipeNetwork.cs
- [ ] Write and pass PipeTests
- [ ] Compile without errors
- [ ] Verify entity movement properties

---

## Task B4: PipeController — Basic Movement

### Files to Create

**`JoyceCode/engine/navigation/PipeController.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace JoyceCode.engine.navigation
{
    /// <summary>
    /// Updates entity movement through pipes each frame.
    /// </summary>
    public class PipeController
    {
        private PipeNetwork _network;
        private Dictionary<int, MovingEntity> _entities;
        private List<MovingEntity> _offPipeEntities;

        public PipeController(PipeNetwork network)
        {
            _network = network;
            _entities = new Dictionary<int, MovingEntity>();
            _offPipeEntities = new List<MovingEntity>();
        }

        /// <summary>
        /// Update all entities' positions this frame.
        /// </summary>
        public void UpdateFrame(float deltaTime, DateTime currentTime)
        {
            // Move entities in pipes
            foreach (var pipe in _network.Pipes)
            {
                UpdatePipeEntities(pipe, deltaTime, currentTime);
            }

            // Update off-pipe entities (physics, manual control)
            UpdateOffPipeEntities(deltaTime, currentTime);
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
            var startPipe = _network.FindPipeContaining(startLane.From);

            if (startPipe == null)
            {
                // No valid pipe for start position
                _offPipeEntities.Add(entity);
                return;
            }

            entity.Position = startLane.From;
            entity.Direction = Vector3.Normalize(startLane.To - startLane.From);
            startPipe.Entities.Enqueue(entity);
            entity.CurrentPipe = startPipe;

            _entities[entity.Id] = entity;
        }

        /// <summary>
        /// Remove an entity from its pipe (pushed by physics, etc.).
        /// </summary>
        public void RemoveEntityFromPipe(MovingEntity entity)
        {
            if (entity.CurrentPipe != null)
            {
                entity.CurrentPipe.Entities.Dequeue();
                entity.CurrentPipe = null;
            }

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
            var nextPipe = _network.FindPipeContaining(nextLane.From);

            if (nextPipe != null)
            {
                entity.Position = nextLane.From;
                entity.Direction = Vector3.Normalize(nextLane.To - nextLane.From);
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
        /// Get all entities.
        /// </summary>
        public IEnumerable<MovingEntity> GetAllEntities()
        {
            return _entities.Values.Concat(_offPipeEntities);
        }
    }
}
```

### Tests to Write

**`tests/JoyceCode.Tests/engine/navigation/PipeControllerTests.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using JoyceCode.engine.navigation;

namespace JoyceCode.Tests.engine.navigation
{
    public class PipeControllerTests
    {
        [Fact]
        public void PlaceEntity_AddsEntityToPipe()
        {
            var lane = new NavLane { From = Vector3.Zero, To = new Vector3(10, 0, 0) };
            var pipe = new Pipe
            {
                Id = 1,
                NavLanes = new List<NavLane> { lane },
                StartPosition = Vector3.Zero,
                EndPosition = new Vector3(10, 0, 0),
                Length = 10
            };

            var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
            var controller = new PipeController(network);

            var entity = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
            controller.PlaceEntity(entity);

            Assert.NotNull(entity.CurrentPipe);
            Assert.Equal(1, pipe.CurrentOccupancy);
        }

        [Fact]
        public void UpdateFrame_MovesEntityForward()
        {
            var lane = new NavLane { From = Vector3.Zero, To = new Vector3(100, 0, 0) };
            var pipe = new Pipe
            {
                Id = 1,
                NavLanes = new List<NavLane> { lane },
                StartPosition = Vector3.Zero,
                EndPosition = new Vector3(100, 0, 0),
                Length = 100,
                SpeedFunction = (pos, time) => 10.0f  // 10 m/s
            };

            var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
            var controller = new PipeController(network);

            var entity = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
            controller.PlaceEntity(entity);

            var initialPos = entity.Position;
            controller.UpdateFrame(1.0f, DateTime.Now);  // 1 second at 10 m/s = 10m

            Assert.True(entity.Position.X > initialPos.X);
            Assert.Equal(10.0f, entity.Position.X - initialPos.X, precision: 0.1f);
        }

        [Fact]
        public void UpdateFrame_StopsEntityWhenBlocked()
        {
            var constraint = new CyclicConstraint
            {
                CycleSeconds = 60,
                ActivePhaseStart = 60,  // Always blocked
                ActivePhaseDuration = 0
            };

            var lane = new NavLane { From = Vector3.Zero, To = new Vector3(100, 0, 0) };
            var pipe = new Pipe
            {
                Id = 1,
                NavLanes = new List<NavLane> { lane },
                StartPosition = Vector3.Zero,
                EndPosition = new Vector3(100, 0, 0),
                Length = 100,
                GlobalConstraint = constraint,
                SpeedFunction = (pos, time) => 10.0f
            };

            var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
            var controller = new PipeController(network);

            var entity = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
            controller.PlaceEntity(entity);

            var initialPos = entity.Position;
            controller.UpdateFrame(1.0f, DateTime.Now);

            // Entity should not move (blocked by constraint)
            Assert.Equal(initialPos, entity.Position);
        }

        [Fact]
        public void RemoveEntityFromPipe_MovesEntityOffPipe()
        {
            var lane = new NavLane { From = Vector3.Zero, To = new Vector3(100, 0, 0) };
            var pipe = new Pipe
            {
                Id = 1,
                NavLanes = new List<NavLane> { lane },
                StartPosition = Vector3.Zero,
                EndPosition = new Vector3(100, 0, 0),
                Length = 100
            };

            var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
            var controller = new PipeController(network);

            var entity = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
            controller.PlaceEntity(entity);

            Assert.NotNull(entity.CurrentPipe);

            controller.RemoveEntityFromPipe(entity);

            Assert.Null(entity.CurrentPipe);
            Assert.Equal(0, pipe.CurrentOccupancy);
        }
    }
}
```

### Checklist

- [ ] Create PipeController.cs
- [ ] Implement UpdateFrame() for basic movement
- [ ] Implement PlaceEntity() to add entities to pipes
- [ ] Implement RemoveEntityFromPipe() for exit
- [ ] Implement ReEnterPipe() for re-entry
- [ ] Write and pass PipeControllerTests
- [ ] Compile without errors

---

## Task B5: Citizens Integration

### Files to Modify

**`JoyceCode/engine/tale/TaleModule.cs` or wherever NavMap is initialized**

Create PipeNetwork from NavMap:

```csharp
private void InitializePipeSystem()
{
    // Build pipe network from NavMap
    var pipeNetwork = new PipeNetwork
    {
        SupportedType = TransportationType.Pedestrian
    };

    // For now: 1:1 mapping of NavLanes to Pipes (rest state)
    int pipeId = 0;
    foreach (var lane in _navMap.AllLanes)
    {
        if (!lane.AllowedTypes.HasFlag(TransportationType.Pedestrian))
            continue;

        var pipe = new Pipe
        {
            Id = pipeId++,
            NavLanes = new List<NavLane> { lane },
            StartPosition = lane.From,
            EndPosition = lane.To,
            SupportedType = TransportationType.Pedestrian
        };
        pipe.ComputeLength();

        pipeNetwork.Pipes.Add(pipe);
    }

    _pipeController = new PipeController(pipeNetwork);
}
```

**`JoyceCode/engine/tale/TaleEntityStrategy.cs`**

Integrate entity with pipe system:

```csharp
public void PlaceInPipeSystem(List<NavLane> route)
{
    var movingEntity = new MovingEntity
    {
        Id = _npcSchedule.NpcId,
        Route = route,
        TransportType = TransportationType.Pedestrian
    };

    _pipeController.PlaceEntity(movingEntity);
    _currentMovingEntity = movingEntity;
}

public void UpdateMovement(float deltaTime, DateTime currentTime)
{
    // PipeController handles position updates
    _pipeController.UpdateFrame(deltaTime, currentTime);
}
```

### Tests to Write

**Integration test: Citizens move through pipes**

```csharp
[Fact]
public void Citizens_MoveUsingPipeSystem()
{
    // Create simple pipe network
    // Create citizen with route
    // Update for N frames
    // Verify citizen moved correctly
}
```

### Checklist

- [ ] Initialize PipeNetwork from NavMap (rest state)
- [ ] Create PipeController in TaleModule
- [ ] Integrate MovingEntity into citizen logic
- [ ] Citizens route to destination via A*
- [ ] Citizens move through pipes at correct speed
- [ ] Citizens reach destination correctly

---

## Task B6: Regression Testing

### Run Test Suite

```bash
./run_tests.sh phase7
./run_tests.sh phase7b
./run_tests.sh all
```

### Verify

- [ ] All Phase 7B tests still passing
- [ ] Citizens still navigate correctly
- [ ] No crashes or unexpected behavior
- [ ] Performance acceptable (frame time not increased)
- [ ] Movement visually matches Phase 7B

---

## Completion Checklist

- [ ] Temporal constraint system complete and tested
- [ ] NavLane extended with constraint support
- [ ] Pipe, PipeNetwork, MovingEntity classes implemented
- [ ] PipeController handles basic movement
- [ ] Citizens integrated with pipe system
- [ ] All new unit tests passing
- [ ] All regression tests passing
- [ ] Code compiles without errors (Release mode)
- [ ] No performance regressions
- [ ] Ready for Phase C (Dynamics)

---

## Notes for Implementation

1. **Start with Rest State:** 1:1 NavLane-to-Pipe mapping is fine for now. Subdivisions come in Phase C.
2. **Speed Functions:** Keep simple initially (constant speed per type). Complex functions in Phase C.
3. **Error Handling:** If entity can't find pipe, move to off-pipe list (handled in Phase C).
4. **Testing:** Write tests incrementally, verify each major piece.
5. **Regression:** Run full test suite frequently to catch unintended changes.
6. **Documentation:** Keep CLAUDE.md updated with Phase 8 progress.

---

## Success Definition

Phase B is complete when:
✅ Pipe system is operational
✅ Entities move smoothly through pipes
✅ Speed is controlled by pipe speed functions
✅ Citizens integrate seamlessly
✅ No regressions in existing tests
✅ Code is clean, tested, documented
