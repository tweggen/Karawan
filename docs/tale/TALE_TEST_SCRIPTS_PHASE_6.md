# Phase 6 Test Scripts — Seed-Based Population & Cluster Lifecycle

**Status**: 49 test scripts created

**Test Location**: `models/tests/tale/phase6-population/`

**Key Features Tested**:
- Deterministic NPC population generation from cluster seed
- Independent NPC seeds (skip-safe for deviation mask)
- Cluster lifecycle integration (populate on activation, depopulate on deactivation)
- TaleManager cluster-aware query API
- Materialization/dematerialization via TaleSpawnOperator
- Deviation tracking and skip mask management
- Deviation-only persistence (save/load round-trip)

---

## Test Organization (49 scripts)

### Category 1: Seed-Based Population Generator (Tests 01-08)

**Test 01: Deterministic Generation**
- **File**: `01-deterministic-generation.json`
- **Priority**: Critical
- **Purpose**: Verify same cluster seed always produces identical NPC population
- **Validation**: Two runs with same ClusterDesc seed produce same NPC count, roles, positions, properties

**Test 02: NPC Count from Cluster**
- **File**: `02-npc-count-from-cluster.json`
- **Priority**: Critical
- **Purpose**: NPC count derives deterministically from cluster street point count
- **Validation**: count = streetPoints / 2, capped at 4095, non-zero for populated clusters

**Test 03: Independent NPC Seeds**
- **File**: `03-independent-npc-seeds.json`
- **Priority**: Critical
- **Purpose**: Skipping NPC index N does not affect NPC at index N+1
- **Validation**: Generate full population, then generate with skip mask {3}; NPC at index 4 is identical in both runs

**Test 04: Role Assignment**
- **File**: `04-role-assignment.json`
- **Priority**: High
- **Purpose**: NPCs receive valid roles weighted by cluster downtown intensity
- **Validation**: All roles in {worker, merchant, socialite, drifter, authority}; downtown clusters have proportionally more merchants/authority

**Test 05: Home Position Assignment**
- **File**: `05-home-position-assignment.json`
- **Priority**: High
- **Purpose**: Each NPC gets a HomePosition from cluster street points
- **Validation**: HomePosition != Vector3.Zero, within cluster AABB, Y at ground height

**Test 06: Workplace Position Assignment**
- **File**: `06-workplace-position-assignment.json`
- **Priority**: High
- **Purpose**: Each NPC gets a WorkplacePosition from cluster street points
- **Validation**: WorkplacePosition within cluster bounds, may differ from HomePosition

**Test 07: Property Initialization**
- **File**: `07-property-initialization.json`
- **Priority**: High
- **Purpose**: NPC properties initialized with role-appropriate defaults and seed variation
- **Validation**: All 10 properties present, in 0.0-1.0 range, role-specific distributions (workers lower wealth than merchants)

**Test 08: NPC ID Encoding**
- **File**: `08-npc-id-encoding.json`
- **Priority**: High
- **Purpose**: NPC IDs encode cluster index (20 bits) + NPC index (12 bits)
- **Validation**: MakeNpcId/GetClusterIndex/GetNpcIndex round-trip correctly, IDs are globally unique

---

### Category 2: Cluster Lifecycle (Tests 09-17)

**Test 09: ClusterCompletedEvent Triggers Population**
- **File**: `09-cluster-completed-populates.json`
- **Priority**: Critical
- **Purpose**: ClusterCompletedEvent triggers TaleManager.PopulateCluster()
- **Validation**: IsClusterPopulated() returns true after event, schedules queryable

**Test 10: No Double Population**
- **File**: `10-cluster-no-double-populate.json`
- **Priority**: High
- **Purpose**: PopulateCluster is idempotent for same cluster index
- **Validation**: Second call is no-op, NPC count unchanged, no duplicates

**Test 11: Depopulate Removes Primary NPCs**
- **File**: `11-cluster-depopulate-primary.json`
- **Priority**: Critical
- **Purpose**: DepopulateCluster removes non-deviated schedules
- **Validation**: GetSchedule returns null for all primary NPCs after depopulation

**Test 12: Depopulate Keeps Deviated NPCs**
- **File**: `12-cluster-depopulate-keeps-deviated.json`
- **Priority**: Critical
- **Purpose**: DepopulateCluster retains NPCs with HasPlayerDeviation=true
- **Validation**: Deviated NPC survives, GetSchedule still returns it

**Test 13: Repopulate Skips Deviated Indices**
- **File**: `13-cluster-repopulate-skips-deviated.json`
- **Priority**: Critical
- **Purpose**: Re-populating cluster skips indices in deviation skip mask
- **Validation**: No duplicate at deviated index, deviated version preserved with modified state

**Test 14: Repopulate Produces Identical Primary NPCs**
- **File**: `14-cluster-repopulate-identical.json`
- **Priority**: High
- **Purpose**: Depopulate then repopulate produces identical primary NPCs
- **Validation**: Roles, positions, properties match original generation

