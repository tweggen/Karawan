# Phase C Implementation Plan: Dynamics — Temporal Constraints & Subdivisions

**Phase:** C (Dynamics)
**Duration:** ~4-5 days
**Status:** 🔄 Ready for Implementation (as of 2026-03-25)
**Depends On:** Phase A ✅, Phase B ✅
**Enables:** Phase D
**Prerequisite Status:** All dependencies satisfied ✅

---

## Overview

Phase C brings pipes to life by implementing dynamic subdivisions for obstructions and integrating temporal constraints. This creates the "wave" behavior where traffic naturally queues and flows around obstacles.

**Objectives:**
1. Apply temporal constraints (traffic lights) to pipes
2. Implement pipe subdivisions for obstructions (slow zones, blocked areas)
3. Model braking/acceleration waves via speed functions
4. Enable exit/re-entry of entities from pipes
5. Track off-pipe entities separately

**Success Criteria:**
- Traffic lights cause natural queuing behavior
- Obstructions create dynamic subdivisions
- Braking waves propagate realistically
- Entities can exit and re-enter pipes
- No per-entity collision checks needed
- All regression tests passing

---

## Task C1: Temporal Constraint Integration

### Files to Modify

**`JoyceCode/engine/navigation/Pipe.cs`**

Update GetSpeedAt() to check global constraint:

```csharp
/// <summary>
/// Get the movement speed at a specific position and time.
/// Takes into account global constraints (traffic lights, etc.).
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

    // Step 2: Check local constraints (subdivisions, next phase)
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
```

### Test Example

```csharp
[Fact]
public void Pipe_GetSpeedAt_TrafficLightTurnsRed_ReturnsZero()
{
    var constraint = new CyclicConstraint
    {
        CycleSeconds = 60,
        ActivePhaseStart = 0,
        ActivePhaseDuration = 30
    };

    var pipe = new Pipe
    {
        GlobalConstraint = constraint,
        SpeedFunction = (pos, time) => 10.0f
    };

    // Green phase (t=15s)
    var greenSpeed = pipe.GetSpeedAt(Vector3.Zero,
        DateTime.UnixEpoch.AddSeconds(15));
    Assert.Equal(10.0f, greenSpeed);

    // Red phase (t=45s)
    var redSpeed = pipe.GetSpeedAt(Vector3.Zero,
        DateTime.UnixEpoch.AddSeconds(45));
    Assert.Equal(0.0f, redSpeed);
}
```

### Checklist

- [ ] Update Pipe.GetSpeedAt() to check global constraint
- [ ] Constraint returns speed=0 when not accessible
- [ ] Write and pass constraint integration tests
- [ ] Compile without errors

---

## Task C2: Pipe Subdivisions

### Files to Create

**`JoyceCode/engine/navigation/PipeSubdivision.cs`**

```csharp
using System;
using System.Numerics;

namespace JoyceCode.engine.navigation
{
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
}
```

### Files to Modify

**`JoyceCode/engine/navigation/Pipe.cs`**

Add subdivision support:

```csharp
/// <summary>
/// Dynamic subdivisions (obstructions, slow zones, etc.).
/// </summary>
public List<PipeSubdivision>? Subdivisions { get; set; }

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
```

### Tests to Write

**`tests/JoyceCode.Tests/engine/navigation/PipeSubdivisionTests.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using JoyceCode.engine.navigation;

namespace JoyceCode.Tests.engine.navigation
{
    public class PipeSubdivisionTests
    {
        [Fact]
        public void AddObstruction_CreatesSubdivision()
        {
            var pipe = new Pipe { Id = 1 };

            pipe.AddObstruction(
                new Vector3(50, 0, 0),
                5.0f,
                (pos, time) => 1.0f,
                "Accident");

            Assert.NotNull(pipe.Subdivisions);
            Assert.Single(pipe.Subdivisions);
            Assert.Equal("Accident", pipe.Subdivisions[0].Reason);
        }

        [Fact]
        public void Subdivision_ContainsPosition_WorksCorrectly()
        {
            var sub = new PipeSubdivision
            {
                StartPosition = new Vector3(45, 0, 0),
                EndPosition = new Vector3(55, 0, 0)
            };

            Assert.True(sub.ContainsPosition(new Vector3(50, 0, 0)));
            Assert.False(sub.ContainsPosition(new Vector3(30, 0, 0)));
        }

        [Fact]
        public void RemoveObstruction_ClearsSubdivision()
        {
            var pipe = new Pipe { Id = 1 };
            var obstaclePos = new Vector3(50, 0, 0);

            pipe.AddObstruction(obstaclePos, 5.0f, (pos, time) => 1.0f);
            Assert.Single(pipe.Subdivisions);

            pipe.RemoveObstruction(obstaclePos);
            Assert.Empty(pipe.Subdivisions);
        }

        [Fact]
        public void Pipe_GetSpeedAt_InSubdivision_ReturnsSubdivisionSpeed()
        {
            var pipe = new Pipe
            {
                SpeedFunction = (pos, time) => 10.0f
            };

            pipe.AddObstruction(
                new Vector3(50, 0, 0),
                5.0f,
                (pos, time) => 2.0f);  // Slow speed in obstruction

            var normalSpeed = pipe.GetSpeedAt(new Vector3(10, 0, 0), DateTime.Now);
            var slowSpeed = pipe.GetSpeedAt(new Vector3(50, 0, 0), DateTime.Now);

            Assert.Equal(10.0f, normalSpeed);
            Assert.Equal(2.0f, slowSpeed);
        }
    }
}
```

