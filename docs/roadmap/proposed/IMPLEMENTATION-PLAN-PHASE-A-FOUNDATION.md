# Phase A Implementation Plan: Foundation — Transportation Types & Routing Graphs

**Phase:** A (Foundation)
**Duration:** ~2-3 days
**Status:** Ready for execution
**Depends On:** None (extends Phase 7C)
**Enables:** Phase B, Phase D

---

## Overview

Phase A establishes the foundation for multi-type navigation by extending NavLane with transportation type support and building type-specific routing graphs.

**Objectives:**
1. Define TransportationType system (enum + flags)
2. Extend NavLane with AllowedTypes and cost functions
3. Implement RoutingGraphBuilder for type-specific graph construction
4. Integrate with existing A* (StreetRouteBuilder)
5. Verify citizens still navigate correctly (Pedestrian type)

**Success Criteria:**
- TransportationType system complete and tested
- Routing graphs correctly filter by type
- A* uses type-specific graphs
- No regressions in Phase 7B tests
- Citizens still reach destinations

---

## Task A1: TransportationType System

### Files to Create

**`JoyceCode/engine/navigation/TransportationType.cs`**

```csharp
using System;

namespace JoyceCode.engine.navigation
{
    /// <summary>
    /// Enumeration of transportation types.
    /// Used as flags to indicate which types can use a lane.
    /// </summary>
    [Flags]
    public enum TransportationType
    {
        Pedestrian = 1,
        Car = 2,
        Bicycle = 4,
        Bus = 8,
        // Future: Motorcycle = 16, Truck = 32, etc.
    }

    /// <summary>
    /// Wrapper for TransportationType flags with convenience methods.
    /// </summary>
    public class TransportationTypeFlags
    {
        public TransportationType Value { get; set; }

        public TransportationTypeFlags()
        {
            Value = TransportationType.Pedestrian;
        }

        public TransportationTypeFlags(TransportationType initialValue)
        {
            Value = initialValue;
        }

        /// <summary>
        /// Check if this type is allowed.
        /// </summary>
        public bool HasFlag(TransportationType type)
        {
            return (Value & type) != 0;
        }

        /// <summary>
        /// Add a type to the flags.
        /// </summary>
        public void Add(TransportationType type)
        {
            Value |= type;
        }

        /// <summary>
        /// Remove a type from the flags.
        /// </summary>
        public void Remove(TransportationType type)
        {
            Value &= ~type;
        }

        /// <summary>
        /// Clear all flags.
        /// </summary>
        public void Clear()
        {
            Value = 0;
        }

        public override string ToString() => Value.ToString();
    }
}
```

### Files to Modify

**`JoyceCode/engine/navigation/NavLane.cs`**

Add to NavLane class:

```csharp
/// <summary>
/// Which transportation types can use this lane.
/// </summary>
public TransportationTypeFlags AllowedTypes { get; set; } =
    new TransportationTypeFlags(TransportationType.Pedestrian);

/// <summary>
/// Get the movement cost for this lane for a specific transportation type.
/// Cost = Distance / Speed (in seconds).
/// </summary>
public float GetCost(TransportationType type)
{
    if (!AllowedTypes.HasFlag(type))
        return float.MaxValue;  // Type not allowed on this lane

    // Get base speed for this type (m/s)
    var baseSpeed = type switch
    {
        TransportationType.Pedestrian => 1.5f,   // ~3.4 mph
        TransportationType.Car => 13.4f,         // ~30 mph
        TransportationType.Bicycle => 5.0f,      // ~11 mph
        TransportationType.Bus => 11.0f,         // ~25 mph
        _ => 1.5f
    };

    var distance = Vector3.Distance(From, To);
    return distance / baseSpeed;  // Time to traverse
}
```

### Tests to Write

**`tests/JoyceCode.Tests/engine/navigation/TransportationTypeTests.cs`**

