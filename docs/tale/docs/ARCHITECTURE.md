# TALE Architecture — the Shipped System

> **Status:** living reference · **Last verified:** 2026-10-18 against `8d5e98b5`

This document describes how TALE works today: what each part does, where it
lives, and how the parts connect. It describes code, not intentions. Where the
system is headed is in [CONCEPT.md](CONCEPT.md), the emergent-story concept and
the plan from here to there. The original March 2026 concept (a world state
graph, emotional arcs, motifs, graph rewriting) was never built. It is kept in
[../archive/ARCHITECTURE_CONCEPT_2026-03.md](../archive/ARCHITECTURE_CONCEPT_2026-03.md)
and [../archive/CONCEPT_2026-03.md](../archive/CONCEPT_2026-03.md). The
history of each part is in the [phase docs](phases/).

## 1. In one paragraph

Every NPC in a populated cluster is an `NpcSchedule`. It holds a role, flat
float properties, a trust map and a group id. NPCs move through a day of
**storylets**: small data-defined activities ("work a shift", "eat at a shop",
"demand protection"). Each storylet has preconditions that gate selection and
postconditions that change properties when it ends. Two engines advance the
schedules:
- **`DesSimulation`** is a discrete-event simulation that runs headless (the
  scenario bake, the TALE tests, the Testbed). It also models encounters,
  requests and claims between NPCs, interrupts, and conditional postconditions.
- **`TaleManager.AdvanceNpc`** steps one NPC to its next storylet in the live
  game. It applies only plain postconditions and group verbs.

Social structure is pre-computed at build time by baking DES scenarios
(Phase D). At runtime it keeps evolving from live encounters and periodic
community detection (Phase E). The player meets NPCs through conversations
(Phase C), which read NPC state and change trust.

## 2. Data model

### 2.1 `NpcSchedule` (`JoyceCode/engine/tale/NpcSchedule.cs`)

| Field | Meaning |
|---|---|
| `NpcId`, `ClusterIndex`, `NpcIndex`, `Seed` | Identity. `NpcId = (clusterIndex << 12) \| npcIndex` (`MakeNpcId`, `:130`): 20 bits of cluster, 12 bits of NPC (≤ 4096 per cluster). `Seed = NpcId`. |
| `Role` | One of the population roles (§6.1). |
| `CurrentStorylet`, `CurrentStart`, `CurrentEnd`, `ScheduleStep`, `CurrentLocationId` | What the NPC is doing now, and where. |
| `HomeLocationId`, `WorkplaceLocationId`, `SocialVenueIds` | Locations assigned from the cluster's `SpatialModel`. |
| `HomePosition`, `WorkplacePosition`, `CurrentWorldPosition` | World positions, used for fragment bucketing (`TaleManager.GetNpcsInFragment`). |
| `IsInTransit`, `TransitFrom/ToLocationId`, `TransitStart/End` | The walk between two storylets; `PositionAt` interpolates during it (`:140`). |
| `Properties` | `Dictionary<string,float>`, 0..1: hunger, anger, fatigue, health, wealth, happiness, reputation, morality, fear, … |
| `Trust` | `Dictionary<int,float>` keyed by the other NPC's id; the player is key `-1`. |
| `GroupId` | Faction membership, `-1` = none. |
| `ArcStack`, `LastEncounterPartnerId`, `NextForcedStorylet` | Interrupt support (DES only, §4). |
| `HasPlayerDeviation`, `LastConversationTime` | Player engagement; decides whether the NPC survives dematerialisation (§6.3). |
| `PreferredTransportationType`, `RoutingPreferences` | Routing inputs for the in-game walker. |

There is no separate narrative-state object. All per-NPC state lives on the
schedule.

### 2.2 `ArcStack` (`ArcStack.cs`)

`enum InterruptScope { Nest, Replace, Cancel }`. The stack holds
`PausedStorylet { StoryletId, RemainingDurationMinutes, PropertiesAtPause }`.
It also holds one pending interrupt, managed with `SetInterrupt`,
`ClearInterrupt`, `Push`, `TryPop` and `HasPendingInterrupt`. The stack holds
storylets, not arcs: there are no arcs, no fork/join and no parallel scope.

## 3. Storylets

### 3.1 Definition (`StoryletDefinition.cs:61`)

Storylets are JSON arrays in `models/tale/*.json`. The parser is
`StoryletLibrary.LoadFromString` (`:247`).

