# TALE-SOCIAL Phase E: Living Factions

**Status:** Proposed
**Date:** 2026-07-13
**Companion design doc (to be created during implementation):** `docs/tale/phases/PHASE_E_SOCIAL.md`

---

## Context

The TALE architecture (docs/tale/TALE_ARCHITECTURE.md) promises NPCs that develop over time and form factions. Phase D pre-computes social structure (groups, trust edges, property snapshots) in an offline 365-day DES bake and re-attaches it at cluster population. Despite this, **no faction formation is observable in the live game**. Investigation (2026-07-13) found three stacked causes:

### Cause 1: Zero social simulation at runtime
`EncounterResolver`, `RelationshipTracker`, and `GroupDetector` are used ONLY inside `DesSimulation` (bake + tests). The live game (`TaleManager`) never generates NPC-NPC encounters, never updates `NpcSchedule.Trust`, and never (re-)detects groups. Social structure is a frozen snapshot applied once in `PopulateCluster` (`TaleManager.cs:145-187`) — and discarded on `DepopulateCluster` (`TaleManager.cs:242-264`) for all non-deviated NPCs.

### Cause 2: The frozen snapshot is nearly invisible
Complete list of runtime group-data consumers:
- `StoryletSelector.cs:135-137` — `in_group` precondition. **Circularity bug:** `form_gang` in `models/tale/escalation.json` *requires* `in_group` but its postconditions never set `GroupId`, so gang formation storylets can never bootstrap a gang.
- `TaleNarrationBindings.cs:87` — `npc.group_id` prop; **no dialogue script uses it**.
- Save/load (`TaleModule.cs:451/529`).

`ScenarioGroup` carries `Type` (patrol_unit/criminal/trade/social) and `MemberRanks`, but `ScenarioApplicator.Apply` (`ScenarioApplicator.cs:182`) writes only an int `GroupId` per NPC — group type, name, and roster are dropped; TaleManager has no group table.

### Cause 3: Baked group data is degraded
- **All baked groups classify as "social".** Root cause (verified): `Chushi/ConsoleMain.cs:124` registers `new GroupTypeRegistry()` — **empty** — while Role/Interaction/Tier registries get default content. The `/groups/types` config load lives only in `TaleModule.OnModuleActivate`, which never runs in Chushi. Empty registry → `"social"` fallback (`GroupTypeRegistry.cs:56`).
- **"Clique soup":** `GroupDetector` (Bron-Kerbosch maximal cliques, TrustThreshold=0.75, MinCliqueSize=3, MaxCliques=500) yields 56–500 overlapping cliques per scenario with ~100% NPC membership — graph artifacts, not narratively coherent factions. Overlapping cliques also repeatedly overwrite `npc.GroupId` (`GroupDetector.cs:66-70`).
- **13 of 25 scenarios**: the "large" category is intentionally disabled (`models/nogame.scenarios.json`: `count: 0`, dev-cost comment) — NOT a bug; leave disabled for development.
- **`fear` permanently 0** (D5 concern #5): initialized 0, no postcondition path raises it — the `target_fear` raisers are behind the `in_group`-gated storylets that can never fire (see Cause 2).
- `IEventLogger.LogGangFormed` exists but has **zero call sites**; phase5 test 14 expects a `gang_formed` event from a nonexistent code path.

## Approved direction (user decision 2026-07-13)

**Hybrid evolution**: runtime trust accumulation from co-located encounters + periodic community re-detection, PLUS storylet verbs (`form`/`join`/`leave` group) for authored faction beats. Scope includes: player-visible factions, bake-quality fixes, and per-cluster social-state persistence.

---

## Sub-phase E1 — Bake quality (no runtime changes)

**E1a: Fix all-"social" classification**
- `Chushi/ConsoleMain.cs`: add `_CreateDefaultGroupTypeRegistry()` mirroring `models/nogame.group-types.json` (patrol_unit prio 100 / criminal prio 90: wealth_max 0.3 + morality_max 0.4 / trade prio 80 / social fallback), register at line 124. Follow the `_CreateDefaultRoleRegistry` pattern (line 467). Mirror in `TestRunner/TestRunnerMain.cs`.
- Note latent bug in doc: `GroupTypeRegistry.EvaluatePropertyThreshold` can't work (`Parameters` is `Dictionary<string,float>`, can't carry a property name); unused by current config.

