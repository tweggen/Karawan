# NavMap: Transportation-Type-Specific Routing Graphs

**Status:** Proposed
**Date:** 2026-03-24
**Related:** Phase 7C (NavMesh pathfinding via IRouteGenerator)

---

## Problem Statement

Currently, NavMap represents only street infrastructure (streetpoints/strokes). To support realistic citizen navigation alongside future infrastructure (pedestrian bridges, multi-level streets, traffic control, etc.), we need to extend the routing system to:

1. **Support multiple transportation types** (pedestrian, car, bicycle, etc.) with different topologies
2. **Handle physically separated infrastructure** (wall-separated main streets with pedestrian bridges)
3. **Handle merged infrastructure** (secondary streets where multiple types coexist)
4. **Scale to complex geometries** (underground tunnels, rooftops, multi-level streets, ferries) without graph complexity

Current bitmask approach treats all types as navigating the same graph topology, which doesn't reflect reality (e.g., a pedestrian can't cross a wall-separated main street without the bridge).

---

## Solution: Transportation-Type-Specific Routing Graphs

Build **separate routing graphs per transportation type**, where each graph represents the topology visible to that type.

### Key Concepts

**NavLane** (refined):
- Represents a street segment or dedicated infrastructure (bridge, escalator, tunnel, etc.)
- Contains basic properties: `From` (Vector3), `To` (Vector3), `Length`, `Cost`
- New properties:
  - `TransportationTypeFlags AllowedTypes` — which types can use this lane
  - `float GetCost(TransportationType type)` — cost varies by type (cars faster on highways, pedestrians faster on quiet streets)
  - Metadata: `HasPedestrianBridge`, `HasElevator`, `IsOneWay`, `TrafficLightId`, etc.

**Routing Graphs** (new):
- Built per transportation type: `CarGraph`, `PedestrianGraph`, `BicycleGraph`, etc.
- Graph builder selects appropriate NavLanes based on `AllowedTypes` and type-specific filters
- Each graph is an input to A* pathfinding for that type
- A* sees only the topology relevant to that transportation type

**Graph Examples:**

```
Main Street (wall-separated, central area):
  NavLane: car_lanes_main_east        [AllowedTypes: Car]
  NavLane: sidewalk_main_east         [AllowedTypes: Pedestrian | Bicycle]
  NavLane: pedestrian_bridge_main     [AllowedTypes: Pedestrian | Bicycle]

CarGraph includes:
  - car_lanes_main_east
  - (NOT sidewalk, NOT bridge — car can't cross)

PedestrianGraph includes:
  - sidewalk_main_east
  - pedestrian_bridge_main
  - (NOT car_lanes — pedestrian can't cross without bridge)


Secondary Street (merged):
  NavLane: street_secondary_east      [AllowedTypes: Car | Pedestrian | Bicycle]

CarGraph includes:
  - street_secondary_east with Car cost

PedestrianGraph includes:
  - street_secondary_east with Pedestrian cost
  - Both use same NavLane, different costs
```

### Multi-Level Example (Escalator)

```
Ground Level:
  NavLane: street_A_ground            [AllowedTypes: Car | Pedestrian]

Elevated Level:
  NavLane: street_B_elevated          [AllowedTypes: Car | Pedestrian]

Connections:
  NavLane: escalator_ab               [AllowedTypes: Pedestrian | Bicycle]
  NavLane: elevator_ab                [AllowedTypes: Car | Pedestrian | Bicycle]

PedestrianGraph:
  - street_A_ground
  - escalator_ab (cost = escalator_time)
  - street_B_elevated
  - (seamlessly connects ground to elevated)

CarGraph:
  - street_A_ground
  - elevator_ab (cost = elevator_time)
  - street_B_elevated
```

Graph sees this as trivial: just three edges connecting them. Geometry/rendering handles the 3D complexity separately.

---

## Implementation Approach

### Phase 1: NavLane Refinement (Minimal Changes)

1. **Extend `NavLane` class** (`JoyceCode/engine/navigation/NavLane.cs`):
   - Add `TransportationTypeFlags AllowedTypes { get; set; }`
   - Add `float GetCost(TransportationType type)` method
   - Keep everything else as-is

2. **Define `TransportationType` enum** (`JoyceCode/engine/navigation/TransportationType.cs`):
   ```csharp
   [Flags]
   public enum TransportationType
   {
       Pedestrian = 1,
       Car = 2,
       Bicycle = 4,
       // Future: Motorcycle, Bus, etc.
   }
   ```

3. **Define `TransportationTypeFlags` type alias**:
   ```csharp
   public class TransportationTypeFlags
   {
       public TransportationType Value { get; set; }
       public bool HasFlag(TransportationType type) => ...
   }
   ```

### Phase 2: Routing Graph Builder (New)

1. **Create `RoutingGraphBuilder` class** (`JoyceCode/engine/navigation/RoutingGraphBuilder.cs`):
   - Method: `RoutingGraph BuildFor(TransportationType type, IEnumerable<NavLane> allLanes)`
   - Filters NavLanes by `AllowedTypes.HasFlag(type)`
   - Returns a graph (adjacency list or similar) suitable for A*
   - Supports type-specific filtering rules (e.g., "cars ignore pedestrian bridges")

