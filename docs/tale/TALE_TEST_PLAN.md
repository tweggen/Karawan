# TALE Implementation Test Plan

## Overview

This document outlines a comprehensive testing strategy for the TALE narrative engine, using the **ExpectEngine** framework to validate each phase of implementation. The plan covers **6 phases** with **20+ test scripts per phase** (120+ total), organized by feature.

**Test Framework**: ExpectEngine (system.threading.Channels, lock-free event tapping)
**Script Location**: `models/tests/tale/`
**Execution**: `JOYCE_TEST_SCRIPT=tests/tale/phase-X/script-name.json dotnet run ...`

---

## Test Organization

### Directory Structure
```
models/tests/tale/
├── phase0-des/               # Core DES engine (event loop, scheduling, encounters)
│   ├── 01-initialization.json
│   ├── 02-event-queue.json
│   ├── ... (20 scripts)
│
├── phase1-storylets/         # JSON-driven storylets with runtime selection
│   ├── 01-library-loading.json
│   ├── 02-storylet-selection.json
│   ├── ... (20 scripts)
│
├── phase2-strategies/        # Story-to-strategy translation
│   ├── 01-strategy-creation.json
│   ├── 02-phase-transitions.json
│   ├── ... (20 scripts)
│
├── phase3-interactions/      # NPC-NPC request/signal system (JUST IMPLEMENTED)
│   ├── 01-request-emission.json
│   ├── 02-request-claiming.json
│   ├── ... (20 scripts)
│
├── phase4-player/            # Player quest integration
│   ├── 01-player-quest-trigger.json
│   ├── 02-player-navigation.json
│   ├── ... (20 scripts)
│
└── phase5-escalation/        # Crime waves, group conflicts
    ├── 01-crime-wave-detection.json
    ├── 02-gang-formation.json
    ├── ... (20 scripts)
```

---

## Phase 0: DES Engine (Core Event Loop & Scheduling)

**Objective**: Validate discrete event simulation, NPC scheduling, encounter detection, and relationship tracking.

### Test Categories (20 scripts)

#### Initialization & Setup (3 scripts)
1. `01-initialization.json` — Verify DesSimulation.Initialize() creates queue, loads library
2. `02-npc-creation.json` — Verify NPCs register with metrics, get initial wakeup event
3. `03-spatial-model.json` — Verify SpatialModel loads locations, calculates travel times

#### Event Queue & Scheduling (4 scripts)
4. `04-event-queue-order.json` — Verify events process in time order (not insertion order)
5. `05-node-arrival-event.json` — Verify NodeArrival triggers location change and next event
6. `06-day-boundary.json` — Verify day boundary events fire at midnight, clean up presence
7. `07-long-simulation.json` — Verify 30-day run completes without deadlock/memory leak

#### Property Dynamics (3 scripts)
8. `08-postcondition-application.json` — Verify postconditions update NPC properties correctly
9. `09-desperation-computation.json` — Verify desperation drifts based on wealth/hunger
10. `10-morality-drift.json` — Verify morality drifts daily based on desperation

#### Encounter Detection (4 scripts)
11. `11-encounter-at-venue.json` — Two NPCs at same location trigger encounter
12. `12-encounter-avoidance.json` — NPCs at different locations don't encounter
13. `13-multiple-encounters.json` — Three+ NPCs at same location generate multiple pairs
14. `14-encounter-trust-update.json` — Encounter changes trust values in RelationshipTracker

#### Relationship Tracking (3 scripts)
15. `15-trust-tier-friendly.json` — Positive interactions increase trust, trigger "friendly" tier
16. `16-trust-tier-hostile.json` — Negative interactions decrease trust, trigger "hostile" tier
17. `17-relationship-persistence.json` — Trust values persist across day boundaries

