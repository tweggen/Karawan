# Temporal Constraints: Time-Dependent Access Control

**Status:** Proposed (Design Phase)
**Date:** 2026-03-24
**Related:** NavMap transportation types, movement execution

---

## Core Abstraction

This proposal defines a **temporal constraint model** — a general time-dependent gate that controls access to resources. While designed initially for traffic lights, the model applies equally to:

- **Traffic lights** — Lane exit to junction (green/red cycles)
- **Elevators** — Door access (open/moving/open-at-destination cycles)
- **Scheduled transit** — Vehicle boarding (present/absent, loading/departing cycles)
- **Access control** — Door locks, checkpoints (unlocked/locked time windows)
- **Any time-dependent resource** — State varies predictably over time

The abstraction: `(bool canAccess, TimeSpan untilChange) = Query(currentTime)`

---

## Problem Statement

To support realistic navigation and resource interaction, we need to model time-dependent access to infrastructure:

1. **Traffic lights** — Junctions allow/deny lane exit based on light cycle
2. **Elevators** — Doors allow/deny entry based on position/state
3. **Transit** — Vehicles allow/deny boarding based on schedule
4. **Movement execution** — Can an NPC proceed now, or must they wait/reroute?

Current system has no temporal constraint model. We need a generic interface for time-dependent access.

---

## Solution: Temporal Constraint Query Interface

### Core Concept

Any entity that controls **time-dependent access** has a **temporal state function**:

```csharp
(bool canAccess, TimeSpan untilChange) Query(DateTime currentTime)
```

Where:
- **`canAccess`** — Is access currently allowed?
- **`untilChange`** — How long until the next state change? (minimum time before rechecking)

**Deterministic:** Same input time always produces same output (state cycles predictably).

**Generic Implementation:** The function doesn't care *why* access is denied — it could be a red light, moving elevator, absent bus, locked door, or any other constraint.

### Application: Traffic Lights

For a **NavLane** with a traffic light:

**Per-lane:** Each directed lane has its own function (each direction of a street may have different light timing).

### Example: Simple Cycle

Intersection with 60-second cycle:
- **Lane A (straight):** Green 0-30s, red 30-60s, repeat
- **Lane B (left turn):** Green 10-20s, red 20-10s (offset from A)
- **Lane C (opposite direction):** Red 0-30s, green 30-60s (anti-phase to A)

Query at t=15s:
- Lane A: `(true, 15s)` — green, changes in 15s
- Lane B: `(true, 5s)` — green, changes in 5s
- Lane C: `(false, 15s)` — red, changes in 15s

Query at t=35s:
- Lane A: `(false, 25s)` — red, changes in 25s
- Lane B: `(false, 45s)` — red, changes in 45s
- Lane C: `(true, 25s)` — green, changes in 25s

---

## Architecture Questions (To Be Resolved)

### Question 1: Who Owns the Traffic Light Function?

**Option A: NavLane Owns It**
```csharp
public class NavLane
{
    public Func<DateTime, (bool canPass, TimeSpan untilChange)> TrafficLightState { get; set; }
    // null if no traffic light
}
```
- Pros: Simple, bundled with lane data
- Cons: NavLane becomes responsible for temporal state (mixing concerns)

**Option B: Separate TrafficControl System**
```csharp
public class TrafficControl
{
    public (bool canPass, TimeSpan untilChange) QueryLane(NavLane lane, DateTime time)
    // Looks up lane ID in traffic light registry
}
```
- Pros: Clean separation, reusable for traffic simulation
- Cons: Extra indirection, requires lane registry

**Option C: NavLane References a TrafficLight Object**
```csharp
public class NavLane
{
    public TrafficLight? Light { get; set; }
}

public class TrafficLight
{
    public (bool canPass, TimeSpan untilChange) Query(DateTime time)
}
```
- Pros: Explicit, allows complex light behavior later
- Cons: Creates new object type, more indirection

