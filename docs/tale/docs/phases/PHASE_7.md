# Phase 7: Spatial Grounding & Navigation

Phase 7 bridges the TALE simulation's logical locations (from `SpatialModel`) with precise world coordinates and NPC visibility. NPCs now materialize at exact entry points, occupy buildings indoors (invisible to players), and compute street routes dynamically.

**Status**: ✅ **IMPLEMENTATION COMPLETE** (2026-03-16)

**Prerequisites**: Phase 6 (seed-based population, cluster lifecycle), spatial geometry (buildings, shops, street segments).

---

## Design Principles

1. **Location entry points**: NPCs spawn at door/shop-front positions, not building centroids, preventing clipping into walls.
2. **Per-cluster spatial models**: Each cluster instance has its own `SpatialModel` extracted from `ClusterDesc` geometry, supporting dynamic world generation.
3. **Indoor invisibility**: NPCs staying at indoor locations (shops, offices, homes) skip behavior setup and fade from player view.
4. **Fragment-accurate position**: `NpcSchedule.CurrentWorldPosition` is updated on each timestep, enabling proper fragment-based spawning.
5. **Pathfinding infrastructure**: `PrecomputedRoute` on movement phases supports future A* street navigation; currently uses straight-line fallback.

---

## Phase 7A — SpatialModel Wire-up

**Goal**: Change from a single global `SpatialModel` to per-cluster models, created when clusters populate and destroyed when they depopulate.

### Changes to TaleManager

```csharp
// OLD: single global model
private SpatialModel _spatialModel;

// NEW: per-cluster dictionary
private Dictionary<int, SpatialModel> _spatialModels = new();

// Updated Initialize signature
public void Initialize(StoryletLibrary library, int seed = 42)
    // No longer takes SpatialModel parameter

// Updated PopulateCluster
public void PopulateCluster(ClusterDesc clusterDesc, SpatialModel spatialModel)
{
    int clusterIndex = /* computed from clusterDesc */;
    _spatialModels[clusterIndex] = spatialModel;
    var schedules = _generator.Generate(clusterDesc, spatialModel, skipMask);
    // ... register schedules
}

// Updated DepopulateCluster
public void DepopulateCluster(int clusterIndex)
{
    _spatialModels.Remove(clusterIndex);
    // ... unregister schedules
}

// New accessor
public SpatialModel GetSpatialModel(int clusterIndex) =>
    _spatialModels.TryGetValue(clusterIndex, out var model) ? model : null;

// Updated AdvanceNpc
public StoryletDefinition AdvanceNpc(int npcId, DateTime gameTime)
{
    var schedule = GetSchedule(npcId);
    var spatialModel = GetSpatialModel(schedule.ClusterIndex);
    // ... use spatialModel.GetLocation(schedule.CurrentLocationId)
}
```

### Changes to TaleModule

In `nogameCode/nogame/modules/tale/TaleModule.cs`, when a cluster completes:

```csharp
private void _onClusterCompleted(ClusterCompletedEvent ev)
{
    var spatialModel = SpatialModel.ExtractFrom(ev.Desc);
    _taleManager.PopulateCluster(ev.Desc, spatialModel);
    Trace($"TALE: Populated cluster with spatial: {spatialModel.BuildingCount} buildings, {spatialModel.ShopCount} shops.");
}
```

And update the initialization call:
```csharp
// OLD: _taleManager.Initialize(_storyletLibrary, null);
// NEW:
_taleManager.Initialize(_storyletLibrary, seed);
```

### Changes to TalePopulationGenerator

Updated `Generate()` signature:
```csharp
public List<NpcSchedule> Generate(ClusterDesc clusterDesc, SpatialModel spatialModel, HashSet<int> skipIndices = null)
```

Use the `spatialModel` in `GenerateNpc()` to assign location IDs:
```csharp
private void GenerateNpc(int npcIndex, RandomSource rnd, SpatialModel spatialModel)
{
    var schedule = new NpcSchedule { /* ... */ };

    // Assign home location by role
    schedule.HomeLocationId = AssignLocationByRole(rnd, spatialModel, role, "residential");

    // Assign workplace by role
    schedule.WorkplaceLocationId = AssignLocationByRole(rnd, spatialModel, role, "workplace");

    // ... etc
}

private int AssignLocationByRole(RandomSource rnd, SpatialModel spatialModel, string role, string preferredType)
{
    // Filter locations by role affinity (merchants → shops, workers → offices/streets, etc.)
    var candidates = spatialModel.GetLocations()
        .Where(loc => IsRoleValidForLocation(role, loc))
        .ToList();

    return candidates.Count > 0 ? candidates[rnd.NextInt(candidates.Count)].Id : -1;
}
```