| JSON key | Field | Notes |
|---|---|---|
| `id`, `name`, `weight` (default 1) | `Id`, `Name`, `Weight` | |
| `roles` | `Roles` | Roles that may select the storylet. |
| `location` | `LocationRef` | Where to go: `workplace`, `home`, `current` (default), `nearest_shop_Eat`, … |
| `location_type` | `LocationType` | `workplace`, `home`, `social_venue`, … (`ResolveLocationType`). |
| `duration_minutes` or `duration_minutes_min`/`_max` | `DurationMinutesMin/Max` | |
| `time_of_day` `{min,max}` | `TimeOfDay` | Top-level, not inside `preconditions`. |
| `preconditions` `{prop: {min,max}}` | `PropertyPreconditions` | `desperation_min` and `morality_max` become `DesperationMin`/`MoralityMax`; `in_group` / `not_in_group` test `GroupId`. |
| `postconditions` `{prop: "+0.1" \| "-0.2" \| "=0.5"}` | `Postconditions` | `"group": "form" \| "join" \| "leave"` becomes `GroupAction`; `"request": {…}` becomes `RequestPostcondition`. |
| `postconditions_if` `[{condition, then, storylet_next}]` | `PostconditionsIf` | Conditions on self, or on the encounter partner with a `target_` prefix. |
| `claim_trigger` `{request_type, role_match, interrupt_scope}` | `ClaimTrigger` | §5. |
| `wait_edge` `{signal_type, request_id, timeout_minutes, fallback_storylet}` | `WaitEdge` | |
| `interrupt_priority` (default 1) | `InterruptPriority` | §4. |
| `tags` | `Tags` | Used for conversation script resolution (§7). |
| `conversation_script` | `ConversationScript` | Explicit dialogue override. |

### 3.2 Loading

- **Game:** each file is declared once in `models/nogame.resources.json` as
  `"type": "taleStorylet"` (14 files). `TaleModule` loads them with
  `StoryletLibrary.LoadFromAssets` (`TaleModule.cs:321`), so what ships and
  what loads are one declaration.
- **Headless:** TestRunner, the Chushi scenario bake and the Testbed call
  `LoadFromDirectory("models/tale")`, which reads only the top-level `*.json`.
- **`models/tale/interactions/`:** the ten files there are loaded by neither
  path. The request and claim storylets that run come from the role files.
- **Fallbacks:** `GetFallback` uses `wander` by day and `rest` by night.

### 3.3 Selection (`StoryletSelector.SelectNext`, `StoryletSelector.cs:69`)

1. Candidates are the storylets for the NPC's role that pass
   `PassesPreconditions`: time window, property ranges, desperation and
   morality limits, group membership.
2. With no candidates, the time-of-day fallback is used.
3. Otherwise a weighted random pick is made with
   `new Random(npc.Seed + npc.ScheduleStep * 7919)` (`:97`). Every NPC's
   sequence is therefore deterministic and independent of the others.

Desperation is derived, not stored (`ComputeDesperation`, `:59`):
`hunger·0.4 + (1−wealth)·0.3 + anger·0.2 + (1−health)·0.1`, clamped to 0..1.

### 3.4 Postconditions

- **`ApplyPostconditions`** runs when a storylet ends. It applies the
  `+`/`-`/`=` changes, plus a background hunger tick (`HungerPerHour = 0.04`)
  unless the storylet sets hunger itself.
- **`ApplyConditionalPostconditions`** (`:283`) evaluates `PostconditionsIf`
  against an encounter partner and may force the next storylet
  (`NextForcedStorylet`). Only `DesSimulation` calls it, at an encounter.
- **Group verbs** run through `GroupActions` (§6.4).

## 4. Interrupts (DES only)

When two NPCs meet in `DesSimulation`, a storylet with `InterruptPriority ≥ 5`
can interrupt the partner's storylet if the partner's priority is lower. The
scope follows from the priority: **≥ 8 → Replace, otherwise Nest**
(`DesSimulation.cs:439-469`).
- **Nest** pushes the partner's current storylet onto its `ArcStack` and
  resumes it afterwards (`storylet_resumed`).
- **Replace** discards the partner's current storylet.
- **Cancel** has a branch in `DesSimulation.cs:271`, but nothing produces it
  (KAR-436).

The live game has no interrupts.

## 5. Interaction pool (DES only)

