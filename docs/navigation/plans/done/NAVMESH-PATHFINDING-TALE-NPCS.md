# Plan: NavMesh Pathfinding for TALE NPCs via IRouteGenerator

**Status**: ✅ COMPLETE (2026-03-26, Commit 4a2080a7)

## Context

TALE NPCs were moving in straight lines through buildings. This plan proposed wiring IRouteGenerator to route them along actual streets. **All infrastructure was complete**; the issue was three critical bugs:
- `NavMap` (registered as singleton `I.Get<NavMap>()`) built from the street graph at startup
- `LocalPathfinder` — full A* over `NavJunction`/`NavLane` graph
- `StreetRouteBuilder.BuildAsync()` — snaps positions to NavMap, runs A*, returns `SegmentRoute` with sidewalk offset
- `GoToStrategyPart.PrecomputedRoute` — already accepts a `SegmentRoute`, falls back to straight-line when null

**What was actually wrong** (discovered during implementation):
- NavCluster.cs:94 had blocking `.Wait()` that deadlocked async cursor loading
- StreetRouteBuilder had no timeout enforcement despite 100ms CancellationTokenSource
- All failures silently returned null with no diagnostics
- Indoor NPCs were rendered when they should be hidden

This plan proposed the interface wire, but the real fix was **three critical bug fixes** that unblocked the infrastructure that was already in place.

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

## Completion Status (2026-03-26)

### What This Plan Proposed
- Create IRouteGenerator interface (was already done in Phase 7C planning)
- Create NavMeshRouteGenerator implementation (was already done in Phase 7C planning)
- Wire TaleEntityStrategy to use IRouteGenerator (was already done in Phase 7C planning)
- Register in DI (was already done in Phase 7C planning)

### What Actually Happened
Instead of creating new infrastructure, Phase 7C fixed three critical bugs preventing the existing infrastructure from working:

1. **NavCluster.cs:94** — `_semCreate.Wait()` → `await _semCreate.WaitAsync()`
   - Deadlock prevented cursor content from loading asynchronously

2. **StreetRouteBuilder.cs** — Added cancellation token checks + diagnostics
   - 100ms timeout now properly enforced
   - 7 Trace logs for every failure path and success

3. **StayAtStrategyPart.cs** — Hide indoor NPCs
   - NPCs doing indoor activities no longer visible

### Verification (Completed)

✅ `dotnet build Karawan.sln -c Release` — no errors
✅ `./run_tests.sh all` — **171/171 passing** (zero regressions)
✅ Run game — NPCs walk along streets, not through buildings
✅ Logs show diagnostic messages when routes are generated/failed
✅ Indoor NPCs (home, office, warehouse) are hidden during activities

### Result
**Phase 7C COMPLETE**: NavMesh routing deadlock fixed. NPCs now walk on street networks instead of through buildings. Full diagnostic logging enables future debugging. Phase D multi-objective routing can now build on working pathfinding foundation.