### Checklist

- [ ] Create PipeSubdivision.cs
- [ ] Add Subdivisions list to Pipe
- [ ] Add AddObstruction() to Pipe
- [ ] Add RemoveObstruction() to Pipe
- [ ] Update GetSpeedAt() to check subdivisions
- [ ] Write and pass subdivision tests
- [ ] Compile without errors

---

## Task C3: Braking Wave Speed Functions

### Files to Create

**`JoyceCode/engine/navigation/SpeedFunctions.cs`**

```csharp
using System;
using System.Numerics;

namespace JoyceCode.engine.navigation
{
    /// <summary>
    /// Predefined speed functions for common scenarios.
    /// These model realistic traffic behavior (braking waves, acceleration).
    /// </summary>
    public static class SpeedFunctions
    {
        /// <summary>
        /// Braking wave when obstruction appears.
        /// Entities near obstacle brake hard, entities far brake gently.
        /// </summary>
        public static Func<Vector3, DateTime, float> BrakingWave(
            Vector3 obstaclePosition,
            DateTime brakeStartTime,
            float normalSpeed = 10.0f)
        {
            return (position, currentTime) =>
            {
                var distance = Vector3.Distance(position, obstaclePosition);

                // Distance zones
                if (distance < 5.0f)
                    return 0.0f;      // Very close: stop
                if (distance < 20.0f)
                    return normalSpeed * 0.2f;  // Near: slow
                if (distance < 50.0f)
                    return normalSpeed * 0.5f;  // Medium: slower

                return normalSpeed;   // Far: normal
            };
        }

        /// <summary>
        /// Acceleration wave when obstruction clears.
        /// Entities near cleared area accelerate first, propagates backward.
        /// </summary>
        public static Func<Vector3, DateTime, float> AccelerationWave(
            Vector3 clearedPosition,
            DateTime accelerationStartTime,
            float normalSpeed = 10.0f,
            float accelerationRate = 2.0f)
        {
            return (position, currentTime) =>
            {
                var distance = Vector3.Distance(position, clearedPosition);
                var timeSinceClear = (currentTime - accelerationStartTime).TotalSeconds;

                if (timeSinceClear < 0)
                    return normalSpeed * 0.2f;  // Before clear: still slow

                // Entities near cleared area accelerate first
                if (distance < 20.0f)
                {
                    var speed = normalSpeed * 0.2f + accelerationRate * (float)timeSinceClear;
                    return Math.Min(speed, normalSpeed);
                }

                // Entities farther away accelerate more gently
                var delayedStart = Math.Max(0, timeSinceClear - (distance / normalSpeed));
                var acceleratingSpeed = normalSpeed * 0.2f + accelerationRate * (float)delayedStart;
                return Math.Min(acceleratingSpeed, normalSpeed);
            };
        }

        /// <summary>
        /// Queue pattern: entities back up and wait.
        /// Used when pipe is blocked completely.
        /// </summary>
        public static Func<Vector3, DateTime, float> Queued(
            Vector3 queueStart,
            bool isBlocked = true)
        {
            return (position, currentTime) =>
            {
                return isBlocked ? 0.0f : 5.0f;
            };
        }

        /// <summary>
        /// Congestion: reduced speed due to traffic density.
        /// </summary>
        public static Func<Vector3, DateTime, float> Congested(
            float congestionLevel = 0.5f,  // 0 = free flow, 1 = completely congested
            float normalSpeed = 10.0f)
        {
            return (position, currentTime) =>
            {
                return normalSpeed * (1.0f - congestionLevel);
            };
        }
    }
}
```

### Tests to Write