`InteractionPool` (`InteractionPool.cs`) holds `InteractionRequest`s and
`InteractionSignal`s:
1. A storylet's `"request"` postcondition makes the NPC call `EmitRequest`
   (type, location, urgency, timeout).
2. When two NPCs meet, `DesSimulation.CheckAndClaimRequests` (`:481`) looks
   for a request matching one of the claimer's `claim_trigger` storylets
   (`FindMatchingRequest`) and claims it (`ClaimRequest`). The claimer then
   runs that storylet and a `request_fulfilled` signal is emitted
   (`EmitSignal`).
3. A requester's `wait_edge` waits for that signal (`HasSignal`) or falls back
   on timeout.
4. Requests nobody claims are resolved abstractly at the day boundary by any
   matching NPC; the signal source is `-1` (`:525-542`).

## 6. Population, lifecycle and social structure

### 6.1 Population (`TalePopulationGenerator.cs`)

- **Seeding:** each cluster is populated deterministically from
  `ClusterDesc.GetKey()`. Every NPC has its own seed,
  `RandomSource(clusterSeed + "-npc-" + npcIndex)` (`:93`), so skipping a
  deviated index leaves the others unchanged.
- **Count:** 3 NPCs per street point, capped at 4095 (`NpcsPerStreetPoint`,
  `:30`).
- **Roles:** the population draws from 8 roles: worker, merchant, socialite,
  drifter, authority, nightworker, hustler, reveler (`:19`, default weights
  `:24`). `models/nogame.roles.json` defines 13 roles in total; the other five
  are the Phase 8 occupations (factory_worker, factory_worker_eve,
  factory_worker_night, office_worker, service_worker). Character model pools
  per role are covered in [phases/PHASE_8.md](phases/PHASE_8.md).
- **Locations:** home, workplace and venues are assigned from the cluster's
  `SpatialModel` (§8.3).

### 6.2 Cluster lifecycle (`TaleManager.cs`)

- **Populate:** `PopulateCluster(clusterDesc, spatialModel, gameNow)` (`:95`)
  runs on cluster completion. It is called from `TaleSpawnOperator.cs:81` and
  `TaleModule.cs:130,501`. It:
  1. generates the schedules;
  2. applies a baked scenario (§6.4);
  3. seeds the group table and overlays saved social snapshots;
  4. warms the schedules up to the live game clock.
- **Depopulate:** `DepopulateCluster` (`:275`) would snapshot social state and
  remove the cluster's schedules, but **nothing calls it** (KAR-434).

### 6.3 Tiers and survival

| Tier | What exists |
|---|---|
| 3 | Only the `NpcSchedule` in `TaleManager`. |
| 2 | An ECS entity whose `TaleEntityStrategy` is frozen (`EnterTier2Mode`, `TaleEntityStrategy.cs:139`). |
| 1 | A fully materialised NPC near the player, spawned by `TaleSpawnOperator`. |

When an NPC dematerialises (`TaleSpawnOperator.cs:430-467`), it stays as
Tier 2 only if `HasPlayerDeviation` is set **and** the player talked to it
within `TaleManager.ConversationMemorySeconds` (1800 s, `TaleManager.cs:700`;
`RecordConversation` / `HasRecentConversation`). A deviated NPC without a
recent conversation is dropped with `ForgetSchedule`, and `IsScheduleAlive`
then reports false. `HasPlayerDeviation` is set only in `TaleModule`:
- by the narration function `tale.npc.remember` (`:385`);
- at the end of a conversation script, together with +0.02 player trust
  (`:767-768`);
- when a saved game is restored (`:254`, `:678`).

### 6.4 Social structure

- **Encounters and trust:**
  - `EncounterResolver` gives the meeting probability per 15-minute quantum
    from the location type: venue 0.07, workplace 0.04, street 0.015,
    transport 0.002. A stay of a given overlap meets with
    `1 − (1 − p)^(overlap/15)` (`EncounterResolver.cs:29-33,110`).
  - `RelationshipTracker.DetermineInteractionType` asks
    `InteractionTypeRegistry.EvaluateConditions` and falls back to `greet`.
    The chosen type's `trustDelta` (from `models/nogame.interactions.json`;
    condition classes in `JoyceCode/engine/tale/conditions/`) changes trust in
    both directions.
  - `RelationshipTierRegistry` maps trust to stranger / acquaintance / friend /
    ally.
