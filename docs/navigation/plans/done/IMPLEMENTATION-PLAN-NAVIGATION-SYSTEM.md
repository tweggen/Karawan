# Implementation Plan: Next-Generation Navigation System

**Status:** ✅ All Phases Complete
**Last Updated:** 2026-03-29
**Original Date:** 2026-03-24
**Related Proposals:**
- NAVMAP-TRANSPORTATION-GRAPHS.md
- TRAFFIC-LIGHTS-SYSTEM.md
- PIPES-FLOW-BASED-MOVEMENT.md

**Completed Phases:**
- ✅ **Phase A** (2026-03-24): Transportation types, routing graphs, cost calculation
- ✅ **Phase B** (2026-03-25): Temporal constraints, pipes, movement controller
- ✅ **Phase C** (2026-03-25): Dynamics — subdivisions, obstructions, speed functions
- ✅ **Phase D** (2026-03-25): Multi-objective routing, NPC goal integration

---

## Overview

This plan implements three integrated systems to support realistic entity movement through complex urban environments. Execution proceeds in four phases, each building on the previous.

**Design Decisions:**
- ✅ Multi-objective routing via cost functions (not time-aware A*)
- ✅ Temporal constraints as external, time-driven functions (not junction control)
- ✅ Permeable pipes (entities can exit/enter at any position)
- ✅ Defer junction capacity/control to future phase

**Scope:** Foundation through dynamic pipe subdivision and re-entry. Multi-objective routing integration deferred to Phase D.

---

## Phase A: Foundation — Transportation Types & Routing Graphs ✅ COMPLETE

**Status:** ✅ Completed 2026-03-24
**Duration:** ~2-3 days (actual: 1 day)
**Goal:** Extend NavLane with transportation types; build routing graphs per type.
**Prerequisite:** None (standalone, extends Phase 7C)
**Commit:** f7fadd49

### A1: Transportation Type System

**New Files:**
- `JoyceCode/engine/navigation/TransportationType.cs`
  ```csharp
  [Flags]
  public enum TransportationType
  {
      Pedestrian = 1,
      Car = 2,
      Bicycle = 4,
      Bus = 8,
      // Future: Motorcycle, Truck, etc.
  }

  public class TransportationTypeFlags
  {
      public TransportationType Value { get; set; }
      public bool HasFlag(TransportationType type) => (Value & type) != 0;
      public void Add(TransportationType type) => Value |= type;
      public void Remove(TransportationType type) => Value &= ~type;
  }
  ```

**Modified Files:**
- `JoyceCode/engine/navigation/NavLane.cs`
  ```csharp
  public class NavLane
  {
      // Existing
      public Vector3 From { get; set; }
      public Vector3 To { get; set; }

      // New
      public TransportationTypeFlags AllowedTypes { get; set; } =
          new TransportationTypeFlags { Value = TransportationType.Pedestrian };

      public float GetCost(TransportationType type)
      {
          // Default: cost is distance / speed
          var baseSpeed = type switch
          {
              TransportationType.Pedestrian => 1.5f,  // m/s
              TransportationType.Car => 13.4f,        // m/s (~30 mph)
              TransportationType.Bicycle => 5.0f,     // m/s
              _ => 1.5f
          };

          return Vector3.Distance(From, To) / baseSpeed;
      }
  }
  ```

**Testing:**
- Unit tests for TransportationTypeFlags (Add, Remove, HasFlag)
- Unit tests for NavLane.GetCost() returns expected values per type

### A2: Routing Graph Builder

**New Files:**
- `JoyceCode/engine/navigation/RoutingGraph.cs`
  ```csharp
  public class RoutingGraph
  {
      // Adjacency list: NavLane → list of outgoing NavLanes
      public Dictionary<NavLane, List<NavLane>> Edges { get; set; }

      // All NavLanes in this graph
      public List<NavLane> AllLanes { get; set; }

      // Metadata
      public TransportationType SupportedType { get; set; }
  }
  ```

