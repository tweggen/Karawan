# Pipes: Flow-Based Entity Movement Model

**Status:** Proposed (Design Phase)
**Date:** 2026-03-24
**Related:** NavMap, Temporal Constraints, Transportation Graphs

---

## Problem Statement

Current movement model: Each frame, for each entity on each NavLane, check if it can move. This is **O(n*m)** complexity (n lanes × m entities) and becomes intractable with obstructions, congestion, and collision detection.

Obstructions create hard problems:
- A slow vehicle in front creates a queue behind it
- Each queued entity needs per-frame collision checks
- Traffic light change must propagate to all affected entities
- Modeling natural braking waves (front cars brake hard, back cars brake gently) requires complex per-entity logic

**Goal:** Reduce complexity by treating movement as **flow through pipes**, where a pipe describes the state of motion in a region, and entities follow the pipe's state rather than deciding individually.

---

## Core Concept: Pipes as Flow Containers

A **pipe** is a connected segment of the navigation network where all entities experience similar flow properties. Instead of controlling each entity's movement individually, the pipe controller defines how the flow moves.

**Key insight:** Complexity shifts from O(n*m) entity checks to O(p) pipe state updates, where p << n*m.

### Pipe Definition

```csharp
public class Pipe
{
    // Topology: connected NavLanes from junction to junction
    public List<NavLane> NavLanes { get; set; }

    // Flow state
    public Queue<MovingEntity> Entities { get; set; }  // entities in this pipe
    public Func<Vector3, DateTime, float> SpeedFunction { get; set; }  // speed varies by position and time

    // Capacity (natural queuing)
    public int MaxCapacity { get; set; }
    public int CurrentOccupancy => Entities.Count;

    // Temporal state
    public TemporalConstraintState State { get; set; }  // from upstream traffic light, etc.
}
```

### Pipe Types

**Rest State (Simple Pipe):**
- 1:1 with NavLanes (before any complexity)
- Uniform speed function: `speed(pos, time) → constant`
- Example: empty suburban street, `speed = 10 m/s`

**Complex State (Dynamic Subdivisions):**
- Splits when obstructions/congestion create zones
- Multiple speed functions applied to different segments
- Example: obstruction at 50m splits pipe into two speed zones

**Merged State:**
- Subdivisions merge when obstruction clears
- Returns to "rest state" or simpler configuration

---

## Example: Obstruction Creating Pipe Subdivision

**Scenario:** Car breaks down at 50m on a 100m street.

**Rest state:**
```
Pipe: street_main_east (0-100m)
  Speed: 5 m/s everywhere
  Entities: [car1, car2, car3, car4, car5]
```

**Obstruction detected at t=0:**
```
Pipe splits into two dynamic zones:

Zone 1 (0-50m, before obstruction):
  Speed: decelerating_function(pos, time)
    - Near obstruction (40-50m): brake hard, v(t) = 5 - 2*(t-t0)
    - Far (0-40m): gentle brake, v(t) = 5 - 0.5*(t-t0)
    - Natural braking wave propagates backward
  Entities: [car1, car2] (waiting/queuing)

Zone 2 (50m, obstruction):
  Speed: 0 (blocked)
  Entities: [broken_car]

Zone 3 (50-100m, after obstruction):
  Speed: 1 m/s (congested, limited exit)
  Entities: [car3, car4, car5] (slowly proceeding)
```

**Obstruction clears at t=30:**
```
Pipe merges back to:

Pipe: street_main_east (0-100m)
  Speed: accelerating_function(pos, time)
    - Near where obstruction was (40-60m): accelerate first, v(t) = 1 + 1.5*(t-t30)
    - Rest: accelerate gently, v(t) = 1 + 0.3*(t-t30)
  Entities: [car1, car2, car3, car4, car5] (all moving together)
```

**Benefit:** No per-entity collision detection. The braking wave is modeled by the speed function, not by individual car behavior. Entities simply follow the pipe's speed.

---

## Pipe Topology: Bounded by Non-Trivial Junctions