**Test 15: Multiple Clusters Independent**
- **File**: `15-multiple-clusters-independent.json`
- **Priority**: High
- **Purpose**: Multiple clusters populated with non-overlapping NPC IDs
- **Validation**: No NPC ID collisions, independent population/depopulation

**Test 16: Empty Cluster No Crash**
- **File**: `16-empty-cluster-no-crash.json`
- **Priority**: Medium
- **Purpose**: Cluster with no street points produces zero NPCs without error
- **Validation**: PopulateCluster returns gracefully, IsClusterPopulated still set

**Test 17: Depopulate Non-Populated Cluster**
- **File**: `17-cluster-depopulate-not-populated.json`
- **Priority**: Medium
- **Purpose**: DepopulateCluster on unknown cluster index is a safe no-op
- **Validation**: No crash, no state change

---

### Category 3: TaleManager Query API (Tests 18-25)

**Test 18: GetSchedule Returns NPC**
- **File**: `18-get-schedule-returns-npc.json`
- **Priority**: Critical
- **Purpose**: GetSchedule(npcId) returns correct NpcSchedule after population
- **Validation**: Non-null result with correct NpcId, Role, Properties

**Test 19: GetSchedule Null for Missing**
- **File**: `19-get-schedule-null-missing.json`
- **Priority**: High
- **Purpose**: GetSchedule returns null for non-existent NPC ID
- **Validation**: Returns null, no crash

**Test 20: GetNpcsInFragment**
- **File**: `20-get-npcs-in-fragment.json`
- **Priority**: Critical
- **Purpose**: Returns NPCs whose HomePosition falls in the queried fragment
- **Validation**: Only NPCs in that fragment returned, not all NPCs

**Test 21: GetNpcsInFragment Empty**
- **File**: `21-get-npcs-empty-fragment.json`
- **Priority**: Medium
- **Purpose**: Returns empty list for fragment with no NPCs
- **Validation**: Empty list, no crash

**Test 22: GetDeviatedNpcs**
- **File**: `22-get-deviated-npcs.json`
- **Priority**: High
- **Purpose**: Returns only NPCs with HasPlayerDeviation=true for given cluster
- **Validation**: Correct count, no false positives

**Test 23: GetDeviationSkipMask**
- **File**: `23-get-deviation-skip-mask.json`
- **Priority**: High
- **Purpose**: Returns correct set of deviated NPC indices per cluster
- **Validation**: Mask contains registered deviated indices, null for unknown clusters

**Test 24: AdvanceNpc with Schedule**
- **File**: `24-advance-npc-with-schedule.json`
- **Priority**: Critical
- **Purpose**: AdvanceNpc works with populated NPC schedule
- **Validation**: Returns non-null StoryletDefinition, updates CurrentStorylet/ScheduleStep

**Test 25: AllSchedules Readable**
- **File**: `25-all-schedules-readable.json`
- **Priority**: Medium
- **Purpose**: AllSchedules property returns all registered schedules
- **Validation**: Count matches expected NPC count

---

### Category 4: Materialization / TaleSpawnOperator (Tests 26-34)

**Test 26: Spawn from Schedule**
- **File**: `26-spawn-from-schedule.json`
- **Priority**: Critical
- **Purpose**: TaleSpawnOperator creates ECS entity from existing NpcSchedule
- **Validation**: Entity created with TaleEntityStrategy referencing correct schedule

**Test 27: No Spawn Without Schedule**
- **File**: `27-no-spawn-without-schedule.json`
- **Priority**: High
- **Purpose**: SpawnCharacter returns no-op when no unmaterialized NPCs in fragment
- **Validation**: Empty action, no crash

**Test 28: Materialization Tracking**
- **File**: `28-materialization-tracking.json`
- **Priority**: Critical
- **Purpose**: Spawned NPCs tracked as materialized, preventing double-spawn
- **Validation**: IsMaterialized=true after spawn, SpawnCharacter skips already-materialized

**Test 29: Dematerialization on Purge**
- **File**: `29-dematerialization-on-purge.json`
- **Priority**: High
- **Purpose**: PurgeFragment marks NPCs as dematerialized
- **Validation**: IsMaterialized=false after purge, NPCs eligible for respawn

**Test 30: Spawn Status from Schedule Count**
- **File**: `30-spawn-status-from-schedule-count.json`
- **Priority**: High
- **Purpose**: GetFragmentSpawnStatus min/max equals NPC count in fragment
- **Validation**: MinCharacters == MaxCharacters == GetNpcsInFragment().Count

**Test 31: Spawned NPC Has Strategy**
- **File**: `31-spawned-npc-has-strategy.json`
- **Priority**: Critical
- **Purpose**: Materialized NPC has TaleEntityStrategy attached
- **Validation**: Strategy references correct NpcId

**Test 32: Spawned NPC at Home Position**
- **File**: `32-spawned-npc-at-home-position.json`
- **Priority**: High
- **Purpose**: Entity positioned at HomePosition from schedule
- **Validation**: Transform position matches NpcSchedule.HomePosition

