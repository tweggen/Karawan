# TALE S2b — Requests, escalation and interrupts in the live game

> Stage S2b of [TALE-EMERGENT-STORIES.md](../proposed/TALE-EMERGENT-STORIES.md) (KAR-440).
> Depends on S0 (done). Builds on KAR-477 (PR #191, merged 2026-11-21): claim-triggered
> storylets start only through claims, and a claim starts its storylet as an interrupt.
> **Status:** done 2026-11-28 (owner confirmed in game: refused protection → threat → the victim flees); reviewed GREEN (Fable, 2026-11-21, four passes, §10).

The factions TALE grows are meant to *do* things: demand protection, threaten, report
crimes, investigate, flee. All of that machinery runs in the offline simulation
(`DesSimulation`) that bakes each city's society. The live game runs none of it, so the
escalation chains that make a faction a story never happen in front of the player. This
stage brings them into the live game for both kinds of NPC: the materialised ones the
player sees, and the abstract (Tier-3) ones the background tick moves.

## 1. What S2b found in the code

Line numbers are on master after PR #191.

### 1.1 What the offline simulation does (`JoyceCode/engine/tale/DesSimulation.cs`)

At each NPC's **node arrival** (`ProcessNodeArrival`, `:243`):
1. It applies the finished storylet's postconditions and group verb (`GroupActions.Apply`, shared code).
2. It selects the next storylet in a fixed order (`:268-330`):
   1. a **forced** storylet (`NextForcedStorylet`, set by an escalation);
   2. a **pending interrupt** (`ArcStack`): with `Nest` the current storylet is pushed as paused, with `Replace` it is dropped, with `Cancel` normal selection runs;
   3. **resuming** a paused arc;
   4. otherwise **routine** selection (`StoryletSelector.SelectNext`).

   **A resumed storylet draws a fresh duration** (`:337`). `Nest` records `RemainingDurationMinutes` and `PropertiesAtPause` (`:289-294`), but nothing reads them.
3. It emits an **interaction request** if the new storylet carries a `request` postcondition (`:345-357`, `InteractionPool.EmitRequest`).
4. It registers the NPC's presence and **checks for encounters at that location right away** (`:362-372`, `EncounterResolver`). So a claim can follow within the same node arrival.

At each **encounter** (`ProcessEncounter`, `:402`):
- **Claims:** each side may claim an active request matching one of its `claim_trigger` storylets (`CheckAndClaimRequests`). Since KAR-477 the claim sets that storylet as the claimer's interrupt with the trigger's `interrupt_scope`. No shipped trigger declares a scope, so every claim nests today. Several matching storylets are picked by weight, seeded by request and claimer.
- **Trust** changes by interaction type (`RelationshipTracker`, DES-specific; see §2).
- **Conditional postconditions** (`postconditions_if`, `:465-516`): the encountering storylet's conditions are checked against the partner (`target_fear`, …); a match applies its effects and may force the next storylet (`storylet_next`), logged as `escalation_triggered`.
- **Encounter interrupts fire only from storylets that have `postconditions_if`.** The `interrupt_priority ≥ 5` check is nested inside that branch (`:469-489`, mirrored `:494-514`). In shipped data only `demand_protection` (7) and `investigate_complaint` (6) can interrupt a partner; `threaten_harder` (8), `prepare_arrest` (9) and `defend_territory` (8) never do. S2b keeps this as is.

At each **day boundary** (`ResolveInteractionsDaily`, `:610`): expired requests are purged, and unclaimed ones are resolved abstractly by a capable role. The `_rng.Next` draw at `:630` picks a resolver that is never used; it is the only draw from the DES's random stream inside the rules this stage extracts.

### 1.2 What the live game does

- **`TaleManager._advanceNpc`** (`TaleManager.cs:1201`) applies postconditions and the group verb, then calls `SelectNext` directly. It ignores forced storylets, pending interrupts, paused arcs and `request` postconditions.
- **It runs on more than one thread.** Spawn workers call `AdvanceNpc` from pool threads (`TaleManager.cs:25-30`; `TaleSpawnOperator.cs:377`, inside the async spawn). The background tick and the encounter tick call it on the logical thread.
- **The populate warm-up** calls `_advanceNpc` twice per NPC with `applyGroupVerbs: false` (`TaleManager.cs:203,211`).
- **`RuntimeSocialEvolver.TickEncounters`** (`RuntimeSocialEvolver.cs:94`) pairs co-located NPCs of a populated cluster, materialised or not, with the bake's encounter probabilities, and changes **trust only**: no claims, no conditional postconditions, no interrupts.
  - It is **not sliced**: one whole cluster per social tick, every `SocialTickGameMinutes` = 10 game minutes, round-robin over the populated clusters (`TaleModule.cs:743-757`).
  - It has no measured cost baseline. S0 W5's p99.9 of 0.07 ms is the background tick's slice, not this.
- **Materialised NPCs** run a storylet as a `TaleEntityStrategy` (walk with `GoToStrategyPart`, then stay with `StayAtStrategyPart`); when it finishes, the strategy calls `TaleManager.AdvanceNpc` (`TaleEntityStrategy.cs:296`).
  - The strategy can already be cut short: a hit or a crash switches it to flee or recover (`:73-95`), and it resumes afterwards through `_preInterruptStrategyName` (`:133-149`).
  - A Tier-2 NPC's strategy is frozen (`:157-163`).
  - The parts hand back through `QueueEventHandler(() => Controller.GiveUpStrategy(this))`; there is no per-frame hook in a strategy.
- **Tier-3 NPCs** advance in the S0 background tick (`TaleManager.AdvanceAbstractSchedules`, `:1373`), which steps a schedule at its `CurrentEnd` with a monotonic guard (`LastAdvanceTime` + 1 min).
- **There is no live `InteractionPool`**, and `InteractionPool` itself is not thread-safe (plain `Dictionary` and counters, `InteractionPool.cs:13-16`).

### 1.3 Gaps in both

- **`wait_edge`** is parsed (`StoryletDefinition.cs:412`) and read nowhere. REFERENCE.md already says so; ARCHITECTURE.md:147 still claims a requester waits.
- **`models/tale/interactions/`** is loaded nowhere: `LoadFromDirectory` does not search subfolders, and `nogame.resources.json` declares only the 14 top-level files. All 20 storylets in its 10 files duplicate shipped ones (measured 2026-11-21).
- **`InterruptScope.Cancel`** exists only as a scope value; no storylet declares any `interrupt_scope`.

### 1.4 Since KAR-477 this matters more

Before KAR-477, claim-triggered storylets (`flee_from_threat`, `respond_to_blackmail`, `investigate_crime`, …) ran in the live game too, by accident: ordinary selection picked them without any claim. Now they run only through claims, and claims exist only in the DES. Until S2b lands, live NPCs never flee, investigate or pay blackmail.

## 2. Approach

**One rules component, two drivers.** Like `GroupActions` and `ApplyConditionalPostconditions` (already shared, static, free of randomness), the DES's rules move into a shared `TaleSocialRules` (`JoyceCode/engine/tale/TaleSocialRules.cs`):
- the selection order (forced, interrupt, resume, routine);
- request emission;
- claims;
- conditional postconditions;
- encounter interrupts;
- day resolution.

**What stays driver-specific:** each driver passes in its request pool, its logger and metrics, and its **random source**.
- **The DES keeps `_rng` unchanged, including the unused draw at `:630`,** so the bake reproduces bit for bit and needs **no re-bake**.
- **The live game passes per-event sources.** The roadmap (§S2b) already accepts that live encounters use different randomness than the DES.
- **The interaction type and trust of an encounter stay driver-specific:** the DES uses its `RelationshipTracker`; the live game uses `InteractionTypeRegistry` on `NpcSchedule.Trust`. `TaleSocialRules` does not unify them.

**Where the live rules run.**

| rule | where it runs live | thread |
|---|---|---|
| node arrival (selection order, request emission) | `TaleManager._advanceNpc`, materialised and Tier-3 alike; **not during the populate warm-up** | spawn workers and the logical thread |
| arrival encounters (claims both ways) | in `_advanceNpc`, with the DES's encounter model at arrival (`:362-372`, `EncounterResolver.CheckEncounters`) | as above |
| encounter (claims, conditional postconditions, interrupts) | `RuntimeSocialEvolver.TickEncounters`, every pair it lets meet | logical |
| day boundary (purge, abstract resolution) | the social frame, when the game day changes (as `_rollSocialMetricsDay`) | logical |

**Thread safety.**
- Each cluster's request pool is guarded by one lock: emission, claiming and purging are short, bounded critical sections.
- A pending interrupt is written and read under the same `_loSocial` lock that already guards the social state; `_advanceNpc` reads and clears it under that lock.
- Two threads advancing the same NPC at once remains a caller error, as `TaleManager.cs:25-30` already states.
- **A live `EncounterResolver` per populated cluster**, for the arrival encounters below. It is stateful and unsynchronised:
  - **State:** presence windows (`_activeByLocation`), a daily pair dedup and a shared result buffer (`EncounterResolver.cs:37-39`).
  - **Lifetime:** created and dropped with the cluster's request pool.
  - **Feeding:** `_advanceNpc` calls `RegisterPresence` on every advance.
  - **Housekeeping:** the social frame calls `ClearBefore` and `ClearDailyDedup`.
  - **Locking:** every call holds the cluster's pool lock (spawn workers and the logical thread both reach it).
  - **Dedup:** its daily pair dedup is separate from `RuntimeSocialEvolver`'s (`ClusterTickState.DailyDedup`), so a pair can meet once at arrival and once in the encounter tick on the same day. That is accepted.
- **Lock order: the cluster's pool lock first, then `_loSocial`.** Every call site takes them in this order; never the other way round.
- **The one cross-NPC write.** An arrival encounter may set an interrupt on the *partner* (§2, arrival claims below): a spawn worker writing another NPC's `ArcStack`. That write happens only under `_loSocial` (re-entrant), and the partner reads its `ArcStack` only under the same lock. No other per-NPC field is written across NPCs.

**Arrival encounters, both ways.** When an NPC arrives, `_advanceNpc` runs the DES's arrival encounter model: `EncounterResolver`'s probability by location type and overlap, with its daily pair dedup, against the NPCs co-located there. Not every co-located NPC counts. For each encounter, claims run **in both directions**, as the DES's `CheckAndClaimRequests(A, B)` and `(B, A)` do (`:412-413`). This direction matters for the headline chain: `threaten_harder` is `location: current`, so the threatener arrives and emits; the co-located target must claim and flee.

**Why claims run at node arrival too.** Requests are short-lived: threat 10 game minutes, pickpocket 5, argument 15, greeting 30. The encounter tick reaches a given cluster at most every 10 game minutes, less often with several populated clusters (round-robin). Claimed only there, most short requests would expire unclaimed, and the headline chain (threat → flee) would almost never happen live.

## 3. Work packages and order

| WP | Content | Depends on |
|---|---|---|
| **S2b-a** | Extract `TaleSocialRules` from `DesSimulation` with an injected random source. The DES behaves identically: the TALE suite, `ScenarioCompilerTests` and `DesSocialMetricsTests` pass unchanged, and a bake reproduces byte for byte. | — |
| **S2b-b** | A live request pool and a live `EncounterResolver` per populated cluster (`TaleManager`), under one lock: created with the cluster, dropped on depopulate, not saved. `_advanceNpc` uses the shared selection order, emits requests (except during the warm-up, by a flag like `applyGroupVerbs`), and runs arrival encounters with claims in both directions (§2), the partner's interrupt written under `_loSocial`. | a |
| **S2b-c** | Live encounters run the shared encounter rules (`RuntimeSocialEvolver` → `TaleSocialRules`), and the social frame runs day resolution and purges expired requests more often than daily. **Measure `TickEncounters` on the 4095-NPC cluster before and after, against an explicit budget** (§7). | a, b |
| **S2b-d** | Interrupts reach running NPCs: Tier-3 through the background tick, materialised through an event on the entity (§4.3, §4.4). | b, c |
| **S2b-e** | Acceptance (§6) and the TALE panel: pending interrupt, paused arcs, the cluster's open requests. The panel already shows `NextForcedStorylet`. | c, d |
| **S2b-f** | Housekeeping (§5): delete `models/tale/interactions/`, correct the roadmap's S3 line about `help_request` (`ask_for_help` in `worker.json` emits it), align ARCHITECTURE.md on `wait_edge`, and fix `ArcStack.cs`'s stale comment that `interrupt_scope` is unused. | — |

## 4. Details

### 4.1 Selection order, live (S2b-b)

- `_advanceNpc` takes the same four branches as §1.1 through `TaleSocialRules.SelectNext`. A resumed storylet draws a fresh duration, as in the DES (§1.1); S2b does not change that.
- The paused arc lives in the NPC's `ArcStack` (`NpcSchedule.cs:54`) and the forced storylet in `NextForcedStorylet` (`:56`).
- **Neither is saved:** `NpcScheduleJson` leaves both out on purpose. A paused or forced storylet is lost on save and load, and at depopulation. That is acceptable for interruptions that last minutes; recommended to keep it so.

### 4.2 Requests, live (S2b-b, S2b-c)

- **Scope:** one locked `InteractionPool` per populated cluster.
- **Lifetime:** requests carry their own timeout; the social frame purges expired ones and resolves unclaimed ones abstractly at the day boundary, as the DES does.
- **Depopulate:** the pool is dropped; requests are not persisted.
- **Claim cost:** `CheckAndClaimRequests` is O(active requests × role candidates) per encounter, and `GetActiveRequests` allocates a list per call. Index the pool by request type so a claimer looks only at requests its triggers can answer (optional in the review, taken here because the live encounter count is high).
- **Determinism:** live request ids are allocated in emission order, which depends on spawn-worker timing. A claim pick seeded by request id is deterministic within a run, not across runs. That is acceptable live; it is not claimed as part of the seed hierarchy.

### 4.3 Interrupting a materialised NPC (S2b-d)

Strategies have no per-frame hook, and the existing interruptions (hit, crash) arrive as events on the entity. Interrupts follow the same pattern:
- **From the NPC to its entity.** `TaleManager` (JoyceCode) has no entity lookup; the NPC-to-entity map is `TaleSpawnOperator._ownership` (nogame). So `TaleManager` raises a callback, `OnInterruptSet(npcId)`, whenever it sets an interrupt; `TaleSpawnOperator` registers it, resolves the entity (`_ownership`), and pushes the event on the logical thread (`QueueMainThreadAction` when called from a spawn worker). An NPC without an entity (Tier-3) needs no event: the background tick picks the interrupt up (§4.4).
- **Publishing:** the event is `InterruptEventPath` on the NPC's entity, as `CitizenCollisionRouter` publishes hit and crash.
- **Handling:** `TaleEntityStrategy` subscribes in `OnEnter` and unsubscribes in `OnExit`. On the event it ends the active walk or stay, records it for resumption the way `_preInterruptStrategyName` does for flee and recover, and calls `TaleManager.AdvanceNpc`, which applies the interrupt.
- **Defined behaviour:**
  - **While fleeing or recovering:** the interrupt stays pending; flee and recover take precedence. Their return paths, `_setupActivity(); TriggerStrategy("activity")` and `_resumeTravel()` (`TaleEntityStrategy.cs:138-148`), do not call `AdvanceNpc`, so each must check for a pending interrupt explicitly and take it.
  - **In Tier 2** (frozen strategy): stays pending until the NPC is active again.
  - **During an `await`** (e.g. `_travelToCurrentDestination`): the continuation re-enters through `TriggerStrategyOnLogicalThread`, which re-checks `IsAlive`, and reads the pending interrupt there. The entity is never touched from the continuation itself (CLAUDE.md, engine and threading).
  - **In a conversation with the player:** not interrupted until the conversation ends. The check is `TaleNarrationBindings.Session?.NpcId` (logical thread), not the presence of `TaleConversationBehavior`, which staying NPCs carry generally.

### 4.4 Interrupting a Tier-3 NPC (S2b-d)

The background tick advances schedules whose `CurrentEnd` has passed. A pending interrupt also counts as ended, stepped at the interrupt's time rather than at `CurrentEnd`, subject to the monotonic guard (`LastAdvanceTime` + 1 min). Budgets and the per-step cap stay as S0 set them.

## 5. Decisions

1. **`models/tale/interactions/`:** **delete it** (recommended). It is loaded nowhere and every storylet in it duplicates a shipped one.
2. **`wait_edge`:** not implemented anywhere. Recommended: leave it out of S2b and document it as parsed, not implemented. A requester that waits for its answer belongs with S3 (NPCs asking the player).
3. **`InterruptScope.Cancel`:** keep; it costs nothing, and since KAR-477 a claim trigger can declare it. (No shipped trigger declares any scope today, so all claims nest.)
4. **Encounter interrupts stay tied to `postconditions_if`, and resumption keeps drawing a fresh duration** (§1.1). Changing either would change the bake; it would be its own ticket with its own re-bake.

## 6. Verification

- **S2b-a:**
  - the TALE suite (133), `ScenarioCompilerTests` and `DesSocialMetricsTests` pass unchanged;
  - a bake before and after: the `sc-*` scenario files are byte-identical (same seed, same storylets); `scenario-statistics.json` is identical except its `generatedAt` timestamp (`ScenarioStatistics.cs:279`).
- **S2b-b/c (xUnit, live rules):** with a `TaleManager` populated from a small fixed model and the shipped storylets:
  - a `request` postcondition emits into the cluster's pool, and the warm-up emits none;
  - an arriving NPC co-located with a requester claims within the same advance;
  - an encounter in `TickEncounters` claims and sets the claimer's interrupt;
  - the social frame purges and resolves.
- **S2b-d:**
  - **Tier-3 (xUnit):** a pending `Nest` interrupt is applied in the next tick at the interrupt's time, and the paused storylet resumes later.
  - **Materialised:** in TestRunner, which can host an `Engine` and `TaleEntityStrategy` (as `TestRunner/BehaviorBugfixTests.cs` does). xUnit cannot drive the strategy.
- **S2b-e (acceptance)**, with the shipped content after PR #191, two halves:
  - **Escalation:** `demand_protection` meets a target with `target_fear` ≤ 0.5 → the refused branch forces `threaten_harder` → it issues a `threat` request.
  - **Claim:** a co-located NPC claims the threat → `flee_from_threat` runs as a `Nest` interrupt.

  Both halves run live in xUnit with abstract NPCs, and in TestRunner with the fleeing NPC materialised. The TALE panel shows the request and the interrupt during an in-game check.

## 7. Risks

- **Performance:** encounters and arrivals do more work. Budgets, to be checked by the S2b-c measurement on the 4095-NPC cluster:
  - `TickEncounters` for one cluster ≤ 2 ms at p99 (a target; there is no baseline yet);
  - the background tick's slice (up to 16 advances) ≤ 0.2 ms at p99.9, against S0 W5's measured 0.07 ms, as `BackgroundTickTests`' Perf measurement reports it.

  If exceeded, slice `TickEncounters` like the background tick.
- **Claim starvation:** if the arrival-time claim check finds too few co-located claimers, short requests still expire. The S2b-c test measures the live claim rate against the DES's for the same cluster; a rate far below the DES's is a finding for S2b-c, not a reason to lengthen timeouts.
- **Visible drama density:** live NPCs will flee, investigate and threaten again, now through real requests. The rate depends on the economy (KAR-485 is redesigning wealth); tune only after both land.
- **Save compatibility:** unaffected. `ArcStack`, `NextForcedStorylet` and the request pool are not saved; a load starts every NPC without a pending interruption.

## 8. Docs to update along the way

- `docs/tale/docs/ARCHITECTURE.md`:
  - §4 ("Interrupts (DES only)") and §5 ("Interaction pool (DES only)") lose "DES only";
  - §8 (the live runtime) describes the live rules;
  - line 147's `wait_edge` claim is corrected.
- `docs/tale/docs/REFERENCE.md`: `interrupt_scope`, and that claims nest by default.
- `TALE-EMERGENT-STORIES.md`: §S2b links to this plan and its decisions; the S3 line about `help_request` is corrected (S2b-f).
- A status-log fragment per work package.

## 9. Not in S2b

- Unifying the DES's and the live game's encounter typing and trust.
- `wait_edge`.
- Resuming with the remaining duration.
- Interrupts from storylets without `postconditions_if`.

Each is a deliberate behaviour change with its own ticket if wanted.

## 10. Review record

Review 1 (Fable, 2026-11-21): **AMBER**, ten required changes, all applied:
- **Threading (§2, §4.3):** spawn-worker `_advanceNpc`, the pool lock, and the interrupt hand-off under `_loSocial`.
- **Randomness (§2):** injected per driver; the DES keeps `_rng` and needs no re-bake.
- **Acceptance chain (§6):** rewritten to the shipped content: the refused branch, then a threat, then a flee.
- **Encounter interrupts and resumption (§1.1, §5.4):** interrupts only from `postconditions_if`; a resumed storylet draws a fresh duration.
- **Claims at node arrival (§2):** against short timeouts and the round-robin encounter tick.
- **Warm-up (S2b-b):** emits no requests.
- **Materialised interrupts (§4.3):** an event on the entity instead of a per-frame check, with flee, recover, Tier 2, continuations and conversation defined.
- **Performance (§7):** a measured `TickEncounters` baseline and an explicit budget.
- **Test harness (§6):** the materialised case runs in TestRunner.
- **References:** line and doc corrections (`:610`, ARCHITECTURE §4/§5/§8 and line 147, the roadmap's S3 line, the roadmap's accepted randomness divergence).

Review 1 read the pre-#191 `escalation.json`: PR #191 had already given `threaten_harder` its `threat` request. Its point about the refused branch stood.

Review 2 (Fable, 2026-11-21): **AMBER**, four required changes, all applied:
- **Arrival encounters (§2, S2b-b):** symmetric, with the DES's probability and dedup; the cross-NPC `ArcStack` write under `_loSocial`.
- **From NPC to entity (§4.3):** the `OnInterruptSet` callback resolved by `TaleSpawnOperator`.
- **Budget (§7):** per background-tick slice, against S0 W5.
- **Byte identity (§6):** of the `sc-*` files; the statistics file differs only in `generatedAt`.

Also applied: the flee and recover return paths are named.

Review 3 (Fable, 2026-11-21): **AMBER**, one required change, applied: the live per-cluster `EncounterResolver`, with its lifetime, feeding, housekeeping, lock and separate dedup (§2, S2b-b).

Review 4 (Fable, 2026-11-21): **GREEN**. Its note on lock order is applied (§2).