A pipe spans **multiple NavLanes** if they form a simple, linear segment. Pipes are bounded by **non-trivial junctions** (3+ outgoing lanes, or junctions with special behavior).

### Example City Layout

```
     ↓ turn_left_lane
street_main → junction_main_5th → street_main_east ↓ straight
     ↑ turn_right_lane

Pipes:
1. street_main_west (NavLanes: westbound_main_1 + westbound_main_2 + westbound_main_3)
   → ends at junction_main_5th (3-way split, non-trivial)

2. street_main_east (NavLanes: eastbound_main_1 + eastbound_main_2 + eastbound_main_3)
   → ends at next non-trivial junction or network boundary

3. turn_left_lane (NavLane: single left turn)
   → ends at left_street junction

4. turn_right_lane (NavLane: single right turn)
   → ends at right_street junction
```

**Rationale:** Spanning NavLanes reduces pipe count. Bounding at junctions makes sense because entities make routing decisions at junctions, not mid-pipe.

---

## Integration with Temporal Constraints

Traffic lights, elevators, and other temporal constraints modify pipe speed, not individual entity speed.

### Traffic Light Example: Turning Red

**Before (t < 0):**
```
Pipe: street_main_east (0-200m)
  State: green (from traffic light)
  Speed: 5 m/s everywhere
  Entities: queued normally
```

**Light turns red (t = 0):**
```
Pipe: street_main_east subdivides by braking distance

Speed function: brake_wave(position, time)
  - Position 0-20m (nearest light): hard brake v(t) = 5 - 2*t
  - Position 20-100m (middle): medium brake v(t) = 5 - 1*t
  - Position 100-200m (far): gentle brake v(t) = 5 - 0.2*t

Effect: Front cars stop quickly, back cars slow gradually
        Wave propagates backward naturally through speed function
        No per-entity logic needed
```

**Light turns green (t = 30):**
```
Speed function: acceleration_wave(position, time)
  - Position 0-20m: hard accelerate v(t) = 0 + 2*(t-30)
  - Position 20-100m: medium accelerate v(t) = 0 + 1*(t-30)
  - Position 100-200m: gentle accelerate v(t) = 0 + 0.2*(t-30)

Effect: Front cars start first, acceleration wave propagates backward
```

**Key insight:** Temporal constraint doesn't query per-entity. Instead, it provides a speed function to the pipe. The pipe is responsible for moving all entities accordingly.

---

## Multi-Type Pipe Networks

Each transportation type has its own pipe network (no cross-type collision).

### Dynamic Obstruction by Slow Entity

Scenario: Slow cyclist on a car-lane pipe.

**Rest state:**
```
Pipe: street_main_east (car pipe)
  Speed: 5 m/s (car speed)
  Entities: [car1, car2, car3, bike, car4, car5]
```

**Bike detected (different transportation type, speed 2 m/s):**
```
Pipe subdivides (geometry adjusts, topology doesn't):

Zone 1 (0 to bike_pos-5m):
  Speed: brake_wave to match bike
  Entities: [car1, car2, car3]

Zone 2 (bike_pos-5m to bike_pos+5m):
  Speed: 2 m/s (bike + following cars)
  Entities: [bike, car4, car5]

Zone 3 (bike_pos+5m to 100m):
  Speed: (empty, no cars behind bike yet)
  Entities: []
```

**Bike exits lane at t=20:**
```
Pipe merges back:

Pipe: street_main_east (car pipe)
  Speed: acceleration_wave (cars catch up)
  Entities: [car1, car2, car3, car4, car5]
```

**Key insight:** Geometric pipe boundaries shift (where bike is), but topology (pipe network structure) stays the same. This is efficient because we don't recalculate routing; we just adjust speed zones.

---

## Entity Routing Through Pipes

Entities pick destination-based pipes at junctions.

```csharp
public class MovingEntity
{
    public Vector3 Destination { get; set; }
    public Pipe CurrentPipe { get; set; }

    public void UpdatePipeAtJunction(Junction junction)
    {
        // Junction knows all outgoing pipes
        // Pick the pipe that leads toward destination
        var nextPipe = junction.SelectPipeToward(Destination);
        TransitionToPipe(nextPipe);
    }
}
```