2. **Integrate into NavMap** (`JoyceCode/engine/navigation/NavMap.cs`):
   - Add `RoutingGraphBuilder _builder`
   - Add method: `RoutingGraph GetGraphFor(TransportationType type)`
   - Caches graphs per type (built once, reused)
   - On NavLane additions/changes, invalidate cache

### Phase 3: Update StreetRouteBuilder (Existing)

1. **Refactor `StreetRouteBuilder`** (`JoyceCode/engine/tale/StreetRouteBuilder.cs`):
   - Accept `TransportationType type` parameter in route-finding methods
   - Use `navMap.GetGraphFor(type)` instead of raw NavLane collection
   - A* now operates on type-specific graph
   - Fallback behavior unchanged (straight line if routing fails)

### Phase 4: NPC Integration

1. **Update `TaleEntityStrategy`** (`JoyceCode/engine/tale/TaleEntityStrategy.cs`):
   - Determine NPC's transportation type (pedestrian by default, car if driving quest, etc.)
   - Pass type to `StreetRouteBuilder` methods
   - Citizens navigate as pedestrians (existing behavior)
   - Future: cars, delivery vehicles, etc. use their own graphs

---

## Testing Strategy

### Regression Testing
- All Phase 7/7B tests continue to pass (no changes to existing behavior)
- Citizens still navigate streets correctly
- Travel times unchanged

### New Test Coverage (Phase 9 candidate)

1. **Graph Construction Tests**:
   - Verify `RoutingGraphBuilder` correctly filters NavLanes by type
   - Test that pedestrian graph includes bridges, excludes car-only lanes
   - Test that car graph excludes pedestrian-only infrastructure
   - Test merged lanes appear in both graphs

2. **Route Finding Tests**:
   - Pedestrian finds path via bridge (car-only lane blocked)
   - Car finds path via main street (bridge not visible)
   - Secondary street: both types find valid paths (shared lane)
   - Multi-level: paths via escalator/elevator constructed correctly

3. **Cost Variation Tests**:
   - Same NavLane has different costs for different types
   - A* chooses appropriate routes (car avoids slow streets, pedestrian avoids highways)

4. **Integration Tests**:
   - Multiple NPCs navigate simultaneously, each on their type's graph
   - No interference between transportation types

### Simulation Tests
- Run 60-day simulations with existing citizens (pedestrian type)
- Verify no regressions in behavior, metrics, or test suite

---

## Files Modified/Created

### New Files
- `JoyceCode/engine/navigation/TransportationType.cs` — enum + flags
- `JoyceCode/engine/navigation/RoutingGraphBuilder.cs` — graph builder

### Modified Files
- `JoyceCode/engine/navigation/NavLane.cs` — add AllowedTypes, GetCost()
- `JoyceCode/engine/navigation/NavMap.cs` — integrate RoutingGraphBuilder, caching
- `JoyceCode/engine/tale/StreetRouteBuilder.cs` — accept TransportationType, use type-specific graphs
- `JoyceCode/engine/tale/TaleEntityStrategy.cs` — pass TransportationType to routing
- `JoyceCode.projitems` — register new files

### Documentation Updates
- `CLAUDE.md` — update Phase 7C description, note transportation types
- `docs/tale/PHASE_7C.md` — document transportation-type routing design
- `docs/TESTING.md` — add Phase 9 test coverage notes
- `PROCESS.md` — no changes expected

---

## Success Criteria

- [ ] NavLane extended with `AllowedTypes` and `GetCost(type)` without breaking existing code
- [ ] RoutingGraphBuilder correctly filters NavLanes by transportation type
- [ ] Existing citizens (pedestrian type) navigate identically to Phase 7B
- [ ] New test suite covers graph construction, filtering, and route-finding for multiple types
- [ ] Multi-level infrastructure (escalator example) produces correct routing graphs trivially
- [ ] All 171+ regression tests passing
- [ ] Design documented in PHASE_7C.md with examples

---

## Future Extensions (Out of Scope)

This design naturally enables:
- **Underground tunnels** — NavLanes with pedestrian/car access, different costs
- **Rooftop networks** — aerial NavLanes for specific NPC types (thieves, acrobats)
- **Water routes** — ferry NavLanes connecting non-adjacent areas
- **Traffic control** — NavLane properties (one-way, traffic lights) factor into routing costs
- **NPC type-specific routing** — guards patrol rooftops (elevated NavLanes), criminals use sewers, etc.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Graph caching invalidation on NavLane changes | Implement proper cache invalidation in NavMap; validate in tests |
| Performance (multiple graphs stored in memory) | Graphs are lightweight (just adjacency lists); lazy-build if needed |
| Complexity in cost computation | Start simple (fixed costs per type); extend later if needed |
| Breaking changes to existing IRouteGenerator | Keep interface unchanged; type parameter optional (defaults to Pedestrian) |

---

## Definition of Done

- [ ] Code compiles without errors (Release mode)
- [ ] All 171+ regression tests passing
- [ ] New tests for graph construction and routing created and passing
- [ ] Documentation updated (CLAUDE.md, PHASE_7C.md, TESTING.md)
- [ ] Commit message clear, references doc updates
- [ ] Plan file moved from proposed/ to done/