---

## Phase 7B — Entry Points

**Goal**: Compute door/entry positions for each location so NPCs materialize at realistic spawn points instead of building centroids.

### Changes to SpatialModel.Location

Add to the `Location` class:
```csharp
public Vector3 EntryPosition;  // Door/shop-front position at street level
```

### SpatialModel.ExtractFrom() Updates

When extracting locations from cluster geometry:

```csharp
public static SpatialModel ExtractFrom(ClusterDesc cluster)
{
    var model = new SpatialModel();
    float streetHeight = cluster.AverageHeight + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset;

    // Extract shops
    foreach (var shopFront in cluster.ShopFronts)
    {
        var loc = new Location
        {
            Type = "shop",
            Position = shopFront.Center,
            EntryPosition = ComputeShopEntryPosition(shopFront, streetHeight),
            // ... other fields
        };
        model.AddLocation(loc);
    }

    // Extract buildings (offices, homes, etc.)
    foreach (var building in cluster.Buildings)
    {
        var loc = new Location
        {
            Type = "office", // or inferred from building metadata in Phase 7b
            Position = building.Center,
            EntryPosition = new Vector3(building.Center.X, streetHeight, building.Center.Z),
            // ... other fields
        };
        model.AddLocation(loc);
    }

    // Extract street segments
    foreach (var segment in cluster.Streets)
    {
        var loc = new Location
        {
            Type = "street_segment",
            Position = segment.Center,
            EntryPosition = segment.Center, // Street NPCs can appear anywhere on the street
            // ...
        };
        model.AddLocation(loc);
    }

    return model;
}

private static Vector3 ComputeShopEntryPosition(ShopFront shopFront, float streetHeight)
{
    // Use midpoint of first two corner points as door position
    if (shopFront.Points.Count >= 2)
    {
        var p0 = shopFront.Points[0];
        var p1 = shopFront.Points[1];
        return new Vector3(
            (p0.X + p1.X) / 2f,
            streetHeight,
            (p0.Z + p1.Z) / 2f
        );
    }
    return new Vector3(shopFront.Center.X, streetHeight, shopFront.Center.Z);
}
```

### NpcSchedule.PositionAt() Update

When querying an NPC's current position, prefer the entry position:

```csharp
public Vector3 PositionAt(DateTime gameTime, SpatialModel model)
{
    if (model != null)
    {
        var loc = model.GetLocation(CurrentLocationId);
        if (loc != null)
        {
            // Prefer EntryPosition if set (not zero vector)
            if (loc.EntryPosition != Vector3.Zero)
                return loc.EntryPosition;
            return loc.Position;
        }
    }
    return HomePosition;
}
```

---

## Phase 7C — Street Pathfinding

**Goal**: Compute routes along street segments when NPCs travel between distant locations.

### StreetRouteBuilder Class

New file: `nogameCode/nogame/characters/citizen/StreetRouteBuilder.cs`

```csharp
public static class StreetRouteBuilder
{
    /// <summary>
    /// Build a street route from start to end position using A* pathfinding.
    /// Falls back to null (straight line) on any error.
    /// </summary>
    public static async Task<SegmentRoute> BuildAsync(Vector3 startPos, Vector3 endPos, NavMap navMap)
    {
        if (navMap?.TopCluster == null)
            return null;

        // Create cursors for start and end positions
        var startCursor = navMap.TopCluster.TryCreateCursor(startPos);
        var endCursor = navMap.TopCluster.TryCreateCursor(endPos);

        if (startCursor == null || endCursor == null)
            return null;

        try
        {
            // A* pathfinding over street network
            var pathfinder = new LocalPathfinder(startCursor, endCursor);
            var path = pathfinder.FindPath();

            if (path == null || path.Count == 0)
                return null;

            // Convert path waypoints to SegmentRoute
            var route = new SegmentRoute();
            foreach (var waypoint in path)
            {
                // Build SegmentEnd with geometry from waypoint
                var end = new SegmentEnd
                {
                    PositionDescription = new PositionDescription { /* from waypoint */ },
                    // ... other fields
                };
                route.Segments.Add(end);
            }

            return route;
        }
        catch
        {
            return null;  // Fallback to straight-line movement
        }
    }
}
```