- `JoyceCode/engine/navigation/RoutingGraphBuilder.cs`
  ```csharp
  public class RoutingGraphBuilder
  {
      private NavMap _navMap;
      private Dictionary<TransportationType, RoutingGraph> _graphCache;

      public RoutingGraph BuildFor(TransportationType type)
      {
          if (_graphCache.TryGetValue(type, out var cached))
              return cached;

          var graph = new RoutingGraph { SupportedType = type };

          // Filter NavLanes: only include those this type can use
          graph.AllLanes = _navMap.AllLanes
              .Where(lane => lane.AllowedTypes.HasFlag(type))
              .ToList();

          // Build adjacency: lanes connected at same endpoint
          graph.Edges = new Dictionary<NavLane, List<NavLane>>();
          foreach (var lane in graph.AllLanes)
          {
              var outgoing = graph.AllLanes
                  .Where(other => AreConnected(lane, other))
                  .ToList();
              graph.Edges[lane] = outgoing;
          }

          _graphCache[type] = graph;
          return graph;
      }

      private bool AreConnected(NavLane a, NavLane b)
          => Vector3.Distance(a.To, b.From) < 0.1f;  // Connected if endpoint near start
  }
  ```

**Modified Files:**
- `JoyceCode/engine/navigation/NavMap.cs`
  ```csharp
  public class NavMap
  {
      private RoutingGraphBuilder _graphBuilder;

      public NavMap(IEnumerable<NavLane> lanes)
      {
          AllLanes = lanes.ToList();
          _graphBuilder = new RoutingGraphBuilder(this);
      }

      public RoutingGraph GetGraphFor(TransportationType type)
          => _graphBuilder.BuildFor(type);

      // Call this when NavLanes change
      public void InvalidateGraphCache()
          => _graphBuilder.InvalidateCache();
  }
  ```

**Testing:**
- Graph construction: verify correct lanes included per type
- Connectivity: verify edges connect appropriately
- Caching: verify repeated calls return cached graph

### A3: A* Integration

**Modified Files:**
- `JoyceCode/engine/tale/StreetRouteBuilder.cs`
  ```csharp
  public class StreetRouteBuilder
  {
      private NavMap _navMap;

      public async Task<List<NavLane>> FindRouteAsync(
          Vector3 start, Vector3 end,
          TransportationType transportType,
          CancellationToken cancellationToken)
      {
          var graph = _navMap.GetGraphFor(transportType);

          // Find nearest lanes
          var startLane = graph.AllLanes
              .OrderBy(l => DistanceToLane(start, l))
              .First();
          var endLane = graph.AllLanes
              .OrderBy(l => DistanceToLane(end, l))
              .First();

          // A* using graph edges and cost function
          return await AStarAsync(startLane, endLane, graph, transportType, cancellationToken);
      }

      private float ComputeCost(NavLane lane, TransportationType type)
          => lane.GetCost(type);
  }
  ```

**Testing:**
- A* finds valid path for each transportation type
- Path respects AllowedTypes constraints
- Different types can find different routes (pedestrian uses bridge, car uses main street)

### A4: Phase A Success Criteria

- [ ] TransportationType enum and TransportationTypeFlags working
- [ ] NavLane extended with AllowedTypes and GetCost()
- [ ] RoutingGraphBuilder constructs correct graphs per type
- [ ] A* uses type-specific graphs
- [ ] Unit tests for all new classes
- [ ] All Phase 7B tests still passing (no regressions)
- [ ] Citizens still navigate correctly (default to Pedestrian type)

---

## Phase B: Flow — Pipes & Basic Movement ✅ COMPLETE

**Status:** ✅ Completed 2026-03-25
**Duration:** ~3-4 days (actual: 1 day)
**Commit:** 10935509

**Original Duration:** ~3-4 days
**Goal:** Implement pipes as flow containers; basic movement without subdivisions.
**Prerequisite:** Phase A complete

### B1: Temporal Constraint System

**New Files:**
- `JoyceCode/engine/navigation/TemporalConstraintState.cs`
  ```csharp
  public record TemporalConstraintState(bool CanAccess, TimeSpan UntilChange);
  ```

