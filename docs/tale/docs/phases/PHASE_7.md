# Phase 7: Spatial Grounding & Navigation

> **Status:** implemented - **Implemented as:** `JoyceCode/engine/tale/SpatialModel.cs:10,163`, `JoyceCode/engine/tale/TaleManager.cs:23,95,587`, `JoyceCode/engine/tale/NpcSchedule.cs:140`, `JoyceCode/engine/tale/IRouteGenerator.cs:17`, `nogameCode/nogame/characters/citizen/NavMeshRouteGenerator.cs:21`, `nogameCode/nogame/characters/citizen/StreetRouteBuilder.cs:78` - **Verified:** 2026-10-18 (rewritten from the code, KAR-431)

Phase 7 ties the TALE simulation's logical locations to world coordinates:
each cluster gets its own `SpatialModel`, every location has a reachable
standing position on the pavement, NPCs travel along real street routes, NPCs
at indoor locations are not shown, and each schedule tracks a world position
for fragment-based spawning.

This document describes the shipped code. The original March 2026 design
(shop doors at shopfront midpoints only, a straight-line routing stub, a
`JObject`-based save format) was superseded; sections 7B, 7C and the
serialization part of 7E were rewritten on 2026-10-18.

**Prerequisites**: Phase 6 (seed-based population, cluster lifecycle), the
cluster's street and building geometry, and — for entry points and routing —
the satnav `NavMap`.

---

## Design Principles

1. **Stand on the pavement, never in the road.** Every location has an entry
   position snapped to a pedestrian lane where navigation data exists.
   Junction (street) locations spread their NPCs over the sidewalk corners.
2. **Per-cluster spatial models**, extracted from `ClusterDesc` when the
   cluster completes.
3. **Indoor invisibility**: NPCs staying at any non-street location get no
   visible behaviour.
4. **Real routes, safe fallback**: travel uses A* over the street graph via
   `IRouteGenerator`; a missing route means a straight line for short hops
   and staying put for long ones.
5. **Fragment-accurate position**: `NpcSchedule.CurrentWorldPosition` follows
   the NPC's current activity so fragment queries find it.

---

## Phase 7A — Per-cluster SpatialModel

`TaleManager` keeps one model per cluster
(`Dictionary<int, SpatialModel> _spatialModels`, `TaleManager.cs:23`).

| API | Where |
|---|---|
| `TaleManager.Initialize(StoryletLibrary library, int seed = 42)` | `TaleManager.cs:77` |
| `TaleManager.PopulateCluster(ClusterDesc clusterDesc, SpatialModel spatialModel, DateTime? gameNow = null)` — stores the model, generates schedules | `TaleManager.cs:95,107` |
| `TaleManager.DepopulateCluster(int clusterIndex)` | `TaleManager.cs` — called by `TaleModule` once no fragment touches the cluster (S0 W4, KAR-434) |
| `TaleManager.GetSpatialModel(int clusterIndex)` | `TaleManager.cs:864` |
| `SpatialModel.ExtractFrom(ClusterDesc cluster, NavCluster navCluster = null)` | `SpatialModel.cs:163` |
| `TalePopulationGenerator.Generate(ClusterDesc, SpatialModel, HashSet<int> skipIndices = null)` | `TalePopulationGenerator.cs:40` |

Population happens in three places, each extracting the model with the
cluster's `NavCluster` (looked up in `NavMap.TopCluster.Content.Clusters` by
`IdString`) and passing the current game time:

- `TaleModule._onClusterCompleted` (`TaleModule.cs:72`, extract at `:115`,
  populate at `:130`), which also runs `SpatialModel.ValidateReachability`;
- the catch-up loop for clusters completed before TALE activated
  (`TaleModule.cs:500-501`);
- `TaleSpawnOperator._ensureClusterPopulated` (`TaleSpawnOperator.cs:60,80-81`).

Building locations take their type from the building's role tags:
`residential` → `home`, `warehouse`, `office`; **an untagged building is a
`home`** (`SpatialModel.cs:271-278`). A building with a shopfront becomes a
`shop` or, for `Drink`/`Eat`, a `social_venue`.

---

## Phase 7B — Entry Points

Every `Location` (`SpatialModel.cs:10`) carries:

| Field | Meaning |
|---|---|
| `Position` | building centre, or the junction centre for a street location |
| `EntryPosition` | where an NPC stands: door, shopfront or sidewalk corner |
| `EntryCandidates` | street locations only: all sidewalk corners of the junction; `EntryPosition` is always `[0]` |
| `IsPedestrianReachable` | false when navigation data exists but no pedestrian lane is near the entry; defaults to true without nav data so bake and testbed callers keep the location |