- **Bake (Phase D, `JoyceCode/engine/tale/bake/`):**
  - At build time Chushi runs 365-day DES scenarios (`ScenarioCompiler`) for
    the categories in `models/nogame.scenarios.json` (small, medium; large is
    disabled) and writes `sc-{hash}` files.
  - At runtime `ScenarioLibrary` loads them, falling back to an in-memory
    compile. `ScenarioSelector` picks one per cluster and `ScenarioApplicator`
    maps its trust, properties and groups onto the real NPCs.
  - Detail: [phases/PHASE_D_SOCIAL.md](phases/PHASE_D_SOCIAL.md).
- **Living factions (Phase E):**
  - `CommunityDetector` finds disjoint communities: label propagation over
    mutual top-K trust edges.
  - `ClusterSocialState` holds the per-cluster `RuntimeGroup` table and the
    NPC snapshots; it survives leaving a cluster and goes into the save game.
  - `RuntimeSocialEvolver`, driven by `TaleModule._onSocialFrame`, runs an
    encounter tick every `SocialTickGameMinutes` (10) and re-detects
    communities every `RedetectGameHours` (2).
  - `GroupActions` executes the storylet group verbs form / join / leave.
  - `TaleManager.ResolveSocialVenue` sends grouped NPCs to their group's
    hangout.
  - Names come from `nogame.modules.tale.GroupNameGenerator`.
  - Detail: [phases/PHASE_E_SOCIAL.md](phases/PHASE_E_SOCIAL.md).

## 7. The player: conversations (Phase C)

Player and NPC meet through dialogue, not through the interaction pool.
`TaleConversationBehavior` offers "E to talk" on outdoor NPCs, promotes them
to Tier 1 and starts a narration script. The script is resolved by
`TaleNarrationBindings.ResolveScript` (`:208`), first match wins:
1. `storylet.ConversationScript`;
2. `tale.{storyletId}`;
3. the first `tale.tag.{tag}`;
4. `tale.role.{role}`;
5. `tale.generic`.

Scripts live in `models/tale/conversations/` and are mounted in
`models/nogame.narration.json`.

Before the script runs, `InjectNpcProps` exposes the NPC's state as narration
props:
- its properties: `npc.hunger` … `npc.fear`, `npc.role`;
- the player relationship: `npc.met_player`, `npc.trust_player`;
- the faction: `npc.group_id`, `npc.has_group`, `npc.group_type`,
  `npc.group_name`, `npc.group_size`;
- remembered facts: `npc.player_fact.*`.

The narration functions include `npcGroupType()` and `npcGroupName()`.
Dialogue can gate on trust, remember player facts (`tale.npc.remember`) and
trigger quests. History: `../plans/done/PHASE_C-NPC-CONVERSATIONS.md`,
`../plans/done/TALE-NPC-CONVERSATION-SYSTEM-C1-C4.md`.

## 8. The NPC in the game

### 8.1 `TaleEntityStrategy` (`nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs`)

An `AOneOfStrategy` with four states:
- `travel`: `GoToStrategyPart`, walking to the storylet's location;
- `activity`: `StayAtStrategyPart`, staying there, with
  `TaleConversationBehavior` attached;
- `flee` and `recover`: entered on a weapon hit or a vehicle crash; afterwards
  the NPC resumes what it was doing before.

When an activity ends, the strategy calls `TaleManager.AdvanceNpc`
(`TaleEntityStrategy.cs:271`). `AdvanceNpc` (`TaleManager.cs:745`) applies the
finished storylet's postconditions and group verb, selects the next storylet,
resolves its location, and sets the transit window from the spatial model's
travel time.

### 8.2 Routing

The strategy resolves `engine.tale.IRouteGenerator` (`IRouteGenerator.cs`;
`GetRouteAsync(from, to, startPod, preferences, transportType)`). The binding
in `models/nogame.implementations.json` points to
`nogame.characters.citizen.NavMeshRouteGenerator`, which builds a street route
through `StreetRouteBuilder.BuildAsync`. A null route means "walk straight".

### 8.3 `SpatialModel` (`JoyceCode/engine/tale/SpatialModel.cs`)

`SpatialModel.ExtractFrom(cluster, navCluster)` (`:163`) turns a cluster into
TALE `Location`s: homes, workplaces, shops, venues and street junctions.
- Buildings have one entry point on their perimeter, at pavement height.
- Street locations carry up to six `EntryCandidates`, the sidewalk corners
  around the junction. `Location.EntryPositionFor(npcId)` (`:38`) spreads NPCs
  across them, so NPCs don't stack on the junction centre.
