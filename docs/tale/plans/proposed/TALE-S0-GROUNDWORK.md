# TALE S0 — Ground to Stand On (stage plan)

> **Status:** proposed; review GREEN 2026-10-18 (Fable, three rounds) · **Written:** 2026-10-18 · **Ticket:** KAR-437 (stage S0)
> **Roadmap:** [TALE-EMERGENT-STORIES.md](TALE-EMERGENT-STORIES.md) §S0 · **Shipped design:** [../../docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md)

S0 makes the society measurable, visible and continuous before S1 lets the
player into it. It has nine work packages (W1…W9). This plan says what each
one changes, in which order, how it is tested, and when it is done. It was
designed against `origin/master` at `ee10f446`; every line reference below
is from that commit and is a starting point, not a contract.

Nothing here changes the save format or `DBStorage.DbVersion` (bumping it
deletes `gamestate.db`, CLAUDE.md).

## 1. What S0 found in the code

The design work turned up more than the roadmap knew. The facts that shape
the packages:

1. **The distance-based despawn of TALE NPCs never runs** (found in
   review). `SpawnSystem` counts entities by their exact `Behavior.Provider`
   type (`SpawnSystem.cs:47`, `BehaviorStats.cs:27-34`), and
   `TaleSpawnOperator.BehaviorType` is `typeof(IdleBehavior)`
   (`TaleSpawnOperator.cs:57`). TALE NPCs never carry `IdleBehavior`:
   outdoors they carry `TaleConversationBehavior`
   (`StayAtStrategyPart.cs:57-62`), indoors no behaviour (hidden,
   `:75-80`), travelling `TaleWalkBehavior` (`GoToStrategyPart.cs:174-186`).
   So the operator's entity count is always 0, nothing is ever "too many",
   and `TerminateCharacters` — with its Tier-2 freeze and its 30-minute
   `ForgetSchedule` (`TaleSpawnOperator.cs:438-469`) — is dead code. The
   player's NPCs are not forgotten in play today; Tier 2 never happens.
   Side effect: every poll (every 40 frames) queues `MinCharacters` spawn
   actions per fragment, which find no unmaterialised NPC and do nothing
   (`SpawnController.cs:162-187`).
2. **Despawn is tracked by the wrong fragment.** TALE entities die when the
   fragment that *created* them unloads (`Owner` set once,
   `EntityCreator.cs:137-139`; `Fragment.RemoveFragmentEntities`,
   `Fragment.cs:353-372`) or through the wiper (`Loader.cs:316`), both via
   the doom drain (`Engine.cs:368-379`). `PurgeFragment` clears the
   materialised flag for the NPCs whose *schedule position* is in the
   purged fragment (`GetNpcsInFragment`, `TaleManager.cs:587-602`). NPCs
   walk across 400 m fragment borders routinely, so: the creation fragment
   unloads while the schedule points elsewhere → the entity is gone but the
   flag stays → the NPC is not respawned until that other fragment is
   purged, possibly never; or the schedule's fragment unloads while the
   creation fragment stays → the flag is cleared while the entity lives →
   a **second entity for the same NPC** on the next poll.
3. **`DepopulateCluster` would wipe the player's NPCs if wired as is.** It
   keeps deviated schedules (`TaleManager.cs:282-287`) but never adds them
   to the cluster's skip mask (only `RegisterNpc`, `:557-570`, does). On
   repopulation `Generate` produces the same `NpcId` (ids are
   `clusterIndex<<12 | npcIndex`, `NpcSchedule.cs:130`) and
   `_schedules[id] = schedule` (`:131`) overwrites the kept schedule.
4. **No cluster-unload event exists.** `ClusterCompletedEvent` fires once
   per cluster per session; only fragments unload
   (`Loader._releaseFragmentList`, `Loader.cs:70-98` →
   `SpawnController.PurgeFragment` → `TaleSpawnOperator.PurgeFragment`).
   Population is fragment-driven: polling a fragment of an unpopulated
   cluster populates it (`_ensureClusterPopulated`,
   `TaleSpawnOperator.cs:59-82,155-171`).
5. **The warm-up is two steps, not a day.** `PopulateCluster` calls
   `AdvanceNpc` exactly twice per NPC (`TaleManager.cs:137-159`); after
   that, every non-materialised schedule stands still. The trace at `:151`
   ("through full cycle (24+ hours)") and ARCHITECTURE §6.2's order
   (warm-up listed last; it runs before scenario and snapshot overlay) are
   wrong.
6. **`AdvanceNpc` is not deterministic per NPC, and not thread-safe.**
   Storylet duration (`:799`) and street choice (`:1013-1016`) draw from
   the one shared `System.Random _rng` (`:81`). Results depend on the
   global call order, and spawn workers already call `AdvanceNpc` from
   pool threads (`TaleSpawnOperator.cs:280`) while the logical thread
   does too. `System.Random` is not thread-safe.
7. **TALE's collections are unguarded.** `_schedules` is a plain
   `Dictionary` (`TaleManager.cs:29`), `_materializedNpcIds` and
   `_tier2NpcIds` are `HashSet`s; only the group table has a lock
   (`_loSocial`). Writers are on the logical thread and on spawn workers.
8. **Snapshots don't carry `player_fact.*`.** `ClusterSocialState`
   snapshots hold trust, group and five properties
   (`ClusterSocialState.cs:68-69`); the save's deviation record holds all
   properties and the schedule state (`TaleModule.cs:585-655`).
