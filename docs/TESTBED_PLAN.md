# Headless Cluster Testbed
*Fast-forward simulation for TALE probability tuning*

---

## Purpose

The TALE narrative system's central challenge is **probability tuning**: balancing interrupt frequencies so that NPC daily routines are credible (80-90% completion rate) while producing enough interactions for emergent social structures (organized crime, police, trade networks) to self-organize over simulated weeks.

This cannot be tuned by playing the game at real-time speed. A headless testbed runs a full cluster simulation — same code paths as the real game — without rendering, at maximum speed. It produces an **interaction graph** for offline analysis.

### What We Learn

- **Interrupt probability calibration**: How often do spatial encounters trigger story interrupts? What threshold produces 1-3 meaningful interactions per NPC per day?
- **Routine completion rate**: What fraction of planned storylet sequences finish uninterrupted?
- **Emergent structure detection**: Do repeated interactions form cliques, hierarchies, power-law degree distributions?
- **Spatial encounter density**: Which locations (commute corridors, shops, social venues) produce the most co-location events? Are the numbers plausible?

---

## Architecture

### Two Simulation Modes

The testbed supports two fundamentally different execution models:

1. **Discrete Event Simulation (DES)** — the primary mode for probability tuning. All NPCs run as **Tier 3** (background). No per-frame ticking. Time advances by jumping between story node transitions. Encounters are computed probabilistically from spatial geometry. A simulated year runs in seconds.

2. **Headless Continuous Simulation** — for spatial validation. The real engine runs headlessly with time acceleration. NPCs physically move, and co-location is measured per-frame. Slower, but validates that the DES probabilities match real spatial behavior.

The DES mode is not a throwaway approximation — it **is** the production Tier 3 system described in `NPC_STORIES_DESIGN.md`. Building it for the testbed builds it for the game. In production, the same DES code runs background NPCs while the engine renders the nearby Tier 1/2 NPCs.

### Shared Infrastructure

Both modes share the same one-time world generation phase:
1. Bootstrap the engine headlessly (`Platform.EasyCreateHeadless()`)
2. Run MetaGen pipeline: cluster generation, street generation, buildings, shopfronts
3. Extract the **spatial model**: a static data structure of locations (buildings, shops, street segments, quarters) with connectivity and travel times

After world generation, the DES mode does **not** use the engine frame loop at all. It operates on the extracted spatial model with its own event queue.

### Project Structure

```
Testbed/                          # New console project
├── Testbed.csproj                # References Joyce, JoyceCode, nogame, nogameCode, Splash.Silk
├── TestbedMain.cs                # Entry point: world gen, then DES or continuous mode
├── ClusterViewer.cs              # IViewer that covers all fragments in a target cluster
│
├── spatial/                      # Spatial model extracted from generated world
│   ├── SpatialModel.cs           # Locations, routes, travel times — derived from ClusterDesc data
│   ├── Location.cs               # A place: building, shop, street segment, home, workplace
│   └── Route.cs                  # Connection between locations with travel time and street segments
│
├── des/                          # Discrete Event Simulation (= production Tier 3 system)
│   ├── EventQueue.cs             # Priority queue (min-heap) ordered by game time
│   ├── NpcSchedule.cs            # Current storylet, destination, departure/arrival times
│   ├── EncounterResolver.cs      # Probabilistic encounter computation from overlapping schedules
│   └── DesSimulation.cs          # Main DES loop: pop event → advance story → compute encounters → push next
│
├── logging/                      # Output and analysis
│   ├── InteractionGraphLogger.cs # Records interaction graph (nodes + edges)
│   ├── SimulationMetrics.cs      # Aggregates per-day statistics
│   └── TextTraceLogger.cs        # Streaming text trace output
│
└── TestbedRootModule.cs          # Stripped module tree for world generation phase
```

---

## Bootstrap Sequence

Modeled on `DesktopMain.cs` (lines 116-238), stripping renderer-dependent steps:

```
1.  GlobalSettings: set resource path, GL version (for headless platform init)
2.  Register TextureCatalogue (operators reference materials even headlessly)
3.  Register casette.Loader with real nogame.json config
4.  AssetImplementation.WithLoader() — filesystem asset access
5.  Loader.InterpretConfig() — parse implementations, global settings
6.  Platform.EasyCreateHeadless() — creates Engine without window
7.  engine.ExecuteLogicalThreadOnly() — logical thread, no render loop
8.  engine.CallOnPlatformAvailable() — unlock frame scheduling
9.  Register ConsoleLogger
10. Skip: audio (Boom.ISoundAPI), window creation, icon loading
11. Loader.StartGame() — activates root module (TestbedRootModule)
```

### TestbedRootModule

A stripped variant of `nogame.Main` that activates only:

| Module | Why |
|--------|-----|
| `nogame.config.Module` | Mix config, properties |
| `nogame.modules.World` | MetaGen, Loader, fragment loading |
| `nogame.modules.daynite.Controller` | Game time (with accelerated `RealSecondsPerGameDay`) |
| `engine.behave.SpawnController` | Character spawning |
| TALE narrative module (Phase 1+) | Story graph ticking, interaction pool |

**Excluded**: `ScreenComposer`, `InputEventPipeline`, `InputController`, `AutoSave`, UI modules, quest log UI, audio modules, map/satnav rendering.

---

## Operator Pipeline

All structural operators run. The metaGen config (`nogame.metaGen.json`) executes the same tree. The full dependency chain:

### World Building Operators (run once at startup via `MetaGen.SetupComplete()`)