#### Metrics & Logging (3 scripts)
18. `18-metrics-daily-count.json` — Verify daily storylet and encounter counts
19. `19-jsonl-event-logging.json` — Verify all events logged to JSONL (npc_created, node_arrival, encounter)
20. `20-group-detection.json` — Verify GroupDetector identifies cliques every 30 days

---

## Phase 1: Storylets (JSON-Driven Story Selection & Execution)

**Objective**: Validate JSON storylet parsing, runtime selection based on preconditions, and story state.

### Test Categories (20+ scripts)

#### Library Loading (3 scripts)
1. `01-library-loading.json` — Verify StoryletLibrary.LoadFromDirectory() loads all .json files
2. `02-library-indexing.json` — Verify GetCandidates() returns role-specific and universal storylets
3. `03-fallback-selection.json` — Verify GetFallback() returns rest/wander at appropriate times

#### Precondition Matching (4 scripts)
4. `04-property-precondition.json` — NPC with hunger=0.8 matches "hunger: {min: 0.6}" storylet
5. `05-property-mismatch.json` — NPC with hunger=0.3 doesn't match "hunger: {min: 0.6}" storylet
6. `06-time-of-day-match.json` — Storylet with time_of_day window only available during window
7. `07-role-filter.json` — "worker" role storylets don't appear in merchant candidate list

#### Storylet Selection (3 scripts)
8. `08-weighted-selection.json` — Higher weight storylets selected more often
9. `09-weight-normalization.json` — Weights sum to probability distribution
10. `10-desperation-gating.json` — Desperate NPC (desperation > 0.4) can access crime storylets

#### Duration & Location (3 scripts)
11. `11-duration-randomness.json` — Storylet duration varies within min/max range
12. `12-location-resolution.json` — Storylet "location: workplace" resolves to NPC's workplace
13. `13-nearest-venue.json` — Storylet "location: nearest_shop_Eat" finds closest social_venue with tag

#### Postcondition Application (4 scripts)
14. `14-simple-postcondition.json` — Postcondition "wealth: +0.05" increases wealth by 0.05
15. `15-multiple-postconditions.json` — Multiple postconditions applied atomically
16. `16-clamped-postcondition.json` — Property changed by postcondition clamped to [0, 1]
17. `17-no-postcondition.json` — Storylet without postconditions leaves properties unchanged

#### Universal vs Role-Specific (3 scripts)
18. `18-universal-storylets.json` — Storylets with empty roles array available to all NPCs
19. `19-role-specific-override.json` — Role-specific storylet has higher weight than universal
20. `20-combined-candidates.json` — GetCandidates() merges universal + role-specific into single list

---

## Phase 2: Strategies (Story-to-Strategy Translation & Multi-Phase Quests)

**Objective**: Validate strategy selection, phase transitions, and quest-like behavior from composed storylets.

### Test Categories (20+ scripts)

#### Strategy Data Structure (3 scripts)
1. `01-strategy-creation.json` — AOneOfStrategy created with correct phases
2. `02-strategy-initial-phase.json` — Strategy starts at phase 0
3. `03-strategy-phase-access.json` — CurrentPhaseIndex and CurrentPhase accessible

#### Phase Transitions (4 scripts)
4. `04-explicit-phase-transition.json` — Transition(newPhase) changes CurrentPhaseIndex
5. `05-sequential-phases.json` — Phase 0 → 1 → 2 (like taxi pickup → driving → drop-off)
6. `06-transition-precondition.json` — Transition only allowed if precondition met
7. `07-strategy-completion.json` — IsDone returns true at final phase

#### Phase Storylet Matching (3 scripts)
8. `08-phase-specific-storylets.json` — Phase 0 and Phase 1 have different candidate storylets
9. `09-phase-lockdown.json` — Non-current-phase storylets filtered out
10. `10-phase-timeout.json` — Phase auto-advances if timeout expires