- `JoyceCode/engine/navigation/ITemporalConstraint.cs`
  ```csharp
  public interface ITemporalConstraint
  {
      TemporalConstraintState Query(DateTime currentTime);
  }
  ```

- `JoyceCode/engine/navigation/CyclicConstraint.cs`
  ```csharp
  public class CyclicConstraint : ITemporalConstraint
  {
      public double CycleSeconds { get; set; }
      public double ActivePhaseStart { get; set; }
      public double ActivePhaseDuration { get; set; }

      public TemporalConstraintState Query(DateTime time)
      {
          var cyclePos = (time.TotalSeconds % CycleSeconds);
          var isActive = cyclePos >= ActivePhaseStart &&
                        cyclePos < (ActivePhaseStart + ActivePhaseDuration);

          var untilChange = isActive
              ? (ActivePhaseStart + ActivePhaseDuration - cyclePos)
              : (CycleSeconds - cyclePos + ActivePhaseStart);

          return new TemporalConstraintState(isActive, TimeSpan.FromSeconds(untilChange));
      }
  }
  ```

**Modified Files:**
- `JoyceCode/engine/navigation/NavLane.cs`
  ```csharp
  public class NavLane
  {
      // Existing...

      // New: temporal constraint (traffic light, etc.)
      public ITemporalConstraint? Constraint { get; set; }

      public TemporalConstraintState QueryConstraint(DateTime time)
      {
          if (Constraint == null)
              return new TemporalConstraintState(true, TimeSpan.MaxValue);
          return Constraint.Query(time);
      }
  }
  ```

### B2: Pipe Core

**New Files:**
- `JoyceCode/engine/navigation/MovingEntity.cs`
  ```csharp
  public class MovingEntity
  {
      public int Id { get; set; }
      public Vector3 Position { get; set; }
      public float Speed { get; set; }  // Current speed
      public Vector3 Direction { get; set; }  // Forward direction

      public Pipe? CurrentPipe { get; set; }
      public List<NavLane> Route { get; set; }  // A* route
  }
  ```

- `JoyceCode/engine/navigation/Pipe.cs`
  ```csharp
  public class Pipe
  {
      public int Id { get; set; }

      // Topology
      public List<NavLane> NavLanes { get; set; }
      public Vector3 StartPosition { get; set; }
      public Vector3 EndPosition { get; set; }
      public float Length { get; set; }

      // Flow
      public Queue<MovingEntity> Entities { get; set; } = new();

      // Speed function: f(position, time) → speed
      public Func<Vector3, DateTime, float>? SpeedFunction { get; set; }

      // Capacity
      public int MaxCapacity { get; set; } = int.MaxValue;

      public float ComputeLength()
          => NavLanes.Sum(lane => Vector3.Distance(lane.From, lane.To));

      public float GetSpeedAt(Vector3 position, DateTime time)
      {
          if (SpeedFunction == null)
              return 5.0f;  // Default
          return SpeedFunction(position, time);
      }
  }
  ```

- `JoyceCode/engine/navigation/PipeNetwork.cs`
  ```csharp
  public class PipeNetwork
  {
      // All pipes in this network
      public List<Pipe> Pipes { get; set; } = new();

      // For transportation type
      public TransportationType SupportedType { get; set; }

      // Lookup: position → pipe containing it
      public Dictionary<Vector3, Pipe> PositionToPipe { get; set; }

      public Pipe? FindPipeContaining(Vector3 position)
      {
          // Find pipe that includes this position
          // (simplified: check if position is on any NavLane in pipe)
          return Pipes.FirstOrDefault(p => IsPositionInPipe(position, p));
      }
  }
  ```

### B3: Pipe Controller — Basic Movement