### GoToStrategyPart Update

Add `PrecomputedRoute` property:

```csharp
public class GoToStrategyPart : AStrategyPart
{
    public SegmentRoute PrecomputedRoute { get; set; }

    public override void OnEnter()
    {
        // Use precomputed route if available
        SegmentRoute route = PrecomputedRoute ?? BuildStraightLineRoute(startPos, endPos, /* ... */);

        _navigator = new SegmentNavigator(route);
        // ... continue movement
    }
}
```

### TaleEntityStrategy Update

In `_advanceAndTravel()`, set up the route before entering GoToStrategyPart:

```csharp
private void _advanceAndTravel()
{
    // ... get destination location

    var spatialModel = _taleManager.GetSpatialModel(schedule.ClusterIndex);
    var destLoc = spatialModel?.GetLocation(schedule.CurrentLocationId);

    if (destLoc != null && _goTo != null)
    {
        // TODO: Wire NavMap service and call:
        // var route = await StreetRouteBuilder.BuildAsync(
        //     startPos, destLoc.EntryPosition, navMap);
        // _goTo.PrecomputedRoute = route;

        // For now: straight-line fallback
        _goTo.PrecomputedRoute = null;
    }
}
```

---

## Phase 7D — Building Occupancy

**Goal**: NPCs at indoor locations (shops, offices, homes) do not spawn visible entities; they remain Tier 3 only.

### StayAtStrategyPart Update

Add `IsIndoorActivity` flag:

```csharp
public class StayAtStrategyPart : AStrategyPart
{
    public bool IsIndoorActivity { get; set; } = false;

    public override void OnEnter()
    {
        if (!IsIndoorActivity)
        {
            // Setup idle behavior only for outdoor activities (street segments)
            _idleBehavior = new IdleBehavior { /* ... */ };
            _entity.Set(new engine.behave.components.Behavior(_idleBehavior));
        }
        else
        {
            // Indoor: NPC is invisible, no behavior needed
        }

        // Update position
        _entity.Set(new Transform { Position = _schedule.PositionAt(DateTime.UtcNow, _spatialModel) });
    }
}
```

### TaleEntityStrategy Update

Detect and flag indoor activities:

```csharp
private void _setupActivity()
{
    var nextStorylet = _getCurrentStorylet();
    var nextActivity = nextStorylet.GetActivity();  // Returns activity type

    _stayAt.IsIndoorActivity = false;

    var spatialModel = _taleManager.GetSpatialModel(schedule.ClusterIndex);
    if (spatialModel != null)
    {
        var loc = spatialModel.GetLocation(schedule.CurrentLocationId);
        if (loc != null && loc.Type != "street_segment")
        {
            // Non-street location = indoor
            _stayAt.IsIndoorActivity = true;
        }
    }
}
```

---

## Phase 7E — Fragment-Accurate Position

**Goal**: Track exact world position of each NPC so spawning uses current activity location, not home location.

### NpcSchedule Update

Add world position tracking:

```csharp
public class NpcSchedule
{
    public Vector3 CurrentWorldPosition;  // Updated each simulation tick

    // ... other fields
}
```

### TaleManager.AdvanceNpc() Update

Update position after each storylet advance:

```csharp
public StoryletDefinition AdvanceNpc(int npcId, DateTime gameTime)
{
    var schedule = GetSchedule(npcId);
    if (schedule == null) return null;

    var spatialModel = GetSpatialModel(schedule.ClusterIndex);

    // Advance storylet and apply postconditions
    var nextStorylet = _selector.SelectNext(schedule, /* ... */);

    // Update world position to current activity location
    var currentLoc = spatialModel?.GetLocation(schedule.CurrentLocationId);
    if (currentLoc != null)
        schedule.CurrentWorldPosition = currentLoc.EntryPosition != Vector3.Zero
            ? currentLoc.EntryPosition
            : currentLoc.Position;

    return nextStorylet;
}
```

### TaleManager.GetNpcsInFragment() Update

Use `CurrentWorldPosition` for fragment filtering:

