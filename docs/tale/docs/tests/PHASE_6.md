# Phase 6 Tests — Seed-Based Population & Cluster Lifecycle

> **Status:** 16 scenario scripts, each asserting a check the Phase 6 setup computes; the rest
> of Phase 6 is tested in xUnit · **Changed:** 2026-10-20 (KAR-433, KAR-468)

The Phase 6 setup in `TestRunner/TestRunnerMain.cs` (`RunPhase6Tests`) registers 10 + 8
schedules in two clusters with a real `TaleManager`, marks NPC 2 as deviated, advances one NPC
and queries the manager. It pushes `world.cluster.completed` and `tale.cluster.populated`
itself, so waiting for those checks nothing; until KAR-468, 34 scripts did only that. Each
script now expects one `tale.check` event with code `<name>:pass` (the setup pushes
`<name>:fail` when the check fails).

**Location:** `models/tests/tale/phase6-population/` · **Run:** `./run_tests.sh phase6`

| Script | Check |
|---|---|
| 08 npc-id-encoding | `npc_id_encoding`: every NpcId decodes to its cluster and index |
| 17 cluster-depopulate-not-populated | `depopulate_unknown_noop` |
| 18 get-schedule-returns-npc | `get_schedule_returns_npc`: the same object for every id |
| 19 get-schedule-null-missing | `get_schedule_missing_null` |
| 20 get-npcs-in-fragment | `npcs_in_home_fragment`: exactly the NPCs whose position (current, else home) is in the fragment, matched on I and K |
| 21 get-npcs-empty-fragment | `empty_fragment_no_npcs` |
| 22 get-deviated-npcs | `deviated_npcs_only` |
| 23 get-deviation-skip-mask | `skip_mask_exact`: exactly the deviated index |
| 24 advance-npc-with-schedule | `advance_npc_selects_storylet` |
| 25 all-schedules-readable | `all_schedules_readable` |
| 28 materialization-tracking | `materialization_tracking`: set, cleared, schedule kept |
| 35 deviation-flag-default-false | `deviation_flag_default_false` |
| 37 deviation-updates-skip-mask | `deviation_skip_mask` |
| 38 primary-npc-not-in-skip-mask | `primary_not_in_skip_mask` |
| 40 multiple-deviations-per-cluster | `multiple_deviations_per_cluster` |
| 41 deviations-across-clusters | `deviations_across_clusters` |

Mutation check: with `RegisterNpc` no longer adding deviated NPCs to the skip mask, 23, 37,
38, 40 and 41 fail.

## Moved to xUnit, or needing the game

| Old scripts | Where now |
|---|---|
| 01–07, 10, 14–16 (generation, determinism, skip-safe seeds, roles, positions, properties, populate twice, repopulate identical, two clusters, empty cluster) | `TalePopulationGeneratorTests`, through the generator's `PopulationInput` seam (no generated city needed) |
| 11–13, 36, 39 (depopulate, keep deviated, skip on repopulate, state kept) | `ClusterDepopulateTests` |
| 29, 30, 33 (purge dematerialises, resident count, terminate keeps the schedule) | `DespawnBookkeepingTests` |
| 42, 43, 46, 49 and the earlier 44, 45, 47, 48 (save/load of deviated NPCs) | `NpcScheduleJsonTests`; the format moved unchanged from `TaleModule` to `engine.tale.NpcScheduleJson` |
| 09, 26, 31, 32, 34 (populate on ClusterCompletedEvent, spawn operator, strategy, entity position, respawn) and 27 | need the running game; not tested automatically |
