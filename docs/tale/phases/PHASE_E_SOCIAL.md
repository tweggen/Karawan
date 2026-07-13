# TALE-SOCIAL Phase E: Living Factions

**Status:** In progress (E1–E4 implemented; E5 narration/co-location and E6 save-game persistence pending)
**Plan:** `docs/roadmap/proposed/IMPLEMENTATION-PLAN-TALE-SOCIAL-PHASE-E.md`
**Predecessor:** `PHASE_D_SOCIAL.md` (scenario pre-computation)

---

## Why

Phase D pre-computed social structure, but factions never visibly formed in the live game. Three stacked causes (investigation 2026-07-13):

1. **Zero runtime social simulation** — `EncounterResolver`/`RelationshipTracker`/`GroupDetector` lived only inside `DesSimulation` (bake + tests). The live game never generated encounters, never updated trust, never re-detected groups; the baked snapshot was applied once and discarded on cluster depopulation.
2. **The snapshot was invisible** — `ScenarioApplicator` reduced groups to a bare `GroupId` int; type/name/roster were dropped; no dialogue used `npc.group_id`; and `form_gang` *required* `in_group` while never setting it (circularity).
3. **Degraded bake data** — every group classified "social" (empty `GroupTypeRegistry` in Chushi), and Bron-Kerbosch produced 56–500 *overlapping* cliques with ~100% membership ("clique soup").

Phase E makes factions form, evolve, persist, and (E5) become visible.

---

## Architecture

```
BUILD (Chushi)                      ENGINE (JoyceCode/engine/tale/)
ScenarioCompiler ───────────────►   CommunityDetector   (E1b: label propagation
 + populated GroupTypeRegistry                            + mutual top-K)
   (E1a)                            ClusterSocialState  (E2: RuntimeGroup table
                                                          + NpcSocialSnapshot)
RUNTIME (nogame)                    RuntimeSocialEvolver (E3: encounter tick
TaleModule._onSocialFrame ──────►                         + re-detection)
 (OnLogicalFrame, 10 game-min)      GroupActions        (E4: form/join/leave,
TaleManager.AdvanceNpc ─────────►                         shared with DES)
ScenarioApplicator.ApplyResult.Groups ► TaleManager group table
GroupNameGenerator (nogame) ────►   TaleManager.GroupNameProvider hook
```

## Sub-phases

### E1a — Bake classification fix (commit `4cf74b45`)
`Chushi/ConsoleMain.cs` and `TestRunner/TestRunnerMain.cs` registered an **empty** `GroupTypeRegistry` (the `/groups/types` casette load only runs in `TaleModule.OnModuleActivate`). Every baked group fell through to `"social"`. Both now register `_CreateDefaultGroupTypeRegistry()` mirroring `models/nogame.group-types.json`.