- `IsPedestrianReachable` filters out entries that can't be reached on foot.

## 9. Persistence

| Where | What |
|---|---|
| `GameState.TaleDeviations` (`nogameCode/nogame/GameState.cs:62`) | JSON array of deviated schedules (`System.Text.Json` `JsonObject`), written and read by `TaleModule`. |
| `GameState.TaleSocialState` (`:70`) | `ClusterSocialState.ToJson()` per cluster, including player trust. `TaleModule._onBeforeSaveGame` snapshots the populated clusters first; `_onAfterLoadGame` restores with `ReplaceSocialState`. |

## 10. Configuration

| Mix path | File | Registry |
|---|---|---|
| `/roles` | `models/nogame.roles.json` | `RoleRegistry` |
| `/interactions` | `models/nogame.interactions.json` | `InteractionTypeRegistry` |
| `/relationships/tiers` | `models/nogame.relationship-tiers.json` | `RelationshipTierRegistry` |
| `/groups/types` | `models/nogame.group-types.json` | `GroupTypeRegistry` (rule-based: `authority_threshold`, `property_threshold`, `combined_threshold`) |
| `/scenarios` | `models/nogame.scenarios.json` | scenario bake categories |
| `/resources/list` (`taleStorylet`) | `models/nogame.resources.json` | storylet files |

`TaleModule` reads the registries with `loader.WhenLoaded(...)`
(`TaleModule.cs:400-462`).

## 11. Class map

| Class | File | Role |
|---|---|---|
| `NpcSchedule` | `JoyceCode/engine/tale/NpcSchedule.cs` | Per-NPC state. |
| `ArcStack` | `ArcStack.cs` | Paused storylets and the pending interrupt. |
| `StoryletDefinition`, `StoryletLibrary` | `StoryletDefinition.cs` | Storylet data, parsing, role index, fallbacks. |
| `StoryletSelector` | `StoryletSelector.cs` | Selection, desperation, postconditions. |
| `TaleManager` | `TaleManager.cs` | Schedules, cluster lifecycle, tiers, live advance, group table. |
| `TalePopulationGenerator` | `TalePopulationGenerator.cs` | Seeded population. |
| `DesSimulation`, `EventQueue` | `DesSimulation.cs`, `EventQueue.cs` | Headless discrete-event simulation. |
| `EncounterResolver` | `EncounterResolver.cs` | Encounter probabilities. |
| `RelationshipTracker` | `RelationshipTracker.cs` | Trust per pair, interaction type. |
| `InteractionPool` | `InteractionPool.cs` | Requests, claims, signals. |
| `GroupDetector` | `GroupDetector.cs` | Old clique detector (Bron-Kerbosch). No production caller since Phase E; only tests reference it. |
| `CommunityDetector` | `CommunityDetector.cs` | Faction detection. |
| `ClusterSocialState` | `ClusterSocialState.cs` | Group table and snapshots. |
| `RuntimeSocialEvolver` | `RuntimeSocialEvolver.cs` | Live encounters and re-detection. |
| `GroupActions` | `GroupActions.cs` | form / join / leave. |
| `RoleRegistry`, `InteractionTypeRegistry`, `RelationshipTierRegistry`, `GroupTypeRegistry` | same-named files | Data-driven definitions. |
| `SpatialModel`, `Location` | `SpatialModel.cs` | TALE view of a cluster. |
| `IRouteGenerator` | `IRouteGenerator.cs` | Routing seam. |
| `JsonlEventLogger`, `SimMetrics` | same-named files | DES event log and metrics (Testbed, tests). |
| `Scenario*` | `bake/` | Phase D bake and apply. |
| `TaleModule`, `TaleNarrationBindings`, `GroupNameGenerator` | `nogameCode/nogame/modules/tale/` | Game wiring, persistence, narration props. |
| `TaleSpawnOperator`, `TaleEntityStrategy`, `NavMeshRouteGenerator`, `TaleConversationBehavior` | `nogameCode/nogame/characters/citizen/` | Materialisation and in-game behaviour. |

## 12. Testing

TALE scenario tests are JSON scripts in `models/tests/tale/phase*/`. The
TestRunner runs them against a fixed DES world. Commands and tiers are in
[PROCESS_TALE.md](PROCESS_TALE.md). The phase-5 and Phase C scripts currently
assert nothing (KAR-433). Unit tests for the engine parts are in
`tests/JoyceCode.Tests/engine/tale/`.