**Test 33: Terminate Does Not Lose Schedule**
- **File**: `33-terminate-does-not-lose-schedule.json`
- **Priority**: Critical
- **Purpose**: TerminateCharacters destroys entity but schedule persists in TaleManager
- **Validation**: GetSchedule returns non-null after entity destruction

**Test 34: Respawn After Terminate**
- **File**: `34-respawn-after-terminate.json`
- **Priority**: High
- **Purpose**: After termination, NPC can be respawned from same schedule
- **Validation**: Same NPC materializes with same schedule state

---

### Category 5: Deviation Tracking (Tests 35-41)

**Test 35: Deviation Flag Default False**
- **File**: `35-deviation-flag-default-false.json`
- **Priority**: Critical
- **Purpose**: Generated NPCs have HasPlayerDeviation=false by default
- **Validation**: Every NPC in fresh population is non-deviated

**Test 36: Deviation Flag Survives Depopulate**
- **File**: `36-deviation-flag-survives-depopulate.json`
- **Priority**: Critical
- **Purpose**: Deviated NPC survives cluster depopulation
- **Validation**: NPC remains in TaleManager after DepopulateCluster

**Test 37: Deviation Updates Skip Mask**
- **File**: `37-deviation-updates-skip-mask.json`
- **Priority**: High
- **Purpose**: RegisterNpc with HasPlayerDeviation=true updates skip mask
- **Validation**: NpcIndex added to _deviationSkipMasks[ClusterIndex]

**Test 38: Primary NPC Not in Skip Mask**
- **File**: `38-primary-npc-not-in-skip-mask.json`
- **Priority**: Medium
- **Purpose**: RegisterNpc with HasPlayerDeviation=false does not modify skip mask
- **Validation**: Only _schedules updated, not _deviationSkipMasks

**Test 39: Deviation Preserves Modified State**
- **File**: `39-deviation-preserves-modified-state.json`
- **Priority**: Critical
- **Purpose**: Deviated NPC retains modified properties across depopulate/repopulate cycle
- **Validation**: Modified anger/wealth values survive, regenerated NPCs have fresh defaults

**Test 40: Multiple Deviations Per Cluster**
- **File**: `40-multiple-deviations-per-cluster.json`
- **Priority**: High
- **Purpose**: Multiple NPCs in same cluster can be independently deviated
- **Validation**: Skip mask contains all deviated indices, all survive depopulation

**Test 41: Deviations Across Clusters**
- **File**: `41-deviations-across-clusters.json`
- **Priority**: High
- **Purpose**: Deviations in different clusters tracked independently
- **Validation**: Depopulating cluster A preserves cluster B's deviations

---

### Category 6: Persistence (Tests 42-49)

**Test 42: Save Deviated Only**
- **File**: `42-save-deviated-only.json`
- **Priority**: Critical
- **Purpose**: OnBeforeSaveGame serializes only deviated NPCs
- **Validation**: TaleDeviations JSON contains exactly N deviated entries, not total NPC count

**Test 43: Save Empty When No Deviations**
- **File**: `43-save-empty-when-no-deviations.json`
- **Priority**: High
- **Purpose**: No deviated NPCs means TaleDeviations remains empty
- **Validation**: OnBeforeSaveGame is a no-op when deviation count is 0

**Test 44: Load Restores Deviated NPCs**
- **File**: `44-load-restores-deviated.json`
- **Priority**: Critical
- **Purpose**: OnAfterLoadGame deserializes deviated NPCs and registers them
- **Validation**: GetSchedule returns loaded NPC with correct properties

**Test 45: Load Sets Skip Mask**
- **File**: `45-load-sets-skip-mask.json`
- **Priority**: Critical
- **Purpose**: Loading deviated NPCs populates deviation skip mask
- **Validation**: GetDeviationSkipMask contains loaded NPC indices

**Test 46: Save/Load Round-Trip**
- **File**: `46-save-load-round-trip.json`
- **Priority**: Critical
- **Purpose**: Full round-trip: deviate, save, load, verify state preserved
- **Validation**: All fields survive: role, properties, positions, trust, venues, storylet state

**Test 47: Save Preserves Trust**
- **File**: `47-save-preserves-trust.json`
- **Priority**: High
- **Purpose**: Trust relationships on deviated NPCs survive save/load
- **Validation**: Trust dictionary round-trips through JSON correctly

**Test 48: Save Preserves Social Venues**
- **File**: `48-save-preserves-social-venues.json`
- **Priority**: Medium
- **Purpose**: SocialVenueIds on deviated NPCs survive save/load
- **Validation**: List round-trips through JSON correctly

**Test 49: Save File Size Bounded**
- **File**: `49-save-file-size-bounded.json`
- **Priority**: High
- **Purpose**: Save file size is proportional to deviations, not total NPC count
- **Validation**: 500 NPCs with 5 deviated → TaleDeviations < 5KB