### E1b — Community detection (commit `f18221a5`)
`engine.tale.CommunityDetector` replaces `GroupDetector`'s maximal cliques in `DesSimulation`:
- **Deterministic label propagation**: no RNG; ascending-NpcId sweep; label adoption by highest summed edge weight, ties to the lowest label id; ≤20 iterations.
- **Mutual top-K sparsification** (`MaxEdgesPerNode = 6`): an edge survives only if *both* endpoints rank it among their K strongest ties. Without it, the near-complete 365-day trust graph collapses into one giant community.
- `TrustThreshold = 0.6`, `MinCommunitySize = 3`, no cap needed (no combinatorial blowup).
- Communities are **disjoint**; all `GroupId`s are cleared before each detection (fixes stale ids from the overlapping-clique era).
- Two entry points: `Detect(RelationshipTracker, npcs)` (bake) and `DetectFromSchedules(npcs)` (runtime, reads `NpcSchedule.Trust`; ignores the player's `Trust[-1]` entry).
- `fear` bootstrapped per role in both property generators (drifter/hustler 0.10–0.20, authority 0.02–0.05, others 0.05–0.10), drawn **after** existing draws so per-seed values of other properties are unchanged.

Bake shape before → after: 56–500 overlapping cliques, 100% membership → **5–14 disjoint communities**, largest 26–86, membership ratio 0.74–0.80, types split criminal/social/trade.

### E2 — Runtime group table + social state (commit `e9bbf7a8`)
- `engine.tale.ClusterSocialState`: `RuntimeGroup { GroupId, Type, Name, MemberIds, HangoutLocationId, Authored }` + `NpcSocialSnapshot { GroupId, Trust, SocialProps }`.
- `ScenarioApplicator.ApplyResult.Groups` (`AppliedGroup`) carries scenario group rosters translated to real NpcIds (groups shrunk below 2 real members dropped).
- `TaleManager`: per-cluster `_socialStates` (lock `_loSocial`), **not** cleared on depopulate. `PopulateCluster` seeds the table from the applied scenario, then overlays snapshots (snapshot wins — evolution trumps the bake). `DepopulateCluster` snapshots every schedule first. Accessors: `GetGroup`, `GetSocialState`, `GetGroupmates`.
- Names: `nogame.modules.tale.GroupNameGenerator` — deterministic per (cluster, group), type-specific word lists ("The Rusted Syndicate", "The Central Watch"…), hooked via `TaleManager.GroupNameProvider`.
- Debug category `Dc.TaleSocial` → `debug.category.talesocial`.

### E3 — Runtime social evolution tick (commit 4)
`engine.tale.RuntimeSocialEvolver`, driven by `TaleModule._onSocialFrame` (`OnLogicalFrame`):
- **Encounter tick**: every `SocialTickGameMinutes = 10` game-minutes (12.5 real s at the default 30-min game day), ONE populated cluster (round-robin). NPCs bucketed by `CurrentLocationId` (transit + homes excluded); co-located pairs meet with the bake's probability model `P = 1-(1-p_loc)^(window/15min)` (venue 0.07, workplace/shop 0.04, street 0.015); trust deltas via `InteractionTypeRegistry` both directions; daily pair dedup; ≤12 pairs/location/tick; window capped at 60 game-min.
- **Re-detection**: every `RedetectGameHours = 2` per cluster, `CommunityDetector.DetectFromSchedules` over the evolved trust graph, reconciled with the group table: a community overlapping ≥half of an existing group's roster keeps its id+name; new communities get `NextGroupId++` + generated name; unmatched non-authored groups dissolve; **authored groups always survive** (members not claimed elsewhere are restored).
- **Determinism**: encounter RNG seeded `clusterIndex*73856093 ^ day*19349663 ^ tickIndex*83492791` (NOT `HashCode.Combine` — per-process randomized). Never touches storylet RNG.
- **Threading**: everything on the logical-frame thread; clusters mid-populate are naturally excluded because `PopulateCluster` marks the cluster populated only at the end.

### E4 — Storylet group verbs (commit 5)
- JSON: `"postconditions": { "group": "form" | "join" | "leave" }` — extracted at parse time into `StoryletDefinition.GroupAction` so `ApplyPostconditions` never `float.Parse`s it. New `"not_in_group": {}` precondition.
- `engine.tale.GroupActions` (no RNG, ordering (trust desc, NpcId asc)):
  - **form**: founder + up to 4 ungrouped neighbors with mutual trust ≥ 0.35; needs ≥ 2 partners else no-op; classifies via `GroupTypeRegistry`; group is `Authored`; emits `LogGangFormed` (first real emission path for the `gang_formed` test event).
  - **join**: adopt the group of the most-trusted grouped neighbor (≥ 0.3).
  - **leave**: remove from roster; dissolve below 2 members.
- Call sites: `TaleManager.AdvanceNpc` (via `_applyGroupVerb`, under `_loSocial`) and `DesSimulation.ProcessNodeArrival` (sim-local `ClusterSocialState`, `NextGroupId` from 100000 to avoid detector-id collisions; the 30-day detection pass may reorganize verb-formed memberships — DES gangs need trust maintenance).
- Content: `form_gang` fixed (`in_group` → `not_in_group`, `"group": "form"`); new `leave_gang` (morality ≥ 0.5) gives factions churn. This unblocks the whole drifter escalation chain (`demand_protection` → `collect_protection` / `threaten_harder`), whose `target_fear` postconditions are the organic path for fear.

### E5 — Player-visible factions (commit 6)
- Narration props (`TaleNarrationBindings.InjectNpcProps`): `npc.has_group` ("true"/"false"), `npc.group_type`, `npc.group_name`, `npc.group_size` — from `TaleManager.GetGroup`; string-safe resets in `ClearNpcProps`.
- Narration functions (`NarrationBindings`): `npcGroupType()` (returns "none" when ungrouped — scripts route on it directly), `npcGroupName()`.
- Dialogue (`models/tale/conversations/`): faction router branches — drifter/authority/merchant route criminal/patrol_unit/trade **before** the mood router (faction identity is the defining trait); worker/socialite route `social` **after** the mood branches (distress still wins); `tale.generic` gets a `has_group` line. Example: *"You don't want trouble with {func.npcGroupName()}. Trust me."*
- Co-location: `TaleManager.ResolveSocialVenue` sends grouped NPCs to their group's hangout — `GetGroupHangout` computes it lazily as the modal first social venue among members (tie → lowest venue id), cached on `RuntimeGroup.HangoutLocationId`. Applies to social-venue storylets only, so work/home schedules keep NPCs dispersed. The player sees a gang at its bar in the evening.
- `TaleManager.GetOrCreateSocialState` added (used by tests now, E6 save-restore later).

### E6 — Save-game persistence (pending)
In-session persistence landed with E2 (snapshots). Save layer: additive `GameState.TaleSocialState` JSON string serializing each `ClusterSocialState`.

---

## Operational gotchas (hard-won)

1. **The bake runs the *published* Chushi binary** (`Chushi/bin/Debug/net9.0/{rid}/publish/`). After any Chushi/JoyceCode change that affects the bake: `dotnet publish Chushi/Chushi.csproj -c Debug -r osx-arm64`.
2. **`sc-*` files must be deleted manually** to force a re-bake — `_IsScenarioUpToDate` watches config/tale JSON, not code.
3. **`models/tale/*.json` are runtime inputs to the TALE test suite** (TestRunner loads them live). Editing them while `./run_tests.sh` runs crashes the suite mid-run (observed: `float.Parse("leave")` FormatException from an old binary + new JSON). Never edit models/ or rebuild binaries during a gate run.
4. `Trace(Dc, ...)` takes an interpolated-string handler — string concatenation (`$"..." + string.Join(...)`) does not compile; fold everything into one interpolated string.

## How to run things

| Action | Command |
|---|---|
| Unit tests (90) | `dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj` |
| TALE regression | `./run_tests.sh standard` (~25 min; smoke for quick checks) |
| Re-bake scenarios | publish Chushi → delete `nogame/generated/sc-*` → `dotnet build nogame/nogame.csproj` |
| Watch factions live | set `debug.category.talesocial` in GlobalSettings; watch tick + re-detect trace lines |

## Live-game verification checklist

1. Enter a cluster → `group table seeded with N groups (criminal=…, social=…)`.
2. Tick lines: encounters > 0 near venues; trust edge counts rising.
3. After ~2.5 real minutes: re-detect line with named groups.
4. (E5) Talk to a grouped drifter → faction-flavored dialogue with the group name.
5. Leave cluster and return → same group names (snapshots).
6. (E6) Save + load → same.
