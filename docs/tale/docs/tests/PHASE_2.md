# TALE Phase 2: Strategies (Multi-Phase Quest Composition) — Test Specifications

## Overview

Phase 2 tests validate the **strategy system** that translates DES storylets into multi-phase quests. Strategies compose storylets across multiple **phases** (e.g., taxi: passenger_waiting → driver_en_route → passenger_delivered), with phase transitions triggered by conditions or time.

**Test Framework**: ExpectEngine with JSON format (same as Phases 0, 1, 3)
**Test Location**: `models/tests/tale/phase2-strategies/` (20 JSON scripts)
**Execution**: TestRunner CLI harness
**Dependencies**: Phase 1 (storylets must exist) + Phase 0 (DES simulation backend)

---

## Test Categories (20 Scripts)

### Category 1: Strategy Data Structure (3 tests)

#### Test 01: Strategy Creation
**File**: `01-strategy-creation.json`
**Priority**: Critical
**Objective**: Verify AOneOfStrategy can be created with multiple phases

**Preconditions**:
- DES simulation running with 10 NPCs
- Storylet library loaded (Phase 1 complete)
- Strategy definitions available (taxi, courier, etc.)

**Steps**:
1. Wait for NPC creation events (verify DES initialized)
2. Emit strategy creation event (inject strategy definition into strategy manager)
3. Verify strategy created with 3+ phases
4. Check phase array is populated correctly

**Expected Outcome**:
- Strategy has phases array with correct IDs
- Phase 0, 1, 2 accessible
- No errors during creation

**Implementation Notes**:
- Strategy is created as part of DES simulation (TaleManager or equivalent)
- May verify via internal state check or via event emission
- Example: Taxi strategy has phases: ["passenger_waiting", "driver_en_route", "passenger_delivered"]

---

#### Test 02: Initial Phase
**File**: `02-initial-phase.json`
**Priority**: High
**Objective**: Verify strategy starts at phase 0

**Preconditions**:
- DES initialized, strategy created (test 01 passes)

**Steps**:
1. Create strategy
2. Verify CurrentPhaseIndex = 0
3. Verify CurrentPhase points to phase 0 definition

**Expected Outcome**:
- NewPhaseReached event with phase_index=0 on creation
- Strategy ready to execute phase 0

---

#### Test 03: Phase Access
**File**: `03-phase-access.json`
**Priority**: High
**Objective**: Verify CurrentPhaseIndex and CurrentPhase properties accessible

**Preconditions**:
- Strategy created and initialized at phase 0

**Steps**:
1. Query CurrentPhaseIndex (expect 0)
2. Query CurrentPhase (expect phase 0 object)
3. Verify phase has storylets, duration constraints
4. Transition to phase 1
5. Verify CurrentPhaseIndex now = 1

**Expected Outcome**:
- Both properties readable and accurate
- Reflect actual strategy state

---

### Category 2: Phase Transitions (4 tests)

#### Test 04: Explicit Transition
**File**: `04-explicit-transition.json`
**Priority**: Critical
**Objective**: Verify Transition(newPhase) changes CurrentPhaseIndex

**Preconditions**:
- Strategy at phase 0

**Steps**:
1. Call strategy.Transition(1) (or emit transition event)
2. Verify CurrentPhaseIndex changes to 1
3. Emit transition event from DES
4. Verify phase_changed event in event stream

**Expected Outcome**:
- Phase index updates immediately
- Event logged for test framework to observe

---

#### Test 05: Sequential Phases (Taxi Quest Example)
**File**: `05-sequential-phases.json`
**Priority**: Critical
**Objective**: Verify multi-phase sequence (taxi: 0→1→2)

**Preconditions**:
- DES running with 2 NPCs (passenger, driver)
- Taxi strategy available

**Steps**:
1. NPC A becomes passenger (enters phase 0: waiting)
2. Verify node_arrival event for passenger at destination (meeting point)
3. NPC B (driver) accepts request, enters phase 1 (driving)
4. Verify both NPCs reach destination
5. Phase auto-transitions to phase 2 (dropoff complete)
6. Verify strategy completion

**Expected Outcome**:
- Phase 0 → Phase 1 → Phase 2 transitions occur in sequence
- Each phase has storylets/duration appropriate to that phase
- Strategy marked completed at final phase

**Implementation Notes**:
- Phases are named: passenger_waiting, driver_en_route, passenger_delivered
- Phase 0 duration: 30 sim-minutes (passenger waits for driver)
- Phase 1 duration: travel time based on distance
- Phase 2 duration: handoff (5 sim-minutes)
- Transition on timeout if condition not met

---