```csharp
using Xunit;
using JoyceCode.engine.navigation;

namespace JoyceCode.Tests.engine.navigation
{
    public class TransportationTypeTests
    {
        [Fact]
        public void TransportationTypeFlags_HasFlag_ReturnsTrueForAddedType()
        {
            var flags = new TransportationTypeFlags();
            flags.Add(TransportationType.Car);

            Assert.True(flags.HasFlag(TransportationType.Car));
        }

        [Fact]
        public void TransportationTypeFlags_HasFlag_ReturnsFalseForMissingType()
        {
            var flags = new TransportationTypeFlags();

            Assert.False(flags.HasFlag(TransportationType.Car));
        }

        [Fact]
        public void TransportationTypeFlags_Remove_RemovesType()
        {
            var flags = new TransportationTypeFlags(
                TransportationType.Pedestrian | TransportationType.Car);

            flags.Remove(TransportationType.Car);

            Assert.True(flags.HasFlag(TransportationType.Pedestrian));
            Assert.False(flags.HasFlag(TransportationType.Car));
        }

        [Fact]
        public void NavLane_GetCost_ReturnsDifferentCostsForDifferentTypes()
        {
            var lane = new NavLane
            {
                From = Vector3.Zero,
                To = new Vector3(100, 0, 0),
                AllowedTypes = new TransportationTypeFlags(
                    TransportationType.Pedestrian | TransportationType.Car)
            };

            var pedestrianCost = lane.GetCost(TransportationType.Pedestrian);
            var carCost = lane.GetCost(TransportationType.Car);

            Assert.NotEqual(pedestrianCost, carCost);
            Assert.True(carCost < pedestrianCost);  // Cars faster
        }

        [Fact]
        public void NavLane_GetCost_ReturnsMaxValueForDisallowedType()
        {
            var lane = new NavLane
            {
                From = Vector3.Zero,
                To = new Vector3(100, 0, 0),
                AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
            };

            var pedestrianCost = lane.GetCost(TransportationType.Pedestrian);

            Assert.Equal(float.MaxValue, pedestrianCost);
        }
    }
}
```

### Checklist

- [ ] Create TransportationType.cs
- [ ] Create TransportationTypeFlags class
- [ ] Add AllowedTypes to NavLane
- [ ] Add GetCost() method to NavLane
- [ ] Write and pass TransportationTypeTests
- [ ] Compile JoyceCode project without errors
- [ ] Document in CLAUDE.md (Phase 7C extension)

---

## Task A2: RoutingGraphBuilder

### Files to Create

**`JoyceCode/engine/navigation/RoutingGraph.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace JoyceCode.engine.navigation
{
    /// <summary>
    /// A directed graph of navigation lanes for a specific transportation type.
    /// </summary>
    public class RoutingGraph
    {
        /// <summary>
        /// Adjacency list: for each lane, which lanes can be reached from it.
        /// </summary>
        public Dictionary<NavLane, List<NavLane>> Edges { get; set; } = new();

        /// <summary>
        /// All lanes accessible for this transportation type.
        /// </summary>
        public List<NavLane> AllLanes { get; set; } = new();

        /// <summary>
        /// The transportation type this graph supports.
        /// </summary>
        public TransportationType SupportedType { get; set; }

        /// <summary>
        /// Find all lanes reachable from a starting lane.
        /// </summary>
        public List<NavLane> GetNeighbors(NavLane lane)
        {
            if (Edges.TryGetValue(lane, out var neighbors))
                return neighbors;
            return new List<NavLane>();
        }

        /// <summary>
        /// Check if this graph contains the lane.
        /// </summary>
        public bool ContainsLane(NavLane lane) => AllLanes.Contains(lane);
    }
}
```

