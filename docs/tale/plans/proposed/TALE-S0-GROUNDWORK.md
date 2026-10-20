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