**New Files:**
- `JoyceCode/engine/navigation/PipeController.cs`
  ```csharp
  public class PipeController
  {
      private PipeNetwork _network;
      private Dictionary<int, MovingEntity> _entities;

      public void UpdateFrame(float deltaTime, DateTime currentTime)
      {
          foreach (var pipe in _network.Pipes)
          {
              // Move all entities in this pipe
              foreach (var entity in pipe.Entities)
              {
                  var speed = pipe.GetSpeedAt(entity.Position, currentTime);
                  entity.Speed = speed;

                  var displacement = speed * deltaTime * entity.Direction;
                  entity.Position += displacement;
              }
          }

          // Handle pipe transitions at boundaries (future)
          // Handle capacity overflow (future)
      }

      public void PlaceEntity(MovingEntity entity, List<NavLane> route)
      {
          entity.Route = route;

          // Find first pipe in route
          var firstPipe = _network.FindPipeContaining(route[0].From);
          if (firstPipe != null)
          {
              firstPipe.Entities.Enqueue(entity);
              entity.CurrentPipe = firstPipe;
          }
      }
  }
  ```

### B4: Phase B Success Criteria

- [ ] TemporalConstraint interface and CyclicConstraint working
- [ ] Pipe class models flow container correctly
- [ ] PipeNetwork organizes pipes by type
- [ ] PipeController updates entity positions per frame
- [ ] Entities move smoothly at constant speed (no subdivisions yet)
- [ ] Movement integrates with existing citizen simulation
- [ ] All Phase 7B tests still passing

---

## Phase C: Dynamics — Temporal Constraints & Subdivisions ✅ COMPLETE

**Status:** ✅ Completed 2026-03-25
**Duration:** ~4-5 days (actual: same day as Phase B)
**Goal:** Apply temporal constraints to pipes; implement dynamic subdivisions for obstructions.
**Prerequisite:** Phase A & B complete ✅
**Commit:** 380faf94

### C1: Temporal Constraint Integration

**Modified Files:**
- `JoyceCode/engine/navigation/Pipe.cs`
  ```csharp
  public class Pipe
  {
      // New: temporal constraint affecting this pipe (traffic light, etc.)
      public ITemporalConstraint? GlobalConstraint { get; set; }

      public float GetSpeedAt(Vector3 position, DateTime time)
      {
          // Check if constraint blocks access
          if (GlobalConstraint != null)
          {
              var state = GlobalConstraint.Query(time);
              if (!state.CanAccess)
                  return 0.0f;  // Red light: stop
          }

          // Apply speed function
          if (SpeedFunction == null)
              return 5.0f;
          return SpeedFunction(position, time);
      }
  }
  ```

**Testing:**
- Pipe with CyclicConstraint (traffic light cycle)
- Entities stop when constraint blocks access
- Entities move when constraint allows
- Speed function applied on top of constraint

### C2: Obstruction & Pipe Subdivision

**New Files:**
- `JoyceCode/engine/navigation/PipeSubdivision.cs`
  ```csharp
  public class PipeSubdivision
  {
      public Vector3 StartPosition { get; set; }
      public Vector3 EndPosition { get; set; }

      // Obstruction info (e.g., slow vehicle, accident)
      public Func<Vector3, DateTime, float>? LocalSpeedFunction { get; set; }

      public float GetSpeed(Vector3 position, DateTime time)
      {
          if (LocalSpeedFunction == null)
              return 5.0f;
          return LocalSpeedFunction(position, time);
      }
  }
  ```

**Modified Files:**
- `JoyceCode/engine/navigation/Pipe.cs`
  ```csharp
  public class Pipe
  {
      // Subdivisions (obstructions, slow zones)
      public List<PipeSubdivision> Subdivisions { get; set; } = new();

      public float GetSpeedAt(Vector3 position, DateTime time)
      {
          // Check global constraint first
          if (GlobalConstraint != null)
          {
              var state = GlobalConstraint.Query(time);
              if (!state.CanAccess)
                  return 0.0f;
          }

          // Check if position is in a subdivision
          var subdivision = Subdivisions
              .FirstOrDefault(s => IsPositionInSubdivision(position, s));

          if (subdivision != null)
              return subdivision.GetSpeed(position, time);

          // Otherwise use default speed function
          if (SpeedFunction == null)
              return 5.0f;
          return SpeedFunction(position, time);
      }

      public void AddObstruction(Vector3 position, float radius,
          Func<Vector3, DateTime, float> speedFunction)
      {
          var subdivision = new PipeSubdivision
          {
              StartPosition = position - new Vector3(radius, 0, radius),
              EndPosition = position + new Vector3(radius, 0, radius),
              LocalSpeedFunction = speedFunction
          };
          Subdivisions.Add(subdivision);
      }

      public void RemoveObstruction(Vector3 position)
      {
          Subdivisions.RemoveAll(s =>
              Vector3.Distance(s.StartPosition, position) < 1.0f);
      }
  }
  ```