| Operator | Keep | Reason |
|----------|------|--------|
| `GenerateClustersOperator` | Yes | Creates cluster list — foundation of everything |
| `GenerateTracksOperator` | Yes | Intercity road network |
| `GenerateNavMapOperator` | Yes | Navigation mesh for pathfinding |

### Fragment Operators (run per fragment via `MetaGen.ApplyFragmentOperators()`)

| Operator | Keep | Reason |
|----------|------|--------|
| `CreateTerrainOperator` | Yes | Terrain elevation — required by all placement |
| `CreateTerrainMeshOperator` | Yes | Terrain mesh data (operators may depend on fragment mesh state) |
| `GenerateClusterStreetsOperator` | Yes | Triggers `ClusterDesc._triggerStreets()` which generates streets, quarters, buildings, and shopfronts. **This is where the entire spatial data model is created.** |
| `GenerateHousesOperator` | Yes | Building 3D geometry. Shopfront data exists before this runs (created by `QuarterGenerator` inside `_triggerStreets()`), but this operator reads shopfront tags to apply building types. Run it for fidelity. |
| `GenerateClusterQuartersOperator` | Yes | Quarter floor geometry |
| `GenerateClusterStreetAnnotationsOperator` | Optional | Street signs/markings — pure visual, safe to skip via config condition |
| `GenerateTreesOperator` | Optional | Visual — safe to skip via config condition |
| `GeneratePolytopeOperator` | Optional | Visual — safe to skip via config condition |
| `cubes/tram GenerateCharacterOperator` | Optional | Non-narrative characters — skip for TALE testing |
| `niceday.FragmentOperator` | Yes | NPC placement — essential |

### Cluster Operators (run once per cluster via `MetaGen.ApplyClusterOperators()`)

| Operator | Keep | Reason |
|----------|------|--------|
| `GenerateShopsOperator` | Yes | Tags shopfronts with shop types (Eat, Drink, Game2) — NPC destinations derive from this |

### Populating Operators (run once via `MetaGen.Populate()`)

| Operator | Keep | Reason |
|----------|------|--------|
| `GenerateCharacterOperator` (intercity) | Optional | Intercity traffic — keep if testing cross-cluster encounters |

### Skipping Visual Operators

Visual-only operators can be disabled via their existing `configCondition` flags (set to `false` in testbed config overlay):
- `nogame.CreateTrees` → false
- `nogame.CreatePolytopes` → false
- `world.CreateStreetAnnotations` → false
- `world.CreateCubeCharacters` → false
- `world.CreateTramCharacters` → false

---

## Discrete Event Simulation (Tier 3 Model)

This is the core innovation: treat all NPCs as Tier 3 background characters. No per-frame simulation. Time jumps from event to event. This is both the testbed's fast mode AND the production system for simulating background NPCs in the real game.

### Why This Works

The NPC_STORIES_DESIGN.md already specifies Tier 3 behavior: *"NPCs exist only as strategy state + current location pointer. No tube ticking — position is computed on demand from schedule. 'NPC #4837 left home at 8:00, commute takes 12 minutes, so at 8:07 they're 58% along tube X.'"*

This means every NPC's position at any point in time is a **pure function** of their schedule. We don't need to tick 60 frames per second to know where they are — we compute it analytically.

### Spatial Model

After world generation, extract a static spatial model from `ClusterDesc`:

```
SpatialModel:
  locations[]:           # Derived from generated world data
    - id, type (home|workplace|shop|social_venue|street_segment)
    - position (world coordinates)
    - capacity (how many NPCs fit comfortably)
    - quarter, estate, building references
  routes[]:              # Derived from StrokeStore street graph
    - origin_location, destination_location
    - street_segments[] (sequence of StreetPoints/Strokes traversed)
    - travel_time (computed from distance + speed)
  shops[]:               # From GenerateShopsOperator tagging
    - location, shop_type (Eat, Drink, Game2)
```

Sources:
- `ClusterDesc.QuarterStore()` → quarters → estates → buildings → shopfronts (locations)
- `ClusterDesc.StrokeStore()` → street points → strokes (routes, travel times)
- `GenerateShopsOperator` output → tagged shopfronts (shop locations by type)

### Event Queue

A min-heap priority queue ordered by game time. Each event represents a story node transition:

```
Event:
  gameTime: DateTime          # When this event fires
  npcId: int                  # Which NPC
  eventType: enum             # NodeArrival, InterruptCheck, EncounterResolution
  data: object                # Storylet-specific payload
```

### DES Main Loop

```
1. Initialize: for each NPC, generate starting storylet, compute first node arrival time, push event
2. Loop:
   a. Pop earliest event from queue
   b. Advance simulation clock to event.gameTime
   c. Process event:
      - NodeArrival: NPC reached next story node
        → Evaluate postconditions (mutate properties)
        → Select next storylet based on preconditions + properties
        → Compute spatial verb: destination + travel time
        → Schedule next NodeArrival at (now + travel_time + activity_duration)
        → Register NPC's time-space window for encounter detection
      - EncounterCheck: periodic sweep for overlapping schedules
        → For each location, find NPCs with overlapping time windows
        → Roll probabilistic encounter based on location type, relationship, story state
        → If encounter triggers: create interrupt events for affected NPCs
      - InterruptResolution: an encounter produced a story interrupt
        → Branch NPC's story graph (nest/replace/parallel per interrupt scope)
        → Recompute next NodeArrival for interrupted NPC
   d. Push resulting events back into queue
   e. If simulation clock > target end time, stop
```