#### Test 06: Transition Precondition
**File**: `06-transition-precondition.json`
**Priority**: High
**Objective**: Verify transition only if precondition met

**Preconditions**:
- Strategy at phase 1 with transition precondition: "driver_trust > 0.5"

**Steps**:
1. Set driver trust = 0.3 (below threshold)
2. Emit transition attempt event
3. Verify transition rejected (phase still 1)
4. Increase driver trust to 0.7
5. Emit transition attempt again
6. Verify transition accepted (phase now 2)

**Expected Outcome**:
- Precondition checked before transition
- Transition blocked when false, allowed when true
- Event logged reflecting decision

---

#### Test 07: Strategy Completion
**File**: `07-strategy-completion.json`
**Priority**: High
**Objective**: Verify IsDone returns true at final phase

**Preconditions**:
- Strategy at final phase (phase 2 of 3-phase taxi)

**Steps**:
1. Query strategy.IsDone (expect false while in phase 2)
2. Wait for phase 2 completion (timeout or condition)
3. Auto-transition to completion marker
4. Query strategy.IsDone again (expect true)
5. Verify strategy_completed event

**Expected Outcome**:
- IsDone property reflects actual completion state
- strategy_completed event logged
- NPC freed to select new strategy

---

### Category 3: Phase-Specific Behavior (3 tests)

#### Test 08: Phase Storylets
**File**: `08-phase-storylets.json`
**Priority**: High
**Objective**: Verify different phases have different candidate storylets

**Preconditions**:
- Strategy loaded (taxi with phases: waiting, driving, dropoff)
- Storylet library has phase-specific storylets

**Steps**:
1. Query candidates for phase 0 (expect: "wait_for_passenger", "check_surroundings", "worry")
2. Query candidates for phase 1 (expect: "drive_route", "check_mirrors", "listen_radio")
3. Query candidates for phase 2 (expect: "collect_payment", "thank_passenger", "close_door")
4. Verify no overlap between phase candidate lists

**Expected Outcome**:
- Each phase has 3+ storylet candidates
- Phase 0 candidates differ from phase 1 and 2
- Storylets aligned to phase semantics

**Implementation Notes**:
- Candidates defined in strategy definition or dynamically filtered by phase
- May use phase ID as precondition filter

---

#### Test 09: Phase Lockdown
**File**: `09-phase-lockdown.json`
**Priority**: High
**Objective**: Verify non-current-phase storylets filtered out

**Preconditions**:
- Strategy at phase 1 of 3

**Steps**:
1. Request all available storylets for NPC (should only include phase 1 candidates)
2. Verify phase 0 storylets NOT in list
3. Verify phase 2 storylets NOT in list
4. Verify phase 1 storylets ARE in list
5. Transition to phase 2
6. Request storylets again (should now show phase 2 only)

**Expected Outcome**:
- Storytellet selection respects phase boundaries
- Non-current phases filtered automatically
- No phase bleed

---

#### Test 10: Phase Timeout
**File**: `10-phase-timeout.json`
**Priority**: High
**Objective**: Verify phase auto-advances if timeout expires

**Preconditions**:
- Strategy at phase 0 with timeout=60 (sim-minutes)
- Phase has precondition that won't naturally trigger

**Steps**:
1. Enter phase 0 (note start time)
2. Wait for phase to reach timeout
3. Verify phase auto-transitions to phase 1 (without precondition met)
4. Verify phase_transitioned event with reason="timeout"

**Expected Outcome**:
- Phase auto-transitions after timeout
- Event indicates timeout reason
- No hard failure; strategy continues

---

### Category 4: DES Integration (4 tests)

#### Test 11: Strategy as Storylet
**File**: `11-strategy-as-storylet.json`
**Priority**: High
**Objective**: Verify strategy selected/runs like a storylet from DES

**Preconditions**:
- DES initialized, NPC ready for new storylet
- Strategy registered as selectable in storylet library

**Steps**:
1. DES selects next storylet for NPC
2. Verify it can be a strategy (strategy returned from GetCandidates)
3. Verify strategy begins phase 0 execution
4. Observe node_arrival events corresponding to phase 0 storylet
5. Observe phase transitions as DES simulation progresses

**Expected Outcome**:
- Strategy appears in GetCandidates() results
- Strategy execution integrated into DES event loop
- Phase 0 storylet selected and executed as NPC's current activity

**Implementation Notes**:
- Strategy must be in storylet library (or strategy library as subset)
- Phase 0 storylet determines initial location/action

---

#### Test 12: Multi-Phase Taxi (Full Scenario)
**File**: `12-multi-phase-taxi.json`
**Priority**: High
**Objective**: Verify full taxi quest lifecycle: passenger waits → driver arrives → delivers

