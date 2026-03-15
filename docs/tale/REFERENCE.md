# TALE Reference — Shared Concepts

Read this file alongside any phase file. It defines concepts referenced across all phases.

---

## Base Entity Properties

Every NPC gets a flat `Dictionary<string, float>` (range 0.0–1.0). No class hierarchy.

| Category | Properties |
|----------|-----------|
| **Emotional** | `anger`, `fear`, `trust`, `happiness` |
| **Physical** | `health`, `fatigue`, `hunger` |
| **Social** | `wealth`, `reputation`, `morality` |

`morality` (0.0–1.0, default ~0.6–0.8) represents how far an NPC will go before resorting to illegitimate means. Drifts downward under sustained desperation; drifts upward through positive social contact.

Specific roles may add entries (e.g., `inventory_stress` for merchants). Trust is per-relationship (`trust_<npcId>`), not global — the base `trust` property is a default/average.

---

## Spatial Verb Alphabet

Every storylet declares a spatial verb — the physical action the NPC performs. The story graph is the brain; the verb is the body.

| Verb | Parameters | Example |
|------|-----------|---------|
| `go_to` | location, speed | Walk to the garage |
| `stay_at` | location, duration, animation_hint | Work at garage for 4h |
| `follow` | target_entity, distance | Follow a friend to the bar |
| `interact_with` | target_entity, interaction_type | Argue with neighbor |
| `use_transport` | origin, destination, transport_type | Take tube from home to work |
| `wait_for` | signal_type, timeout | Wait for pizza delivery |

A storylet may produce a verb sequence: `go_to(garage)` → `stay_at(garage, 4h, "working")`.

---

## Interaction Primitives

NPC-NPC communication via a shared interaction pool (cluster-scoped):

| Primitive | DSL Location | Meaning |
|-----------|-------------|---------|
| **Request** | Postcondition | Emit a typed request into the pool |
| **Wait** | Edge type | Block edge advancement until signal or timeout |
| **Signal** | Postcondition | Notify a request that it was fulfilled/failed |
| **Claim** | Interrupt trigger | Pick up a matching request from the pool |

Flow: NPC A emits request → NPC B claims it → NPC B fulfills → NPC B signals → NPC A advances.

---

## NPC Roles

Roles determine which storylets an NPC can access and their schedule template.

| Role | Character | Schedule Pattern |
|------|-----------|-----------------|
| **Worker** | Regular commute, long work shifts | wake → commute → work → lunch → work → commute → sleep |
| **Merchant** | At shop during business hours | wake → open_shop → serve → close_shop → socialize → sleep |
| **Socialite** | Late riser, frequent venue visits | wake_late → wander → eat_out → socialize → bar → sleep_late |
| **Drifter** | No fixed schedule, wanders | wake_anywhere → scavenge → wander → rest → sleep_anywhere |
| **Authority** | Patrols, responds to crime reports | wake → patrol → investigate → patrol → report → sleep |

Assigned deterministically from NPC seed. Roles are initial tendencies, not permanent — a desperate Worker with collapsed morality may start selecting Drifter or criminal storylets.

---

## Simulation Tiers

From `NPC_STORIES_DESIGN.md`:

| Tier | Range | Simulation | Cost |
|------|-------|------------|------|
| 1 — Visible | ~150m | Full rendering, animation, physics | Dozens |
| 2 — Simulated | Active fragment set (~1.2km) | ECS entities, strategies, per-frame behavior | Hundreds |
| 3 — Background | Rest of world | DES: schedule state + location pointer, no per-frame ticking | Near-zero |

The DES built in Phase 0 IS the Tier 3 system. Same code runs in testbed and production game.

### Tier Transitions

- **3 → 2 → 1 (materialization):** `SpawnController` detects underpopulated visible fragments, `TaleSpawnOperator` looks up existing Tier 3 `NpcSchedule` from `TaleManager` and creates an ECS entity with `TaleEntityStrategy` + visuals.
- **1 → 2 → 3 (dematerialization):** `SpawnController` detects overpopulated fragments, `TaleSpawnOperator` destroys the ECS entity. The `NpcSchedule` remains in `TaleManager` (Tier 3 state persists).
- All transitions are **lossless** — the NPC's persistent data lives on the schedule regardless of tier. Only simulation fidelity scales.

### Cluster Population Lifecycle

NPC populations are **per-cluster**, created on demand:

1. **Cluster activation** (`ClusterCompletedEvent`): Generate `NpcSchedule` objects deterministically from cluster seed. Register with `TaleManager`.
2. **Active cluster**: All tiers running. `TaleSpawnOperator` materializes/dematerializes NPCs based on fragment visibility.
3. **Cluster deactivation**: Drop non-deviated schedules (regenerable from seed). Deviated NPCs persist in save.
4. **Cluster reactivation**: Regenerate from seed, **skip deviated NPC indices**, overlay deviated NPCs from save.

---

## Seed-Based NPC Generation

NPC populations are deterministic from the cluster seed. This enables on-demand generation and regeneration without persisting the full world state.

### Seed Hierarchy

```
World Seed: "mydear"
  → Cluster Seed: "cluster-clusters-mydear-[clusterIndex]"
    → NPC Seed: Hash(clusterSeed, npcIndex)
```