**`JoyceCode/engine/navigation/RoutingGraphBuilder.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs;

namespace JoyceCode.engine.navigation
{
    /// <summary>
    /// Builds type-specific routing graphs from a collection of NavLanes.
    /// </summary>
    public class RoutingGraphBuilder
    {
        private NavMap _navMap;
        private Dictionary<TransportationType, RoutingGraph> _graphCache;

        public RoutingGraphBuilder(NavMap navMap)
        {
            _navMap = navMap;
            _graphCache = new Dictionary<TransportationType, RoutingGraph>();
        }

        /// <summary>
        /// Build (or retrieve cached) routing graph for a transportation type.
        /// </summary>
        public RoutingGraph BuildFor(TransportationType type)
        {
            // Return cached graph if available
            if (_graphCache.TryGetValue(type, out var cached))
                return cached;

            var graph = new RoutingGraph { SupportedType = type };

            // Step 1: Filter lanes by type
            graph.AllLanes = _navMap.AllLanes
                .Where(lane => lane.AllowedTypes.HasFlag(type))
                .ToList();

            // Step 2: Build adjacency (connectivity)
            graph.Edges = new Dictionary<NavLane, List<NavLane>>();
            foreach (var lane in graph.AllLanes)
            {
                var outgoing = new List<NavLane>();

                foreach (var other in graph.AllLanes)
                {
                    // Lane A connects to Lane B if A's endpoint is near B's start
                    if (AreConnected(lane, other))
                        outgoing.Add(other);
                }

                graph.Edges[lane] = outgoing;
            }

            // Cache and return
            _graphCache[type] = graph;
            return graph;
        }

        /// <summary>
        /// Check if two lanes are connected (A.To ≈ B.From).
        /// </summary>
        private bool AreConnected(NavLane from, NavLane to, float threshold = 0.1f)
        {
            return Vector3.Distance(from.To, to.From) < threshold;
        }

        /// <summary>
        /// Invalidate cache when lanes change.
        /// </summary>
        public void InvalidateCache()
        {
            _graphCache.Clear();
        }

        /// <summary>
        /// Get cache statistics (for debugging).
        /// </summary>
        public Dictionary<TransportationType, int> GetCacheStats()
        {
            return _graphCache
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AllLanes.Count);
        }
    }
}
```

### Files to Modify

**`JoyceCode/engine/navigation/NavMap.cs`**

Add to NavMap class:

```csharp
private RoutingGraphBuilder _graphBuilder;

// In constructor or initialization:
public NavMap(IEnumerable<NavLane> lanes)
{
    AllLanes = lanes.ToList();
    _graphBuilder = new RoutingGraphBuilder(this);
}

/// <summary>
/// Get the routing graph for a specific transportation type.
/// </summary>
public RoutingGraph GetGraphFor(TransportationType type)
{
    return _graphBuilder.BuildFor(type);
}

/// <summary>
/// Invalidate routing graph cache (call when lanes change).
/// </summary>
public void InvalidateGraphCache()
{
    _graphBuilder.InvalidateCache();
}
```

### Tests to Write