9. **Testbed rates are meaningless, not just hard-coded.** Besides the
   three literal 1.0 rates (`SimMetrics.cs:189,244,248`, KAR-436 item 4):
   DES never cuts a storylet short (a pending interrupt is applied only at
   the next arrival, `DesSimulation.cs:216-275`), so "completed without
   interruption" is 1.0 by construction; and `_dailyInterruptsByNpc`
   (`SimMetrics.cs:39`) is read but never written, so
   `interrupts_per_day` is always 0. DES group ids are renumbered at every
   detection (`DesSimulation.cs:134-138`), so churn can't be computed by
   comparing ids.
10. **The ImGui debugger renders on the render thread**
    (`Splash.OpenGL/Platform.cs:555`), so a TALE panel must not read
    `TaleManager` directly.
11. **The bake's match result is thrown away.** `ScenarioApplicator`'s
    result is logged (`TaleManager.cs:181-188`) and dropped; nothing can
    report the match ratio today.

## 2. Work packages and order

| W | Package | Ticket | Needs |
|---|---|---|---|
| W1 | TALE scripts that check something | KAR-433 | — |
| W2 | `AdvanceNpc` deterministic and thread-safe; entry positions | KAR-446 | — |
| W3 | Despawn follows the entity; the player's NPCs are kept explicitly | KAR-447 | W2 |
| W4 | Leaving a cluster keeps its social state | KAR-434 | W3 |
| W5 | Background tick for Tier-3 schedules | KAR-450 | W2, W3 |
| W6 | Shared social metrics (incl. Testbed rates) | KAR-448 | — |
| W7 | Per-NPC debug overlay | KAR-449 | — |
| W8 | ImGui TALE panel | KAR-451 | W6 |
| W9 | Measurements and baselines | KAR-452 | W5, W6, KAR-435; W8 for the live readout |
| — | Re-measure NPC commuting | KAR-435 | — |

W1, W2, W6, W7 and KAR-435 can start in parallel. The critical path is
W2 → W3 → W5 → W9.

KAR-436 item 3 (junction-centre position) is done in W2, item 4 (Testbed
rates) in W6; its other items stay in KAR-436 or moved to S2b.

## 3. W1 — TALE scripts that check something (KAR-433)

> **Status:** implemented (KAR-433), 2026-10-20. Phase 5 rewritten (15 kept, 5 removed);
> C1–C4 removed and their pure rules moved to xUnit (`ConversationRulesTests`); phase 7's
> placeholders deleted; `ScriptRunner` validation plus `TaleScriptsLoadTests` in CI; both
> mutation checks fail named xUnit tests. Not done: 34 phase-6 scripts still wait only for
> harness-pushed events (KAR-468). Details: `docs/tale/docs/tests/PHASE_5.md`, `PHASE_C.md`.

As in KAR-433 and its scope update:

- Convert the 49 phase-5 and C1–C4 scripts from the ignored `"expected"`
  block to `steps` with `expect` steps, using events that are actually
  emitted; decide per script whether it can be meaningful in the
  TestRunner world, and rewrite or delete it otherwise.
- The 102 `phase7-spatial` prose placeholders: convert what can assert
  something, delete the rest.
- `ScriptRunner` fails a script with zero steps or an unknown top-level
  key.
- Phase 5 test 03 (Cancel) is rewritten or dropped until S2b decides on
  `InterruptScope.Cancel`.
