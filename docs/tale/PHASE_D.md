# Phase D: Multi-Objective Routing (NPCs Take Different Routes)

**Status**: IMPLEMENTATION COMPLETE (2026-03-25)

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

1. **Goals drive costs**: Each goal adjusts NavLane costs via multipliers. Fast minimizes time, OnTime penalizes blockedlanes near deadline, Scenic favors visually interesting areas, Safe avoids danger.
2. **Role-based initialization**: NPCs receive goal-appropriate routing preferences at creation time (workers get OnTime, socialites get Scenic, etc.), not dynamically.
3. **Urgency is dynamic**: While goal persists, urgency (0.0–1.0) updates each frame based on deadline proximity, allowing more aggressive routing as deadlines approach.
4. **Pluggable via interface**: `IRouteGenerator` supports swapping routing implementations (NavMesh, transit, cars) without changing NPC strategy code.
5. **Graceful fallback**: If pathfinding fails (timeout, no route), NPCs walk straight-line. No blocking.

---

## Vocabulary

- **RoutingPreferences**: Class holding NPC goal, deadline, scenery/safety weights, urgency level.
- **NpcGoal**: Enum: Fast, OnTime, Scenic, Safe, Custom. Defines routing strategy.
- **Cost multiplier**: Value (typically 0.5–2.0) applied to a NavLane's base cost during A*. Enables goal-driven preferences.
- **Urgency**: Float 0.0–1.0, computed from deadline - now. Higher urgency = more aggressive routing.
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

Core class for routing logic:
```csharp
public class RoutingPreferences
{
    public NpcGoal Goal { get; set; } = NpcGoal.Fast;
    public DateTime? DeadlineTime { get; set; }
    public float SceneryWeight { get; set; } = 0.5f;  // Bias toward scenic lanes
    public float SafetyWeight { get; set; } = 0.5f;   // Bias against danger
    public float Urgency { get; set; } = 0.0f;        // 0.0–1.0

    /// <summary>
    /// Compute cost multiplier for a lane based on goal and current state.
    /// </summary>
    public float ComputeCostMultiplier(NavLane lane, TransportationType type)
    {
        float multiplier = 1.0f;

        switch (Goal)
        {
            case NpcGoal.Fast:
                // Prefer short routes; no modulation
                break;

            case NpcGoal.OnTime:
                // Penalize blocked lanes, especially when late
                if (lane.IsBlocked)
                    multiplier = 1.2f + Urgency * 0.3f;  // Up to 1.5x cost if running very late
                break;

            case NpcGoal.Scenic:
                // Prefer lanes marked scenic; lower cost for scenic lanes
                if (lane.Tags.Contains("scenic"))
                    multiplier = Math.Max(0.5f, 1.0f - SceneryWeight * 0.5f);
                break;

            case NpcGoal.Safe:
                // Prefer lanes away from danger zones
                if (lane.Tags.Contains("danger"))
                    multiplier = 1.5f + SafetyWeight * 0.5f;
                break;
        }

        return multiplier;
    }

    /// <summary>
    /// Update urgency based on current time and deadline.
    /// </summary>
    public void UpdateUrgency(DateTime currentTime)
    {
        if (!DeadlineTime.HasValue)
        {
            Urgency = 0.0f;
            return;
        }

        var timeRemaining = (DeadlineTime.Value - currentTime).TotalSeconds;
        if (timeRemaining <= 0)
            Urgency = 1.0f;  // Past deadline: maximum urgency
        else if (timeRemaining > 300)  // 5 minutes
            Urgency = 0.0f;  // Plenty of time
        else
            Urgency = (float)(1.0 - timeRemaining / 300.0);  // Linear 0–1 as deadline approaches
    }

    public bool IsLate => DeadlineTime != null && DateTime.Now > DeadlineTime.Value;
}
```

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

// Constructor now accepts preferences
public LocalPathfinder(NavCursor ncStart, NavCursor ncTarget,
    RoutingPreferences? preferences = null,
    TransportationType transportType = TransportationType.Pedestrian)
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
    var pathfinder = new LocalPathfinder(startCursor, endCursor, preferences, transportType);
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

// _advanceAndTravel passes preferences to route generator
private async void _advanceAndTravel()
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

**Status**: PENDING (awaiting test results from `./run_tests.sh all`)

All 171 regression tests (phases 0–8) should continue to pass, confirming that multi-objective routing integrates without breaking existing NPC scheduling, TALE narrative, or navigation systems.

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
2. **Performance**: Preference updates happen every 10 seconds in TaleEntityStrategy.UpdateRoutingPreferences, not per-frame, to avoid pathfinding overhead.
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
| Cost Multiplier | Factor (0.5–2.0) applied to NavLane cost during A*; enables goal-based routing |
| Deadline | DateTime by which NPC must arrive; drives OnTime goal urgency |
| DeadlineTime | RoutingPreferences field holding the deadline DateTime |
| Goal | Enum (Fast, OnTime, Scenic, Safe) defining NPC routing strategy |
| NavLane | Graph edge (street segment); A* explores edges weighted by cost |
| Routing Preferences | Class holding goal, deadline, weights, urgency; passed to pathfinder |
| Urgency | Float 0.0–1.0 computed from deadline – now; modulates cost multipliers |