At a non-trivial junction, the routing decision is made. The entity then follows the selected pipe's speed function until the next junction.

---

## Pipe Controller: Simplified Movement Logic

```csharp
public class PipeController
{
    private List<Pipe> _pipes;

    public void UpdateFrame(float deltaTime, DateTime currentTime)
    {
        foreach (var pipe in _pipes)
        {
            // Evaluate speed function for every position on pipe
            var speedMap = pipe.EvaluateSpeedFunction(currentTime);

            // Move all entities in this pipe according to speed map
            foreach (var entity in pipe.Entities)
            {
                var speed = speedMap.SpeedAt(entity.Position);
                entity.Position += speed * deltaTime * entity.Direction;
            }

            // Handle entity exit/entry at pipe boundaries
            HandlePipeTransitions(pipe, currentTime);
        }
    }
}
```

**Complexity:** O(p + e) where p = pipes, e = total entities. Linear in both, no n*m collision matrix.

---

## File Structure

### New Files
- `JoyceCode/engine/navigation/Pipe.cs` — pipe container, speed function
- `JoyceCode/engine/navigation/PipeNetwork.cs` — collection of pipes, bounded by junctions
- `JoyceCode/engine/navigation/PipeController.cs` — movement update logic
- `JoyceCode/engine/navigation/SpeedFunction.cs` — function interface for speed profiles

### Modified Files
- `NavMap.cs` — build pipe network from NavLanes
- `TaleEntityStrategy.cs` — integrate entity movement with pipes (or move to PipeController)
- `JoyceCode.projitems` — register new files

---

## Design Questions (To Be Resolved)

1. **Speed function representation:**
   - `Func<Vector3, DateTime, float>` (position + time → speed)?
   - Or piecewise segments with interpolation?
   - How to represent braking/acceleration waves efficiently?

2. **Capacity management:**
   - When a pipe is full, does traffic back up into previous pipe?
   - Or does exit speed of previous pipe automatically adjust?
   - How to handle queue overflow?

3. **Pipe merging strategy:**
   - When subdivisions merge (obstruction clears), how do entities recombine?
   - Does order matter? (car3 was behind car2 before split, after merge?)

4. **Integration timing:**
   - Pipes live in PipeController, separate from TaleEntityStrategy?
   - Or integrated into TaleEntityStrategy's movement phase?
   - How to coordinate with NavMap pathfinding?

5. **Temporal constraints → speed function mapping:**
   - Does TemporalConstraint.Query() directly produce a speed function?
   - Or does Pipe compute speed function based on constraint state?
   - How to compose multiple constraints (light + obstruction)?

6. **Multi-type interaction:**
   - Bike on car lane: does it dynamically split the car pipe, or does bike have separate pipe?
   - If separate, how do we model "following a slow bike" (car frustrated behavior)?
   - Or is this a future extension?

---

## Success Criteria

- [ ] Pipe network built correctly from NavLanes (1:1 rest state)
- [ ] Speed function model chosen and documented
- [ ] PipeController moves entities with O(p+e) complexity
- [ ] Obstruction causes pipe subdivision, entities queue naturally
- [ ] Obstruction clears, pipe merges, entities continue
- [ ] Temporal constraint affects pipe speed (traffic light example)
- [ ] Braking/acceleration waves modeled smoothly by speed function
- [ ] Multi-type pipes remain separate (no cross-type collision)
- [ ] Dynamic obstruction by slow entity works (geometry shifts, topology unchanged)
- [ ] Entities route correctly through multi-way junctions
- [ ] All regression tests passing
- [ ] Performance measurably better than O(n*m) entity checks

---

## Future Extensions

- **Queue behavior:** How cars behave while queued (lane changing, lane selection)
- **Emergent behavior:** Vehicles matching speed of vehicles ahead (car-following model)
- **Adaptive routing:** Vehicles choosing pipes based on queue length (not just direction)
- **Multi-type interaction:** Bike on car lane, pedestrians crossing, etc.
- **Visualization:** Pipe subdivisions, speed zones, queue lengths in debug renderer
