# Phase D Implementation Plan: Routing Integration — Multi-Objective Costs

**Phase:** D (Routing Integration)
**Duration:** ~2-3 days
**Status:** Ready for execution
**Depends On:** Phase A complete; Phase B, C optional (works without flow system)
**Enables:** Future phases (emergent behavior, detailed simulation)

---

## Overview

Phase D enables NPCs to route based on their goals and schedule, not just shortest path. This brings TALE characters to life by making their navigation reflect their personality and circumstances.

**Objectives:**
1. Define NPC goals and routing preferences
2. Implement multi-objective cost functions for A*
3. Integrate with TaleEntityStrategy
4. Allow different NPC types to take different routes for same destination
5. Support time-pressure routing (late for work → prefer faster routes)

**Success Criteria:**
- NPCs with different goals find different routes
- Workers route based on arrival deadline
- Leisure NPCs prefer scenic routes
- Cost multipliers can be per-NPC and dynamic
- All regression tests passing
- Behavioral variety in simulation

---

## Task D1: NPC Goals & Routing Preferences

### Files to Create

**`JoyceCode/engine/tale/NpcGoal.cs`**

```csharp
namespace JoyceCode.engine.tale
{
    /// <summary>
    /// The primary goal affecting an NPC's routing decisions.
    /// </summary>
    public enum NpcGoal
    {
        /// <summary>
        /// Minimize travel time (shortest path).
        /// Default for most NPCs.
        /// </summary>
        Fast,

        /// <summary>
        /// Arrive by a specific deadline (e.g., work by 9am).
        /// Prefers routes that allow time for delays.
        /// </summary>
        OnTime,

        /// <summary>
        /// Prefer scenic or pleasant routes.
        /// Leisure NPCs, people exploring.
        /// </summary>
        Scenic,

        /// <summary>
        /// Avoid dangerous or high-traffic areas.
        /// Cautious NPCs, people with anxiety.
        /// </summary>
        Safe,

        /// <summary>
        /// Custom goal (defined by subclass or property).
        /// For future extension.
        /// </summary>
        Custom
    }
}
```

**`JoyceCode/engine/tale/RoutingPreferences.cs`**

