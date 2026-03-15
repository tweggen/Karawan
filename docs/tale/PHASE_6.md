# Phase 6: Production Integration — Seed-Based Population & Cluster Lifecycle

Phase 6 bridges the TALE simulation (Phases 0-5) with the live game engine. It replaces the placeholder `TaleSpawnOperator` wiring with the full Tier 3 → Tier 2 → Tier 1 pipeline: deterministic NPC population per cluster, schedule-aware spawning, and deviation-only persistence.

## Design Principles

1. **Only save what can't be regenerated.** An NPC in primary state is fully determined by its seed. Only player-caused deviations are persisted.
2. **Seed independence.** Each NPC's seed is computed from `Hash(clusterSeed, npcIndex)`, not sequentially. Skipping deviated indices does not shift other NPCs.
3. **Cluster-scoped lifecycle.** NPC populations are created when a cluster activates and dropped (if undeviated) when it deactivates. No world-wide upfront allocation.
4. **TaleManager is a query index, not the owner.** The source of truth is the seed (for primary NPCs) or the save file (for deviated NPCs). TaleManager provides fast runtime lookup.

---

## Phase 6A — Seed-Based Population Generator

**Goal:** Given a `ClusterDesc`, deterministically generate a list of `NpcSchedule` objects.

### What to Build

`TalePopulationGenerator` (new class in `JoyceCode/engine/tale/`):

```
Input:  ClusterDesc (provides IdString/seed, street points, quarters, building data)
Output: List<NpcSchedule> — one per NPC, fully initialized
```

### Key Decisions

- **NPC count**: Derived deterministically from cluster properties (quarter count, building count, density attributes). Must be stable across regeneration — same cluster seed always produces the same count.
- **NPC seed**: `new RandomSource(clusterDesc.IdString + "-npc-" + npcIndex)` — follows the existing pattern of string-concatenation seeding used by streets, quarters, and buildings.
- **Role assignment**: From NPC seed. Distribution weighted by cluster character (e.g., downtown clusters have more merchants, residential clusters have more workers).
- **Location assignment**: Home, workplace, and social venues assigned from the cluster's quarter/building data. Each NPC gets locations deterministically from its seed.
- **Property initialization**: Default property values with per-NPC variation from seed (e.g., morality 0.6-0.8, wealth varies by role).

### Files to Create/Modify

| File | Action |
|------|--------|
| `JoyceCode/engine/tale/TalePopulationGenerator.cs` | **Create** — population generation logic |
| `JoyceCode/engine/tale/NpcSchedule.cs` | Minor — ensure all fields have sensible defaults |

### Validation

- Unit-testable: same `ClusterDesc` seed → same NPC list every time
- NPC at index N is identical regardless of which other indices are skipped
- Can run in Testbed standalone

---

## Phase 6B — Cluster Lifecycle Hook

**Goal:** Wire `TalePopulationGenerator` into the cluster activation lifecycle so NPCs are created when the player approaches a cluster.

### What to Build

Subscribe to `ClusterCompletedEvent` (fired by `ClusterDesc._triggerStreets_nl()` at line 492 after cluster operators run). On event:

1. Run `TalePopulationGenerator` for the completed cluster
2. Register all generated `NpcSchedule` objects with `TaleManager`
3. Track which clusters are currently populated

On cluster deactivation (when all fragments in a cluster leave visibility):