#### Multi-NPC Strategies (3 scripts)
11. `11-multi-phase-taxi-quest.json` — Taxi quest: passenger waits (phase 0), driver arrives (phase 1)
12. `12-strategy-npc-awareness.json` — Strategy can reference other NPCs (passenger, driver, etc.)
13. `13-strategy-data-persistence.json` — Strategy state persists across save/load

#### Integration with DES (4 scripts)
14. `14-strategy-as-storylet.json` — Strategy selected like a storylet, runs phases sequentially
15. `15-strategy-interrupt.json` — Active strategy can be interrupted by high-priority quest
16. `16-strategy-resume.json` — After interrupt, strategy resumes from saved phase
17. `17-strategy-metrics.json` — Completed strategies counted in daily metrics

#### Failure & Recovery (3 scripts)
18. `18-strategy-failure-path.json` — Strategy can transition to failure phase instead of success
19. `19-strategy-timeout-fallback.json` — Strategy timeouts trigger fallback storylet
20. `20-strategy-nesting.json` — Strategy can contain nested sub-strategies (advanced)

---

## Phase 3: NPC-NPC Interactions (Request/Signal System) — ✅ ALL 22 TESTS PASSING

**Objective**: Validate request emission, claiming, fulfillment, abstract resolution, and event logging.

**Status**: All 22/22 tests passing as of 2026-03-14. Fixed issues:
1. `node_arrival` event Code now uses storylet ID instead of location ID (TestRunner/TestRunnerMain.cs:323)
2. Signal emission added on direct (Tier-2) claim completion (DesSimulation.cs:389-391)
3. Debug console output removed from TestEventLogger for cleaner test output

### Test Categories (20+ scripts)

#### Request Emission (3 scripts)
1. `01-request-postcondition.json` — Storylet with RequestPostcondition emits request to pool
2. `02-request-id-assignment.json` — Emitted request gets unique auto-incrementing ID
3. `03-request-logging.json` — request_emitted event logged with type, urgency, timeout

#### Request Pool Lifecycle (3 scripts)
4. `04-active-request-list.json` — GetActiveRequests() returns unclaimed, non-expired requests
5. `05-claimed-request-list.json` — GetPendingRequests() returns claimed, unfulfilled requests
6. `06-expired-request-purge.json` — Daily cleanup removes requests past timeout

#### Request Claiming (4 scripts)
7. `07-claim-during-encounter.json` — Two NPCs meet, one with matching ClaimTrigger claims other's request
8. `08-claim-role-matching.json` — Claiming NPC's role must match ClaimTrigger.RoleMatch
9. `09-claim-request-type.json` — ClaimTrigger.RequestType must match request type
10. `10-claim-once-per-request.json` — Request can only be claimed once; second claimer fails

#### Signal Emission (3 scripts)
11. `11-signal-on-fulfill.json` — Claimer emits "request_fulfilled" signal after completing request
12. `12-signal-logging.json` — signal_emitted event logged with request ID, signal type, source NPC
13. `13-signal-abstract-source.json` — Tier 3 resolution emits signal with SourceNpcId = -1 (abstract)

#### Tier 3 Abstract Resolution (4 scripts)
14. `14-abstract-resolution-daily.json` — During daily cleanup, unclaimed requests matched to capable roles
15. `15-abstract-food-delivery.json` — "food_delivery" request matched to "merchant" or "drifter" Tier 3 NPC
16. `16-abstract-help-request.json` — "help_request" matched to "worker" or "socialite"
17. `17-abstract-no-capable-role.json` — Request stays in pool if no capable roles exist

#### Event Integration (3 scripts)
18. `18-request-emission-during-encounter.json` — Request can be emitted, claimed, and fulfilled in same day
19. `19-interaction-metrics.json` — Pool metrics (active, claimed, expired, fulfilled) tracked
20. `20-interaction-pool-clear.json` — Daily boundary doesn't clear pool; only cleanup purges

---

## Phase 4: Player Integration (Player Quests & Navigation)

**Objective**: Validate player quest triggers, satnav routing, and quest feedback.