**E1b: Community detection (replace clique soup)**
- New `JoyceCode/engine/tale/CommunityDetector.cs`: deterministic label propagation over trust edges (threshold ~0.5, no RNG — ascending-NpcId iteration, tie-break by summed edge weight then lowest label, max ~20 iterations). Output reuses `GroupDetectionResult`/`DetectedGroup` DTOs so `SimMetrics` and `ScenarioExporter.Build` are untouched. Disjoint groups, `MinCommunitySize = 3`, classify via `GroupTypeRegistry`. Two entry points: `Detect(RelationshipTracker, npcs)` (bake) and `DetectFromSchedules(npcs)` (runtime, reads `NpcSchedule.Trust`).
- `DesSimulation.cs`: swap `_groupDetector` (lines 82, 126, 167) for `CommunityDetector`. Keep `GroupDetector.cs` (mark obsolete in doc).
- Fear bootstrap: init `fear` per role at 0.05–0.15 in **both** `TalePopulationGenerator.GenerateProperties` and `ScenarioCompiler.GenerateProperties` (dual-site tuning mandated by comment at `ScenarioCompiler.cs:364`).
- Re-bake: **manually delete `nogame/generated/sc-*` first** (`_IsScenarioUpToDate` doesn't watch detector code), then `dotnet build nogame/nogame.csproj`. Verify statistics: 3–15 disjoint groups/scenario, membership ratio < 1.0, non-zero criminal/trade/patrol counts. Scenario schema unchanged, `Version` stays 1.

**Tests:** new `CommunityDetectorTests` (determinism ×2, disjointness, min-size, threshold boundary), `GroupTypeRegistryTests` (rules fire; empty registry → "social"). Existing `ScenarioCompilerTests` byte-equality determinism test must stay green. `./run_tests.sh smoke` per commit, `standard` before push.

## Sub-phase E2 — Runtime group table + social state container

- New `JoyceCode/engine/tale/ClusterSocialState.cs`:
  - `RuntimeGroup { GroupId, Type, Name, MemberIds, HangoutLocationId, Authored }`
  - `ClusterSocialState { ClusterIndex, Groups, NextGroupId, LastDetectionGameTime, LastTickGameTime, DailyPairDedup, LastDedupGameDay, Snapshots }`
  - `NpcSocialSnapshot { GroupId, Trust, SocialProps }` (keyed by NpcId — NpcIds are deterministic per cluster: `TalePopulationGenerator.cs:84-95`)
  - Trust stays **single-sourced in `NpcSchedule.Trust`**; do NOT add a runtime `RelationshipTracker` (second source of truth save/load doesn't know).
- `TaleManager.cs`: `_socialStates` dict + `_loSocial` lock, NOT cleared on depopulate. `PopulateCluster`: after `Apply`, build group table from new `ApplyResult.Groups`, then overlay `Snapshots` (snapshot wins over bake). `DepopulateCluster`: write snapshots before removing schedules. Accessors: `GetGroup`, `GetSocialState`, `GetGroupmates`.
- `ScenarioApplicator.cs`: extend `ApplyResult` with `List<AppliedGroup> { GroupRank, Type, RealMemberIds }` — map `scenario.Groups[].MemberRanks` through the existing rank→NpcId map; drop groups shrinking below 2 members.
- New `nogameCode/nogame/modules/tale/GroupNameGenerator.cs` (game content layer): deterministic names from `RandomSource($"{clusterIndex}-group-{groupId}")` with type-specific word lists; TaleManager exposes a `GroupNameProvider` hook set by TaleModule.
- `JoyceCode/engine/DebugCategories.cs`: append `TaleSocial` (never reorder); all new code uses `Dc.TaleSocial`.

**Tests:** `ClusterSocialStateTests` (snapshot round-trip, overlay precedence, NextGroupId above baked ranks); `ScenarioApplicatorTests` additions (Groups carried, determinism preserved).

## Sub-phase E3 — Runtime social evolution (periodic tick)

**Mechanism: periodic social tick** (not lazy-on-AdvanceNpc). Rationale: `AdvanceNpc` fires only for materialized NPCs + at spawn and runs concurrently (see `_deltasBuffer` history); a tick is throttleable, staggered per cluster, deterministic, single-threaded.

- New `JoyceCode/engine/tale/RuntimeSocialEvolver.cs`:
  - `TickEncounters(state, clusterNpcs, spatial, gameNow, windowStart)`: bucket schedules by `CurrentLocationId` excluding in-transit (`IsInTransit`/`TransitEnd`, `NpcSchedule.cs:102-123`); encounter probability reuses `EncounterResolver` constants (`P=0.07/0.04/0.015`, `p = 1-(1-pBase)^(windowMinutes/15)`) but not the class; dedup via `state.DailyPairDedup` + `RelationshipTracker.PairKey`, reset on day rollover; cap ~12 sampled pairs/location/tick; RNG = `Random(HashCode.Combine(clusterIndex, gameDay, tickIndex))` — **never consume `TaleManager._rng`** (would shift storylet sequences). Trust delta path: `InteractionTypeRegistry.EvaluateConditions` + `GetTrustDelta` (same as `RelationshipTracker.RecordInteraction`, `RelationshipTracker.cs:56-84`), written bidirectionally into `Trust` dicts, clamped 0..1; set `LastEncounterPartnerId` both ways.
  - `Redetect(...)`: `CommunityDetector.DetectFromSchedules`, reconcile with existing groups by >50% member overlap (keep GroupId/Name) else `NextGroupId++`; update `GroupId`s; drop dissolved groups; **authored (verb-formed) groups are never removed by detection**, only grown.
- Wiring in `TaleModule.cs` (daynite `Controller.cs:140` pattern): `_engine.OnLogicalFrame += _onSocialFrame` in `OnModuleActivate`. Every **10 game minutes** (12.5 real s at default timescale) tick ONE populated cluster round-robin. Re-detect every **2 game hours** per cluster (2.5 real min): snapshot on logical thread → `_engine.Run(...)` off-thread detect → apply via `QueueMainThreadAction`. One summary trace line per tick.

**Tests:** `RuntimeSocialEvolverTests` (deterministic trust gain ×2, transit exclusion, dedup + rollover, pair cap, reconciliation keeps GroupId on overlap, authored groups survive).

## Sub-phase E4 — Storylet group verbs + form_gang fix

- JSON syntax inside existing `postconditions`: `"group": "form" | "join" | "leave"`. Parse in `StoryletDefinition.cs` (loop ~lines 229-236) into new `GroupAction` field, **excluded from `Postconditions`** (else `float.Parse` throws in `ApplyPostconditions`).
- New precondition `"not_in_group": {}` in `StoryletSelector.PassesPreconditions` (next to `in_group` at line 136; also skip in the range loop at line 128).
- New `JoyceCode/engine/tale/GroupActions.cs` shared by DES + runtime:
  - **form**: ungrouped candidates with avg trust ≥ 0.4, top 4 by (trust desc, NpcId asc), require ≥ 2 partners; create group, classify, set members' GroupId, call `logger?.LogGangFormed(...)` (first real emission path).
  - **join**: adopt group of highest-trust grouped neighbor (trust ≥ 0.3).
  - **leave**: remove from roster; dissolve group if < 2 remain.
  - Call sites: `TaleManager.AdvanceNpc` after `ApplyPostconditions` (~lines 473-482, guard `_socialStates` with `_loSocial` — spawn catch-up runs in async tasks); `DesSimulation.ProcessNodeArrival` with a sim-local adapter.
- `models/tale/escalation.json`: `form_gang` precondition `in_group` → `not_in_group`, add `"group": "form"`. Breaks the circularity; unlocks `demand_protection`/`collect_protection` whose `target_fear` postconditions finally move fear off zero. Optional `leave_gang` storylet (morality ≥ 0.5, `in_group`, `"group": "leave"`).

**Tests:** `GroupActionsTests`, `StoryletDefinitionGroupParseTests`. TALE regression: re-run `phase5` (test 14 `gang_formed >= 1` should now genuinely pass; tune fixture NPC properties if the 11-NPC/60-day window doesn't form a gang — never ignore). Run `standard`; budget recalibration of phase2/phase6 distribution assertions (in_group gates opening shifts equilibria).

## Sub-phase E5 — Player-visible factions

- `TaleNarrationBindings.InjectNpcProps` (after line 87): set `npc.has_group`, `npc.group_type`, `npc.group_name`, `npc.group_size` from `TaleManager.GetGroup`; extend `ClearNpcProps` string-safe resets (pattern: `npc.role` special case at line 130).
- `NarrationBindings.cs` (next to `npcMood`/`npcRole`, lines 319-344): register `npcGroupType()`, `npcGroupName()`.
- Dialogue variants in `models/tale/conversations/` — `goto` router on `func.npcGroupType()` before the mood router (follow `tale.role.drifter.json` pattern): criminal branch in drifter, patrol_unit in authority, trade in merchant, social in worker/socialite, one `has_group` fallback in `tale.generic.json`.
- Co-location bias: `TaleManager.ResolveSocialVenue` (line 635) — if grouped and group's `HangoutLocationId` valid, return it; else existing rotation. `HangoutLocationId` = modal `SocialVenueIds[0]` among members at group creation/reconciliation. Bias applies only to social-venue storylets (no runaway convergence).
- Debug visibility: trace dump of cluster group table on re-detection (id/type/name/size/hangout). Map overlay deferred.

**Tests:** hangout-bias unit test (may need `internal` + `InternalsVisibleTo` for `ResolveSocialVenue`); manual live-game checks (below).

## Sub-phase E6 — Persistence

- In-memory layer (already E2's `Snapshots`): survives depopulate/repopulate within a session — this fixes the observable "factions reset when you leave" bug.
- Save-game layer: `GameState.cs` add `public string TaleSocialState { get; set; } = "";` (additive, old saves compatible). `TaleModule._onBeforeSaveGame`/`_onAfterLoadGame` (lines 176-228): serialize each `ClusterSocialState` (groups + snapshots) in the existing `JsonObject` style. Caveat to trace: if `ComputeNpcCount` changes across world-gen changes, drop out-of-range snapshot entries with a `Trace(_dc, ...)`.

**Tests:** serialization round-trip; manual save/load + leave/return checks.

---

## Commit breakdown (each independently green)

1. `Fix TALE-SOCIAL bake: group classification` — E1a + tests + re-bake (verify statistics show non-social types)
2. `Implement TALE-SOCIAL E1: community detection for scenario bake` — E1b + fear bootstrap + tests + re-bake + PHASE_D_SOCIAL.md follow-up
3. `Implement TALE-SOCIAL E2: runtime group table + social state`
4. `Implement TALE-SOCIAL E3: runtime social evolution tick`
5. `Implement TALE-SOCIAL E4: group postcondition verbs` (+ phase5 recalibration)
6. `Implement TALE-SOCIAL E5: faction narration + co-location`
7. `Implement TALE-SOCIAL E6: social state persistence` (+ docs finalization, move this plan to done/)

## Manual verification (live game, enable `debug.category.talesocial`)

1. Enter cluster → `Applied scenario ... touched N groups` + group table dump with non-zero criminal/trade/patrol types.
2. Tick summaries: encounters > 0 near venues at social hours; trust edges rising.
3. After ~2.5 real min: re-detection line, small membership churn.
4. Talk to grouped drifter → gang-flavored line with group name; ungrouped → old mood lines.
5. Evening: multiple members of one group at the same venue.
6. Leave cluster + return: same group names; save + load: same.

## Risks

- **Thread safety** (TALE has a history: postcondition delta buffers, StoryletSelector candidate lists): confine mutation to the logical-frame tick + `AdvanceNpc`; off-thread detection on snapshots only, applied via `QueueMainThreadAction`; guard the group table with `_loSocial` (spawn catch-up is async).
- **Determinism**: evolver uses its own seeded RNG; never touch `TaleManager._rng` or storylet RNG. `ScenarioCompilerTests` byte-equality is the bake tripwire.
- **Regression recalibration**: opening `in_group` gates shifts long-sim equilibria; retune phase5/phase6 ranges deliberately, re-run recalibration per PROCESS_TALE.md.
- **Bake artifacts**: commit re-baked `sc-*`/statistics with commits 1–2; remember the manual `sc-*` deletion (incremental check misses detector code changes).
- **Save compatibility**: additive only.

## Documentation updates (mandatory per PROCESS.md)

- New `docs/tale/phases/PHASE_E_SOCIAL.md` (architecture + tuning numbers).
- `docs/tale/phases/PHASE_D_SOCIAL.md`: resolve the five D5 concerns (1: obsoleted by community detection; 2/3: new statistics; 4: partial; 5: fear bootstrap + circularity fix); correct the "25 scenarios" claim (large disabled by config, not a bake failure).
- `CLAUDE.md`: status lines (D follow-up resolved, Phase E entries), test counts.
- `docs/TESTING/TESTING_STRATEGY.md` + `docs/PROCESS_TALE.md` if fixture counts change.
- Move this plan to `docs/roadmap/done/` when complete.