**`tests/JoyceCode.Tests/engine/navigation/RoutingGraphBuilderTests.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using Xunit;
using JoyceCode.engine.navigation;

namespace JoyceCode.Tests.engine.navigation
{
    public class RoutingGraphBuilderTests
    {
        [Fact]
        public void BuildFor_FiltersLanesByType()
        {
            // Create test lanes
            var lane1 = new NavLane
            {
                From = Vector3.Zero,
                To = new Vector3(10, 0, 0),
                AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
            };

            var lane2 = new NavLane
            {
                From = new Vector3(10, 0, 0),
                To = new Vector3(20, 0, 0),
                AllowedTypes = new TransportationTypeFlags(
                    TransportationType.Car | TransportationType.Pedestrian)
            };

            var lane3 = new NavLane
            {
                From = new Vector3(20, 0, 0),
                To = new Vector3(30, 0, 0),
                AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
            };

            var navMap = new NavMap(new[] { lane1, lane2, lane3 });
            var builder = new RoutingGraphBuilder(navMap);

            // Build pedestrian graph
            var pedGraph = builder.BuildFor(TransportationType.Pedestrian);

            // Should include lane1 and lane2 (both allow pedestrians)
            Assert.Contains(lane1, pedGraph.AllLanes);
            Assert.Contains(lane2, pedGraph.AllLanes);
            Assert.DoesNotContain(lane3, pedGraph.AllLanes);  // Car only

            // Build car graph
            var carGraph = builder.BuildFor(TransportationType.Car);

            // Should include lane2 and lane3 (both allow cars)
            Assert.DoesNotContain(lane1, carGraph.AllLanes);  // Pedestrian only
            Assert.Contains(lane2, carGraph.AllLanes);
            Assert.Contains(lane3, carGraph.AllLanes);
        }

        [Fact]
        public void BuildFor_ConstructsEdgesCorrectly()
        {
            // Create connected lanes
            var lane1 = new NavLane
            {
                From = Vector3.Zero,
                To = new Vector3(10, 0, 0),
                AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
            };

            var lane2 = new NavLane
            {
                From = new Vector3(10, 0, 0),
                To = new Vector3(20, 0, 0),
                AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
            };

            var navMap = new NavMap(new[] { lane1, lane2 });
            var builder = new RoutingGraphBuilder(navMap);

            var graph = builder.BuildFor(TransportationType.Car);

            // lane1 should have lane2 as an outgoing edge
            var neighbors = graph.GetNeighbors(lane1);
            Assert.Contains(lane2, neighbors);
        }

        [Fact]
        public void BuildFor_CachesGraphs()
        {
            var lanes = new[]
            {
                new NavLane
                {
                    From = Vector3.Zero,
                    To = new Vector3(10, 0, 0),
                    AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
                }
            };

            var navMap = new NavMap(lanes);
            var builder = new RoutingGraphBuilder(navMap);

            // Build twice
            var graph1 = builder.BuildFor(TransportationType.Car);
            var graph2 = builder.BuildFor(TransportationType.Car);

            // Should return same instance
            Assert.Same(graph1, graph2);
        }

        [Fact]
        public void InvalidateCache_ClearsCachedGraphs()
        {
            var lanes = new[]
            {
                new NavLane
                {
                    From = Vector3.Zero,
                    To = new Vector3(10, 0, 0),
                    AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
                }
            };

            var navMap = new NavMap(lanes);
            var builder = new RoutingGraphBuilder(navMap);

            var graph1 = builder.BuildFor(TransportationType.Car);
            builder.InvalidateCache();
            var graph2 = builder.BuildFor(TransportationType.Car);

            // Should be different instances
            Assert.NotSame(graph1, graph2);
        }
    }
}
```

### Checklist

- [ ] Create RoutingGraph.cs
- [ ] Create RoutingGraphBuilder.cs
- [ ] Add _graphBuilder to NavMap
- [ ] Add GetGraphFor() to NavMap
- [ ] Add InvalidateGraphCache() to NavMap
- [ ] Write and pass RoutingGraphBuilderTests
- [ ] Verify graph construction for 2+ transportation types
- [ ] Compile JoyceCode project without errors

---

## Task A3: A* Integration

### Files to Modify

**`JoyceCode/engine/tale/StreetRouteBuilder.cs`**

Update FindRouteAsync to use type-specific graphs:

```csharp
/// <summary>
/// Find a route for a specific transportation type.
/// </summary>
public async Task<List<NavLane>> FindRouteAsync(
    Vector3 start,
    Vector3 end,
    TransportationType transportType = TransportationType.Pedestrian,
    CancellationToken cancellationToken = default)
{
    var graph = _navMap.GetGraphFor(transportType);

    if (graph.AllLanes.Count == 0)
    {
        // No valid lanes for this type, fallback to straight line
        return new List<NavLane>();
    }

    // Find nearest lane to start
    var startLane = graph.AllLanes
        .OrderBy(lane => DistancePointToLane(start, lane))
        .FirstOrDefault();

    if (startLane == null)
        return new List<NavLane>();

    // Find nearest lane to end
    var endLane = graph.AllLanes
        .OrderBy(lane => DistancePointToLane(end, lane))
        .FirstOrDefault();

    if (endLane == null)
        return new List<NavLane>();

    // A* pathfinding
    return await AStarAsync(
        startLane,
        endLane,
        graph,
        transportType,
        cancellationToken);
}

private float ComputeCost(NavLane lane, TransportationType type)
{
    return lane.GetCost(type);
}

private async Task<List<NavLane>> AStarAsync(
    NavLane start,
    NavLane goal,
    RoutingGraph graph,
    TransportationType transportType,
    CancellationToken cancellationToken)
{
    // Existing A* implementation, but use graph.GetNeighbors(lane)
    // instead of raw lane collection
    // ... (existing A* code, adapted to use RoutingGraph)

    return path;
}
```