**Use `Location.EntryPositionFor(npcId)`** (`SpatialModel.cs:38`) wherever an
NPC's standing point is needed. It picks `EntryCandidates[npcId mod n]`, so
NPCs bound to the same junction spread over its corners deterministically;
without candidates it returns `EntryPosition` (or `Position` if that is zero).

How each kind of location gets its entry (`ExtractFrom`, `SpatialModel.cs:163`):

- **Shops and venues**: the midpoint of the shopfront's first two points
  (`ComputeShopEntryPosition`, `:635`), at the shop's **own storey** height
  (`BuildingFooting.StoreyGroundAt`, `:240-248`) — shopfronts are aligned to
  storeys, and a door a storey below its window would be in the wall. The
  result is snapped to the nearest pedestrian lane within
  `PedestrianSnapRadius` (50 m, `:73`) by `_snapToPedestrianLane` (`:251`).
- **Other buildings**: the first perimeter point moved 2 m towards the
  centre, at the **pavement height at that point**
  (`BuildingFooting.PavementHeightAt`, `:290-307`; a block's kerb can fall
  13 m across the block on the shipped terrain), then snapped the same way
  (`:314`).
- **Street locations (junctions)**: `_computeStreetEntryCandidates`
  (`:500`), never the junction centre when anything better exists:
  1. with nav data, the endpoints of pedestrian NavLanes near the junction
     (sidewalk corners; endpoints, not projections, because the nearest lane
     is usually a crossing), deduplicated, nearest first, within 15 m of the
     nearest corner (`CornerSpreadTolerance`), at most 6
     (`MaxEntryCandidates`, `:481`);
  2. nav data but no corner in range: the street point's section corners,
     each pushed 1 m outward, flagged **unreachable**;
  3. no nav data: the same section corners, flagged reachable;
  4. only for a degenerate street point: the raw junction centre.

  Junctions that exist only to hold up a ramp or bridge deck get no street
  location (`StreetLocationJunctionsOf`, `:150`).

`TaleManager.AdvanceNpc` and `TalePopulationGenerator` set
`CurrentWorldPosition` to `EntryPositionFor(NpcId)`, the NPC's corner, so
fragment bucketing (7E), its only reader, agrees with `PositionAt` (S0 W2,
KAR-446; it used to be the junction centre).

### NpcSchedule.PositionAt

`PositionAt(DateTime gameTime, SpatialModel model)` (`NpcSchedule.cs:140`) is
a pure function of the schedule:

- in transit (`IsInTransit` and `TransitStart ≤ gameTime < TransitEnd`): a
  linear interpolation between `EntryPositionFor(NpcId)` of the transit
  source and destination;
- otherwise: `EntryPositionFor(NpcId)` of the current location;
- `HomePosition` when there is no model or location (the latter logs an
  error).

---

## Phase 7C — Street Routing

Routing is pluggable and real:

```csharp
// JoyceCode/engine/tale/IRouteGenerator.cs:17
public interface IRouteGenerator
{
    Task<SegmentRoute> GetRouteAsync(Vector3 fromPos, Vector3 toPos, PositionDescription startPod,
        RoutingPreferences? preferences = null,
        TransportationType transportType = TransportationType.Pedestrian);
}
```

- **Binding**: `models/nogame.implementations.json:12` maps
  `engine.tale.IRouteGenerator` to
  `nogame.characters.citizen.NavMeshRouteGenerator`.
- **`NavMeshRouteGenerator.GetRouteAsync`** (`NavMeshRouteGenerator.cs:26`)
  takes the `NavMap` from DI and calls the builder under a 100 ms
  cancellation timeout (`TimeoutMs`, `:24`); no `NavMap` or a timeout means
  `null`.
- **`StreetRouteBuilder.BuildAsync`** (`StreetRouteBuilder.cs:78`) runs A*
  over the street graph:

  ```csharp
  public static async Task<SegmentRoute> BuildAsync(Vector3 fromPos, Vector3 toPos, NavMap navMap,
      PositionDescription startPod,
      TransportationType transportType = TransportationType.Pedestrian,
      RoutingPreferences? preferences = null,
      CancellationToken cancellationToken = default)
  ```

  It returns `null` on any failure; the caller decides the fallback.

**Callers** in `TaleEntityStrategy`
(`nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs`):