**Modified Files:**
- `JoyceCode/engine/navigation/PipeController.cs`
  ```csharp
  public class PipeController
  {
      // Track obstructions (accidents, slow vehicles, etc.)
      private List<(Vector3 position, float radius, ITemporalConstraint duration)>
          _activeObstructions;

      public void RegisterObstruction(Vector3 position, float radius,
          ITemporalConstraint duration)
      {
          _activeObstructions.Add((position, radius, duration));

          // Find affected pipes and add subdivisions
          var pipe = _network.FindPipeContaining(position);
          if (pipe != null)
          {
              pipe.AddObstruction(position, radius,
                  (pos, time) => ComputeObstructionSpeed(position, pos, time));
          }
      }

      public void UpdateObstructions(DateTime currentTime)
      {
          // Remove expired obstructions
          var expired = _activeObstructions
              .Where(o => !o.duration.Query(currentTime).CanAccess)
              .ToList();

          foreach (var (pos, radius, _) in expired)
          {
              var pipe = _network.FindPipeContaining(pos);
              if (pipe != null)
                  pipe.RemoveObstruction(pos);

              _activeObstructions.Remove((pos, radius, _));
          }
      }

      private float ComputeObstructionSpeed(Vector3 obstaclePos,
          Vector3 entityPos, DateTime time)
      {
          // Braking wave: entities near obstacle brake hard,
          // entities far away brake gently
          var distance = Vector3.Distance(entityPos, obstaclePos);
          if (distance < 5.0f)
              return 0.5f;  // Near obstacle: slow
          if (distance < 20.0f)
              return 2.0f;  // Medium distance: slower
          return 4.0f;      // Far: normal speed
      }
  }
  ```

### C3: Exit/Re-entry

**Modified Files:**
- `JoyceCode/engine/navigation/PipeController.cs`
  ```csharp
  public class PipeController
  {
      // Entities off-pipe (e.g., pushed by physics)
      private List<MovingEntity> _offPipeEntities = new();

      public void RemoveEntityFromPipe(MovingEntity entity)
      {
          if (entity.CurrentPipe != null)
          {
              entity.CurrentPipe.Entities.Dequeue();  // or remove(entity)
              entity.CurrentPipe = null;
          }
          _offPipeEntities.Add(entity);
      }

      public void ReEnterPipe(MovingEntity entity, Vector3 position)
      {
          // Find pipe at position
          var pipe = _network.FindPipeContaining(position);
          if (pipe != null)
          {
              entity.Position = position;
              pipe.Entities.Enqueue(entity);
              entity.CurrentPipe = pipe;

              // If entity speed differs from local pipe speed,
              // it temporarily creates a slow zone (handled by PipeSubdivision)
          }

          _offPipeEntities.Remove(entity);
      }

      public void UpdateOffPipeEntities(float deltaTime, DateTime currentTime)
      {
          // Off-pipe entities move freely (physics, manual control, etc.)
          // They can re-enter when ready
      }
  }
  ```

### C4: Phase C Success Criteria

- [ ] Temporal constraints applied to pipes (traffic lights)
- [ ] Obstructions create subdivisions with local speed functions
- [ ] Entities queue naturally when obstruction present
- [ ] Obstructions clear, subdivisions merge, traffic flows
- [ ] Braking wave modeled by speed function (not per-entity)
- [ ] Entities can exit pipe (RemoveEntityFromPipe)
- [ ] Entities can re-enter pipe (ReEnterPipe)
- [ ] Off-pipe entities tracked separately
- [ ] All Phase 7B tests still passing
- [ ] New tests for obstructions, queuing, re-entry

---