### Tests to Write

**Integration test in existing test suite:**

Update existing StreetRouteBuilder tests to verify:

```csharp
[Fact]
public async Task FindRouteAsync_PedestrianType_AvoidsCarsOnlyLanes()
{
    // Create pedestrian-only lane and car-only lane
    var pedLane = new NavLane { /* ... */ AllowedTypes = Pedestrian };
    var carLane = new NavLane { /* ... */ AllowedTypes = Car };

    var navMap = new NavMap(new[] { pedLane, carLane });
    var builder = new StreetRouteBuilder(navMap);

    var route = await builder.FindRouteAsync(
        start, end, TransportationType.Pedestrian);

    Assert.Contains(pedLane, route);
    Assert.DoesNotContain(carLane, route);
}

[Fact]
public async Task FindRouteAsync_CarType_AvoidsPedestrianOnlyLanes()
{
    // Similar test with roles reversed
}
```

### Checklist

- [ ] Update FindRouteAsync to accept TransportationType parameter
- [ ] Use GetGraphFor() to get type-specific graph
- [ ] Verify A* uses graph.GetNeighbors() for connectivity
- [ ] Write integration tests for different types
- [ ] Test that pedestrians avoid car-only lanes
- [ ] Test that cars avoid pedestrian-only lanes
- [ ] Compile and run all tests

---

## Task A4: Citizens Integration & Testing

### Files to Modify

**`JoyceCode/engine/tale/TaleEntityStrategy.cs`**

Update to use TransportationType.Pedestrian as default:

```csharp
private TransportationType _transportType = TransportationType.Pedestrian;

private async Task UpdateRouteAsync(Vector3 destination)
{
    _route = await _routeBuilder.FindRouteAsync(
        _npcSchedule.CurrentWorldPosition,
        destination,
        _transportType);  // Pass type

    _routeIndex = 0;
}
```

### Regression Tests

**Run existing test suite:**

```bash
./run_tests.sh phase7
./run_tests.sh phase7b
./run_tests.sh all
```

**Verify:**
- All Phase 7B tests still pass
- Citizens still reach destinations
- No performance regressions
- No crashes or errors

### Checklist

- [ ] Citizens default to Pedestrian type
- [ ] Existing routes still work
- [ ] No changes to citizen behavior (same movement)
- [ ] All Phase 7B regression tests passing
- [ ] Full test suite `./run_tests.sh all` passing

---

## Completion Checklist

- [ ] TransportationType system complete
- [ ] NavLane extended with AllowedTypes and GetCost()
- [ ] RoutingGraphBuilder implemented and cached
- [ ] NavMap integrated with RoutingGraphBuilder
- [ ] StreetRouteBuilder uses type-specific graphs
- [ ] All new unit tests passing
- [ ] Integration tests passing
- [ ] All Phase 7B regression tests passing
- [ ] Citizens still navigate correctly
- [ ] Code compiles without errors (Release mode)
- [ ] CLAUDE.md updated with Phase 7C extension notes
- [ ] Ready for Phase B

---

## Notes for Implementation

1. **Start Simple:** Keep cost functions basic (distance / speed). Optimize later if needed.
2. **Test Incrementally:** Write tests as you code, not after.
3. **Verify Continuously:** Run regression tests after each major change.
4. **Cache Graphs:** Caching significantly improves performance (graphs don't change often).
5. **Default Type:** Citizens use Pedestrian type; extend to Car/other types in Phase D.
6. **Error Handling:** If graph has no valid path, fallback to straight line (existing behavior).

---

## Success Definition

Phase A is complete when:
✅ Citizens navigate using the new transportation graph system
✅ Multiple transportation types have separate, correct graphs
✅ A* respects type-specific lane restrictions
✅ No regressions in existing tests
✅ Code is clean, well-tested, documented