```csharp
using System;
using JoyceCode.engine.navigation;

namespace JoyceCode.engine.tale
{
    /// <summary>
    /// Routing preferences for an NPC, affecting A* cost calculation.
    /// </summary>
    public class RoutingPreferences
    {
        /// <summary>
        /// The primary routing goal.
        /// </summary>
        public NpcGoal Goal { get; set; } = NpcGoal.Fast;

        /// <summary>
        /// For OnTime goal: when does the NPC need to arrive?
        /// </summary>
        public DateTime? DeadlineTime { get; set; }

        /// <summary>
        /// For Scenic goal: weight for scenic attribute (0-1).
        /// Higher = more preference for scenic routes.
        /// </summary>
        public float SceneryWeight { get; set; } = 0.5f;

        /// <summary>
        /// For Safe goal: weight for safety/traffic avoidance (0-1).
        /// Higher = more avoidance of high-traffic areas.
        /// </summary>
        public float SafetyWeight { get; set; } = 0.5f;

        /// <summary>
        /// Urgency level (0-1).
        /// 0 = relaxed, 1 = very urgent
        /// Used to modulate routing preference.
        /// </summary>
        public float Urgency { get; set; } = 0.0f;

        /// <summary>
        /// Compute a cost multiplier for a specific lane.
        /// Values > 1.0 make the lane less desirable.
        /// </summary>
        public float ComputeCostMultiplier(NavLane lane, TransportationType type)
        {
            // Base cost (distance/speed) is already in the lane
            // This multiplier adjusts it based on goal

            return Goal switch
            {
                NpcGoal.Fast =>
                    1.0f,  // No adjustment

                NpcGoal.OnTime =>
                    ComputeOnTimeMultiplier(lane, type),

                NpcGoal.Scenic =>
                    ComputeScenicMultiplier(lane),

                NpcGoal.Safe =>
                    ComputeSafetyMultiplier(lane),

                NpcGoal.Custom =>
                    1.0f,

                _ => 1.0f
            };
        }

        /// <summary>
        /// For OnTime goal: prefer routes that avoid high-wait situations.
        /// Increase cost of lanes with long expected waits.
        /// </summary>
        private float ComputeOnTimeMultiplier(NavLane lane, TransportationType type)
        {
            if (DeadlineTime == null || Urgency < 0.5f)
                return 1.0f;  // Not urgent

            // Penalize lanes with temporal constraints (traffic lights, etc.)
            if (lane.Constraint != null)
            {
                var state = lane.QueryConstraint(DateTime.Now);
                var untilChange = state.UntilChange.TotalSeconds;

                // If light is red and will take a while to change, penalize heavily
                if (!state.CanAccess && untilChange > 30)
                    return 1.5f;  // 50% cost increase
                else if (!state.CanAccess)
                    return 1.2f;  // 20% cost increase
            }

            return 1.0f;
        }

        /// <summary>
        /// For Scenic goal: prefer lanes with high scenic score.
        /// </summary>
        private float ComputeScenicMultiplier(NavLane lane)
        {
            // Lane.ScenicScore (0-1, not defined in current NavLane but can be added)
            // For now, assume lanes near parks/water have higher scores
            // This is a placeholder; actual implementation depends on lane data

            // Example: prefer lanes with scenic=0.7+ (parks, waterfronts)
            // For simplicity, we'll assume some lanes have scenic properties
            const float scenicThreshold = 0.6f;
            // const float scenicScore = lane.ScenicScore ?? 0.0f;

            // For now, return neutral; implement scenic scoring later
            return 1.0f;
        }

        /// <summary>
        /// For Safe goal: avoid high-traffic areas.
        /// Penalize lanes in busy intersections, highways.
        /// </summary>
        private float ComputeSafetyMultiplier(NavLane lane)
        {
            // Lane.TrafficDensity (0-1, not defined in current NavLane but can be added)
            // For now, assume busy lanes have higher density
            // This is a placeholder; actual implementation depends on observed traffic

            // Example: avoid lanes with traffic_density > 0.7
            // For simplicity, return neutral; implement traffic density tracking later

            return 1.0f;
        }

        /// <summary>
        /// Check if this NPC is late (current time > deadline).
        /// </summary>
        public bool IsLate => DeadlineTime.HasValue &&
                              DateTime.Now > DeadlineTime.Value;

        /// <summary>
        /// Get urgency based on time until deadline.
        /// </summary>
        public void UpdateUrgency(DateTime currentTime)
        {
            if (!DeadlineTime.HasValue)
            {
                Urgency = 0.0f;
                return;
            }

            var timeRemaining = (DeadlineTime.Value - currentTime).TotalMinutes;

            if (timeRemaining < 0)
                Urgency = 1.0f;  // Already late
            else if (timeRemaining < 5)
                Urgency = 0.9f;  // Very urgent
            else if (timeRemaining < 15)
                Urgency = 0.7f;  // Urgent
            else if (timeRemaining < 30)
                Urgency = 0.5f;  // Moderately urgent
            else
                Urgency = 0.2f;  // Relaxed
        }

        public override string ToString()
            => $"RoutingPreferences({Goal}, urgency={Urgency:F1})";
    }
}
```

### Tests to Write

**`tests/JoyceCode.Tests/engine/tale/RoutingPreferencesTests.cs`**