- Two mutation checks with named tests: reverting the +0.02 conversation
  trust (`TaleModule.cs:766-767`) and dropping the group verb in
  `AdvanceNpc` each make a named test fail. TestRunner references nogame
  (`TestRunner.csproj:21`), so the +0.02 can be checked by a script if
  TestRunner can end a conversation; otherwise that check lands with W3's
  `MarkPlayerContact` (and moves to S1-a's chat interaction later). The
  group verb is tested through the public `AdvanceNpc` with a storylet
  from `StoryletLibrary.LoadFromString` (`_applyGroupVerb` is private).

*Done when:* every TALE script has at least one assertion, a script with no
steps fails, both mutations fail a named test, and test counts in CLAUDE.md
and the test docs are true.

## 4. W2 — `AdvanceNpc` deterministic and thread-safe; entry positions

- **Per-NPC randomness.** Replace the shared `_rng` in `AdvanceNpc` and
  street resolution (`TaleManager.cs:799,1013-1016`) with per-(NPC, step)
  sources, e.g. `RandomSource($"{NpcId}-dur-{ScheduleStep}")` and
  `…-street-…`, as the jitter already does (`:803`). No `System.Random`
  is shared across NPCs in `AdvanceNpc` afterwards.
- **Entry positions (KAR-436 item 3).** `CurrentWorldPosition =
  loc.EntryPositionFor(NpcId)` instead of `loc.Position` (`:832`), so
  fragment bucketing (`GetNpcsInFragment`, `:587-601`, the only reader
  that matters) agrees with where the NPC stands (`NpcSchedule.PositionAt`,
  `:150-170`). Same for the initial `CurrentWorldPosition` at birth
  (`TalePopulationGenerator.cs:191`); `HomePosition` stays as it is.
- **Thread-safe collections.** `_schedules` becomes
  `ConcurrentDictionary<int, NpcSchedule>` (it implements
  `IReadOnlyDictionary`, so `AllSchedules` keeps its type);
  `_materializedNpcIds` and `_tier2NpcIds` become concurrent sets
  (`ConcurrentDictionary<int, byte>`); so do `_spatialModels`,
  `_populatedClusters` and `_deviationSkipMasks`, which W4's depopulation
  will write on the logical thread while spawn workers read them
  (`GetSpatialModel`, `TaleSpawnOperator.cs:285`, and inside `AdvanceNpc`).
- **Shared delta buffer:** `_deltasBuffer` (`TaleManager.cs:56`) is one
  buffer cleared per call; it only feeds logging (`RecordDelta` writes
  `npc.Properties` directly), so make it per call or document that its
  contents are unreliable under concurrency.
- **Cheap wins:** cache the street-location list in `SpatialModel`
  (today `Locations.FindAll(...)` per advance).
- **Docs:** ARCHITECTURE §6.2 (warm-up is two steps and runs before the
  scenario), the trace at `:151`, PHASE_7's "known gap" note
  (`PHASE_7.md:115-117`).

*Tests (xUnit, `tests/JoyceCode.Tests/engine/tale/`):*
- `SameSeedSamePositions`: two managers with the same NPCs, one with an
  extra unrelated NPC advanced in between, give identical results for the
  shared NPCs (fails today).
- `CurrentWorldPositionIsEntryPosition`: after `AdvanceNpc` at a street
  location with entry candidates, `CurrentWorldPosition ==
  loc.EntryPositionFor(id)` (fails today).

*Done when:* both tests pass and fail on master, and `./run_tests.sh all`
is green. Check whether any TestRunner phase-6 output depends on the old
`_rng` sequence (`TestRunnerMain.cs:552` calls `AdvanceNpc`).

## 5. W3 — Despawn follows the entity; the player's NPCs are kept explicitly

Today the player's NPCs survive in play only because the forget path is
dead (finding 1), and despawn bookkeeping is wrong (finding 2). W3 makes
both explicit.

**Rule.** A schedule the player has touched is never removed from
`TaleManager` — not on despawn, not on depopulation (W4).

- `NpcSchedule.IsKnownToPlayer => HasPlayerDeviation ||
  Trust.ContainsKey(-1) || any Properties key starting with
  "player_fact."`.
- `TaleManager.MarkPlayerContact(schedule)` is the one place that sets
  `HasPlayerDeviation` whenever `Trust[-1]` or a `player_fact.*` is
  written; `TaleModule.cs:768` and `:385` use it, and S1 will. A schedule
  with `IsKnownToPlayer` but no flag logs a `Warning` (so the save, which
  includes only flagged NPCs, cannot miss it).

**Despawn by entity, not by schedule position.**
- `TaleSpawnOperator` records, at spawn, which fragment owns each NPC's
  entity (the polled fragment, which `EntityCreator` makes the `Owner`,
  `TaleSpawnOperator.cs:251,350`) and the entity handle, in a map
  npcId → (owner fragment, entity) under its `_lo`. The handle serves the
  position sync and the wiper liveness check without a world query.
- `PurgeFragment(idx)` dematerialises exactly the NPCs whose entity is
  owned by `idx` (from the map), not those whose schedule position lies
  in it, and syncs each schedule's position from the entity first while
  it still exists.
- The wiper path (`Loader.cs:316` → `WiperSystem`) needs the same
  bookkeeping: either the wiper's victims are reported to the operator, or
  a cheap check on each poll drops map entries whose entity is no longer
  alive and dematerialises them. The implementer picks one; both run on
  the logical thread. (The ImGui debugger's "release fragments" action
  calls `Loader.WorldLoaderReleaseFragments` from the render thread,
  `ui/Main.cs:184`; route it through `QueueMainThreadAction` in W3 — despite its name
  that queues onto the logical thread, `Engine.cs:467-470`.)
- New `TaleManager.OnNpcDematerialized(npcId, worldPos)`: records the
  position and clears the materialised flag. It never removes a schedule.
  Being in the engine, it is testable in xUnit (the test project cannot
  reference nogame).

**Remove the dead path.** Delete `ForgetSchedule` and the Tier-2 branch of
`TerminateCharacters`; `TerminateCharacters` stays as an empty
`ISpawnOperator` member. Leave the spawn counting as it is: making it
count TALE entities would activate distance kills with a random victim per
fragment, which S0 doesn't need. Stop the no-op spawn actions instead.
`SpawnStatus.ResidentCharacters` is a getter (`InCreation + Dead`,
`SpawnStatus.cs:57-69`), so the operator sets `SpawnStatus.Dead` (or a new
settable `Resident` field on `SpawnStatus`, an engine change) to the number
of **owner-map entries for this fragment whose entity is alive**, written
in the entity-setup action — the same action that does `InCreation--`
(`TaleSpawnOperator.cs:394`); keep the two together — so an NPC still in
creation is counted once (via `InCreation`), and keeps `MinCharacters = MaxCharacters` = the
bucketed count. Then spawns are queued only for missing NPCs and the
living count never exceeds the maximum. Counting materialised flags
instead would count an NPC in creation twice (`SetMaterialized` comes
before `InCreation++`, `TaleSpawnOperator.cs:229,236`) and grow `ToKill`,
which is never reset (`PerFragmentStats.cs:33-36`). Tier 2 (frozen entities kept after
despawn) is not used by S0; `ConversationMemorySeconds`,
`EnterTier2Mode`/`ExitTier2Mode` stay as they are and are documented as
unused, to be removed or revived by a later stage. Fix the comments at
`TaleManager.cs:681-713`, `NpcSchedule.cs:84-96`,
`TaleSpawnOperator.cs:436-469`.

*Tests (xUnit, engine level):*
- `Dematerialize_KeepsScheduleTrustAndFacts`: a known NPC dematerialised
  an hour after the last conversation keeps its schedule, `Trust[-1]` and
  `player_fact.*`, is no longer materialised and is bucketed at the
  synced position.
- `KnownToPlayerWithoutFlag_Warns`.
- Owner-map logic as a small engine helper (`TaleEntityOwnership`):
  purging fragment A dematerialises the NPC created in A whose schedule
  points into B; purging B leaves the NPC created in A materialised
  (both fail with today's bucket logic); the resident count excludes
  entities in creation and dead ones.

*Done when:* the tests pass, no caller of `ForgetSchedule` remains, and in
the game: after walking an NPC across a fragment border and driving away
and back, it reappears exactly once (W7's overlay shows one label per
NPC), and an NPC talked to earlier reappears with its `Trust[-1]` and
facts; a trace in `SpawnCharacter`'s no-op branch (today commented out,
`TaleSpawnOperator.cs:223`) shows no queued spawns for fragments whose NPCs
are all present.

## 6. W4 — Leaving a cluster keeps its social state (KAR-434)

**Trigger: TALE's own count of resident fragments per cluster.**
- New `JoyceCode/engine/tale/TaleClusterResidency.cs` (add to
  `JoyceCode.projitems`): resident fragments per cluster and the time a
  cluster became empty; `FragmentActive`, `FragmentPurged`,
  `DueForDepopulate(now, grace)`.
- `TaleSpawnOperator._findSpawnStatus_nl` registers a fragment for every
  cluster whose AABB intersects it (not only the heat map's owner:
  `ClusterHeatMap.GetClusterDesc` picks the last cluster,
  `ClusterHeatMap.cs:66-70`, which would hide a neighbour's live NPCs).
  `PurgeFragment` reports the purge.
- On the logical thread (`TaleModule._onSocialFrame`), clusters due after
  a grace period (≥ `KeepFragmentMinTime` plus margin, ~60 s real time,
  tunable) are depopulated. The grace period damps flapping at a cluster
  edge and guarantees the purged fragments' entities are gone first.
  A fragment becoming active again cancels a pending depopulate.
  `TaleClusterResidency` is written from `_findSpawnStatus_nl` and
  `PurgeFragment` and read in `_onSocialFrame`, so it takes its own lock.
- When a fragment is resident for a cluster that is not populated, that
  cluster is (re)populated too — today `_ensureClusterPopulated` runs only
  for the heat map's owner cluster (`TaleSpawnOperator.cs:164-165`).

**Fixes in `DepopulateCluster` (`TaleManager.cs:275-326`):**
- keep schedules by `IsKnownToPlayer` (not `HasPlayerDeviation`) and add
  each kept NPC's index to the cluster's skip mask (finding 3);
- clear the materialised and Tier-2 flags for all of the cluster's NPCs;
- correct the comment at `TaleSpawnOperator.cs:463-464`.

**On return** (automatic, via `_ensureClusterPopulated`): regenerate all
but the skip mask, warm up, apply the scenario, overlay snapshots by
`NpcId` (`TaleManager.cs:206-220`). Kept NPCs are the same objects.

**Group consistency.** The warm-up's group verbs run against the
persistent group table before the snapshot overlay restores `GroupId`
(`_applyGroupVerb`, `TaleManager.cs:513-531`), so table and schedules can
disagree after a repopulation. Worse, on first population a group verb
fired in the warm-up creates a group, and `_buildGroupTable` then sees a
non-empty table and does not seed the bake's groups at all (`:378-383`).
**Decision: group verbs are suppressed during the warm-up** (a flag on
`AdvanceNpc` from `PopulateCluster`); reconciling afterwards would leave
the bake unseeded. Tests: a warm-up with a group-verb storylet still seeds
all of the bake's groups; after a repopulation W6's `GroupTableMismatch`
is 0.

**Test seam:** an internal `PopulateClusterWith(..., generate)` overload so
xUnit can populate without `ClusterDesc`/`ClusterStorage`
(internals are visible to the test project, `Joyce.csproj:40-42`).

*Tests:*
- `LeaveAndReturn_RestoresTrustGroupAndFacts`: populate, mark one NPC
  known to the player, depopulate, repopulate with fresh schedules → the
  known NPC is the same object with `Trust[-1]` and facts; a primary NPC
  is new but has its group and trust back; rosters are consistent
  (fails today on the skip mask).
- `Depopulate_KeepsAnyKnownToPlayer`.
- `TaleClusterResidency` tests: not due before the grace period, due
  after the last purge, cancelled by re-activation, border fragments count
  for every intersecting cluster.

*Done when:* the tests pass, leaving a cluster in the game logs the
depopulate snapshot after the grace period and returning logs the restore
(`TaleManager.cs:217-218,308-310`), with an NPC known to the player
unchanged; the repopulation time of the largest cluster is measured and
written into this plan.

## 7. W5 — Background tick for Tier-3 schedules

**What is ticked:** schedules of populated clusters that are not
materialised (Tier 3). Tier 2 is unused (W3); if a
later stage revives it, `ExitTier2Mode` (`TaleEntityStrategy.cs:150-155`)
must advance only if `now >= CurrentEnd`, otherwise resume travel, so a
schedule that moved on is not advanced twice — W5 makes that change now,
since it is one line.

**How:** `TaleManager.AdvanceAbstractSchedules(now, scanBudget,
advanceBudget)` and `CatchUpCluster(clusterIndex, now)`.
- **Event time, not tick time.** Per NPC: while `CurrentEnd <= now`,
  advance at `CurrentEnd` (as DES does, `DesSimulation.cs:290-305`), with
  a monotonic guard (`evt = max(CurrentEnd, evt + 1 min)`, because 2–3 min
  storylets with −5 min jitter can end before they start) and at most
  32 steps; stop on a `null` return, and back that NPC off (retry after,
  say, 10 game minutes) so a schedule whose selection keeps failing is not
  retried every slice with a `Warning` each time. If the NPC is more than 24 h behind
  or hits the cap, resync with one `AdvanceNpc(now)` (game time follows the
  wall clock, so a pause or a load can jump). With W2's per-NPC
  randomness, the result is independent of tick cadence.
- **Time-sliced:** called on every logical frame from
  `TaleModule._onSocialFrame` (before its 10-minute gate) with a cursor
  over populated clusters and their NPC ids, ~512 scanned and ~64
  advanced per frame. Before `TickEncounters(cluster)`
  (`TaleModule.cs:557`) an unbudgeted `CatchUpCluster` makes encounters
  see current locations.
- **Index:** a sorted id list per cluster, built at the end of
  `PopulateCluster` and appended in `RegisterNpc`, instead of
  `GetNpcsInCluster`'s full scan. `_applyGroupVerb` uses it instead of
  building a dictionary over all schedules on every call (`:524`).
- **Optional:** refresh `CurrentWorldPosition = PositionAt(now)` for
  Tier-3 NPCs in transit, so bucketing matches where spawn places them.

**Cost (estimates, to be measured):** ~15–25 advances per NPC per game
day; at 4095 NPCs and a 30-minute game day that is ~35–55 advances per
real second per populated cluster. Unsliced, one 10-minute burst would be
3–7 ms per cluster — a frame hitch; sliced, ~1 advance per cluster per
frame. TALE populates every completed cluster (`TaleModule.cs:489-503`),
so the number of populated clusters matters; W9 records it.

**Stats:** `AbstractTickStats { Scanned, SkippedMaterialised, Advanced,
Steps, Resynced, Capped, ElapsedTicks }`, traced per game hour under the
existing TALE category.

**Risk: live drift.** Live `AdvanceNpc` ignores forced storylets, the arc
stack and interrupts (DES-only until S2b). With the tick, all NPCs
accumulate postcondition drift (hunger, wealth, anger) under live rules.
W9 compares property histograms after 1 and 7 game days with the baked
state; a drift toward desperation storylets is a finding for S2b, not a
reason to hold W5.

*Tests:*
- `LocationDistributionChangesOverOneGameDay`: warm up at 06:00, tick
  every 10 min for 24 h; the home/work/venue shares differ between 06:00,
  12:00 and 20:00, and every NPC visits at least two locations (fails
  without the tick).
- `CatchUpIsCadenceIndependent`: ticking every 1 min and every 60 min
  gives identical schedules.
- `MaterialisedAndTier2AreNotAdvanced`, then catch up after
  `SetDematerialized`.
- `NoInfiniteLoopOnShortStorylets`, `LargeGapResyncs`.
- Perf test (`[Trait("Category","Perf")]`, not gating CI): one simulated
  game day at 4095 NPCs, total time logged.

*Done when:* the tests pass; no single `AdvanceAbstractSchedules` call
exceeds 1 ms at default budgets in the perf test; in the game, per-cluster
`Advanced` per game hour is non-zero for clusters the player isn't in, and
the evolver's per-location presence varies by time of day. Measured
numbers are written into this plan.

## 8. W6 — Shared social metrics

> **Status:** implemented (KAR-448), as planned, with two deviations:
> - The Testbed crashed before reaching the DES: since the role
>   abstraction it never registered `RoleRegistry`. The registry
>   registration and config loading moved out of `TaleModule` into
>   `nogame.modules.tale.TaleRegistryLoader`, which the Testbed now calls
>   too.
> - `interrupts_per_day` counts interrupts *set*, overwrites included;
>   the applied ones are in `selections_by_source`.

One calculator, used by a test, the panel (W8), the Testbed and the
in-game log lines.

- New `JoyceCode/engine/tale/SocialMetrics.cs`: a result object
  (`ToDictionary()`, `ToLogLine()`) and a pure static
  `SocialMetricsCalculator.Compute(...)` with no `I.Get`. Trust edges
  come from schedules in the live game and from `RelationshipTracker` in
  DES; group membership always from `NpcSchedule.GroupId`. Player key −1
  is excluded from NPC–NPC metrics. `TaleManager.ComputeSocialMetrics(i)`
  wraps it (logical thread).
- **Metrics:** group count by type; group sizes (min/median/mean/max);
  share of NPCs in groups; `GroupTableMismatch` (schedules vs rosters,
  live); known pairs and trust density (the same definition as the bake
  report's `RelationshipDensity`, `ScenarioStatistics.cs:35-39`, so the
  numbers compare); strong pairs (avg ≥ 0.6, `CommunityDetector`'s
  threshold); mean intra- and inter-group trust and an inter-group matrix;
  NPCs who met the player, their share and mean `Trust[-1]`; bake match
  ratio and coverage; membership churn and group turnover per day.
- **Bake info:** `PopulateCluster` stores `ScenarioApplicator`'s result
  per cluster (finding 11).
- **Churn without ids:** a transient `SocialMetricsHistory` on
  `ClusterSocialState` (not serialised, so no save change) holds each
  NPC's group at the start of the day and a 120-day ring of daily
  samples. Groups are matched by member overlap, using the rule from
  `RuntimeSocialEvolver.cs:263-285`, moved into a shared
  `GroupMatching.Match` that the evolver also calls. Day roll: live in
  `TaleModule._onSocialFrame` when the game date changes; DES at its
  day boundary. DES regroups every 30 days, the live game every 2 hours,
  so their churn numbers are not comparable; the baseline says so.
- **Testbed rates (KAR-436 item 4).** Replace the three literals with
  counters fed from `DesSimulation`: selections by source (normal,
  resumed, forced, interrupt-nest/replace), interrupts per NPC (also
  fixing the never-written `_dailyInterruptsByNpc`), requests emitted and
  fulfilled by id. `completion_rate` changes meaning: from "finished
  without being cut short" (always 1.0, finding 9) to "share of
  selections that are the NPC's own routine" (owner agreed,
  2026-10-18). `ComputeFinalMetrics` gets a `social` block from the shared
  calculator; `CheckTargets` checks the targets already present in
  `testbed_targets.json`.
- **Debug category:** `Dc.TaleMetrics` (`DebugCategories.cs`) with
  `"debug.category.talemetrics": false` in `nogame.properties.json`.

*Tests:* calculator (two groups of three with hand-written trust: counts,
density, asymmetric and one-sided pairs, −1 excluded, intra/inter means,
met-player share, roster mismatch); history (renumbering all groups gives
0 churn, one move gives 1, dissolve + create); bake info; the rate
counters; and one DES integration test asserting that the Testbed's
`social` block equals the calculator on the same data.

*Done when:* the tests pass, `ComputeFinalMetrics` has no literal rates,
the Testbed JSON has a `social` block, and
`TaleManager.ComputeSocialMetrics(i)` reports the bake match ratio.

## 9. W7 — Per-NPC debug overlay

> **Status:** implemented (KAR-449). The in-game check is still open. The
> labels use `|` as separator because the OSD font has no middle dot.

- New `nogameCode/nogame/modules/debugger/TaleOverlayModule.cs`,
  registered next to `DebuggerToggle` (`nogame/Main.cs:32`).
- A **child label entity** per materialised TALE NPC (an entity holds one
  `OSDText`; `ANearbyBehavior.cs:213-225` shows the pattern):
  `OSDText` + `SetParent(label, npc)` + offset (0, 2.2, 0), `MaxDistance`
  40 m, font 12, `CameraMask` 1 (as `GeneratePolytopeOperator.cs:140-147`).
  The real death paths dispose entities without walking children
  (`Engine.cs:375-378`; only `HierarchyApi.Delete` is recursive), so the
  module keeps its own npcId → label map and deletes labels whose NPC
  entity is no longer alive on every refresh.
- Found through the `TaleNpcId` component every TALE entity carries
  (`TaleSpawnOperator.cs:361`), refreshed at most twice per second on the
  logical thread; text only rewritten when it changed.
- Text from a pure, tested `engine.tale.TaleOverlayText.Format(schedule,
  group, isTier2)`: role · storylet [T2] / group id and name · `Trust[-1]`;
  from S1 also the standing level.
- Switched by a new Props bool `debug.tale.overlay` (default false) in
  `models/nogame.properties.json`; F12 → Config → Props already shows
  bool props as checkboxes (`ui/Config.cs:24-31`). Off means no label
  entities at all.

*Done when:* `TaleOverlayText` tests pass; in the game, switching the prop
on shows labels on TALE NPCs within 40 m within a second, they follow
storylet changes, switching off removes them, and no labels are orphaned
after driving away.

## 10. W8 — ImGui TALE panel

> **Status:** implemented (KAR-451), with three deviations. The density
> line waits for W9's counter. The panel also takes an NPC id typed in or
> clicked in a list (`RequestedNpcId`), and the inspector's selection
> reaches the logical thread as the entity (`TaleDebugState.SelectedEntity`),
> because only that thread may read its `TaleNpcId`. Group standing is
> the plain mean of `Trust[-1]` over the members who have one, until S1-e
> (KAR-458) defines it. The in-game check is still open.

- **Snapshot, not live reads** (finding 10). New
  `engine/tale/TaleDebugSnapshot.cs` (immutable) and a pure, tested
  `TaleDebugSnapshotBuilder.Build(manager, clusterIndex, gameNow,
  playerPos, selectedNpcId)`. A `TaleDebugState` registered by
  `TaleModule` holds `volatile Latest`, `RequestedNpcId`,
  `RequestedClusterIndex`. `TaleModule._onSocialFrame` builds a snapshot
  only while the editor is open (`engine.editor.isOpen`, set by
  `ui/Module.cs:41`), at most once per real second, for one cluster (the
  player's by default).
- New `JoyceCode/ui/Tale.cs : APart`, added to `ui/Main.cs` as a "TALE"
  collapsing header after "Inspector" (`Main.cs:220-223`) and to
  `JoyceCode.projitems`. It resolves `I.TryGet<TaleManager>()` only to
  say "(TALE not active)".
- **Content:** cluster, game time, counts, the overlay checkbox; W6's
  metrics with a churn sparkline; density near the player (from W9's
  counter); groups by size with name, type, hangout, distance and player
  standing (expandable to members); the inter-group matrix of the 12
  largest groups; the player's standing per NPC; and, for the entity
  selected in the inspector (`EntityState.CurrentEntity` with `TaleNpcId`),
  its schedule, properties, strongest trust entries, group and flags.
- **A pre-existing race to fix here:** `Redetect` runs without `_loSocial`
  (`TaleModule.cs:574`) while `_applyGroupVerb` takes it.

*Done when:* builder tests pass (sorting, matrix equals W6's, selected
NPC detail, snapshot unchanged after the manager changes); in the game
F12 → TALE shows one group's members and one NPC's state, with no
exceptions after ten minutes of driving with the pane open.

## 11. W9 — Measurements and baselines

- **Shared counter:** `CountNear(npcs, model, centre, radius, gameNow)` in
  W6's file, using `NpcSchedule.PositionAt` (not `CurrentWorldPosition`),
  classifying by state: in transit → street; at home or work; otherwise by
  location type (street, venue, home/work).
- **Headless (the numbers for the plan):** `Testbed --tale-arrival
  [--cluster small|medium|<name>]`. Picks the smallest and the median
  cluster by `TalePopulationGenerator.ComputeNpcCount`, registers the TALE
  registries from the shipped config (a loader extracted from
  `TaleModule.cs:400-462`, so Testbed, TestRunner and game agree; required,
  not optional: `DesSimulation.cs:194,551` and `AuthorityCondition.cs:26`
  call `I.Get<RoleRegistry>()`, which the Testbed never registers), populates, prints
  the arrival metrics, then steps 24 h in 10-minute steps with W5's tick
  and samples density hourly at the cluster's start pose
  (`ClusterDesc.FindStartPose`) and over street locations.
- **In the game (the cross-check):** one `Dc.TaleMetrics` line per game
  hour with the schedule-based and the entity-based count within 100 m of
  the player; arrival metrics logged at the end of `PopulateCluster` and
  when the player enters a cluster.
- **Also measured:** the spawn-poll overhead before and after W3's
  resident-count fix, repopulation time (W4), tick cost (W5), the number of
  populated clusters in a normal session, property drift after 1 and 7
  game days (W5), DES 30- and 90-day metrics, and the save size of
  `TaleSocialState` for 4095-NPC clusters (one LiteDB string; a failed
  deserialise is dropped silently, CLAUDE.md).

*Done when:* two runs of `--tale-arrival` per cluster give identical JSON;
the in-game hourly counts agree with the headless ones within ±30 % or ±3
NPCs at the checked hours (or the gap is explained); and all numbers are
written into roadmap §S0 and §12 here.

## 12. Results

*(Filled in by W4, W5 and W9.)*

- **W4 (KAR-434, 2026-10-20):** implemented as planned, plus: the heat
  map's cluster is always counted resident (it rounds outwards, so it may
  not intersect the fragment strictly); a populated cluster never seen
  resident starts its grace period at the first check, so clusters
  populated on completion far from the player are depopulated too; a return
  visit never reseeds the bake's groups (an emptied table would otherwise
  list members of no group). **Repopulation time of the largest cluster:
  not yet measured** — needs a play session; `PopulateCluster` now logs
  its duration ("Populated cluster N with M NPCs in X ms", category
  TaleManager; excludes `SpatialModel.ExtractFrom`).