**Preconditions**:
- DES running with 3+ NPCs
- Taxi strategy available with request emission
- Duration and timing realistic for simulation

**Steps**:
1. Passenger NPC enters taxi strategy phase 0 (waits at origin)
2. Observe passenger node_arrival at origin
3. Taxi request emitted (request_emitted event)
4. Driver NPC claims request
5. Driver enters taxi strategy phase 1 (driving to passenger)
6. Driver node_arrival at passenger location
7. Driver transitions strategy to phase 2 (driving to destination with passenger)
8. Both NPCs reach destination
9. Strategy completes (phase 2 timeout/condition)
10. Signal emitted (signal_emitted with "taxi_completed")

**Expected Outcome**:
- All phase transitions occur
- Both NPCs coordinate correctly
- Request/signal events logged
- Strategy completion recorded

**Implementation Notes**:
- Passenger starts at location A, requests taxi
- Driver starts somewhere else, claims request, travels to A, then to B
- Success = passenger at destination B, driver present, strategy done

---

#### Test 13: Strategy Interrupt
**File**: `13-strategy-interrupt.json`
**Priority**: Medium
**Objective**: Verify active strategy can be interrupted by high-priority event

**Preconditions**:
- NPC executing strategy at phase 1
- High-priority event can interrupt (emergency call, urgent message)

**Steps**:
1. NPC executing taxi strategy phase 1 (driving)
2. Emit high-priority interrupt event
3. Verify strategy paused (phase still 1 but no progress)
4. Verify interrupt handled (new activity begins)
5. Later: resume interrupt, verify strategy resumes from phase 1

**Expected Outcome**:
- Strategy can be paused without loss of state
- Resume returns to same phase
- No error; graceful pause/resume

---

#### Test 14: Strategy Resume
**File**: `14-strategy-resume.json`
**Priority**: Medium
**Objective**: Verify strategy resumes from saved phase after interrupt

**Preconditions**:
- Test 13 passes (interrupt mechanism works)

**Steps**:
1. Interrupt strategy at phase 1, save phase index
2. Handle interrupt (phase 1 still active in memory)
3. Resume strategy (restore from phase 1)
4. Verify CurrentPhaseIndex still 1
5. Continue phase 1 execution
6. Eventually complete phase 1 → 2

**Expected Outcome**:
- Resume restores correct phase
- No rewind to phase 0
- Execution continues smoothly

---

### Category 5: Failure & Advanced (6 tests)

#### Test 15: Strategy Failure Path
**File**: `15-strategy-failure-path.json`
**Priority**: Medium
**Objective**: Verify strategy can transition to failure phase instead of success

**Preconditions**:
- Strategy with failure condition (e.g., timeout without reaching destination)

**Steps**:
1. Enter strategy phase 0
2. Create condition for failure (phase 1 transition fails after timeout)
3. Verify strategy transitions to "failure" phase instead of next normal phase
4. Verify strategy marked as failed (IsDone=true, success=false)
5. Verify failure event logged

**Expected Outcome**:
- Strategy can explicitly fail
- Failure tracked as distinct outcome
- NPC can handle failure (retry, move on, etc.)

---

#### Test 16: Strategy Timeout Fallback
**File**: `16-strategy-timeout-fallback.json`
**Priority**: Medium
**Objective**: Verify timeout triggers fallback storylet if strategy stalls

**Preconditions**:
- Strategy with overall timeout (e.g., 2 hours sim-time)
- Fallback storylet defined (rest, wander)

**Steps**:
1. Enter strategy phase 0
2. Simulate stall (no progress toward phase transition for extended time)
3. Reach overall timeout
4. Verify strategy abandoned
5. Verify fallback storylet selected for NPC
6. Verify timeout_fallback event logged

**Expected Outcome**:
- Strategy doesn't hang forever
- Fallback allows NPC to continue
- Event indicates reason

---

#### Test 17: Strategy Persistence
**File**: `17-strategy-persistence.json`
**Priority**: Medium
**Objective**: Verify strategy state persists across save/load

**Preconditions**:
- Strategy at phase 1
- Save game triggered
- Load game from save

**Steps**:
1. Strategy at phase 1 with partial progress (time spent in phase)
2. Trigger save game event (game state serialized)
3. Trigger load game event (game state restored)
4. Query strategy state
5. Verify CurrentPhaseIndex still 1 (not reset to 0)
6. Verify time progress preserved (phase timer correct)

**Expected Outcome**:
- Strategy state survives save/load
- No reset or loss of progress
- Game can be resumed mid-strategy

---

