# Plan: NavMesh Pathfinding for TALE NPCs via IRouteGenerator

## Context

TALE NPCs currently move in straight lines through buildings, which breaks visual credibility. The fix is to route them along actual streets. Critically, **all infrastructure already exists**:
- `NavMap` (registered as singleton `I.Get<NavMap>()`) built from the street graph at startup
- `LocalPathfinder` — full A* over `NavJunction`/`NavLane` graph
- `StreetRouteBuilder.BuildAsync()` — snaps positions to NavMap, runs A*, returns `SegmentRoute` with sidewalk offset
- `GoToStrategyPart.PrecomputedRoute` — already accepts a `SegmentRoute`, falls back to straight-line when null

The **only missing wire**: `TaleEntityStrategy._advanceAndTravel()` always passes `null` for the route (`_goTo.PrecomputedRoute = null`), with a `// TODO Phase 7C` comment.

This plan implements the wire, wrapped in a pluggable `IRouteGenerator` interface to support future transport types (public transit, cars).

---

## Files to Create/Modify

| Action | File | Change |
|--------|------|--------|
| CREATE | `JoyceCode/engine/tale/IRouteGenerator.cs` | New interface |
| CREATE | `nogameCode/nogame/characters/citizen/NavMeshRouteGenerator.cs` | NavMap implementation |
| MODIFY | `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` | Wire async routing |
| MODIFY | `nogameCode/nogame/characters/citizen/StreetRouteBuilder.cs` | Remove dead `navMapObj` param, use typed NavMap + CancellationToken |
| MODIFY | `models/nogame.implementations.json` | Register `IRouteGenerator` → `NavMeshRouteGenerator` |
| MODIFY | `JoyceCode/JoyceCode.projitems` | Add `IRouteGenerator.cs` |
| MODIFY | `nogameCode/nogameCode.projitems` | Add `NavMeshRouteGenerator.cs` |

---

## Step 1: `IRouteGenerator` Interface

**File:** `JoyceCode/engine/tale/IRouteGenerator.cs`

```csharp
namespace engine.tale;

/// <summary>
/// Computes a walkable route between two world positions.
/// Returns null if pathfinding fails — callers must handle null as "use straight-line fallback".
/// </summary>
public interface IRouteGenerator
{
    Task<engine.joyce.SegmentRoute> GetRouteAsync(
        System.Numerics.Vector3 fromPos,
        System.Numerics.Vector3 toPos,
        engine.world.PositionDescription startPod);
}
```

---

## Step 2: `NavMeshRouteGenerator`

**File:** `nogameCode/nogame/characters/citizen/NavMeshRouteGenerator.cs`

```csharp
public class NavMeshRouteGenerator : engine.tale.IRouteGenerator
{
    private const float TimeoutMs = 100f;

    public async Task<SegmentRoute> GetRouteAsync(Vector3 fromPos, Vector3 toPos, PositionDescription startPod)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(TimeoutMs));
            var navMap = I.Get<builtin.modules.satnav.desc.NavMap>();
            return await StreetRouteBuilder.BuildAsync(fromPos, toPos, navMap, startPod, cts.Token);
        }
        catch (Exception e)
        {
            engine.Logger.Warning($"NavMeshRouteGenerator: pathfinding failed: {e.Message}");
            return null;
        }
    }
}
```

---

## Step 3: Simplify `StreetRouteBuilder`

**File:** `nogameCode/nogame/characters/citizen/StreetRouteBuilder.cs`

Change signature from `object navMapObj` (always null) to typed `NavMap` + `CancellationToken`:

```csharp
public static async Task<SegmentRoute> BuildAsync(
    Vector3 fromPos, Vector3 toPos,
    builtin.modules.satnav.desc.NavMap navMap,
    PositionDescription startPod,
    CancellationToken cancellationToken = default)
```

Remove the null guard on `navMapObj` — the caller now guarantees a valid NavMap.

---

## Step 4: Wire Async Routing in `TaleEntityStrategy`

**File:** `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs`

Change `_advanceAndTravel()` to `async void`:

```csharp
private async void _advanceAndTravel()
{
    // ... existing: get gameNow, AdvanceNpc, destination, handle null storylet ...

    SegmentRoute route = null;
    try
    {
        var routeGen = I.Get<engine.tale.IRouteGenerator>();
        route = await routeGen.GetRouteAsync(
            _currentPosition.Position, destination, _currentPosition);
    }
    catch (Exception) { /* null = straight-line fallback */ }

    _goTo.Destination = destination;
    _goTo.CurrentPosition = _currentPosition;
    _goTo.PrecomputedRoute = route;
    TriggerStrategy("travel");
}
```

**Why `async void`:** Called from synchronous `GiveUpStrategy()` callback. NPC stays in current state briefly while A* runs (<100ms), then starts moving on streets.

---

## Step 5: DI Registration

**File:** `models/nogame.implementations.json`

```json
"engine.tale.IRouteGenerator": {
    "className": "nogame.characters.citizen.NavMeshRouteGenerator"
}
```

---

## Future Architecture

```
IRouteGenerator
├── NavMeshRouteGenerator         ← This plan
├── PublicTransitRouteGenerator   ← Future (groups on bus/tram)
└── CarRouteGenerator             ← Future (car lane offsets)
```

TALE Tier 3 simulation stays Euclidean (fast, no change). Only materialized Tier 1/2 NPCs use `IRouteGenerator`.

---

## Out of Scope

- Cross-cluster pathfinding (`ProxyJunctions` not populated)
- Route caching
- Pedestrian lane separation (NPCs walk single-file on sidewalk)
- Public transit / cars

---

## Verification

1. `dotnet build nogame/nogame.csproj -c Release` — no errors
2. Run game, open map view — NPCs walk along streets, not through buildings
3. Occasional `NavMeshRouteGenerator: pathfinding failed` logs acceptable (straight-line fallback)
4. `./run_tests.sh all` — all regression tests pass