- **W5 (KAR-450, 2026-10-20):** implemented as planned, except: the
  default **advance budget is 16, not 64** — measured below; the optional
  transit-position refresh was left out (bucketing still uses the
  destination's entry position while an abstract NPC travels). Perf test
  (`BackgroundTickTests.PerfOneGameDayAt4095Npcs`, one game day = 108 000
  frames, 4095 NPCs, one cluster, this dev machine): 53 094 steps a day
  (~13 per NPC); mean 0.023 ms per slice; p99 0.05 ms, p99.9 0.07 ms at
  budget 16 (0.22 ms at 64). Calls over 1 ms: 1–2 per game day at 16, 2–3 at
  64, almost all during a gen-0 GC caused by `AdvanceNpc`'s allocations,
  plus first-use JIT; so the "no call over 1 ms" target holds for the tick's
  own work but not for GC pauses that land in it. In-game checks (non-zero
  `Advanced` per hour for clusters the player isn't in; evolver presence
  varying by time of day) are pending a play session.

- **W9 (KAR-452, 2026-10-20):** `Testbed --tale-arrival [--cluster
  small|medium|large|all|<name>] [--days N]` (`Testbed/TaleArrival.cs`)
  populates real clusters as the game does (shipped storylet resources and
  registries, scenario bake, warm-up) at the game's start time
  (`GameState.GameT0`, 19:00), then runs N game days in 10-minute steps:
  W5's catch-up, the encounter tick, re-detection every 2 h, the metrics
  day roll. JSON on stdout, timings on stderr; two runs of `--cluster all
  --days 7` gave byte-identical JSON. The shared counter is
  `NpcDensityCounter` (in W6's `SocialMetrics.cs`); the registry loader
  already existed (`TaleRegistryLoader`). Measured on this dev machine,
  Release; timings from the two quiet runs of three (one overlapped other
  builds and was 1.5–3x slower):

  | | small | medium | large |
  |---|---|---|---|
  | cluster | 50 "Akika" | 15 "Seefu" | 69 "Deees" |
  | NPCs / locations / street locations | 168 / 64 / 56 | 4095 / 2083 / 1485 | 4095 / 2007 / 1518 |
  | bake scenario, match ratio | medium/2, 0.70 | medium/7, 0.061 | medium/5, 0.054 |
  | groups at arrival → after 7 days | 6 → 6 | 12 → 21 | 9 → 29 |
  | NPCs in groups at arrival | 92 (55 %) | 175 (4.3 %) | 146 (3.6 %) |
  | churn per day (7-day mean) | 0.17 | 3.8 | 2.7 |
  | group-table mismatch after 1 day | 6 | 8 | 3 (KAR-466) |
  | AdvanceNpc steps per NPC per game day | 31 (59 on day 1) | 24 | 26 |
  | catch-up cost per game day (two quiet runs) | 0.15–0.24 s | 1.7–2.2 s | 1.7–2.1 s |
  | populate at arrival / repopulate on return | 0.12 s / 0.04 s | 1.0 s / 0.40 s | 0.40 s / 0.37 s |
  | social state JSON after leaving | 0.15 MB | 2.0 MB | 2.1 MB |

  **Density within 100 m, day 1 hourly** (visible = on the street; an NPC at
  any non-street location is hidden indoors, `TaleEntityStrategy.cs:449`):

  | | small | medium | large |
  |---|---|---|---|
  | visible, mean over street locations | 2.4 (0.9 at 18:00 … 4.3 at 05:00) | 5.3 (3.7 at 18:00 … 6.4 at 04:00) | 5.1 (3.3 at 12:00 … 6.8 at 01:00) |
  | all NPCs, mean over street locations | 6.7 | 10.1 | 10.5 |
  | street locations with ≥ 4 visible | 9–34 % | 31–45 % | 29–63 % |
  | at the start pose: visible / all | 1–10 / 3–14 | 0–4 / 1–8 | 0–4 / 0–16 |

  **Property drift (means, arrival → 1 day → 7 days, large cluster):**
  hunger 0.02 → 0.49 → 0.43, fatigue 0.12 → 0.52 → 0.44, happiness 0.57 →
  0.70 → 0.89, **wealth 0.46 → 0.33 → 0.19**, anger 0.06 → 0.03 → 0.02,
  morality, fear, reputation, health flat. The medium cluster is the same
  (wealth 0.48 → 0.27); the small one keeps its wealth (0.36 → 0.41).

  **Clusters:** 70, NPC counts 168 … 4095, **45 at the 4095 cap**
  (median 4095, 25th percentile 996), 206 667 NPCs in all.

  Readings:
  - Even the largest clusters show about 5 visible NPCs within 100 m of a
    street location, fewer at the start pose; the owner's 4–5
    opportunities within 100 m (§8 Q4) would need nearly every visible NPC
    to be one. S5 should count situations (an NPC, a pair, a group) or use
    a wider radius.
  - Streets are busier at night than at midday (workers indoors at work,
    night roles out); the evening rush is the quietest hour.
  - The bake shapes 5–6 % of a capped cluster's NPCs (scenario ≤ 250
    NPCs): 96 % of the NPCs meet the player ungrouped. That settles S4's
    choice against "the bake alone"; either enable the large scenarios or
    let S4's group logic run on W5's tick.
  - Wealth halves in a week under live rules (no DES-only forced
    storylets or interrupts until S2b): a drift towards desperation, as
    plan §7 warned. A finding for S2b.
  - Populating a capped cluster takes 0.4–1.0 s on the logical thread
    (inside the spawn poll): a hitch when the player reaches a new
    cluster. Repopulation after W4 costs 0.4 s.
  - A capped cluster's social state is ~2 MB of JSON in one LiteDB string
    per cluster visited.
  - **DES baseline** (`Testbed --cluster medium --days 30|90`, 500 NPCs on
    cluster 15, 10 s / 17 s wall clock): completion 0.998 / 0.997;
    interrupts 28.5 / 30.9 per day for the whole cluster, i.e. 0.057 /
    0.062 per NPC-day (first written here as "per NPC-day": the metric was
    a cluster total until KAR-467 divided it by the NPC count; the design
    target is 1–3 per NPC-day, so interrupts are about twenty times rarer
    than designed — a finding for S2b); groups 29 / 57 (social 25, trade 4 → social 30, criminal
    27; first gang on day 60); NPCs in groups 23 % / 98 %; trust density
    0.094 / 0.164; strong pairs 137 / 1572; mean intra-/inter-group trust
    0.76 / 0.10 → 0.64 / 0.13; churn per day 0 / 5.5; mismatch 0. Property
    means at day 90: wealth 0.22, health 0.32, morality 0.36, hunger 0.46,
    happiness 1.0. The DES therefore grows a fully grouped, criminal-heavy
    society by day 90, where the live game (bake + tick, above) starts with
    4 % grouped. Before W9 the default run used `clusters[0]` (5 homes, no
    workplace: all NPCs in one venue, a complete trust graph) and the
    Testbed's own `NpcAssigner` still looked for a `workplace` type that
    the spatial model does not have (it has `office`/`warehouse`), so no
    NPC had a workplace; both are fixed (`--cluster`, assigner types).
  - Not measured headless (need a play session): the in-game cross-check
    (TaleMetrics now logs schedule- and entity-based counts within 100 m
    of the player each game hour, the arrival metrics at each populate and
    on entering a cluster), the spawn-poll overhead before/after W3, and
    the number of populated clusters in a normal session.

## 13. Owner decisions and notes

1. **`completion_rate` meaning (W6):** decided 2026-10-18 — the share of
   selections that are the NPC's own routine (not forced or interrupted).
2. **S2 note (no decision needed now):** S2's gossip will give `Trust[-1]`
   to NPCs the player never met; under W3's rule they would all be kept
   and saved. S2 decides whether "heard of" needs only the snapshot and
   "met" the full schedule.

## 14. Docs to update along the way

ARCHITECTURE (§6.2 warm-up, §6.3 tiers and survival, §11 class map, §12
testing, a short observability section), REFERENCE (survival rule),
PHASE_6 (cluster lifecycle), PHASE_0C (rates), the test docs and counts
(W1), CLAUDE.md where the current state changes, and a STATUS-LOG entry
per package.