## Phase D: Routing Integration — Multi-Objective Costs ✅ COMPLETE

**Status:** ✅ Completed 2026-03-25
**Duration:** ~2-3 days (actual: same day as Phase B & C)
**Goal:** Extend A* with multi-objective routing; integrate with pipe system.
**Prerequisite:** Phase A ✅, Phase B ✅, Phase C ✅
**Commits:** 5acc475c (infrastructure), d143f3e1 (full integration)

### D1: NPC Goals & Routing Preferences

**New Files:**
- `JoyceCode/engine/tale/NpcGoal.cs`
  ```csharp
  public enum NpcGoal
  {
      Fast,           // Minimize distance/time
      OnTime,         // Arrive by deadline
      Scenic,         // Prefer pleasant routes
      Safe,           // Avoid dangerous areas
  }
  ```

- `JoyceCode/engine/tale/RoutingPreferences.cs`
  ```csharp
  public class RoutingPreferences
  {
      public NpcGoal Goal { get; set; }

      // Goal-specific parameters
      public DateTime? DeadlineTime { get; set; }      // For OnTime goal
      public float SceneryWeight { get; set; } = 0.5f;  // For Scenic goal
      public float SafetyWeight { get; set; } = 0.5f;   // For Safe goal

      public float ComputeCostMultiplier(NavLane lane, TransportationType type)
      {
          return Goal switch
          {
              NpcGoal.Fast => 1.0f,

              NpcGoal.OnTime =>
                  // Prefer faster routes (urgency)
                  EstimateWaitTime(lane) > 60 ? 2.0f : 1.0f,

              NpcGoal.Scenic =>
                  // Prefer lanes with scenic attributes
                  lane.ScenicScore > 0.5f ? 0.7f : 1.0f,

              NpcGoal.Safe =>
                  // Avoid high-traffic areas
                  lane.TrafficDensity > 0.7f ? 1.5f : 1.0f,

              _ => 1.0f
          };
      }
  }
  ```

### D2: Multi-Objective A*

**Modified Files:**
- `JoyceCode/engine/tale/StreetRouteBuilder.cs`
  ```csharp
  public class StreetRouteBuilder
  {
      public async Task<List<NavLane>> FindRouteAsync(
          Vector3 start, Vector3 end,
          TransportationType transportType,
          RoutingPreferences? preferences = null,
          CancellationToken cancellationToken = default)
      {
          var graph = _navMap.GetGraphFor(transportType);

          // Find nearest lanes
          var startLane = graph.AllLanes
              .OrderBy(l => DistanceToLane(start, l))
              .First();
          var endLane = graph.AllLanes
              .OrderBy(l => DistanceToLane(end, l))
              .First();

          // A* with multi-objective cost function
          return await AStarAsync(
              startLane, endLane, graph, transportType,
              preferences, cancellationToken);
      }

      private float ComputeCost(NavLane lane, TransportationType type,
          RoutingPreferences? preferences)
      {
          var baseCost = lane.GetCost(type);

          if (preferences == null)
              return baseCost;

          var multiplier = preferences.ComputeCostMultiplier(lane, type);
          return baseCost * multiplier;
      }
  }
  ```

### D3: NPC Routing Integration

**Modified Files:**
- `JoyceCode/engine/tale/TaleEntityStrategy.cs`
  ```csharp
  public class TaleEntityStrategy
  {
      private NpcSchedule _npcSchedule;
      private StreetRouteBuilder _routeBuilder;

      private RoutingPreferences BuildPreferencesForActivity(StayAtStrategyPart stay)
      {
          // Determine routing preferences based on current activity
          var preferences = new RoutingPreferences
          {
              Goal = _npcSchedule.IsLate ? NpcGoal.OnTime : NpcGoal.Fast,
              DeadlineTime = _npcSchedule.NextEventTime
          };

          return preferences;
      }

      public async Task UpdateRouteAsync(Vector3 destination)
      {
          var preferences = BuildPreferencesForActivity(_currentActivity);

          _route = await _routeBuilder.FindRouteAsync(
              _npcSchedule.CurrentWorldPosition,
              destination,
              TransportationType.Pedestrian,
              preferences);
      }
  }
  ```