### Probabilistic Encounter Computation

Instead of checking co-location every frame, compute encounters analytically from overlapping **time-space windows**:

```
NPC A: at location L from T1 to T2 (stay_at verb)
NPC B: at location L from T3 to T4 (stay_at verb)
Overlap: max(T1,T3) to min(T2,T4)

If overlap > 0:
  overlap_duration = min(T2,T4) - max(T1,T3)
  P(encounter) = 1 - (1 - p_location)^(overlap_duration / time_quantum)
  where p_location depends on location type and time_quantum is ~15 game-minutes
```

For **transit encounters** (two NPCs sharing a street segment during travel):

```
NPC A: traversing route R_a from T1 to T2
NPC B: traversing route R_b from T3 to T4
Shared segments: R_a ∩ R_b

For each shared segment S:
  A passes through S during [T1 + offset_a, T1 + offset_a + segment_time_a]
  B passes through S during [T3 + offset_b, T3 + offset_b + segment_time_b]
  Compute temporal overlap on S
  P(encounter on S) = f(overlap, segment_type)
```

This is exact where per-frame co-location checking is a noisy approximation. The DES computes the *ground truth* encounter probability.

### NPC Schedule Assignment

Each NPC needs an initial daily schedule derived from their seed and the spatial model:

```
NPC seed → deterministic role (worker, merchant, socialite, etc.)
Role → schedule template:
  06:00-07:00  wake_up     at home
  07:00-07:30  commute     home → workplace (route computed from street graph)
  07:30-12:00  work        at workplace
  12:00-12:30  lunch       at nearby shop(type=Eat)
  12:30-17:00  work        at workplace
  17:00-17:30  commute     workplace → social_venue OR home
  17:30-20:00  socialize   at social_venue (or rest at home)
  20:00-20:30  commute     → home
  20:30-06:00  sleep       at home
```

Home assignment: pick a building in a residential quarter (based on NPC seed).
Workplace assignment: pick a building/shop matching NPC role.
Social venue: pick from shops/buildings with social attributes.

All derived deterministically from seed + spatial model. No randomness at assignment time — the seed IS the randomness.

### Performance Estimate

Per event: ~1 storylet evaluation + ~1 encounter check = microseconds.
Events per NPC per day: ~8-15 (one per schedule slot + interrupts).
For 500 NPCs × 365 days: ~500 × 12 × 365 = ~2.2M events.
At 1μs per event: **~2 seconds for a simulated year.**

This is 3-4 orders of magnitude faster than the per-frame headless approach.

### Tier 3 → Production Migration

The DES code is not testbed-specific. It is the Tier 3 simulation that runs in the real game:

| DES Component | Production Role |
|---------------|-----------------|
| `NpcSchedule` | Background NPC state for all Tier 3 NPCs |
| `EncounterResolver` | Computes whether a background NPC had a meaningful encounter while off-screen |
| `EventQueue` | Drives background story progression for NPCs outside the active fragment set |
| `SpatialModel` | Shared lookup for NPC pathfinding and location assignment |

When a Tier 3 NPC enters Tier 2 (player approaches), the DES computes their current position from schedule, materializes them at that location, and hands off to the per-frame behavior system. The story graph state is continuous — the NPC doesn't know which tier simulated them.

---

## ClusterViewer: Loading an Entire Cluster

`FixedPosViewer` loads a (2×Range+1)² grid of fragments around a point. For a cluster, we need to cover its full AABB.

`ClusterDesc` provides `Pos` (center), `Size` (side length), and `AABB`. A cluster of `Size=1000` covers `1000/400 ≈ 2.5` fragments per axis. `FixedPosViewer` with `Range=3` covers 7×7=49 fragments — sufficient for any single cluster.

For larger clusters or to be exact, a custom `ClusterViewer : IViewer` computes fragment indices directly from `ClusterDesc.AABB`:

```csharp
public class ClusterViewer : IViewer
{
    private ClusterDesc _cluster;

    public void GetVisibleFragments(ref IList<FragmentVisibility> lsVisib)
    {
        var aabb = _cluster.AABB;
        int iMin = (int)MathF.Floor(aabb.AA.X / MetaGen.FragmentSize);
        int iMax = (int)MathF.Ceiling(aabb.BB.X / MetaGen.FragmentSize);
        int kMin = (int)MathF.Floor(aabb.AA.Z / MetaGen.FragmentSize);
        int kMax = (int)MathF.Ceiling(aabb.BB.Z / MetaGen.FragmentSize);

        for (int k = kMin; k <= kMax; k++)
            for (int i = iMin; i <= iMax; i++)
                lsVisib.Add(new() {
                    How = (byte)(FragmentVisibility.Visible3dNow),
                    I = (short)i,
                    K = (short)k
                });
    }
}
```

This ensures `SpawnController` sees all cluster fragments as `Visible3dNow` and populates them via `CitizenSpawnOperator`.

**Fragment persistence**: The `ClusterViewer` stays registered permanently, preventing `Loader._purgeFragments()` from unloading cluster fragments during simulation.

---

## Time Management

### DES Mode (Primary)

The DES has its own simulation clock — a simple `DateTime` variable that jumps to the next event's timestamp. No relationship to wall-clock time. No engine frame loop. Time advances as fast as events can be processed.

The engine is only used during the world generation bootstrap phase. After spatial model extraction, the engine loop is not started (or is stopped). The DES runs in a tight `while` loop on the main thread.

### Continuous Headless Mode (Validation)

