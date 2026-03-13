# TALE Reference — Shared Concepts

Read this file alongside any phase file. It defines concepts referenced across all phases.

---

## Base Entity Properties

Every NPC gets a flat `Dictionary<string, float>` (range 0.0–1.0). No class hierarchy.

| Category | Properties |
|----------|-----------|
| **Emotional** | `anger`, `fear`, `trust`, `happiness` |
| **Physical** | `health`, `fatigue`, `hunger` |
| **Social** | `wealth`, `reputation` |

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

Assigned deterministically from NPC seed.

---

## Simulation Tiers

From `NPC_STORIES_DESIGN.md`:

| Tier | Range | Simulation | Cost |
|------|-------|------------|------|
| 1 — Visible | ~150m | Full rendering, animation, physics | Dozens |
| 2 — Simulated | Active fragment set (~1.2km) | ECS entities, strategies, per-frame behavior | Hundreds |
| 3 — Background | Rest of world | DES: schedule state + location pointer, no per-frame ticking | Near-zero |

The DES built in Phase 0 IS the Tier 3 system. Same code runs in testbed and production game.

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
| Street generation | `JoyceCode/engine/streets/Generator.cs`, `QuarterGenerator.cs` |
| Buildings | `JoyceCode/engine/streets/Building.cs`, `ShopFront.cs` |
| Shop generation | `nogameCode/nogame/cities/GenerateShopsOperator.cs` |
| Spawn system | `JoyceCode/engine/behave/SpawnController.cs` |
| NPC spawning | `nogameCode/nogame/characters/citizen/SpawnOperator.cs` |
| Day/night time | `nogameCode/nogame/modules/daynite/Controller.cs` |
| Fixed viewer | `JoyceCode/engine/world/FixedPosViewer.cs` |
| MetaGen config | `models/nogame.metaGen.json` |
| Game config | `models/nogame.json` |