1. Remove non-deviated schedules from `TaleManager`
2. Keep deviated schedules (they'll be overlaid on next activation)

### Cluster Deactivation Detection

Currently no explicit cluster deactivation event exists. Options:
- **Option A**: `TaleModule` periodically checks which clusters have no visible fragments (piggyback on `SpawnController`'s frame loop)
- **Option B**: Add a `ClusterDeactivatedEvent` to `Loader` fragment purging logic
- **Recommended**: Option A for simplicity — check on a slow timer (every few seconds), not every frame

### Files to Create/Modify

| File | Action |
|------|--------|
| `nogameCode/nogame/modules/tale/TaleModule.cs` | **Modify** — subscribe to `ClusterCompletedEvent`, manage cluster population lifecycle |
| `JoyceCode/engine/tale/TaleManager.cs` | **Modify** — add `RegisterCluster(clusterId, List<NpcSchedule>)`, `UnregisterCluster(clusterId)`, `GetNpcsInFragment(Index3)` |
| `JoyceCode/engine/world/WorldEvents.cs` | Review — `ClusterCompletedEvent` already exists |

### Validation

- Start game, approach a cluster → TaleManager now has schedules for that cluster
- Drive away → non-deviated schedules are dropped
- Drive back → identical schedules regenerated

---

## Phase 6C — TaleSpawnOperator Rework

**Goal:** Replace the `_seed++` placeholder in `TaleSpawnOperator` with schedule-aware materialization from `TaleManager`.

### Current Problem

`TaleSpawnOperator.SpawnCharacter()` creates anonymous NPCs with `int npcId = _seed++`. No corresponding `NpcSchedule` exists in `TaleManager`, so `GetSchedule()` returns null and NPCs are inert.

### What to Change

`TaleSpawnOperator.SpawnCharacter()`:

1. Query `TaleManager.GetNpcsInFragment(idxFragment)` for Tier 3 NPCs in this fragment
2. Filter to NPCs not already materialized (need a `MaterializedNpcIds` set)
3. Pick one, create `TaleEntityStrategy` using its existing `NpcSchedule`
4. Track the materialized NPC ID

`TaleSpawnOperator.TerminateCharacters()`:

1. Remove NPC ID from `MaterializedNpcIds`
2. The `NpcSchedule` stays in `TaleManager` (Tier 3 persists)

`TaleSpawnOperator.GetFragmentSpawnStatus()`:

1. Query `TaleManager` for NPC count in fragment instead of using hardcoded density formula
2. Min/max characters = number of Tier 3 NPCs assigned to this fragment's locations

### Mapping NPCs to Fragments

`NpcSchedule.CurrentLocationId` points to a spatial model location. For the live game, we need to map location IDs to fragment indices. Options:
- Location IDs could directly encode fragment index (simple but rigid)
- A lookup table in `TaleManager` maps location ID → `Index3` (flexible)
- NPC home/workplace positions are resolved to world coordinates at generation time; fragment index = `Fragment.PosToIndex(worldPos)` (cleanest — reuses existing infrastructure)

**Recommended**: Store world position on `NpcSchedule` at generation time (home position as default). `GetNpcsInFragment()` filters by `Fragment.PosToIndex(npc.HomePosition)`.

### Files to Create/Modify

| File | Action |
|------|--------|
| `nogameCode/nogame/characters/citizen/TaleSpawnOperator.cs` | **Rewrite** — query TaleManager instead of _seed++ |
| `JoyceCode/engine/tale/TaleManager.cs` | **Modify** — add `GetNpcsInFragment(Index3)` |
| `JoyceCode/engine/tale/NpcSchedule.cs` | **Modify** — add `HomePosition` (Vector3) for fragment mapping |
| `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` | **Modify** — `TryCreate` takes existing NpcSchedule instead of bare npcId |

### Validation

- Approach cluster → NPCs spawn with valid schedules
- `TaleManager.GetSchedule(npcId)` returns non-null for all spawned NPCs
- NPCs advance through storylets (travel → activity → advance → travel)
- Leave fragment, return → same NPCs regenerated at correct positions

---

## Phase 6D — Deviation Tracking

**Goal:** Detect when the player causes an NPC to deviate from primary state and flag it for persistence.

### What Constitutes a Deviation

An NPC deviates from primary state when:
1. **Player interaction fires a postcondition** that modifies NPC properties (e.g., player starts a fight → anger increases)
2. **Player-initiated quest** involves the NPC (e.g., taxi passenger, quest target)
3. **Recursive entanglement**: a deviated NPC's group partner or trust relationship partner is also deviated if the player's action caused a cascade

An NPC does **not** deviate from:
- Being observed (player walked past, asked name)
- NPC-NPC interactions with no player involvement (these are primary state — the algorithm would produce equivalent outcomes)

### What to Build

Add to `NpcSchedule`:
```csharp
public bool HasPlayerDeviation;
public int DeviatedAtClusterIndex;  // which cluster this NPC belongs to
public int NpcIndex;                // slot in the cluster's population (for skip mask)
```

Set `HasPlayerDeviation = true` in:
- `StoryletSelector.ApplyPostconditions()` — when called from a player-interaction context
- `StoryletSelector.ApplyConditionalPostconditions()` — when player is involved
- Any quest system hook that modifies NPC state

Propagation:
- When marking NPC A as deviated, check A's group (`GroupId`) and trust relationships. If any partner NPC B was affected by the same interaction chain, mark B too.

### Files to Create/Modify

| File | Action |
|------|--------|
| `JoyceCode/engine/tale/NpcSchedule.cs` | **Modify** — add `HasPlayerDeviation`, `DeviatedAtClusterIndex`, `NpcIndex` |
| `JoyceCode/engine/tale/StoryletSelector.cs` | **Modify** — accept interaction context, set deviation flag |
| `JoyceCode/engine/tale/TaleManager.cs` | **Modify** — `GetDeviatedNpcs(clusterIndex)` query |

### Validation

- Player interacts with NPC → `HasPlayerDeviation` becomes true
- Player doesn't interact → flag stays false
- Group interaction → all affected members flagged
- `TaleManager.GetDeviatedNpcs(clusterIndex)` returns only flagged NPCs

---

## Phase 6E — Deviation Persistence

**Goal:** Save only deviated NPCs. On load, regenerate primary NPCs and overlay deviated ones.

### Save Flow

Hook into `Saver.OnBeforeSaveGame`:

1. Collect all `NpcSchedule` objects where `HasPlayerDeviation == true`
2. Serialize each as: `{ clusterIndex, npcIndex, npcSeed, schedule state }`
3. Store in `GameState` as a JSON field (e.g., `GameState.TaleDeviations`)

This is **not** the entity persistence system (`EntitySaver` / `ICreator`). Deviated NPCs are saved as TALE-level data, not as ECS entities. The ECS entity is transient (Tier 2/1 only); the `NpcSchedule` is the persistent record.

### Load Flow

Hook into `Saver.OnAfterLoadGame`:

1. Deserialize `GameState.TaleDeviations` → list of deviated `NpcSchedule` objects
2. Register them in `TaleManager` with a deviation index: `{ clusterIndex → Set<npcIndex> }`
3. When a cluster later activates (Phase 6B), the population generator skips deviated indices and `TaleManager` already has the deviated schedules

### Save File Size

Estimated per deviated NPC: ~500 bytes (properties dict + arc stack + relationships + seed coordinates). For 50 deviated NPCs (heavy player engagement): ~25KB. Negligible for mobile upload.

### Files to Create/Modify

| File | Action |
|------|--------|
| `nogameCode/nogame/modules/tale/TaleModule.cs` | **Modify** — hook OnBeforeSaveGame/OnAfterLoadGame |
| `JoyceCode/engine/tale/TaleManager.cs` | **Modify** — `GetDeviatedNpcs()`, `GetDeviationSkipMask(clusterIndex)` |
| `JoyceCode/engine/tale/NpcSchedule.cs` | **Modify** — JSON serialization support |
| `nogameCode/nogame/GameState.cs` | **Modify** — add `TaleDeviations` field |

### Validation

- Interact with NPC, save game → `GameState.TaleDeviations` contains that NPC
- Load game → deviated NPC restored with correct state
- Visit cluster after load → primary NPCs regenerated, deviated NPC has modified properties
- Save file size: verify < 50KB for typical play session

---

## Phase 6F — TaleManager Rework

**Goal:** Refactor `TaleManager` from a flat dictionary into a cluster-aware, tier-conscious schedule registry.

### Current State

`TaleManager` is a simple `Dictionary<int, NpcSchedule>` with `RegisterNpc` / `GetSchedule` / `AdvanceNpc`. No concept of clusters, tiers, or deviation state.

### Target State

```
TaleManager
├── Per-cluster schedule sets (populated on cluster activation)
│   ├── Cluster 0: { npc0, npc1, ..., npcN } (excluding deviated)
│   ├── Cluster 3: { npc0, npc2, ..., npcN } (npc1 skipped — deviated)
│   └── ...
├── Deviated NPC schedules (loaded from save, persist across cluster lifecycle)
│   ├── Cluster 3, npc1: { full schedule state }
│   └── ...
├── Materialized NPC tracking (which Tier 3 NPCs are currently Tier 2/1)
└── Query API
    ├── GetSchedule(npcId) → NpcSchedule (any tier)
    ├── GetNpcsInFragment(Index3) → List<NpcSchedule> (for materialization)
    ├── GetDeviatedNpcs(clusterIndex) → List<NpcSchedule>
    ├── GetDeviationSkipMask(clusterIndex) → Set<int> (npc indices to skip)
    ├── RegisterCluster(clusterIndex, List<NpcSchedule>)
    └── UnregisterCluster(clusterIndex)
```

### NPC ID Scheme

Current: bare `int` from `_seed++`. New: encode cluster + NPC index for globally unique, stable IDs.

```csharp
// 20 bits cluster index (up to 1M clusters) + 12 bits NPC index (up to 4096 NPCs per cluster)
public static int MakeNpcId(int clusterIndex, int npcIndex) => (clusterIndex << 12) | npcIndex;
public static int GetClusterIndex(int npcId) => npcId >> 12;
public static int GetNpcIndex(int npcId) => npcId & 0xFFF;
```

This ensures NPC IDs are stable across regeneration and globally unique without a central counter.

### Files to Create/Modify

| File | Action |
|------|--------|
| `JoyceCode/engine/tale/TaleManager.cs` | **Rewrite** — cluster-aware registry with deviation overlay |
| `JoyceCode/engine/tale/NpcSchedule.cs` | **Modify** — ensure `NpcId` uses the cluster+index encoding |

### Validation

- Full round-trip: start game → approach cluster → NPCs populate → interact with one → save → load → approach same cluster → deviated NPC has correct state, others regenerated fresh
- Drive to new cluster → new population, independent IDs
- Performance: 500 NPCs per cluster, 3 active clusters = 1500 schedules — verify no frame impact

---

## Implementation Order & Dependencies

```
Phase 6A (Population Generator)
    │
    ▼
Phase 6B (Cluster Lifecycle Hook)
    │
    ▼
Phase 6C (SpawnOperator Rework) ─── First playable: NPCs spawn with real schedules
    │
    ▼
Phase 6D (Deviation Tracking)
    │
    ▼
Phase 6E (Deviation Persistence) ── Save/load works with minimal data
    │
    ▼
Phase 6F (TaleManager Rework) ──── Clean architecture, stable NPC IDs
```

Each phase is designed to be completable in a single conversation session. Phase 6C is the first milestone where TALE NPCs actually work in-game. Phase 6E is the first milestone where saves are correct.

---

## Open Questions

1. **Cluster deactivation signal**: No explicit event exists today. Phase 6B proposes a periodic check. Is a new `ClusterDeactivatedEvent` worth adding to the engine?
2. **NPC count tuning**: How many NPCs per cluster? Needs gameplay testing. The generator should make this easy to adjust via a density parameter.
3. **Spatial model for live game**: `TaleManager.Initialize()` currently takes `SpatialModel` (used in Testbed). The live game doesn't have a `SpatialModel` instance — location resolution needs to work from `ClusterDesc` data directly.
4. **Role distribution**: Should role weights be per-cluster (configured in cluster data) or global? Per-cluster is more interesting but adds configuration surface.