```csharp
[Fact]
public void BrakingWave_SpeedDecreases_TowardObstacle()
{
    var obstaclePos = new Vector3(50, 0, 0);
    var brakeFunc = SpeedFunctions.BrakingWave(obstaclePos, DateTime.Now, 10.0f);

    var farSpeed = brakeFunc(new Vector3(100, 0, 0), DateTime.Now);
    var nearSpeed = brakeFunc(new Vector3(30, 0, 0), DateTime.Now);

    Assert.True(farSpeed > nearSpeed);
}

[Fact]
public void AccelerationWave_SpeedIncreases_AfterClear()
{
    var clearedPos = new Vector3(50, 0, 0);
    var startTime = DateTime.Now;
    var accelFunc = SpeedFunctions.AccelerationWave(clearedPos, startTime, 10.0f);

    var t0 = startTime.AddSeconds(0);
    var t5 = startTime.AddSeconds(5);

    var speed0 = accelFunc(new Vector3(50, 0, 0), t0);
    var speed5 = accelFunc(new Vector3(50, 0, 0), t5);

    Assert.True(speed5 > speed0);
}
```

### Checklist

- [ ] Create SpeedFunctions.cs
- [ ] Implement BrakingWave()
- [ ] Implement AccelerationWave()
- [ ] Implement Congestion()
- [ ] Implement Queued()
- [ ] Write and pass speed function tests
- [ ] Compile without errors

---

## Task C4: Obstructions in PipeController

### Files to Modify

**`JoyceCode/engine/navigation/PipeController.cs`**

Add obstruction tracking:

```csharp
/// <summary>
/// Active obstructions (accidents, slow vehicles, etc.).
/// </summary>
private List<(Vector3 position, float radius, ITemporalConstraint duration)>
    _activeObstructions = new();

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
/// Update all entities and obstructions.
/// </summary>
public void UpdateFrame(float deltaTime, DateTime currentTime)
{
    UpdateObstructions(currentTime);  // Check for expired obstructions

    // Move entities in pipes
    foreach (var pipe in _network.Pipes)
    {
        UpdatePipeEntities(pipe, deltaTime, currentTime);
    }

    // Update off-pipe entities
    UpdateOffPipeEntities(deltaTime, currentTime);
}
```

### Test Example

```csharp
[Fact]
public void RegisterObstruction_CreatesSubdivision()
{
    var pipe = new Pipe
    {
        Id = 1,
        StartPosition = Vector3.Zero,
        EndPosition = new Vector3(100, 0, 0),
        Length = 100
    };

    var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
    var controller = new PipeController(network);

    var obstaclePos = new Vector3(50, 0, 0);
    var duration = new CyclicConstraint
    {
        CycleSeconds = 120,
        ActivePhaseStart = 0,
        ActivePhaseDuration = 120  // Lasts 2 minutes
    };

    controller.RegisterObstruction(obstaclePos, 5.0f, duration, "Accident");

    Assert.True(pipe.HasSubdivisions);
    Assert.Single(pipe.Subdivisions);
}
```

### Checklist

- [ ] Add _activeObstructions tracking to PipeController
- [ ] Add RegisterObstruction() method
- [ ] Add UpdateObstructions() method
- [ ] Update UpdateFrame() to call UpdateObstructions()
- [ ] Write and pass obstruction tests
- [ ] Compile without errors

---

## Task C5: Exit/Re-entry

### Files to Modify

**`JoyceCode/engine/navigation/PipeController.cs`**

Update existing methods for better off-pipe handling:

```csharp
/// <summary>
/// Entities currently off the pipe system (pushed by physics, etc.).
/// </summary>
private List<MovingEntity> _offPipeEntities = new();

/// <summary>
/// Remove an entity from its pipe (e.g., pushed by physics).
/// </summary>
public void RemoveEntityFromPipe(MovingEntity entity)
{
    if (entity.CurrentPipe == null)
        return;

    // Remove from pipe's queue
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
    {
        // No pipe at this position
        return;
    }

    entity.Position = position;
    entity.CurrentPipe = pipe;
    pipe.Entities.Enqueue(entity);

    _offPipeEntities.Remove(entity);
}

/// <summary>
/// Get all off-pipe entities.
/// </summary>
public IEnumerable<MovingEntity> GetOffPipeEntities()
{
    return _offPipeEntities;
}

/// <summary>
/// Update off-pipe entities (physics-driven movement, etc.).
/// </summary>
private void UpdateOffPipeEntities(float deltaTime, DateTime currentTime)
{
    // Off-pipe entities are updated by physics or external systems
    // Just track them; don't move them here
}
```

### Tests to Write