```csharp
using System;
using Xunit;
using JoyceCode.engine.tale;
using JoyceCode.engine.navigation;

namespace JoyceCode.Tests.engine.tale
{
    public class RoutingPreferencesTests
    {
        [Fact]
        public void Fast_Goal_ReturnsUnityMultiplier()
        {
            var prefs = new RoutingPreferences { Goal = NpcGoal.Fast };
            var lane = new NavLane();

            var multiplier = prefs.ComputeCostMultiplier(lane, TransportationType.Pedestrian);

            Assert.Equal(1.0f, multiplier);
        }

        [Fact]
        public void OnTime_Goal_PenalizeBlockedLanes()
        {
            var constraint = new CyclicConstraint
            {
                CycleSeconds = 60,
                ActivePhaseStart = 60,  // Always red
                ActivePhaseDuration = 0
            };

            var lane = new NavLane
            {
                Constraint = constraint,
                AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
            };

            var prefs = new RoutingPreferences
            {
                Goal = NpcGoal.OnTime,
                DeadlineTime = DateTime.Now.AddMinutes(5),
                Urgency = 1.0f  // Very urgent
            };

            var multiplier = prefs.ComputeCostMultiplier(lane, TransportationType.Pedestrian);

            Assert.True(multiplier > 1.0f);  // Penalized
        }

        [Fact]
        public void UpdateUrgency_Late_ReturnsMaxUrgency()
        {
            var prefs = new RoutingPreferences
            {
                DeadlineTime = DateTime.Now.AddMinutes(-5)  // 5 min ago
            };

            prefs.UpdateUrgency(DateTime.Now);

            Assert.Equal(1.0f, prefs.Urgency);
        }

        [Fact]
        public void UpdateUrgency_VeryCloseDeadline_ReturnsHighUrgency()
        {
            var prefs = new RoutingPreferences
            {
                DeadlineTime = DateTime.Now.AddMinutes(3)  // 3 min away
            };

            prefs.UpdateUrgency(DateTime.Now);

            Assert.True(prefs.Urgency > 0.8f);
        }

        [Fact]
        public void UpdateUrgency_FarDeadline_ReturnsLowUrgency()
        {
            var prefs = new RoutingPreferences
            {
                DeadlineTime = DateTime.Now.AddHours(2)  // 2 hours away
            };

            prefs.UpdateUrgency(DateTime.Now);

            Assert.True(prefs.Urgency < 0.3f);
        }

        [Fact]
        public void IsLate_BeforeDeadline_ReturnsFalse()
        {
            var prefs = new RoutingPreferences
            {
                DeadlineTime = DateTime.Now.AddMinutes(10)
            };

            Assert.False(prefs.IsLate);
        }

        [Fact]
        public void IsLate_AfterDeadline_ReturnsTrue()
        {
            var prefs = new RoutingPreferences
            {
                DeadlineTime = DateTime.Now.AddMinutes(-5)
            };

            Assert.True(prefs.IsLate);
        }
    }
}
```

### Checklist

- [ ] Create NpcGoal.cs enum
- [ ] Create RoutingPreferences.cs class
- [ ] Implement ComputeCostMultiplier() for each goal
- [ ] Implement UpdateUrgency() based on deadline
- [ ] Implement IsLate property
- [ ] Write and pass RoutingPreferencesTests
- [ ] Compile without errors

---

## Task D2: Multi-Objective A*

### Files to Modify

**`JoyceCode/engine/tale/StreetRouteBuilder.cs`**

Update A* to accept routing preferences:

```csharp
/// <summary>
/// Find a route with optional routing preferences (multi-objective).
/// </summary>
public async Task<List<NavLane>> FindRouteAsync(
    Vector3 start,
    Vector3 end,
    TransportationType transportType = TransportationType.Pedestrian,
    RoutingPreferences? preferences = null,
    CancellationToken cancellationToken = default)
{
    var graph = _navMap.GetGraphFor(transportType);

    if (graph.AllLanes.Count == 0)
        return new List<NavLane>();

    var startLane = graph.AllLanes
        .OrderBy(lane => DistancePointToLane(start, lane))
        .FirstOrDefault();

    if (startLane == null)
        return new List<NavLane>();

    var endLane = graph.AllLanes
        .OrderBy(lane => DistancePointToLane(end, lane))
        .FirstOrDefault();

    if (endLane == null)
        return new List<NavLane>();

    // A* with multi-objective cost function
    return await AStarAsync(
        startLane,
        endLane,
        graph,
        transportType,
        preferences,
        cancellationToken);
}

/// <summary>
/// Compute cost of traversing a lane.
/// Takes into account preferences if provided.
/// </summary>
private float ComputeCost(NavLane lane, TransportationType type,
    RoutingPreferences? preferences)
{
    // Base cost: distance / speed
    var baseCost = lane.GetCost(type);

    if (preferences == null)
        return baseCost;

    // Apply preference multiplier
    var multiplier = preferences.ComputeCostMultiplier(lane, type);
    return baseCost * multiplier;
}

/// <summary>
/// A* pathfinding with optional multi-objective cost.
/// </summary>
private async Task<List<NavLane>> AStarAsync(
    NavLane start,
    NavLane goal,
    RoutingGraph graph,
    TransportationType transportType,
    RoutingPreferences? preferences,
    CancellationToken cancellationToken)
{
    // Existing A* implementation, updated to use:
    // ComputeCost(lane, transportType, preferences)
    // instead of:
    // ComputeCost(lane, transportType)

    // ... (standard A* algorithm)
    // The key change is passing preferences to ComputeCost

    return path;
}
```

