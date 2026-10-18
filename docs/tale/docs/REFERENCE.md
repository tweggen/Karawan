# TALE Reference — Shared Concepts

> **Status:** living reference · **Last updated:** 2026-10-18 (KAR-431: storylet format, roles and persistence rewritten from the code)

Read this file alongside any phase file. It defines the concepts the phases share and
describes the system **as shipped**; where a phase doc says otherwise, this file and
[ARCHITECTURE.md](ARCHITECTURE.md) win.

---

## Base Entity Properties

Every NPC gets a flat `Dictionary<string, float>` (range 0.0–1.0). No class hierarchy.

| Category | Properties |
|----------|-----------|
| **Emotional** | `anger`, `fear`, `trust`, `happiness` |
| **Physical** | `health`, `fatigue`, `hunger` |
| **Social** | `wealth`, `reputation`, `morality` |

`morality` (0.0–1.0, default ~0.6–0.8) represents how far an NPC will go before resorting to illegitimate means. In `DesSimulation` it drifts down by `(desperation − 0.4) · 0.03` per step while desperation exceeds 0.4 and recovers by 0.003 while desperation is below 0.2; social contact plays no part. The live game (`TaleManager`) applies no drift, so morality changes there only through postconditions.

Specific roles may add entries (e.g., `inventory_stress` for merchants). Trust is per-relationship (`NpcSchedule.Trust`, keyed by NPC id; -1 is the player), not global — the base `trust` property is a default/average.

---

## Storylet Format

A storylet is one entry of a JSON array in `models/tale/*.json` (role and theme files) or
`models/tale/interactions/<category>/*.json` (request/claim pairs). It says **where** the
NPC goes, **how long** it stays, **when** it may be chosen, and **what it changes**.
There is no verb: the game turns every storylet into a travel phase (`GoToStrategyPart`)
followed by an activity phase (`StayAtStrategyPart`) in `TaleEntityStrategy`.
Parsing is in `StoryletLibrary.ParseStorylet` (`JoyceCode/engine/tale/StoryletDefinition.cs`).

A real entry (`models/tale/worker.json`):

```json
{
  "id": "lunch_break",
  "name": "Lunch Break",
  "roles": ["worker"],
  "time_of_day": { "min": "11:00", "max": "14:00" },
  "preconditions": { "hunger": { "min": 0.4 } },
  "postconditions": { "hunger": "-0.55", "wealth": "-0.03" },
  "location": "nearest_shop_Eat",
  "duration_minutes": 25,
  "weight": 2.5,
  "tags": ["routine", "eating"],
  "conversation_script": "tale.lunch_break"
}
```

### Selection keys

| Key | Meaning |
|-----|---------|
| `id` (required), `name` | Identity. `wander` and `rest` must exist: they are the day and night fallbacks. |
| `roles` | Roles that may pick it; empty or absent = every role. |
| `weight` | Weighted-random weight (default 1.0). |
| `time_of_day` | **Top level**, `{ "min": "HH:MM", "max": "HH:MM" }`. A `time_of_day` inside `preconditions` is ignored. |
| `preconditions` | Property ranges, `{ "<property>": { "min": x, "max": y } }`, plus three special keys: `desperation_min` (number; desperation = `0.4·hunger + 0.3·(1−wealth) + 0.2·anger + 0.1·(1−health)`, `StoryletSelector.ComputeDesperation`), `morality_max` (number), and `in_group` / `not_in_group` (`{}`; tests `NpcSchedule.GroupId`). |
| `location` | Where the NPC goes: `home`, `workplace`, `social_venue`, `nearest_shop_Eat`, `random_street` or `street_segment` (both = a street location), `current` (default: stay). Anything else is treated as `current` (`StoryletDefinition.ResolveLocationType`). A grouped NPC's `social_venue` resolves to its group's hangout (Phase E5). |
| `duration_minutes`, or `duration_minutes_min` + `duration_minutes_max` | How long the activity lasts. |
| `tags` | Free labels; they also choose the conversation script (`tale.tag.<tag>`). |

### Effect keys