Each NPC's seed is computed **independently** (not sequentially from a shared RNG), so skipping one NPC does not affect any other NPC's generation. This is critical for the deviation skip mask.

### What the Seed Determines

From a single NPC seed, the following are deterministic:
- **Role** (Worker, Merchant, Socialite, Drifter, Authority)
- **Home location** (assigned from cluster's residential locations)
- **Workplace location** (assigned from cluster's work locations)
- **Social venue preferences** (assigned from cluster's social venues)
- **Initial properties** (hunger, wealth, anger, health, morality, reputation — seeded defaults with per-NPC variation)
- **Storylet path** (seed + schedule step → deterministic storylet selection via `StoryletSelector`)

### NPC Count per Cluster

Determined by cluster properties (size, density, building count). The count itself must be deterministic from the cluster seed so that NPC indices are stable across regeneration.

---

## Deviation Tracking & Persistence

### Principle

**Only save what can't be regenerated.** An NPC in primary (algorithmically generated) state can be regenerated from its seed. Only NPCs with player-caused deviations need persistence.

### Deviation States

| State | Player Impact | Persistence | Example |
|-------|--------------|-------------|---------|
| **Unobserved** | None | Not saved | NPC the player never saw |
| **Observed, primary** | Seen but no impact | Not saved | Player asked NPC's name; replacement NPC is indistinguishable |
| **Deviated** | Player changed NPC state | Saved | Player fought NPC, causing property changes |
| **Recursively deviated** | Entangled via deviated NPC | Saved | NPC in a group the player interacted with |

### Save Format

Deviated NPCs are stored as:
- **Seed coordinates**: cluster index + NPC index (to identify which slot)
- **Full NpcSchedule state**: properties, current storylet, arc stack, relationships
- Not the entire world — just the sparse set of player-impacted NPCs

### Regeneration with Deviation Skip Mask

On cluster reactivation:
1. Load deviation list for cluster: "NPC indices {7, 23, 41} are deviated"
2. Run deterministic population generator, **skip those indices**
3. Load deviated NPC schedules from save, inject into `TaleManager`

The skip mask prevents duplicates without cascading seed shifts.

### HasPlayerDeviation Flag

Set on an `NpcSchedule` when a postcondition fires from a player-initiated interaction. Once set, the NPC enters the persistence pool. The flag propagates recursively: if NPC A is deviated and NPC A's group partner NPC B was affected, NPC B is also marked.

---

## Relationship Tiers

NPCs form relationships through repeated interaction:

| Tier | Trust Range | Behavior |
|------|-------------|----------|
| **Stranger** | 0.0–0.2 | No voluntary interaction, may interact if co-located by chance |
| **Acquaintance** | 0.2–0.5 | Greet, brief chat, aware of each other's existence |
| **Friend** | 0.5–0.8 | Ask for help, share information, seek out deliberately |
| **Ally/Rival** | 0.8–1.0 / special | Deep entanglement, mutual obligations, story-driving |

---

## Key Codebase Files

| System | File |
|--------|------|
| Engine entry | `Karawan/DesktopMain.cs` |
| Headless engine | `Splash.Silk/Platform.cs` → `EasyCreateHeadless()` |
| MetaGen pipeline | `JoyceCode/engine/world/MetaGen.cs` |
| Fragment loader | `JoyceCode/engine/world/Loader.cs` |
| Cluster data | `JoyceCode/engine/world/ClusterDesc.cs` |
| Cluster lifecycle events | `JoyceCode/engine/world/WorldEvents.cs` (`ClusterCompletedEvent`) |
| Street generation | `JoyceCode/engine/streets/Generator.cs`, `QuarterGenerator.cs` |
| Buildings | `JoyceCode/engine/streets/Building.cs`, `ShopFront.cs` |
| Shop generation | `nogameCode/nogame/cities/GenerateShopsOperator.cs` |
| Spawn system | `JoyceCode/engine/behave/SpawnController.cs` |
| NPC spawning (legacy) | `nogameCode/nogame/characters/citizen/SpawnOperator.cs` |
| NPC spawning (TALE) | `nogameCode/nogame/characters/citizen/TaleSpawnOperator.cs` |
| TALE strategy | `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` |
| TALE manager | `JoyceCode/engine/tale/TaleManager.cs` |
| TALE module | `nogameCode/nogame/modules/tale/TaleModule.cs` |
| NPC schedule | `JoyceCode/engine/tale/NpcSchedule.cs` |
| Storylet selector | `JoyceCode/engine/tale/StoryletSelector.cs` |
| Entity persistence | `JoyceCode/builtin/EntitySaver.cs` |
| Save hooks | `JoyceCode/engine/Saver.cs` (`OnBeforeSaveGame`, `OnAfterLoadGame`) |
| Creator registry | `JoyceCode/engine/world/CreatorRegistry.cs` |
| Day/night time | `nogameCode/nogame/modules/daynite/Controller.cs` |
| Fixed viewer | `JoyceCode/engine/world/FixedPosViewer.cs` |
| MetaGen config | `models/nogame.metaGen.json` |
| Game config | `models/nogame.json` |
| Seed RNG | `JoyceCode/builtin/tools/RandomSource.cs` |