### Test Categories (20+ scripts)

#### Player Quest System (3 scripts)
1. `01-player-quest-trigger.json` — Player meets NPC with active request → quest triggered
2. `02-quest-data-structure.json` — Quest has ID, title, description, phase, reward
3. `03-quest-registry.json` — QuestFactory maintains active quest list

#### Quest Phases (4 scripts)
4. `04-quest-phase-0-wait.json` — Phase 0: player waits for event
5. `05-quest-phase-1-navigate.json` — Phase 1: player navigates to location
6. `06-quest-phase-2-complete.json` — Phase 2: player interacts with NPC
7. `07-quest-completion.json` — Quest completes, reward applied

#### Satnav Integration (3 scripts)
8. `08-satnav-route-creation.json` — Active quest creates route marker and satnav path
9. `09-satnav-progress.json` — Satnav shows distance-to-goal, updates on movement
10. `10-satnav-arrival.json` — Satnav clears when player reaches destination

#### Quest Log UI (3 scripts)
11. `11-quest-log-display.json` — Quest Log shows active, completed, failed quests
12. `12-quest-follow-unfollow.json` — Player can follow/unfollow quests (one followed at a time)
13. `13-quest-auto-advance.json` — On quest completion, next available quest auto-follows

#### Multiple Quests (3 scripts)
14. `14-multiple-active-quests.json` — Player can have multiple active quests, but only one followed
15. `15-quest-priority.json` — High-priority quests can interrupt lower-priority
16. `16-quest-abandonment.json` — Player can abandon active quest from log

#### Quest Feedback (3 scripts)
17. `17-quest-triggered-toast.json` — Toast appears when quest triggered (text + duration)
18. `18-quest-progress-update.json` — On-screen feedback for quest phase progress
19. `19-quest-completion-toast.json` — Toast appears when quest completed (success/failure)

#### Story Integration (3 scripts)
20. `20-quest-from-npc-interaction.json` — Quest triggered via NPC dialogue, not just encounters
21. `21-quest-reward-application.json` — Quest completion updates player inventory/gold
22. `22-quest-failure-conditions.json` — Quest can fail (timeout, dialogue choice, etc.)

---

## Phase 5: Escalation Mechanics (Crime Waves, Gang Conflicts)

**Objective**: Validate crime wave detection, authority response, group formation, and escalating conflict.

### Test Categories (20+ scripts)

#### Crime Wave Detection (3 scripts)
1. `01-crime-detection.json` — Crime events (pickpocket, theft, robbery) detected and logged
2. `02-crime-wave-threshold.json` — N crimes in time window triggers crime wave alert
3. `03-crime-location-clustering.json` — Crimes in same location count as higher severity

#### Authority NPC Behavior (4 scripts)
4. `04-authority-patrol.json` — Authority NPC patrols areas with high crime
5. `05-authority-encounter-criminal.json` — Authority encounters criminal with active warrant
6. `06-authority-arrest-attempt.json` — Authority attempts to arrest; criminal can flee or fight
7. `07-authority-reinforcement.json` — Multiple crimes trigger authority reinforcement

#### Gang Formation (3 scripts)
8. `08-group-formation-detection.json` — GroupDetector identifies criminals forming informal group
9. `09-group-solidarity.json` — Group members aid each other during authority encounters
10. `10-group-territory.json` — Group claims territory (location cluster), defends it

#### Economic Crime (3 scripts)
11. `11-blackmail-chain.json` — Blackmailer extracts wealth repeatedly from victim
12. `12-fence-stolen-goods.json` — Criminal fences stolen goods to merchant for profit
13. `13-robbery-escalation.json` — Isolated robbery → organized theft ring

#### Conflict Escalation (3 scripts)
14. `14-trust-violation.json` — Betrayal by group member causes conflict within group
15. `15-revenge-cycle.json` — Victim of crime seeks revenge, triggers counter-action
16. `16-authority-vs-gang.json` — Authority conducts raid on gang hideout