| Key | Meaning |
|-----|---------|
| `postconditions` | String-valued entries change the NPC's own properties when the storylet completes: `"+0.1"` / `"-0.1"` add, `"=0.5"` sets (`StoryletSelector.ApplyPostconditions`). Hunger rises in the background unless the storylet changes it. |
| `postconditions.group` | `"form"`, `"join"` or `"leave"` — a group verb, run by `GroupActions` (Phase E4). |
| `postconditions.request` | `{ type, location, urgency, timeout_minutes, variable_key }` — emit a request into the `InteractionPool`. |
| `claim_trigger` | `{ request_type, role_match }` — the storylet is the answer to such a request. (`interrupt_scope` is parsed but never read.) |
| `postconditions_if` | Ordered branches `{ condition, then, storylet_next }`, evaluated when two NPCs meet. Keys prefixed `target_` test or change the *other* NPC; the first matching branch applies, and its `storylet_next` becomes the NPC's next storylet. |
| `interrupt_priority` | Integer. When two NPCs meet, a storylet with priority ≥ 5 interrupts the other NPC's lower-priority storylet: priority ≥ 8 replaces it, otherwise it nests (paused, then resumed from the `ArcStack`). |
| `conversation_script` | Narration script used when the player talks to the NPC during this storylet (Phase C2); otherwise the script is resolved by storylet id, tag, role, then `tale.generic`. |

Parsed but not used by any shipped storylet or code path: `location_type` (stored, never
read), `wait_edge` (`signal_type`, `request_id`, `timeout_minutes`, `fallback_storylet`;
stored, never read).

### Where each key takes effect

The live game (`TaleManager.AdvanceNpc`) selects storylets, applies plain postconditions
and group verbs, and resolves locations. **Requests, claims, `postconditions_if` and
interrupts run only in `DesSimulation`** — the scenario bake, the TALE test suite and the
Testbed. In the live game, NPC–NPC encounters only change trust (`RuntimeSocialEvolver`,
Phase E3).

---

## Interaction Primitives

NPC-NPC communication via a shared interaction pool (cluster-scoped):

| Primitive | DSL Location | Meaning |
|-----------|-------------|---------|
| **Request** | Postcondition | Emit a typed request into the pool |
| **Claim** | `claim_trigger` | Pick up a matching request from the pool |
| **Signal** | (automatic) | `DesSimulation` emits `request_fulfilled` when the claimer meets the requester, or resolves it abstractly |

Flow: NPC A emits request → NPC B claims it → NPC B fulfils → a signal marks the request
fulfilled. The planned **Wait** edge (`wait_edge`) is parsed but never evaluated. The pool
exists only in `DesSimulation` (see above).

---

## NPC Roles

Roles decide which storylets an NPC may pick and when it wakes. They are defined in
`models/nogame.roles.json` (13 roles), loaded into `RoleRegistry`, with
`BaseWakeHour`, `FulfillableRequestTypes` (which requests a role can claim) and a model
pool (Phase 8).

**Population draws 8 roles** — `worker`, `merchant`, `socialite`, `drifter`,
`authority`, `nightworker`, `hustler`, `reveler` — from a hard-coded list and weights in
`TalePopulationGenerator` (0.30 / 0.13 / 0.15 / 0.12 / 0.10 / 0.08 / 0.07 / 0.05),
shifted towards merchant, authority, hustler and reveler in downtown clusters.

The 5 Phase 8 occupations — `factory_worker`, `factory_worker_eve`,
`factory_worker_night`, `office_worker`, `service_worker` — are defined in the registry
with storylet files and model pools, **but no generator assigns them**: the registry's
`DefaultWeight` / `PickRoleFromWeights` are unused.

Storylets per role live in `models/tale/<role>.json`; `universal.json`,
`desperation.json` and `escalation.json` cut across roles. A role is a starting point,
not a cage: a desperate NPC with low morality reaches criminal storylets through
`desperation_min` / `morality_max`.

---

## Simulation Tiers

From `design/NPC_STORIES.md`:

| Tier | Range | Simulation | Cost |
|------|-------|------------|------|
| 1 — Visible | ~150m | Full rendering, animation, physics | Dozens |
| 2 — Simulated | Active fragment set (~1.2km) | ECS entities, strategies, per-frame behavior | Hundreds |
| 3 — Background | Rest of world | DES: schedule state + location pointer, no per-frame ticking | Near-zero |

The DES built in Phase 0 IS the Tier 3 system. Same code runs in testbed and production game.

### Tier Transitions

- **3 → 2 → 1 (materialization):** `SpawnController` detects underpopulated visible fragments, `TaleSpawnOperator` looks up existing Tier 3 `NpcSchedule` from `TaleManager` and creates an ECS entity with `TaleEntityStrategy` + visuals.
- **1 → 2 → 3 (dematerialization):** `SpawnController` detects overpopulated fragments, `TaleSpawnOperator` destroys the ECS entity. What happens to the schedule depends on the player (see [Deviation Tracking & Persistence](#deviation-tracking--persistence)): a never-engaged NPC's schedule stays in `TaleManager` as Tier 3; an NPC the player engaged with **and** talked to within the last `TaleManager.ConversationMemorySeconds` (1800 s) is frozen as Tier 2; an engaged NPC the player has not talked to recently is dropped (`ForgetSchedule`), so its slot is regenerated from seed.
- Transitions are lossless only for those kept schedules.

### Cluster Population Lifecycle

NPC populations are **per-cluster**, created on demand:

1. **Cluster activation** (`ClusterCompletedEvent` in `TaleModule`, or on demand from `TaleSpawnOperator`): `TaleManager.PopulateCluster` generates `NpcSchedule` objects deterministically from the cluster seed, skipping deviated indices, applies the baked social scenario (Phase D) and overlays the cluster's `ClusterSocialState` snapshot (Phase E).
2. **Active cluster**: All tiers running. `TaleSpawnOperator` materializes/dematerializes NPCs based on fragment visibility.
3. **Cluster deactivation**: *designed* as `TaleManager.DepopulateCluster` — snapshot social state, drop non-deviated schedules. **Nothing calls it today** (KAR-434), so a visited cluster's schedules stay in memory.
4. **Cluster reactivation**: Regenerate from seed, **skip deviated NPC indices**, overlay deviated NPCs from save.

---

## Seed-Based NPC Generation

NPC populations are deterministic from the cluster seed. This enables on-demand generation and regeneration without persisting the full world state.

### Seed Hierarchy

```
World Seed: "mydear"
  → Cluster Seed: "cluster-clusters-mydear-[clusterIndex]"
    → NPC Seed: clusterSeed + "-npc-" + npcIndex
```

Each NPC's seed is computed **independently** (not sequentially from a shared RNG), so skipping one NPC does not affect any other NPC's generation. This is critical for the deviation skip mask.

### What the Seed Determines

From a single NPC seed, the following are deterministic:
- **Role** (worker, merchant, socialite, drifter, authority, nightworker, hustler, reveler — `TalePopulationGenerator.Roles`)
- **Home location** (assigned from cluster's residential locations)
- **Workplace location** (assigned from cluster's work locations)
- **Social venue preferences** (assigned from cluster's social venues)
- **Initial properties** (hunger, health, fatigue, anger, fear, trust, happiness, reputation, wealth, morality — seeded defaults with per-role variation)
- **Storylet path** — `StoryletSelector` draws with `new Random(npc.Seed + npc.ScheduleStep * 7919)`, where `Seed` is the NPC id

### NPC Count per Cluster

`TalePopulationGenerator.ComputeNpcCount`: 3 NPCs per street point, capped at 4095 (the 12-bit NPC index). Deterministic from cluster data, so NPC indices are stable across regeneration.

---

## Deviation Tracking & Persistence

### Principle

**Only save what can't be regenerated.** An NPC in primary (algorithmically generated) state can be regenerated from its seed. Only NPCs with player-caused deviations need persistence.

### Deviation States

| State | Player Impact | Persistence | Example |
|-------|--------------|-------------|---------|
| **Unobserved / never engaged** | None | Not saved; regenerated from seed | NPC the player never talked to |
| **Engaged, recent** | `HasPlayerDeviation` set, conversation within 1800 s | Kept as Tier 2 while in range; saved | Player just talked to the NPC |
| **Engaged, aged out** | `HasPlayerDeviation` set, no recent conversation | Dropped at dematerialization (`ForgetSchedule`) | Player talked to the NPC long ago |

There is no group or "recursive" propagation: only the NPC the player talked to is marked.

### Save Format

`GameState.TaleDeviations` holds a JSON array of the deviated NPCs' schedules (cluster
index + NPC index, properties, current storylet, trust, group id, position), written in
`TaleModule` before a save. Social state (groups, trust snapshots including the player's
`Trust[-1]`) is saved separately in `GameState.TaleSocialState` (Phase E6).

### Regeneration with Deviation Skip Mask

On cluster reactivation:
1. Load deviation list for cluster: "NPC indices {7, 23, 41} are deviated" (`TaleManager.RegisterNpc` builds the skip mask)
2. Run deterministic population generator, **skip those indices**
3. The deviated schedules loaded from the save are already in `TaleManager`

The skip mask prevents duplicates without cascading seed shifts.

### HasPlayerDeviation Flag

Set only in `nogameCode/nogame/modules/tale/TaleModule.cs`:
- at the end of a conversation script, together with a +0.02 trust step towards the player (`Trust[-1]`);
- by the narration action `tale.npc.remember`, which stores a `player_fact.<fact>` property;
- on every schedule loaded from the save (which also counts as a recent conversation, so it is not aged out at once).

Storylet postconditions never set it.

---

## Relationship Tiers

NPCs form relationships through repeated interaction:

| Tier | Trust Range | Behavior |
|------|-------------|----------|
| **Stranger** | 0.0–0.15 | No voluntary interaction, may interact if co-located by chance |
| **Acquaintance** | 0.15–0.4 | Greet, brief chat, aware of each other's existence |
| **Friend** | 0.4–0.7 | Ask for help, share information, seek out deliberately |
| **Ally** | 0.7–1.0 | Deep entanglement, mutual obligations, story-driving |

The ranges come from `models/nogame.relationship-tiers.json` (`/relationships/tiers`,
`RelationshipTierRegistry`). There is no rival tier; hostility shows as low trust and high
anger or fear. Group types are configured in `models/nogame.group-types.json`
(`/groups/types`, `GroupTypeRegistry`).

---

## Key Codebase Files

| System | File |
|--------|------|
| Engine entry | `Karawan/DesktopMain.cs` |
| Headless engine | `Splash.OpenGL/Platform.cs` → `EasyCreateHeadless()` |
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
| Storylet format / library | `JoyceCode/engine/tale/StoryletDefinition.cs` |
| DES (bake, tests, Testbed) | `JoyceCode/engine/tale/DesSimulation.cs` |
| Interaction pool | `JoyceCode/engine/tale/InteractionPool.cs` |
| Population | `JoyceCode/engine/tale/TalePopulationGenerator.cs` |
| Roles | `JoyceCode/engine/tale/RoleRegistry.cs`, `models/nogame.roles.json` |
| Live social evolution | `JoyceCode/engine/tale/RuntimeSocialEvolver.cs`, `CommunityDetector.cs`, `GroupActions.cs` |
| Entity persistence | `JoyceCode/builtin/EntitySaver.cs` |
| Save hooks | `JoyceCode/engine/Saver.cs` (`OnBeforeSaveGame`, `OnAfterLoadGame`) |
| Creator registry | `JoyceCode/engine/world/CreatorRegistry.cs` |
| Day/night time | `nogameCode/nogame/modules/daynite/Controller.cs` |
| Fixed viewer | `JoyceCode/engine/world/FixedPosViewer.cs` |
| MetaGen config | `models/nogame.metaGen.json` |
| Game config | `models/nogame.json` |
| Seed RNG | `JoyceCode/builtin/tools/RandomSource.cs` |