### Test Example

```csharp
[Fact]
public async Task FindRouteAsync_DifferentGoals_FindDifferentRoutes()
{
    // Create scenario with scenic and fast routes
    // Fast route: highway, 5 min
    // Scenic route: parks, 7 min

    var fastLane = new NavLane
    {
        From = Vector3.Zero,
        To = new Vector3(500, 0, 0),
        AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
    };

    var scenicLane = new NavLane
    {
        From = Vector3.Zero,
        To = new Vector3(100, 0, 0),
        AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
    };

    var navMap = new NavMap(new[] { fastLane, scenicLane });
    var builder = new StreetRouteBuilder(navMap);

    // Fast goal
    var fastPrefs = new RoutingPreferences { Goal = NpcGoal.Fast };
    var fastRoute = await builder.FindRouteAsync(
        Vector3.Zero, new Vector3(600, 0, 0),
        TransportationType.Pedestrian, fastPrefs);

    // Scenic goal (would prefer scenic lane if implementation complete)
    var scenicPrefs = new RoutingPreferences { Goal = NpcGoal.Scenic };
    var scenicRoute = await builder.FindRouteAsync(
        Vector3.Zero, new Vector3(600, 0, 0),
        TransportationType.Pedestrian, scenicPrefs);

    // Routes might differ (or at least cost calculations differ)
    Assert.NotNull(fastRoute);
    Assert.NotNull(scenicRoute);
}
```

### Checklist

- [ ] Update FindRouteAsync() to accept RoutingPreferences
- [ ] Pass preferences to A* pathfinding
- [ ] Update ComputeCost() to use preference multiplier
- [ ] A* respects different cost functions
- [ ] Write integration test for multi-objective routing
- [ ] Compile without errors

---

## Task D3: TaleEntityStrategy Integration

### Files to Modify

**`JoyceCode/engine/tale/TaleEntityStrategy.cs`**

Add routing preferences to NPC strategy:

```csharp
public class TaleEntityStrategy : AStrategy
{
    private RoutingPreferences _routingPreferences;
    private DateTime _lastPreferenceUpdate;

    // ... existing code ...

    /// <summary>
    /// Update routing preferences based on current schedule and state.
    /// </summary>
    private void UpdateRoutingPreferences(DateTime currentTime)
    {
        // Only update periodically (not every frame)
        if ((currentTime - _lastPreferenceUpdate).TotalSeconds < 10.0)
            return;

        _lastPreferenceUpdate = currentTime;

        // Determine goal based on activity and schedule
        if (_npcSchedule.IsLate)
        {
            _routingPreferences.Goal = NpcGoal.OnTime;
            _routingPreferences.DeadlineTime = _npcSchedule.NextEventTime;
        }
        else
        {
            // Default to fast (most NPCs)
            _routingPreferences.Goal = NpcGoal.Fast;
        }

        // Update urgency level
        _routingPreferences.UpdateUrgency(currentTime);
    }

    /// <summary>
    /// Route to destination using current preferences.
    /// </summary>
    private async Task UpdateRouteAsync(Vector3 destination, DateTime currentTime)
    {
        UpdateRoutingPreferences(currentTime);

        _route = await _routeBuilder.FindRouteAsync(
            _npcSchedule.CurrentWorldPosition,
            destination,
            _npcSchedule.PreferredTransportationType,
            _routingPreferences);  // Pass preferences!

        _routeIndex = 0;
    }

    // ... existing code ...
}
```

### Files to Create

**Update NpcSchedule.cs** (if not already present):

Add properties to support routing:

```csharp
/// <summary>
/// Preferred transportation type for this NPC.
/// </summary>
public TransportationType PreferredTransportationType { get; set; } =
    TransportationType.Pedestrian;

/// <summary>
/// Is this NPC late for its next scheduled activity?
/// </summary>
public bool IsLate
{
    get
    {
        if (NextEventTime == null)
            return false;
        return DateTime.Now > NextEventTime.Value;
    }
}

/// <summary>
/// Next scheduled activity time.
/// </summary>
public DateTime? NextEventTime { get; set; }
```