#### Wave Lifecycle (3 scripts)
17. `17-wave-intensification.json` — Crime wave increases in frequency/severity over days
18. `18-wave-de-escalation.json` — Successful arrests reduce crime wave over time
19. `19-wave-resolution.json` — Crime wave ends when perpetrators arrested or flee

#### Cascading Effects (3 scripts)
20. `20-wave-economy-impact.json` — Crime wave reduces merchant activity, increases prices
21. `21-wave-civilian-fear.json` — Civilians avoid high-crime areas, change routines
22. `22-wave-narrative-integration.json` — Crime wave affects player quest availability/rewards

---

## Test Execution Strategy

### Single-Phase Validation

Build TestRunner:
```bash
dotnet build TestRunner/TestRunner.csproj -c Release -p:EnableSourceLink=false
```

Run Phase 3 tests:
```bash
# Run all Phase 3 tests
for script in models/tests/tale/phase3-interactions/*.json; do
  JOYCE_TEST_SCRIPT="tests/tale/phase3-interactions/$(basename $script)" \
    ./TestRunner/bin/Release/net9.0/TestRunner
  if [ $? -ne 0 ]; then
    echo "FAILED: $script"
    exit 1
  fi
done
```

### Cross-Phase Integration
After each phase, run full integration suite (previous + current phase):
```bash
dotnet run --project Testbed -- --days 30 --events-file events.jsonl --expect-phase-3-metrics
```

### Continuous Integration
- Phase 0: **✅ PASSING** (foundation) — 20/20 PASS
- Phase 1: **✅ PASSING** (core loop) — 20/20 PASS
- Phase 2: **⏳ IN PROGRESS** (quest foundation) — test specs ready, implementation pending
- Phase 3: **✅ PASSING** (interactions) — 22/22 PASS ← JUST COMPLETED
- Phase 4: **⏳ QUEUED** (player)
- Phase 5: **⏳ QUEUED** (emergent)

---

## Test Metadata (For Each Script)

Each JSON script includes:
```json
{
  "name": "descriptive-test-name",
  "description": "What this test validates",
  "phase": "phase-N",
  "category": "feature-category",
  "priority": "critical|high|medium|low",
  "globalTimeout": 60,
  "dependencies": ["other-test-1", "other-test-2"],
  "preconditions": "What state is needed before test",
  "expectedOutcome": "What should happen",
  "steps": [ ... ]
}
```

---

## Next Actions

1. **Immediate** (Phase 3 — Just Implemented):
   - Implement 20 Phase 3 test scripts (request/signal flow)
   - Validate against compiled code
   - Run in Testbed to confirm event logging

2. **Short Term** (Phase 1 & 2):
   - Create Phase 1 storylet tests (JSON parsing, selection)
   - Create Phase 2 strategy tests (multi-phase quests)

3. **Medium Term** (Phase 4 & 5):
   - Create Phase 4 player integration tests
   - Create Phase 5 escalation tests

4. **CI Integration**:
   - Build GitHub Actions workflow to run all phases on push
   - Report coverage, failure rate, average test time

---

## File Organization

This document (`TALE_TEST_PLAN.md`) outlines the strategy.

Actual test script implementations split across:
- `TALE_TEST_SCRIPTS_PHASE_0.md` — 20 DES engine tests
- `TALE_TEST_SCRIPTS_PHASE_1.md` — 20+ storylet tests
- `TALE_TEST_SCRIPTS_PHASE_2.md` — 20+ strategy tests
- `TALE_TEST_SCRIPTS_PHASE_3.md` — 20+ interaction tests (with JSON script files)
- `TALE_TEST_SCRIPTS_PHASE_4.md` — 20+ player integration tests
- `TALE_TEST_SCRIPTS_PHASE_5.md` — 20+ escalation tests