### D4: Phase D Success Criteria

- [ ] NpcGoal enum defines routing preferences
- [ ] RoutingPreferences computes cost multipliers per goal
- [ ] A* accepts routing preferences parameter
- [ ] Different NPC types route differently (worker vs. leisure)
- [ ] NPCs route based on schedule pressure (late → OnTime goal)
- [ ] All Phase 7B tests still passing
- [ ] Integration tests: NPCs with different goals find different routes

---

## Cross-Phase Testing Strategy

### Unit Tests (Per Phase)
- Phase A: Transportation types, graph building, A*
- Phase B: Pipes, entity movement, speed functions
- Phase C: Obstructions, queuing, exit/re-entry
- Phase D: Multi-objective routing, preferences

### Integration Tests (After Each Phase)
- Phase A+B: Citizens navigate using pipes
- Phase A+B+C: Traffic lights affect citizen movement
- Phase A+B+C+D: Citizens route based on schedule pressure

### Regression Tests
- **Before each commit:** `./run_tests.sh all` (all Phase 0-7B tests passing)
- **After Phase D:** Full 60-day simulation with new system

### Performance Tests
- Pipe controller complexity: O(p + e) (p pipes, e entities)
- Measure frame time with 100+ entities on pipes
- Verify no performance regression vs. Phase 7B

---

## Timeline & Dependencies

```
Phase A (2-3 days estimated, actual: 1 day)  ✅ 2026-03-24  commit f7fadd49
Phase B (3-4 days estimated, actual: 1 day)  ✅ 2026-03-25  commit 10935509
Phase C (4-5 days estimated, actual: <1 day) ✅ 2026-03-25  commit 380faf94
Phase D (2-3 days estimated, actual: <1 day) ✅ 2026-03-25  commits 5acc475c, d143f3e1

Total: ~12-15 days estimated → 2 days actual
```

---

## Documentation & Cleanup

### After Phase A Complete
- Update `docs/tale/PHASE_7C.md` with transportation graphs section
- Update `CLAUDE.md` with Phase 7C multi-type routing

### After Phase B Complete
- Create `docs/tale/PHASE_8.md` documenting pipe system
- Update `docs/TESTING.md` with new test counts

### After Phase C Complete
- Document temporal constraints integration
- Document obstruction handling
- Create test specifications

### After Phase D Complete
- Document multi-objective routing
- Update `CLAUDE.md` with Phase 8 completion status
- Move plan from proposed/ to done/
- Create final commit with all doc updates

---

## Success Criteria (Final)

- [ ] All Phase 0-7B tests passing (no regressions)
- [ ] Citizens navigate using new pipe system
- [ ] Traffic lights affect movement (temporal constraints)
- [ ] Obstructions create natural queuing (no per-entity collision checks)
- [ ] Entities can exit/re-enter pipes (physics integration ready)
- [ ] Multi-objective routing working (NPCs route based on goals)
- [ ] Performance: O(p + e) complexity verified
- [ ] Full 60-day simulation passes
- [ ] All documentation updated and synchronized
- [ ] Plan moved from proposed/ to done/

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| A* graph building is slow | Cache graphs, profile performance, optimize if needed |
| Pipe transitions at boundaries are complex | Start simple (entities check boundaries each frame), add optimization later |
| Multi-objective routing creates too many code paths | Keep goals simple (4 basic ones), add flexibility later |
| Off-pipe entity tracking becomes complex | Start with simple list, use spatial hash if performance issues arise |
| Temporal constraints need domain-specific types | CyclicConstraint covers most cases, extend with specific types later |

---

## Notes for Future Phases (Out of Scope)

- **Junction Control:** Model junction capacity, turn restrictions, right-of-way
- **Advanced Routing:** Time-dimensional A*, predicted congestion
- **Behavioral Models:** Car-following physics, lane changing strategies
- **Multi-Type Interaction:** Pedestrians crossing car lanes, bikes in traffic
- **Visualization:** Debug renderer for pipes, speed zones, constraints