#### Test 18: Multi-NPC Coordination
**File**: `18-multi-npc-coordination.json`
**Priority**: Low
**Objective**: Verify multiple NPCs can coordinate phases (taxi driver waits for passenger)

**Preconditions**:
- DES with 2+ NPCs
- Strategy allows multi-NPC awareness (shared phase state or signal coordination)

**Steps**:
1. Passenger NPC at phase 0 (waiting)
2. Driver NPC at phase 1 (en route)
3. Both reach meeting location
4. Driver observes passenger present (via signal or event)
5. Both coordinate to phase 2 (dropoff)
6. Verify both reach destination before completion

**Expected Outcome**:
- NPCs synchronize without deadlock
- Signals allow coordination
- Both complete together

---

#### Test 19: Strategy Nesting
**File**: `19-strategy-nesting.json`
**Priority**: Low
**Objective**: Verify strategy can contain nested sub-strategies

**Preconditions**:
- Complex strategy with sub-strategy (e.g., crime operation has sub-steps: case, plan, execute)

**Steps**:
1. Enter strategy phase 0 (planning)
2. Phase 0 itself is a sub-strategy with 2 phases
3. Execute sub-strategy phase 0, then phase 1
4. After sub-strategy complete, transition to main strategy phase 1
5. Verify proper nesting and unwinding

**Expected Outcome**:
- Nested strategies execute correctly
- No infinite recursion or deadlock
- Parent strategy continues after child completes

---

#### Test 20: Strategy Failure Recovery
**File**: `20-strategy-failure-recovery.json`
**Priority**: Low
**Objective**: Verify failed strategy allows NPC to retry or move on

**Preconditions**:
- Strategy failed (test 15 outcome)

**Steps**:
1. Strategy failed at phase 2
2. NPC should be able to select new strategy
3. Verify NPC not stuck in failed state
4. Simulate NPC selecting different strategy (e.g., rest instead of taxi)
5. Verify new strategy starts fresh at phase 0

**Expected Outcome**:
- NPC not permanently stuck
- Can retry same strategy type (new instance) or select different strategy
- System resilient to failure

---

## Execution Guide

### Prerequisites
```bash
# Build TestRunner
dotnet build TestRunner/TestRunner.csproj -c Release -p:EnableSourceLink=false
```

### Run All Phase 2 Tests
```bash
for script in models/tests/tale/phase2-strategies/*.json; do
  JOYCE_TEST_SCRIPT="tests/tale/phase2-strategies/$(basename $script)" \
    ./TestRunner/bin/Release/net9.0/TestRunner
  if [ $? -ne 0 ]; then
    echo "FAILED: $script"
    exit 1
  fi
done
```

### Run Single Test
```bash
JOYCE_TEST_SCRIPT="tests/tale/phase2-strategies/05-sequential-phases.json" \
  ./TestRunner/bin/Release/net9.0/TestRunner
```

### Expected Output
```
[TEST] PASS: Quit action: pass
[TEST] Elapsed: 00:00:XX.XXXXXXXX
```

---

## Test Metadata

Each Phase 2 test JSON includes:
```json
{
  "name": "test-name",
  "description": "What this validates",
  "phase": "phase-2",
  "category": "strategy-category",
  "priority": "critical|high|medium|low",
  "globalTimeout": 60,
  "steps": [
    {"expect": {"type": "event.type", "code": "optional"}, "timeout": 30},
    {"action": "quit", "result": "pass"}
  ]
}
```

---

## Summary

| Test # | Name | Priority | Category |
|--------|------|----------|----------|
| 01 | strategy-creation | Critical | Structure |
| 02 | initial-phase | High | Structure |
| 03 | phase-access | High | Structure |
| 04 | explicit-transition | Critical | Transitions |
| 05 | sequential-phases | Critical | Transitions |
| 06 | transition-precondition | High | Transitions |
| 07 | strategy-completion | High | Transitions |
| 08 | phase-storylets | High | Behavior |
| 09 | phase-lockdown | High | Behavior |
| 10 | phase-timeout | High | Behavior |
| 11 | strategy-as-storylet | High | Integration |
| 12 | multi-phase-taxi | High | Integration |
| 13 | strategy-interrupt | Medium | Integration |
| 14 | strategy-resume | Medium | Integration |
| 15 | strategy-failure-path | Medium | Advanced |
| 16 | strategy-timeout-fallback | Medium | Advanced |
| 17 | strategy-persistence | Medium | Advanced |
| 18 | multi-npc-coordination | Low | Advanced |
| 19 | strategy-nesting | Low | Advanced |
| 20 | strategy-failure-recovery | Low | Advanced |

**Total**: 20 tests covering strategy lifecycle, DES integration, and advanced scenarios.