### Test Example

```csharp
[Fact]
public void TaleEntityStrategy_WhenLate_UsesOnTimeGoal()
{
    var schedule = new NpcSchedule
    {
        NextEventTime = DateTime.Now.AddMinutes(-5)  // Already late
    };

    var strategy = new TaleEntityStrategy(schedule);
    strategy.UpdateRoutingPreferences(DateTime.Now);

    Assert.Equal(NpcGoal.OnTime, strategy.RoutingPreferences.Goal);
}

[Fact]
public void TaleEntityStrategy_WhenOnTime_UsesFastGoal()
{
    var schedule = new NpcSchedule
    {
        NextEventTime = DateTime.Now.AddHours(2)  // Plenty of time
    };

    var strategy = new TaleEntityStrategy(schedule);
    strategy.UpdateRoutingPreferences(DateTime.Now);

    Assert.Equal(NpcGoal.Fast, strategy.RoutingPreferences.Goal);
}
```

### Checklist

- [ ] Add RoutingPreferences to TaleEntityStrategy
- [ ] Implement UpdateRoutingPreferences()
- [ ] Update UpdateRouteAsync() to pass preferences
- [ ] Add IsLate property to NpcSchedule
- [ ] Add NextEventTime to NpcSchedule
- [ ] Add PreferredTransportationType to NpcSchedule
- [ ] Write integration tests
- [ ] Compile without errors

---

## Task D4: Behavioral Variety

### Files to Modify

**`nogameCode/nogame/tale/CitizenScheduleFactory.cs`** (or equivalent)

Assign different goals to different NPC types:

```csharp
private RoutingPreferences CreateRoutingPreferencesFor(NpcRole role,
    DateTime currentTime)
{
    var prefs = new RoutingPreferences();

    // Different roles have different routing preferences
    switch (role)
    {
        case NpcRole.Worker:
            // Workers have deadlines
            prefs.Goal = NpcGoal.OnTime;
            prefs.DeadlineTime = currentTime.AddHours(8);  // Work starts at 9am
            break;

        case NpcRole.Leisure:
            // Leisure NPCs prefer scenic routes
            prefs.Goal = NpcGoal.Scenic;
            prefs.SceneryWeight = 0.8f;
            break;

        case NpcRole.Cautious:
            // Cautious NPCs avoid traffic/danger
            prefs.Goal = NpcGoal.Safe;
            prefs.SafetyWeight = 0.9f;
            break;

        default:
            prefs.Goal = NpcGoal.Fast;
            break;
    }

    return prefs;
}
```

### Example: Different Routes Same Destination

**Test Scenario:**

```csharp
[Fact]
public void DifferentNpcTypes_TakeDifferentRoutes()
{
    var navMap = CreateTestNavMap();  // Multiple paths available

    // Worker: fast route via highway
    var worker = new NpcSchedule
    {
        Role = NpcRole.Worker,
        NextEventTime = DateTime.Now.AddMinutes(5)
    };
    var workerRoute = FindRoute(worker.CurrentPos, destination, worker);

    // Leisure: scenic route via park
    var leisure = new NpcSchedule
    {
        Role = NpcRole.Leisure,
        // No deadline
    };
    var leisureRoute = FindRoute(leisure.CurrentPos, destination, leisure);

    // Routes should differ
    Assert.NotEqual(workerRoute, leisureRoute);
}
```

### Checklist

- [ ] Define NPC role-based routing preferences
- [ ] Create different preference sets for different types
- [ ] Workers: OnTime goal with deadlines
- [ ] Leisure: Scenic goal with scenic weight
- [ ] Cautious: Safe goal with safety weight
- [ ] Verify different NPCs take different routes
- [ ] Write behavioral tests

---

## Task D5: Integration & Regression Testing

### Run Full Test Suite

```bash
./run_tests.sh phase7
./run_tests.sh phase7b
./run_tests.sh all
```

### 60-Day Simulation

Run full simulation with new routing system:

```bash
./run_tests.sh phase8
./run_recalibration_tests.sh phase8 TALE_SIM_DAYS=365
```