**Recommendation:** Start with **Option A** (simple function on NavLane, null if no light). Migrate to Option B/C if traffic becomes complex.

---

### Question 2: How Is Traffic Light Data Stored?

**Option A: JSON Configuration**
```json
{
  "trafficLights": {
    "intersection_main_5th": {
      "lanes": {
        "lane_main_east_straight": {
          "cycleSeconds": 60,
          "greenPhaseStart": 0,
          "greenPhaseDuration": 30
        }
      }
    }
  }
}
```
- Pros: Configurable, supports different cities/intersections
- Cons: Complex parsing, repeated cycle logic

**Option B: Code-Based (C# Functions)**
```csharp
navLane.TrafficLightState = (time) => {
    var cycle = 60.0; // seconds
    var phase = (time.TotalSeconds % cycle);
    var canPass = phase < 30; // green 0-30s
    var untilChange = canPass ? (30 - phase) : (60 - phase);
    return (canPass, TimeSpan.FromSeconds(untilChange));
};
```
- Pros: Flexible, testable, no parsing
- Cons: Hardcoded per intersection

**Option C: Hybrid (Config + Evaluation)**
```csharp
public class TrafficLightCycle
{
    public double CycleSeconds { get; set; }
    public double GreenStart { get; set; }
    public double GreenDuration { get; set; }

    public (bool, TimeSpan) Evaluate(DateTime time) { ... }
}
```
- Pros: Structured, configurable, reusable logic
- Cons: More boilerplate

**Recommendation:** Start with **Option C** (simple cycle class, loaded from JSON). Supports both simple cycles and future extension (coordinated lights, sensor-based changes).

---

### Question 3: Routing vs. Execution — When Is Traffic Light State Queried?

**Scenario:** NPC at point A wants to reach point B, route passes through a junction with a traffic light.

**Option A: Routing Only**
- A* computes cost of passing through junction
- Cost = expected wait time (based on light cycle at route planning time)
- At execution: NPC steps forward regardless of current light state
- Problem: Light might have changed; NPC walks into red light

**Option B: Execution Only**
- A* ignores traffic lights entirely (treats all lanes as passable)
- At execution: NPC checks light state before stepping
- If red: wait, reroute, or jaywal (behavioral choice)
- Problem: A* might choose routes with longer expected waits

**Option C: Both (Time-Aware Routing)**
- A* includes light cycles in route cost
- Prefers routes with shorter expected total wait time
- At execution: NPC checks light state before stepping
- Problem: Complexity (A* needs time parameter)

**Recommendation:** Start with **Option B** (execution only), defer A* integration. Simpler to implement, still produces correct behavior. A* integration (Option C) as future optimization.

---

### Question 4: What Happens When NPC Reaches a Red Light?

Three behavioral options:

**Option A: Always Wait**
```csharp
// NPC at junction, light is red
// Wait until light turns green
// Then proceed
```
- Pros: Realistic, no decision-making needed
- Cons: NPC stands still, might get stuck in deadlock

**Option B: Reroute**
```csharp
// NPC at junction, light is red
// Abandon current route, find alternate path
```
- Pros: Realistic, keeps NPC moving
- Cons: Performance (rerouting is expensive)

**Option C: Probabilistic (Jaywalking)**
```csharp
// NPC at junction, light is red
// Probability-based: jaywal (ignore light) or wait
// Depends on NPC properties (law-abiding, in hurry, etc.)
```
- Pros: Behavioral variety
- Cons: Complex, requires NPC property system

**Recommendation:** Defer this decision to **movement execution design** (out of scope for this proposal). For now, just define the query interface. Movement layer will decide behavior.

---

## Proposed API

```csharp
// Generic temporal constraint state
public record TemporalConstraintState(bool CanAccess, TimeSpan UntilChange);

// Simple cycle-based constraint (repeating pattern)
public class CyclicConstraint
{
    public double CycleSeconds { get; set; }
    public double ActivePhaseStart { get; set; }
    public double ActivePhaseDuration { get; set; }

    public TemporalConstraintState Query(DateTime time)
    {
        var cyclePosition = (time.TotalSeconds % CycleSeconds);
        var isActive = cyclePosition >= ActivePhaseStart &&
                       cyclePosition < (ActivePhaseStart + ActivePhaseDuration);

        var untilChange = isActive
            ? (ActivePhaseStart + ActivePhaseDuration - cyclePosition)
            : (CycleSeconds - cyclePosition + ActivePhaseStart);

        return new TemporalConstraintState(isActive, TimeSpan.FromSeconds(untilChange));
    }
}

// Example: Traffic light (a cyclic constraint on a lane)
// CyclicConstraint with: CycleSeconds=60, ActivePhaseStart=0, ActivePhaseDuration=30
// (green for 30s, red for 30s)

// Example: Elevator schedule
// CyclicConstraint with: CycleSeconds=120, ActivePhaseStart=0, ActivePhaseDuration=10
// (doors open 0-10s, moving 10-120s, repeat)

// Example: Bus schedule (simplified)
// Different constraint type: EventBasedConstraint
// (doors open 0-30s after arrival, then unavailable until next cycle)

// NavLane owns its temporal constraint (if any)
public class NavLane
{
    public ITemporalConstraint? Constraint { get; set; }  // traffic light, etc.

    public TemporalConstraintState QueryConstraint(DateTime time)
    {
        if (Constraint == null)
            return new TemporalConstraintState(true, TimeSpan.MaxValue); // no constraint = always accessible

        return Constraint.Query(time);
    }
}

// Generic interface for future extensions
public interface ITemporalConstraint
{
    TemporalConstraintState Query(DateTime time);
}
```

---

## Implementation Sketch (Minimal)

### Phase 1: Traffic Light Cycle Class

**New File:**
- `JoyceCode/engine/navigation/TrafficLightCycle.cs` — cycle definition + query

**Modified:**
- `JoyceCode/engine/navigation/NavLane.cs` — add `TrafficLight` property, `QueryTrafficLight()` method

### Phase 2: JSON Loading

**Modify:**
- `TaleModule.cs` or similar — load traffic light config from JSON
- Instantiate `TrafficLightCycle` objects, assign to NavLanes during world init

### Phase 3: Movement Execution Integration

**Future** — out of scope for this proposal, handled when implementing NPC movement through junctions.

---

## Open Design Questions for Discussion

1. **Option A vs. B vs. C for ownership** — Which feels right?
2. **Routing integration** — Defer to later (execution-only), or build time-aware A* now?
3. **Behavioral model** — When NPC hits red light, just provide the query interface and let movement layer decide?
4. **Coordination** — Should lights support "green wave" (coordinated cycles) from the start, or add later?
5. **Lanes vs. Junctions** — Are traffic lights per lane (my proposal) or per junction (alternative)?

---

## Future Extensions (Out of Scope)

### Traffic Control
- **Sensor-based lights** — change based on traffic flow
- **Pedestrian buttons** — separate signal for crossing
- **Coordination** — green waves, adaptive timing
- **Traffic flow simulation** — actual congestion affecting routing costs
- **Priority vehicles** — emergency vehicles override lights

### Building Access & Transit
- **Elevators** — Multi-floor constraint (doors open when present, closed while moving)
- **Escalators** — Time-dependent (stairs available when running, restricted when stopped)
- **Doors/Access control** — Unlock windows (card swipe, timer, event-triggered)
- **Scheduled transit** — Bus/train schedules (available when present, departed when absent)
- **Checkpoints** — Guard patrols (passable when guard away, blocked when present)

### General Time-Dependent Resources
- **Resource availability** — Any system with predictable time-dependent access
- **Event-based constraints** — Extend beyond cyclic (triggered by events, not just time)
- **Probabilistic constraints** — Access varies by random state (future behavioral model)