```csharp
[Fact]
public void RemoveEntityFromPipe_MovesEntityOffPipe()
{
    var pipe = new Pipe
    {
        Id = 1,
        StartPosition = Vector3.Zero,
        EndPosition = new Vector3(100, 0, 0),
        Length = 100
    };

    var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
    var controller = new PipeController(network);

    var entity = new MovingEntity { Id = 1 };
    controller.PlaceEntity(entity);

    Assert.NotNull(entity.CurrentPipe);

    controller.RemoveEntityFromPipe(entity);

    Assert.Null(entity.CurrentPipe);
    Assert.Contains(entity, controller.GetOffPipeEntities());
}

[Fact]
public void ReEnterPipe_AddsEntityBackToPipe()
{
    var pipe = new Pipe
    {
        Id = 1,
        StartPosition = Vector3.Zero,
        EndPosition = new Vector3(100, 0, 0),
        Length = 100
    };

    var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
    var controller = new PipeController(network);

    var entity = new MovingEntity { Id = 1 };
    controller.RemoveEntityFromPipe(entity);

    Assert.Null(entity.CurrentPipe);

    controller.ReEnterPipe(entity, new Vector3(50, 0, 0));

    Assert.NotNull(entity.CurrentPipe);
    Assert.DoesNotContain(entity, controller.GetOffPipeEntities());
}
```

### Checklist

- [ ] Add _offPipeEntities list
- [ ] Update RemoveEntityFromPipe() for proper queue handling
- [ ] Implement ReEnterPipe()
- [ ] Add GetOffPipeEntities()
- [ ] Update UpdateOffPipeEntities()
- [ ] Write and pass exit/re-entry tests
- [ ] Compile without errors

---

## Task C6: Integration & Regression Testing

### Test Scenario: Obstruction Causes Queuing

```csharp
[Fact]
public void ObstructionInPipe_CausesNaturalQueuing()
{
    // Create pipe with 3 entities
    var pipe = new Pipe
    {
        Id = 1,
        StartPosition = Vector3.Zero,
        EndPosition = new Vector3(100, 0, 0),
        Length = 100,
        SpeedFunction = (pos, time) => 10.0f
    };

    var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
    var controller = new PipeController(network);

    var entity1 = new MovingEntity { Id = 1, Position = new Vector3(10, 0, 0) };
    var entity2 = new MovingEntity { Id = 2, Position = new Vector3(20, 0, 0) };
    var entity3 = new MovingEntity { Id = 3, Position = new Vector3(30, 0, 0) };

    pipe.Entities.Enqueue(entity1);
    pipe.Entities.Enqueue(entity2);
    pipe.Entities.Enqueue(entity3);

    // Add obstruction at 50m
    var duration = new CyclicConstraint
    {
        CycleSeconds = 120,
        ActivePhaseStart = 0,
        ActivePhaseDuration = 60  // 1 minute obstruction
    };

    controller.RegisterObstruction(new Vector3(50, 0, 0), 5.0f, duration);

    // Update for 1 second
    controller.UpdateFrame(1.0f, DateTime.Now);

    // Entity 1 should move less due to braking wave
    Assert.True(entity1.Position.X > 10);
    Assert.True(entity1.Position.X < 20);  // Less movement than normal
}
```

### Run Regression Tests

```bash
./run_tests.sh phase7
./run_tests.sh phase7b
./run_tests.sh all
```

### Checklist

- [ ] Write integration test for obstruction queuing
- [ ] Write test for traffic light stopping entities
- [ ] Write test for obstruction clearing
- [ ] Run all Phase 7B regression tests
- [ ] Verify no performance regressions
- [ ] All tests passing

---

## Completion Checklist

- [ ] Temporal constraints integrated with pipes
- [ ] Pipe subdivisions implemented and tested
- [ ] Speed functions created (braking, acceleration, congestion)
- [ ] Obstructions registered and tracked
- [ ] Exit/re-entry working correctly
- [ ] Off-pipe entities managed properly
- [ ] All new unit tests passing
- [ ] All regression tests passing
- [ ] Code compiles without errors (Release mode)
- [ ] No performance regressions
- [ ] Ready for Phase D (Routing Integration)

---

## Notes for Implementation

1. **Braking Waves:** Use distance-based speed zones, not time-based. More intuitive and matches reality.
2. **Temporal Constraints:** Reuse ITemporalConstraint interface from Phase B.
3. **Subdivisions:** Keep simple (rectangular regions). Complex geometry in future phases.
4. **Off-Pipe Tracking:** Important for physics integration and accident scenarios.
5. **Testing:** Test obstruction lifecycle (create, update, clear).
6. **Performance:** Monitor entity updates; subdivisions should not add significant cost.

---

## Success Definition

Phase C is complete when:
✅ Temporal constraints affect pipe speed realistically
✅ Obstructions create dynamic subdivisions
✅ Queuing behavior emerges naturally (no per-entity collision checks)
✅ Braking waves propagate backward from obstacles
✅ Entities can exit and re-enter pipes
✅ All regression tests passing
✅ Code is clean, tested, well-documented
