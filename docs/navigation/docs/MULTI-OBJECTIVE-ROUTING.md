# Phase D: Multi-Objective Routing (NPCs Take Different Routes)

> **Status:** partially-implemented - **Implemented as:** `JoyceCode/engine/tale/NpcGoal.cs:6-37`, `RoutingPreferences.cs:10,45-69`, `JoyceCode/builtin/modules/satnav/LocalPathfinder.cs:32,63-88`, `JoyceCode/engine/tale/TalePopulationGenerator.cs:395-439`; Scenic/Safe multipliers are placeholders returning 1.0f (`RoutingPreferences.cs:99-125`); multi-objective A* (D-2) and role-based preferences (D-4) are still in flight - **Verified:** 2026-10-12

**Prerequisite**: Phase C (Dynamic flow modeling via pipe system, A* pathfinding infrastructure)

**Design Goal**: Enable NPCs to choose different routes to the same destination based on personality and urgency. A worker hurrying to a deadline takes a fast direct route; a leisurely socialite takes a scenic detour; a cautious drifter avoids dangerous areas.

---

## Problem Statement

Phase C established the foundation: NavMesh pathfinding works, but all NPCs use identical cost functions (straight-line distance). This produces unrealistic behavior:
- All NPCs take the same optimal route
- No personality differentiation in movement
- No temporal pressure (urgency doesn't affect route choice)
- NPCs don't account for danger zones or scenic areas

Phase D introduces **multi-objective routing**: A* pathfinding with role-based cost multipliers. Each NPC's goal (Fast, OnTime, Scenic, Safe, Custom) modulates lane costs during path computation, producing naturally different routes per NPC.

---

## Design Principles

1. **Goals drive costs**: Each goal adjusts NavLane costs via multipliers. Fast minimizes time, OnTime penalizes closed lanes near the deadline; Scenic (visually interesting areas) and Safe (avoid danger) are placeholders that do not change costs yet.
2. **Role-based initialization**: NPCs receive goal-appropriate routing preferences at creation time (workers get OnTime, socialites get Scenic, etc.), not dynamically.
3. **Urgency is dynamic**: While goal persists, urgency (0.0–1.0) is refreshed at the start of each travel leg from deadline proximity, allowing more aggressive routing as deadlines approach.
4. **Pluggable via interface**: `IRouteGenerator` supports swapping routing implementations (NavMesh, transit, cars) without changing NPC strategy code.
5. **Graceful fallback**: If pathfinding fails (timeout, no route), NPCs walk straight-line. No blocking.

---

## Vocabulary

- **RoutingPreferences**: Class holding NPC goal, deadline, scenery/safety weights, urgency level.
- **NpcGoal**: Enum: Fast, OnTime, Scenic, Safe, Custom. Defines routing strategy.
- **Cost multiplier**: Value (currently 1.0–1.5) applied to a NavLane's base cost during A*. Enables goal-driven preferences.
- **Urgency**: Float 0.0–1.0, a step function of minutes until the deadline. Higher urgency = more aggressive routing.
- **DeadlineTime**: DateTime when NPC must arrive for scheduled activity (set by schedule).

---

## Phase D-1: NPC Goals & Routing Preferences

### NpcGoal.cs (NEW)

Simple enum defining routing goals:
```csharp
public enum NpcGoal
{
    Fast,       // Minimize travel time
    OnTime,     // Arrive by deadline, penalize blocked lanes
    Scenic,     // Prefer visually interesting routes
    Safe,       // Avoid dangerous areas
    Custom      // Custom goal (for future expansion)
}
```

### RoutingPreferences.cs (NEW)

`JoyceCode/engine/tale/RoutingPreferences.cs` holds `Goal` (default `Fast`), `DeadlineTime`, `SceneryWeight` / `SafetyWeight` (default 0.5) and `Urgency` (0–1), plus `IsLate`. `ComputeCostMultiplier(lane, type)` returns:

| Goal | Multiplier |
|---|---|
| Fast, Custom | 1.0 |
| OnTime | 1.0 unless a deadline is set **and** `Urgency >= 0.5`; then a lane whose temporal constraint is closed (`lane.QueryConstraint(DateTime.Now)` — wall clock, not game time — `!CanAccess`) costs 1.2×, or 1.5× when it stays closed for more than 30 s |
| Scenic, Safe | 1.0 — placeholders; there is no scenic or danger data on `NavLane` yet, so the weights have no effect |

`UpdateUrgency(currentTime)` is a step function over minutes until `DeadlineTime`: past the deadline 1.0, under 5 min 0.9, under 15 min 0.7, under 30 min 0.5, otherwise 0.2; no deadline gives 0.0. OnTime therefore only bites in the last 30 minutes.

**Files**:
- `JoyceCode/engine/tale/NpcGoal.cs` — NEW
- `JoyceCode/engine/tale/RoutingPreferences.cs` — NEW

---

## Phase D-2: Multi-Objective A* Integration

### LocalPathfinder.cs (MODIFIED)

Updated to accept routing preferences and apply cost multipliers:

```csharp
private RoutingPreferences? _preferences;
private TransportationType _transportType;

// Constructor now accepts preferences (the transport type is required, no default)
public LocalPathfinder(NavCursor ncStart, NavCursor ncTarget,
    TransportationType transportType,
    RoutingPreferences? preferences = null)
{
    Start = ncStart;
    Target = ncTarget;
    _preferences = preferences;
    _transportType = transportType;
}

// New method to apply goal-based cost modulation
private float _applyPreferenceMultiplier(NavLane lane, float baseCost)
{
    if (_preferences == null)
        return baseCost;
    var multiplier = _preferences.ComputeCostMultiplier(lane, _transportType);
    return baseCost * multiplier;
}

// Modified _childNode to use preference multiplier
private Node _childNode(Node parent, NavLane nlToMe, NavJunction njNext)
{
    var baseCost = _realDistance(parent.Junction, njNext);
    var adjustedCost = _applyPreferenceMultiplier(nlToMe, baseCost);  // ← NEW

    return new Node()
    {
        Junction = njNext,
        LaneToMe = nlToMe,
        Parent = parent,
        CostFromStart = parent.CostFromStart + adjustedCost,
        EstimateToEnd = _realDistance(njNext, Target.Junction)
    };
}
```

### IRouteGenerator.cs (MODIFIED)

Signature updated to include routing preferences:

```csharp
Task<SegmentRoute> GetRouteAsync(Vector3 fromPos, Vector3 toPos, PositionDescription startPod,
    RoutingPreferences? preferences = null,
    TransportationType transportType = TransportationType.Pedestrian);
```

### StreetRouteBuilder.cs (MODIFIED)

BuildAsync signature updated to pass preferences through to pathfinder:

```csharp
public static async Task<SegmentRoute> BuildAsync(Vector3 fromPos, Vector3 toPos, NavMap navMap,
    PositionDescription startPod,
    TransportationType transportType = TransportationType.Pedestrian,
    RoutingPreferences? preferences = null,
    CancellationToken cancellationToken = default)
{
    // ...
    var pathfinder = new LocalPathfinder(startCursor, endCursor, transportType, preferences);
    var lanes = pathfinder.Pathfind();
    // ...
}
```

### NavMeshRouteGenerator.cs (MODIFIED)

Passes preferences from route request through to builder:

```csharp
public async Task<SegmentRoute> GetRouteAsync(Vector3 fromPos, Vector3 toPos, PositionDescription startPod,
    RoutingPreferences? preferences = null,
    TransportationType transportType = TransportationType.Pedestrian)
{
    // ...
    var route = await StreetRouteBuilder.BuildAsync(fromPos, toPos, navMap, startPod,
        transportType, preferences, cts.Token);
    return route;
}
```

The `TransportationType.Pedestrian` defaults on `GetRouteAsync` / `BuildAsync` are still in the code, against the CLAUDE.md rule that routing names its transport type — open, tracked separately.

**Files Modified**:
- `JoyceCode/builtin/modules/satnav/LocalPathfinder.cs`
- `JoyceCode/engine/tale/IRouteGenerator.cs`
- `nogameCode/nogame/characters/citizen/StreetRouteBuilder.cs`
- `nogameCode/nogame/characters/citizen/NavMeshRouteGenerator.cs`

---

## Phase D-3: TaleEntityStrategy Integration

### TaleEntityStrategy.cs (MODIFIED)

Wired to use NPC's role-based routing preferences:

```csharp
private RoutingPreferences _routingPreferences;

// Constructor initializes from schedule
private TaleEntityStrategy(...)
{
    // ...
    var schedule = taleManager.GetSchedule(npcId);
    _routingPreferences = schedule?.RoutingPreferences ?? new RoutingPreferences();
}

// UpdateRoutingPreferences refreshes urgency only (goal persists from creation)
private void UpdateRoutingPreferences(DateTime currentTime)
{
    var schedule = _taleManager.GetSchedule(_npcId);
    if (schedule == null) return;

    _routingPreferences = schedule.RoutingPreferences;
    if (schedule.NextEventTime.HasValue)
        _routingPreferences.DeadlineTime = schedule.NextEventTime;
    _routingPreferences.UpdateUrgency(currentTime);
}

// _travelToCurrentDestination (called once per travel leg, from
// _advanceAndTravelAsync and after a collapse from _resumeTravelAsync)
// refreshes urgency and passes preferences to the route generator
private async Task _travelToCurrentDestination(DateTime gameNow)
{
    // ...
    UpdateRoutingPreferences(gameNow);
    var route = await routeGen.GetRouteAsync(_currentPosition.Position, destination,
        _currentPosition, _routingPreferences, schedule.PreferredTransportationType);
    // ...
}
```

**Files Modified**:
- `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs`

---

## Phase D-4: Behavioral Variety (Role-Based Preferences)

### TalePopulationGenerator.cs (MODIFIED)

Added role-based preference generation during NPC creation:

```csharp
private RoutingPreferences GenerateRoutingPreferences(string role, RandomSource rnd)
{
    var prefs = new RoutingPreferences();

    switch (role)
    {
        // Time-conscious: deadline-driven routing
        case "worker":
        case "authority":
        case "nightworker":
            prefs.Goal = NpcGoal.OnTime;
            prefs.SceneryWeight = 0.1f;
            prefs.SafetyWeight = 0.2f;
            break;

        // Commerce-driven: speed maximized
        case "merchant":
        case "hustler":
            prefs.Goal = NpcGoal.Fast;
            prefs.SceneryWeight = 0.0f;
            prefs.SafetyWeight = 0.1f;
            break;

        // Leisure-focused: scenic routes preferred
        case "socialite":
        case "reveler":
            prefs.Goal = NpcGoal.Scenic;
            prefs.SceneryWeight = 0.8f;
            prefs.SafetyWeight = 0.3f;
            break;

        // Cautious: safety paramount
        case "drifter":
            prefs.Goal = NpcGoal.Safe;
            prefs.SceneryWeight = 0.4f;
            prefs.SafetyWeight = 0.9f;
            break;
    }

    return prefs;
}

// Assign preferences during NPC creation
private NpcSchedule GenerateNpc(...)
{
    // ...
    var routingPreferences = GenerateRoutingPreferences(role, rnd);

    return new NpcSchedule
    {
        // ... existing fields
        RoutingPreferences = routingPreferences,
    };
}
```

### NpcSchedule.cs (MODIFIED)

Added RoutingPreferences field:

```csharp
/// <summary>
/// Routing preferences for multi-objective pathfinding (goal, urgency, weights).
/// </summary>
public RoutingPreferences RoutingPreferences { get; set; } = new();
```

**Files Modified**:
- `JoyceCode/engine/tale/TalePopulationGenerator.cs`
- `JoyceCode/engine/tale/NpcSchedule.cs`

---

## Phase D-5: Integration & Regression Testing

**Status**: open — no result for this phase is recorded here.

The TALE regression suite (`./run_tests.sh all`) should continue to pass, confirming that multi-objective routing integrates without breaking existing NPC scheduling, TALE narrative, or navigation systems.

---

## Phase D-6: Documentation

**Status**: COMPLETE (this file)

- Phase D overview (this file)
- Design principles and vocabulary documented
- Code examples and file modifications listed
- Test expectations established

---

## Files Added / Modified

### NEW Files
- `JoyceCode/engine/tale/NpcGoal.cs`
- `JoyceCode/engine/tale/RoutingPreferences.cs`

### MODIFIED Files
- `JoyceCode/engine/tale/IRouteGenerator.cs` — Added preferences parameter
- `JoyceCode/engine/tale/NpcSchedule.cs` — Added RoutingPreferences field
- `JoyceCode/engine/tale/TalePopulationGenerator.cs` — Added role-based preference generation
- `JoyceCode/builtin/modules/satnav/LocalPathfinder.cs` — Multi-objective A* cost application
- `nogameCode/nogame/characters/citizen/StreetRouteBuilder.cs` — Pass preferences through pathfinding
- `nogameCode/nogame/characters/citizen/NavMeshRouteGenerator.cs` — Accept preferences from caller
- `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` — Use schedule's routing preferences

---

## Implementation Notes

1. **Backward Compatibility**: All preference parameters are optional with sensible defaults (Fast goal, no deadline, neutral weights). Existing code paths continue to work.
2. **Performance**: Urgency is refreshed once per travel leg (`UpdateRoutingPreferences` in `_travelToCurrentDestination`, right before the route request), not per frame or on a timer.
3. **Fallback Behavior**: If NavMesh routing is unavailable, NPCs use straight-line movement (GoToStrategyPart handles this).
4. **Testing**: Unit tests for RoutingPreferences verify goal-specific multiplier logic; regression tests verify no breakage in NPC lifecycle.

---

## Future Extensions (Phase D+)

1. **Custom Goals**: NPCs with unusual goals (e.g., "collect items", "avoid specific NPCs") via NpcGoal.Custom
2. **Dynamic Safety Zones**: Mark lanes dangerous/safe based on NPC encounters, group formation
3. **Learned Routes**: NPCs cache frequently-used routes, reuse them instead of recomputing
4. **Traffic Modeling**: NavLanes accumulate congestion; busy routes become more expensive
5. **Multi-Modal Routing**: NPCs switch between walking, transit, cars based on time pressure

---

## Glossary

| Term | Definition |
|------|-----------|
| A* Pathfinding | Graph search algorithm finding lowest-cost path; cost = distance + heuristic |
| Cost Multiplier | Factor (currently 1.0–1.5) applied to NavLane cost during A*; enables goal-based routing |
| Deadline | DateTime by which NPC must arrive; drives OnTime goal urgency |
| DeadlineTime | RoutingPreferences field holding the deadline DateTime |
| Goal | Enum (Fast, OnTime, Scenic, Safe) defining NPC routing strategy |
| NavLane | Graph edge (street segment); A* explores edges weighted by cost |
| Routing Preferences | Class holding goal, deadline, weights, urgency; passed to pathfinder |
| Urgency | Float 0.0–1.0, step function of minutes to the deadline; gates the OnTime multiplier |