### Metrics to Verify

- Average trip time (should be reasonable)
- Late arrivals (workers arriving after deadline)
- Behavior diversity (different NPCs taking different paths)
- Performance (no frame time increase)

### Checklist

- [ ] Run Phase 7B regression tests
- [ ] Run full test suite
- [ ] Run 60-day simulation
- [ ] Verify metrics are reasonable
- [ ] No crashes or errors
- [ ] Performance acceptable

---

## Task D6: Documentation

### Update CLAUDE.md

```markdown
**Phase 8: Multi-Objective Routing (Complete)**
- NPCs route based on goals (Fast, OnTime, Scenic, Safe)
- Routing preferences factor schedule pressure and personality
- Different NPC types take different routes for same destination
- Workers route to arrive on time; leisure NPCs prefer scenic routes
- Cost functions configurable per-goal, per-NPC
```

### Create PHASE_8.md

Document routing system design:

```markdown
# Phase 8: Multi-Objective Routing

## Overview
NPCs now route based on their goals and current state, not just shortest path.

## Routing Goals
1. **Fast** — Minimize travel time (default)
2. **OnTime** — Arrive by deadline (workers)
3. **Scenic** — Prefer pleasant routes (leisure)
4. **Safe** — Avoid dangerous areas (cautious)

## Implementation
- RoutingPreferences class manages goal-specific cost multipliers
- A* pathfinding uses dynamic cost function based on preferences
- UpdateUrgency() calculates urgency based on deadline

## Behavioral Examples
- Worker arriving at 8:59am: Takes fastest route, ignores scenic paths
- Leisure NPC exploring: Takes scenic routes, takes longer
- Cautious NPC: Avoids busy streets and high-traffic areas

## Metrics
- Average trip time per goal type
- On-time arrival percentage (workers)
- Behavior diversity score
```

### Checklist

- [ ] Update CLAUDE.md with Phase 8 status
- [ ] Create PHASE_8.md design document
- [ ] Update docs/TESTING.md with new test counts
- [ ] Document RoutingPreferences class
- [ ] Document multi-objective A* approach
- [ ] Add examples of different routing goals

---

## Completion Checklist

- [ ] NpcGoal enum defined (Fast, OnTime, Scenic, Safe)
- [ ] RoutingPreferences class implemented
- [ ] Multi-objective cost functions working
- [ ] A* uses preferences for routing
- [ ] TaleEntityStrategy integrated with preferences
- [ ] NPC behavior changes based on schedule/goal
- [ ] Different NPC types take different routes
- [ ] All new unit tests passing
- [ ] All regression tests passing
- [ ] 60-day simulation runs successfully
- [ ] Code compiles without errors (Release mode)
- [ ] Documentation updated
- [ ] Ready for deployment

---

## Notes for Implementation

1. **Cost Multipliers:** Keep simple (1.0-2.0 range). Complex weighting in future phases.
2. **Urgency:** Update periodically (every 10s), not every frame. Reduces CPU cost.
3. **Goals:** Start with 4 basic goals. Add custom/complex goals later.
4. **Preference Persistence:** Cache preferences per NPC, update on schedule changes.
5. **Testing:** Create test scenarios with deadline, deadline-less, scenic paths available.
6. **Performance:** Multi-objective A* should be no slower than baseline (same algorithm, different costs).

---

## Success Definition

Phase D is complete when:
✅ NPCs route based on their goals (not just shortest path)
✅ Schedule pressure affects routing decisions
✅ Different NPC types exhibit different routing behavior
✅ Workers arrive on time; leisure NPCs take scenic routes
✅ All regression tests passing
✅ 60-day simulation stable and metrics reasonable
✅ Code is clean, tested, well-documented
✅ System is extensible (easy to add new goals)

---

## Future Enhancements (Out of Scope)

- **Scenic Scoring:** Implement lane-level scenic attributes (parks, waterfronts, etc.)
- **Traffic Density:** Track real-time congestion, use for route decisions
- **Social Routing:** NPCs meeting friends, group movements
- **Emergent Behaviors:** Lane changing, overtaking, congestion feedback
- **Time-Aware A*:** Account for temporal constraints in routing (true time-dimensional pathfinding)
- **Advanced Goals:** Stealth, profit, social status, etc.