For the secondary validation mode, the engine runs headlessly with time acceleration. The engine has two independent clocks:

1. **Engine dt** — fixed 1/60s per logical frame. Controls physics, animation, behavior.
2. **Game time** — `daynite.Controller` maps wall-clock time to game time via `RealSecondsPerGameDay` (default 1800s).

Acceleration options for continuous mode:
- **Moderate**: `RealSecondsPerGameDay = 120f` (2 real minutes per game day). Physical movement has time to complete. Use for validating that DES encounter probabilities match spatial reality.
- **Real-time headless**: `RealSecondsPerGameDay = 1800f`. Full fidelity. Use for correctness validation of strategy executors.

The engine loop sleeps when ahead of the 60 FPS target. In headless mode, set `engine.NailLogicalFPS = false` or modify `_logicalThreadFunction()` to skip the wait.

---

## Spatial Encounter Probability Model

The key question the testbed answers empirically. But a theoretical model helps set initial parameters:

### Co-location Rate

For a cluster of N NPCs with K locations, at time slot t:
- Each NPC occupies one location (determined by their storylet's spatial verb)
- **Occupancy** at location L: `n_L(t)` = number of NPCs at L at time t
- **Pairwise encounter opportunities** at L: `n_L × (n_L - 1) / 2`

Locations are not uniform. Commute corridors at rush hour concentrate NPCs:
- **Residential** (morning/evening): ~5-15 NPCs per fragment
- **Workplace** (daytime): ~10-30 per workplace cluster
- **Social venues** (evening): ~5-20 per venue
- **Transport corridors** (commute): ~50-200 per major road

### Interaction Probability Per Encounter

Not every co-location is an interaction. The probability depends on:

| Factor | Effect |
|--------|--------|
| **Location type** | Venue (high) > street (medium) > transport (very low) |
| **Existing relationship** | Known NPCs interact more readily |
| **Story state** | NPC with open interrupt slot more receptive |
| **Property compatibility** | Matching needs (hungry NPC + food vendor) |

A reasonable starting model:
- `P(interaction | venue co-location)` = 0.05-0.10
- `P(interaction | street co-location)` = 0.01-0.02
- `P(interaction | transport co-location)` = 0.001-0.005

### Expected Interrupts Per Day

For a cluster of 500 NPCs, ~100 locations, 16 waking hours, ~64 time slots:
- Average venue occupancy: 10 NPCs → 45 pairs × 0.07 ≈ 3 potential interactions per slot
- Per NPC per day (venue time ≈ 25% of day): ~0.5-1.5 meaningful interactions
- Street encounters add ~0.3-0.5

**Target**: 1-3 meaningful interrupts per NPC per day. The testbed validates this empirically and lets us sweep the probability parameters.

---

## Testbed Output

The testbed produces three layers of output, designed for both human review and automated iteration by Claude Code.

### Layer 1: Structured Metrics (machine-readable)

Written to `stdout` as JSON. This is the primary input for automated parameter tuning.

```json
{
  "run_id": "20260313_143022_seed42",
  "config": {
    "cluster_index": 0,
    "npc_count": 500,
    "days_simulated": 365,
    "seed": 42,
    "encounter_probabilities": {
      "venue": 0.07,
      "street": 0.015,
      "transport": 0.002,
      "workplace": 0.04
    },
    "interaction_thresholds": {
      "greet_min_trust": 0.3,
      "help_min_trust": 0.5,
      "conflict_max_trust": 0.3,
      "claim_eagerness": 0.6
    }
  },
  "metrics": {
    "routine_completion_rate": 0.84,
    "interrupts_per_day": {
      "mean": 2.1,
      "median": 2,
      "std": 1.4,
      "p5": 0,
      "p95": 5
    },
    "interactions_total": 383250,
    "interactions_by_type": {
      "greet": 201000,
      "trade": 52000,
      "chat": 89000,
      "argue": 12500,
      "threaten": 3200,
      "help": 25550
    },
    "request_fulfillment_rate": 0.92,
    "requests_timed_out": 4120,
    "graph": {
      "nodes": 500,
      "edges": 18420,
      "largest_component_fraction": 0.88,
      "clustering_coefficient": 0.42,
      "degree_distribution_gini": 0.38,
      "max_degree": 147,
      "mean_degree": 73.7
    },
    "properties": {
      "hunger":    { "mean": 0.48, "std": 0.15, "min": 0.0, "max": 1.0, "mean_daily_range": 0.55 },
      "fatigue":   { "mean": 0.41, "std": 0.18, "min": 0.0, "max": 0.95, "mean_daily_range": 0.65 },
      "wealth":    { "mean": 0.44, "std": 0.22, "min": 0.02, "max": 0.98, "mean_daily_range": 0.12 },
      "trust_avg": { "mean": 0.45, "std": 0.12 },
      "anger_avg": { "mean": 0.18, "std": 0.14 }
    },
    "role_breakdown": {
      "worker":    { "count": 200, "completion_rate": 0.89, "mean_interrupts": 1.6, "mean_wealth": 0.52 },
      "merchant":  { "count": 100, "completion_rate": 0.76, "mean_interrupts": 3.1, "mean_wealth": 0.61 },
      "socialite": { "count": 100, "completion_rate": 0.82, "mean_interrupts": 2.4, "mean_wealth": 0.35 },
      "drifter":   { "count": 100, "completion_rate": 0.91, "mean_interrupts": 1.2, "mean_wealth": 0.18 }
    },
    "escalation": {
      "first_conflict_day": 18,
      "first_gang_formation_day": null,
      "first_protection_racket_day": null,
      "first_authority_investigation_day": 45,
      "npcs_in_escalation_at_end": 23,
      "escalation_fraction": 0.046
    },
    "location_hotspots": [
      { "location_id": "shop_east_42", "type": "shop", "total_encounters": 1240 },
      { "location_id": "bar_central_7", "type": "social_venue", "total_encounters": 980 },
      { "location_id": "street_seg_1104", "type": "street", "total_encounters": 2100 }
    ]
  },
  "warnings": [
    "merchant completion_rate 0.76 below target minimum 0.80",
    "drifter mean_interrupts 1.2 at lower bound of target range"
  ],
  "pass": false
}
```

### Layer 2: Sample Traces (human + AI readable)

Written to a file (`traces.log`). A configurable number of sample NPCs (default 5, one per role + one random) get their full daily trace logged for qualitative review:

```
=== npc_023 (worker, seed=8847) — Day 1 ===
[06:15] node_arrival → "wake_up" (fatigue=0.08, hunger=0.52)
  verb: stay_at(home_qtr4_est12, 45min, "morning_routine")
  post: fatigue -0.05, hunger +0.08
[07:00] node_arrival → "commute" (fatigue=0.03, hunger=0.60)
  verb: use_transport(home_qtr4_est12, garage_qtr2_est5, walk)
  route: 3 segments, 14min travel
[07:14] node_arrival → "work_manual" (fatigue=0.03, hunger=0.60)
  verb: stay_at(garage_qtr2_est5, 4h50min, "working")
  post: fatigue +0.28, wealth +0.08
[12:04] node_arrival → "lunch_break" (fatigue=0.31, hunger=0.82)
  verb: go_to(shop_eat_qtr2_17) → stay_at(shop_eat_qtr2_17, 25min, "eating")
  post: hunger -0.55, wealth -0.03
  ** encounter: npc_112 (merchant) at shop_eat_qtr2_17 — greet (trust 0.35→0.39)
[12:29] node_arrival → "work_manual" (fatigue=0.33, hunger=0.27)
  ...
```

### Layer 3: Event Log (complete simulation history)

Written to `events.jsonl` — one JSON object per line (JSON Lines format). This is the authoritative record of everything that happened. Streamable, greppable via `jq`, and the same format the production game uses to persist Tier 3 NPC history.

#### Event Types

Every event in the simulation produces one log line. The common envelope:

```json
{"t":"2026-06-15T14:30:00","day":3,"npc":47,"evt":"<type>", ...type-specific fields... }
```

**`npc_created`** — NPC was initialized at simulation start (one per NPC, always the first event):

```json
{"t":"2026-06-13T00:00:00","day":0,"npc":47,"evt":"npc_created",
 "seed":8847,"role":"worker",
 "home":"home_qtr4_est12","workplace":"garage_qtr2_est5",
 "social_venues":["bar_central_7","shop_eat_qtr2_17"],
 "quarter":"qtr4","estate":"est12",
 "props":{"hunger":0.5,"fatigue":0.5,"wealth":0.4,"anger":0.1,"fear":0.1,"trust":0.5,"happiness":0.5,"health":0.9,"reputation":0.3},
 "schedule_template":"worker_standard"}
```

**`node_arrival`** — NPC reached a story node and selected the next storylet:

```json
{"t":"2026-06-15T14:30:00","day":3,"npc":47,"evt":"node_arrival",
 "storylet":"lunch_break","role":"worker",
 "verb":"stay_at","verb_params":{"location":"shop_eat_qtr2_17","duration_min":25,"hint":"eating"},
 "props":{"hunger":0.82,"fatigue":0.31,"wealth":0.55,"anger":0.1,"trust_avg":0.42},
 "post":{"hunger":-0.55,"wealth":-0.03}}
```

**`encounter`** — Two NPCs interacted (co-location triggered an interaction):

```json
{"t":"2026-06-15T14:35:00","day":3,"npc":47,"evt":"encounter",
 "other":112,"interaction":"greet","location":"shop_eat_qtr2_17",
 "location_type":"shop","trigger":"co_location",
 "npc_props_before":{"trust_112":0.35,"anger":0.1},
 "npc_props_after":{"trust_112":0.39,"anger":0.1},
 "other_props_before":{"trust_47":0.30},
 "other_props_after":{"trust_47":0.34},
 "relationship_before":"acquaintance","relationship_after":"acquaintance"}
```

**`request_emitted`** — NPC posted a request to the interaction pool:

```json
{"t":"2026-06-15T18:00:00","day":3,"npc":47,"evt":"request_emitted",
 "request_id":"req_3_47_001","request_type":"food_delivery",
 "location":"home_qtr4_est12","urgency":0.7,
 "timeout_minutes":60,"storylet_context":"evening_hungry"}
```

**`request_claimed`** — Another NPC picked up the request:

```json
{"t":"2026-06-15T18:12:00","day":3,"npc":112,"evt":"request_claimed",
 "request_id":"req_3_47_001","requester":47,
 "request_type":"food_delivery","claimed_from":"interaction_pool",
 "npc_storylet_interrupted":"close_shop","interrupt_scope":"nest"}
```

**`request_resolved`** — A request was fulfilled, timed out, or cancelled:

```json
{"t":"2026-06-15T18:40:00","day":3,"npc":112,"evt":"request_resolved",
 "request_id":"req_3_47_001","requester":47,"outcome":"fulfilled",
 "duration_minutes":28,
 "npc_props_after":{"wealth":0.65},"requester_props_after":{"hunger":0.25,"wealth":0.50}}
```

```json
{"t":"2026-06-15T19:00:00","day":3,"npc":47,"evt":"request_resolved",
 "request_id":"req_3_47_002","outcome":"timed_out",
 "fallback_storylet":"eat_stale_food"}
```

**`interrupt`** — An NPC's current storylet was interrupted:

```json
{"t":"2026-06-15T14:36:00","day":3,"npc":112,"evt":"interrupt",
 "interrupted_storylet":"serve_customers","interrupt_source":"encounter",
 "other":47,"scope":"nest","priority":5,
 "new_storylet":"chat_at_venue","return_to":"serve_customers"}
```

**`resume`** — NPC returned to a previously interrupted storylet:

```json
{"t":"2026-06-15T14:50:00","day":3,"npc":112,"evt":"resume",
 "resumed_storylet":"serve_customers","was_interrupted_by":"chat_at_venue",
 "time_lost_minutes":14}
```

**`escalation`** — A high-threshold storylet fired (Phase 5 content):

```json
{"t":"2026-09-22T16:00:00","day":99,"npc":203,"evt":"escalation",
 "escalation_type":"demand_protection","target":112,
 "preconditions_met":{"wealth":0.82,"reputation":0.71,"trust_112":0.15},
 "outcome":"pending"}
```

**`relationship_changed`** — Relationship tier crossed a threshold:

```json
{"t":"2026-07-10T20:15:00","day":28,"npc":47,"evt":"relationship_changed",
 "other":112,"old_tier":"acquaintance","new_tier":"friend",
 "trust":0.72,"interaction_count":34}
```

**`day_summary`** — Emitted once per NPC at end of each simulated day (compact state snapshot):

```json
{"t":"2026-06-15T23:59:59","day":3,"npc":47,"evt":"day_summary",
 "role":"worker","location":"home_qtr4_est12",
 "storylets_completed":8,"storylets_interrupted":1,
 "encounters":3,"requests_emitted":1,"requests_claimed":0,
 "props":{"hunger":0.35,"fatigue":0.15,"wealth":0.52,"anger":0.08,"happiness":0.6},
 "relationships":{"112":{"trust":0.39,"tier":"acquaintance","interactions_today":1},
                   "88":{"trust":0.55,"tier":"friend","interactions_today":2}}}
```

#### Schema Summary

| Event Type | When | Key Data |
|-----------|------|----------|
| `npc_created` | Simulation start (once per NPC) | Seed, role, home, workplace, social venues, initial properties, schedule |
| `node_arrival` | Each story node transition | Storylet selected, verb, full property snapshot, postconditions |
| `encounter` | Two NPCs interact | Both NPCs' property deltas, relationship change, location context |
| `request_emitted` | Storylet postcondition emits request | Request type, location, urgency, timeout |
| `request_claimed` | NPC picks up request | Who claimed, what was interrupted, scope |
| `request_resolved` | Request fulfilled/timed out/cancelled | Outcome, duration, property effects |
| `interrupt` | Storylet interrupted by external event | What was interrupted, scope, new storylet |
| `resume` | Return from nested interrupt | What resumed, time lost |
| `escalation` | High-threshold storylet fires | Type, target, preconditions that triggered it |
| `relationship_changed` | Trust crosses tier boundary | Old/new tier, trust value, interaction count |
| `day_summary` | End of each simulated day | Compact daily snapshot: props, relationships, counts |

#### Acceptance Criterion: Character Biography Test

The event log must contain enough information — implicitly or explicitly — to reconstruct a **complete narrative report from any single NPC's perspective** by filtering `events.jsonl` to that NPC's id. Specifically, filtering for `npc==X` (plus `encounter` events where `other==X`) must answer:

| Question | Answerable From |
|----------|----------------|
| Who is this character? (role, home, workplace) | `npc_created` |
| What did they do each day? | `node_arrival` sequence |
| Where were they at any point in time? | `node_arrival.verb_params.location` + verb duration |
| Who do they know, and how well? | `encounter` events + `relationship_changed` + `day_summary.relationships` |
| What did they ask for? What did others ask of them? | `request_emitted` + `request_claimed` |
| What disrupted their routine? | `interrupt` + `resume` |
| How did their personality/state evolve? | `node_arrival.props` (snapshot at each transition) + `day_summary.props` |
| What significant events shaped them? | `escalation` + `relationship_changed` |
| What is their current state? | Latest `day_summary` |

A valid test: pipe an NPC's events to Claude and ask it to write a one-page biography. If the biography has to guess or say "unknown" about anything in the table above, the schema has a gap.

#### Volume Estimate

Per NPC per day: ~8-15 `node_arrival` + ~2 `encounter` + ~1 `request_*` + 1 `day_summary` ≈ **~15 events**.
For 500 NPCs × 365 days: **~2.7M lines**.
At ~300 bytes/line average: **~800 MB** uncompressed, **~60 MB** gzip'd.

For quick analysis, use `day_summary` events only (~182K lines, ~55 MB uncompressed). For deep investigation into a specific NPC or interaction chain, grep by `npc` id or `request_id`.

#### Queryable Patterns

```bash
# All encounters for NPC 47
jq 'select(.npc==47 and .evt=="encounter")' events.jsonl

# All escalation events
jq 'select(.evt=="escalation")' events.jsonl

# Daily property trajectory for NPC 47
jq 'select(.npc==47 and .evt=="day_summary") | {day, props}' events.jsonl

# All timed-out requests
jq 'select(.evt=="request_resolved" and .outcome=="timed_out")' events.jsonl

# Relationship history between NPC 47 and NPC 112
jq 'select((.npc==47 or .npc==112) and .evt=="encounter" and (.other==112 or .other==47))' events.jsonl

# NPCs who became friends (relationship_changed to "friend" tier)
jq 'select(.evt=="relationship_changed" and .new_tier=="friend")' events.jsonl

# Interaction graph edges (for graph tool import)
jq 'select(.evt=="encounter") | {source:.npc, target:.other, day, type:.interaction}' events.jsonl
```

#### Production Reuse

This event log format is not testbed-specific. In the real game:
- **Tier 3 NPCs** accumulate `day_summary` events as their compressed history. When a Tier 3 NPC enters Tier 2, the recent `day_summary` entries provide enough context for dialogue ("I've been busy at work" / "I had a fight with NPC 88 last week").
- **Save/load**: The `day_summary` stream is the NPC's persistent memory. Serialized with the save file. Full event detail is transient — only the summaries survive across sessions.
- **Player-facing narrative**: When the player asks an NPC "what have you been up to?", the game reads recent `day_summary` and `encounter`/`escalation` events to generate contextual dialogue.

### Layer 4: Relationship Graph Snapshot

Written to `graph.json` at simulation end. This is the **final state** of the social graph — derived from the event log but convenient for graph visualization tools:

```json
{
  "snapshot_day": 365,
  "nodes": [
    { "id": 23, "seed": 8847, "role": "worker", "quarter": "qtr4",
      "props": { "hunger": 0.45, "wealth": 0.56, "reputation": 0.48 },
      "total_encounters": 892, "total_requests_emitted": 156,
      "escalation_events": 0, "degree": 47 }
  ],
  "relationships": [
    { "a": 23, "b": 112, "trust_ab": 0.72, "trust_ba": 0.68,
      "tier": "friend", "total_interactions": 89,
      "interactions_by_type": { "greet": 52, "trade": 18, "chat": 14, "help": 5 },
      "first_interaction_day": 1, "last_interaction_day": 362 }
  ]
}
```

Note: `relationships` are **bidirectional** — trust can be asymmetric (A trusts B more than B trusts A). Each relationship entry stores both directions. Only pairs that have interacted at least once appear.

---

## Target Metrics

Stored in a configuration file (`testbed_targets.json`) so the testbed can self-evaluate and Claude Code can compare programmatically:

```json
{
  "routine_completion_rate": { "min": 0.80, "max": 0.90 },
  "interrupts_per_day_mean": { "min": 1.0, "max": 3.0 },
  "request_fulfillment_rate": { "min": 0.90 },
  "largest_component_fraction": { "min": 0.80 },
  "clustering_coefficient": { "min": 0.30, "max": 0.60 },
  "degree_distribution_gini": { "min": 0.25, "comment": "some inequality = emerging hubs" },
  "property_mean_daily_range": {
    "hunger": { "min": 0.40, "comment": "hunger should cycle meaningfully each day" },
    "fatigue": { "min": 0.50, "comment": "fatigue should cycle wake-to-sleep" }
  },
  "role_completion_rate_min": {
    "all_roles": 0.75,
    "comment": "no role should have completion rate below this — indicates broken content"
  },
  "escalation_first_conflict_day": { "max": 30, "comment": "conflicts should emerge within a month" },
  "escalation_fraction_at_day_180": { "min": 0.03, "max": 0.15, "comment": "3-15% of NPCs in escalation — not too quiet, not total chaos" }
}
```

The testbed compares its output against these targets and:
1. Prints `"pass": true/false` in the metrics JSON
2. Populates the `"warnings"` array with specific violations
3. Returns exit code 0 (pass) or 1 (fail) — usable in CI or automated loops

---

## Automated Iteration with Claude Code

The testbed is designed as a closed-loop system that Claude Code can operate:

### The Loop

```
1. Claude reads testbed_targets.json (knows what "good" looks like)
2. Claude reads current storylet library + encounter parameters
3. Claude runs: dotnet run --project Testbed -- --days 365 --seed 42
4. Claude reads metrics JSON from stdout
5. Claude checks "pass" field and "warnings" array
6. If pass: done (or run with different seeds to confirm stability)
7. If fail: Claude diagnoses from warnings + sample traces:
   - "merchant completion_rate 0.76" → read merchant traces
     → merchants interrupted too often at shop locations
     → reduce encounter_probabilities.workplace from 0.04 to 0.025
   - "drifter mean_interrupts 1.2" → drifters have too few social storylets
     → add "beg" and "scavenge_together" interaction storylets
8. Claude edits storylet JSON / parameter files
9. Go to step 3
```

### What Claude Adjusts

| Adjustment Type | Files | Example |
|----------------|-------|---------|
| Encounter probabilities | `testbed_params.json` | Lower venue probability from 0.07 to 0.05 |
| Storylet preconditions | `storylets/*.json` | Change `work_manual` hunger threshold from 0.8 to 0.9 |
| Storylet postconditions | `storylets/*.json` | Reduce anger accumulation from argue: 0.2 → 0.15 |
| Property mutation rates | `storylets/*.json` | Slow hunger growth: +0.08/hour → +0.06/hour |
| Role schedule templates | `roles/*.json` | Give merchants a longer lunch break (less time at shop = fewer interruptions) |
| New storylets | `storylets/*.json` | Add `evening_stroll` for merchants with nothing to do after shop closes |
| Interaction thresholds | `testbed_params.json` | Lower greet_min_trust from 0.3 to 0.2 (more acquaintances form) |

### Stability Validation

A single seed can produce misleading results. After finding parameters that pass on seed 42, Claude runs multiple seeds:

```bash
for seed in 42 137 256 999 1337; do
  dotnet run --project Testbed -- --days 365 --seed $seed
done
```

All seeds must pass. If one fails, the parameters are overfit to a specific world layout — adjust and re-sweep.

### CLI Interface

```
dotnet run --project Testbed -- [options]

Options:
  --days N              Simulated days (default: 365)
  --seed N              World + NPC seed (default: 42)
  --cluster N           Cluster index to simulate (default: 0, largest)
  --npcs N              Override NPC count (default: from spawn operators)
  --params FILE         Parameter file (default: testbed_params.json)
  --targets FILE        Target metrics file (default: testbed_targets.json)
  --traces N            Number of sample NPCs to trace (default: 5)
  --trace-file FILE     Trace output file (default: traces.log)
  --graph-file FILE     Graph output file (default: graph.json)
  --quiet               Suppress trace output, only emit metrics JSON to stdout
  --batch FILE          Batch mode: read parameter sweep from FILE, output CSV
```

In quiet mode, the only stdout is the metrics JSON — ideal for Claude Code to parse.

---

## Phased Delivery

### Testbed Phase A — World Generation & Spatial Model (before TALE Phase 1)

**Goal**: Headless engine boots, generates a cluster, extracts the spatial model.

- Create `Testbed` console project with headless bootstrap
- Implement `ClusterViewer` (load all fragments in one cluster)
- Run full operator pipeline: terrain, streets, buildings, shops
- Extract `SpatialModel` from generated `ClusterDesc` data (locations, routes, travel times)
- Assign NPC home/workplace/social locations from seed + spatial model
- Output: cluster statistics (fragment count, location count, building count, shop count, street point count, route count)

**Deliverable**: `dotnet run --project Testbed` prints spatial model stats and exits.

### Testbed Phase B — Discrete Event Simulation (parallel with TALE Phase 1)

**Goal**: NPCs advance through story nodes via the DES event queue, producing text traces.

- Implement `EventQueue`, `NpcSchedule`, `DesSimulation`
- Implement `EncounterResolver` with probabilistic co-location computation
- Integrate TALE storylet library and `NpcNarrativeState` (story graph from Phase 1)
- Implement interaction graph logger and text trace output
- Run simulation for N game-days, dump graph + metrics
- Validate: seed determinism (same seed → same trace), plausible daily routines

**Deliverable**: Run 1 simulated year (500 NPCs), produce interaction graph + metrics in seconds.

### Testbed Phase C — Probability Tuning (parallel with TALE Phase 2-3)

**Goal**: Sweep interaction probability parameters, find sweet spot.

- Add CLI parameters for probability knobs (interrupt threshold, request generation rate, per-location-type encounter probability)
- Batch mode: run M configurations × N days each
- Output: CSV of metrics per configuration
- Find parameter region where routine-completion-rate is 80-90% AND degree-distribution shows power-law tail
- Validate that emergent structures form: repeated interaction cliques, power concentration, faction-like groupings

**Deliverable**: Parameter recommendations backed by empirical data. Visualizable interaction graphs.

### Testbed Phase D — Continuous Spatial Validation (parallel with TALE Phase 2)

**Goal**: Validate DES encounter probabilities against per-frame spatial simulation.

- Run the headless continuous engine with NPCs physically moving via strategy executors
- Measure actual per-frame co-location rates
- Compare against DES probabilistic predictions
- Calibrate the DES encounter probability table so Tier 3 predictions match Tier 2 reality
- Validate that transport corridors don't produce unrealistic encounter spikes

**Deliverable**: Calibrated encounter probability table. Confirmation that DES and continuous simulation produce statistically equivalent interaction graphs.

---

## Relationship to TALE Phases

| TALE Phase | Testbed Phase | Interaction |
|------------|---------------|-------------|
| Phase 1 (Single NPC story) | B | Testbed DES is the **primary validation environment** for Phase 1. The text trace specified in TALE Phase 1 is produced by the DES, not the game. |
| Phase 2 (Story-to-strategy) | D | DES validates story logic at speed. Continuous mode validates spatial behavior. Game validates visual fidelity. |
| Phase 3 (NPC-NPC interaction) | B, C | Testbed is the **only practical environment** for tuning interaction pool probabilities at scale. 500 NPCs × 1 year in seconds vs. hours. |
| Phase 4 (Player intersection) | — | Player interaction tested in the real game. Testbed has no player. |
| Phase 5 (Branching & interrupts) | C | Testbed validates interrupt frequency and scope resolution at scale. |

### Testbed as Production Code

The testbed is not a throwaway tool. The DES components (`EventQueue`, `NpcSchedule`, `EncounterResolver`, `SpatialModel`) are the **production Tier 3 simulation** that ships in the game. The testbed is a harness that runs Tier 3 for all NPCs simultaneously. The game runs Tier 3 for background NPCs while rendering Tier 1/2 NPCs normally.

This means:
- DES code lives in **JoyceCode or nogameCode**, not in the Testbed project
- The Testbed project is a thin driver: world generation → spatial model extraction → DES execution → logging
- Every parameter change or storylet library update is validated by running the testbed before testing in-game
- The DES is the **continuous integration environment** for the TALE system