- `_travelToCurrentDestination` (`:322`, used by `_advanceAndTravelAsync`
  and `_resumeTravelAsync`): destination is `destLoc.EntryPositionFor(_npcId)`
  (`:336-340`); a location of `"current"` or a destination under 2 m skips
  travel; the route is requested with the NPC's `_routingPreferences` and
  `schedule.PreferredTransportationType` (`:365-368`).
- `SpawnInTravel` (`:495`, route at `:532-535`) for NPCs materialised
  mid-transit.

Both apply the same fallback: **no route and more than 10 m to go → the NPC
stays and starts its activity** instead of walking through buildings;
otherwise `GoToStrategyPart.PrecomputedRoute` gets the route (`:401`,
`:567`), and a `null` route means `GoToStrategyPart` walks a two-point
straight line (`GoToStrategyPart.cs:14,31`). Everything after the `await`
runs on a thread-pool thread, so strategy changes go through
`TriggerStrategyOnLogicalThread`.

---

## Phase 7D — Building Occupancy

`StayAtStrategyPart.IsIndoorActivity` (`StayAtStrategyPart.cs:34`) hides
the NPC: no idle behaviour, no animation. `TaleEntityStrategy._setupActivity`
sets it for **every location whose type is not `street_segment`**
(`TaleEntityStrategy.cs:436-445`). Outdoor NPCs get a
`TaleConversationBehavior` so the player can talk to them (`:464`).

---

## Phase 7E — Fragment-Accurate Position

- `NpcSchedule.CurrentWorldPosition` is updated in `TaleManager.AdvanceNpc`
  from the destination location (`TaleManager.cs:828-832`; see the 7B gap).
- `TaleManager.GetNpcsInFragment(Index3)` (`TaleManager.cs:587`) buckets by
  `CurrentWorldPosition`, falling back to `HomePosition` when it is zero
  (`:594`).

### Serialization

Deviated schedules are saved with `System.Text.Json.Nodes.JsonObject`
(`TaleModule.SerializeNpcSchedule`, `TaleModule.cs:585`; the reverse at
`:695-710`). Positions are flat float keys, read with a `0` default so older
saves load:

```
homePositionX/Y/Z, workplacePositionX/Y/Z, currentWorldPositionX/Y/Z
```

(`TaleModule.cs:627-629`, `:700-710`).

---

## Files

| File | Role |
|------|------|
| `JoyceCode/engine/tale/SpatialModel.cs` | `Location`, `EntryPositionFor`, `ExtractFrom`, entry computation, `ValidateReachability` |
| `JoyceCode/engine/tale/TaleManager.cs` | per-cluster models, `PopulateCluster`, `AdvanceNpc` position, `GetNpcsInFragment` |
| `JoyceCode/engine/tale/NpcSchedule.cs` | `CurrentWorldPosition`, transit fields, `PositionAt` |
| `JoyceCode/engine/tale/TalePopulationGenerator.cs` | role-based location assignment from the model |
| `JoyceCode/engine/tale/IRouteGenerator.cs` | routing interface |
| `nogameCode/nogame/characters/citizen/NavMeshRouteGenerator.cs` | `IRouteGenerator` over the NavMap, 100 ms timeout |
| `nogameCode/nogame/characters/citizen/StreetRouteBuilder.cs` | A* street route builder |
| `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` | travel, fallback, indoor detection |
| `nogameCode/nogame/characters/citizen/GoToStrategyPart.cs` | `PrecomputedRoute`, straight-line fallback |
| `nogameCode/nogame/characters/citizen/StayAtStrategyPart.cs` | `IsIndoorActivity` |
| `nogameCode/nogame/modules/tale/TaleModule.cs` | cluster population, schedule (de)serialization |

---

## Validation

- `models/tests/tale/phase7-spatial/` holds 102 script **outlines** that
  the ExpectEngine cannot execute and no `run_tests.sh` tier runs (see
  `../tests/PHASE_7.md`; KAR-433).
- xUnit: `tests/JoyceCode.Tests/engine/tale/StreetEntryPositionTests.cs`
  pins the junction entry candidates.
- In the game: NPCs at junctions stand on different sidewalk corners, never
  in the roadway; NPCs at shops and offices are not visible; NPCs walk along
  streets rather than through buildings.

## Open Questions

1. `CurrentWorldPosition` for street locations uses the junction centre
   (KAR-436).
2. ~~`DepopulateCluster` is never called~~ — wired in S0 W4 (KAR-434); the
   spatial model is dropped with the cluster.
3. Route caching per location pair has not been needed so far; the 100 ms
   timeout bounds the cost per request.