```csharp
public List<NpcSchedule> GetNpcsInFragment(Index3 fragmentIndex)
{
    return AllSchedules
        .Where(schedule =>
        {
            // Use CurrentWorldPosition if set, otherwise fall back to HomePosition
            var checkPos = schedule.CurrentWorldPosition != Vector3.Zero
                ? schedule.CurrentWorldPosition
                : schedule.HomePosition;
            return Fragment.PosToIndex(checkPos) == fragmentIndex;
        })
        .ToList();
}
```

### Serialization Update

In `TaleModule.SerializeNpcSchedule()` and `DeserializeNpcSchedule()`:

```csharp
private JObject SerializeNpcSchedule(NpcSchedule schedule)
{
    return new JObject
    {
        ["CurrentWorldPosition"] = new JArray(schedule.CurrentWorldPosition.X, schedule.CurrentWorldPosition.Y, schedule.CurrentWorldPosition.Z),
        // ... other fields
    };
}

private NpcSchedule DeserializeNpcSchedule(JObject obj)
{
    var schedule = new NpcSchedule { /* ... */ };

    if (obj["CurrentWorldPosition"] is JArray posArray && posArray.Count == 3)
        schedule.CurrentWorldPosition = new Vector3(
            (float)posArray[0],
            (float)posArray[1],
            (float)posArray[2]
        );

    return schedule;
}
```

---

## Implementation Order

```
Phase 7A (Per-cluster SpatialModel)
    │
    ▼
Phase 7B (Entry Points) ────── NPCs spawn at doors, not walls
    │
    ▼
Phase 7C (Pathfinding) ─────── Infrastructure in place for future A* wiring
    │
    ▼
Phase 7D (Building Occupancy) ── NPCs invisible indoors
    │
    ▼
Phase 7E (Fragment Position) ──── Accurate spawning per current activity
```

Each phase builds on the previous. Phase 7B is the first major user-facing improvement (no more clipping). Phase 7D makes NPCs feel "inside buildings." Phase 7E ensures consistency with fragment-based spawning.

---

## Files Created/Modified

| File | Change |
|------|--------|
| `JoyceCode/engine/tale/TaleManager.cs` | Per-cluster spatial models, per-cluster initialization, GetSpatialModel accessor |
| `JoyceCode/engine/tale/TalePopulationGenerator.cs` | Accept SpatialModel, role-based location assignment |
| `JoyceCode/engine/tale/NpcSchedule.cs` | CurrentWorldPosition field, EntryPosition support in PositionAt() |
| `JoyceCode/engine/tale/SpatialModel.cs` | Location.EntryPosition field, ExtractFrom() compute entry positions |
| `nogameCode/nogame/modules/tale/TaleModule.cs` | Call SpatialModel.ExtractFrom(), pass to PopulateCluster(), serialize/deserialize CurrentWorldPosition |
| `nogameCode/nogame/characters/citizen/GoToStrategyPart.cs` | PrecomputedRoute property, use in OnEnter() |
| `nogameCode/nogame/characters/citizen/StayAtStrategyPart.cs` | IsIndoorActivity flag, skip behavior setup when indoor |
| `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` | _setupActivity detects indoor, _advanceAndTravel wires pathfinding stub |
| `nogameCode/nogame/characters/citizen/StreetRouteBuilder.cs` | **Create** — A* pathfinding wrapper |
| `nogameCode/nogameCode.projitems` | Add StreetRouteBuilder.cs to compilation |

---

## Validation

- Approach a cluster → NPCs spawn at entry positions (visible in debug view)
- Leave and return → same NPCs, same positions
- NPCs at indoor locations fade invisibly
- `CurrentWorldPosition` updated correctly across activity changes
- Save/load preserves position state
- No visible clipping or wall-phasing

---

## Open Questions

1. **NavMap service injection**: How to cleanly access NavMap from TaleEntityStrategy for async pathfinding? Currently stubbed pending architecture review.
2. **Building role metadata**: Where should building types (residential/office/warehouse) be declared? Phase 7b proposes `Building.Tags`.
3. **Street density**: Should path-following reduce pathfinding load by caching routes per cluster pair? Or precompute at cluster load time?
4. **Performance**: With A* enabled, will 100+ simultaneous NPC movements cause frame stutters? May need async work queue or LOD fallback.
