# Phase 0 Detailed Test Scripts — DES Engine

## Overview

Phase 0 validates the **discrete event simulation (DES) engine**, the foundation for all TALE phases. This document provides detailed specifications for 20 test scripts organized into 6 categories.

**Test Categories**:
1. Initialization & Setup (3 tests)
2. Event Queue & Scheduling (4 tests)
3. Property Dynamics (3 tests)
4. Encounter Detection (4 tests)
5. Relationship Tracking (3 tests)
6. Metrics & Logging (3 tests)

---

## Group 1: Initialization & Setup (Tests 01-03)

### Test 01: DES Simulation Initialization

**Objective**: Verify that DesSimulation.Initialize() correctly creates and initializes all core data structures.

**Preconditions**:
- Empty DesSimulation instance
- 10 NPCs with assigned roles
- SpatialModel with 5 locations (workplace, home, social_venue, street_segment, street_segment)

**Expected Outcome**:
- _queue created and empty
- _metrics initialized with zero counts
- _encounters initialized as empty collection
- _relationships initialized as RelationshipTracker
- All 10 NPCs have initial properties set (hunger, fatigue, wealth, etc. all in [0, 1])
- All NPCs have initial location assigned (home)

**Test Steps**:
1. Expect `npc_created` event (x10)
2. Sleep 100ms (allow initialization)
3. Inject event to verify _queue is accessible
4. Expect event processed in time order
5. Action: quit, result: pass

**Duration**: 30s
**Priority**: Critical
**Category**: initialization

---

### Test 02: NPC Creation and Initial Wakeup

**Objective**: Verify that NPC creation triggers proper initialization and wakeup event scheduling.

**Preconditions**:
- Fresh DesSimulation
- 5 NPCs to be created
- Time set to 2024-01-01 04:00:00

**Expected Outcome**:
- Each NPC gets `npc_created` event logged
- Each NPC has CurrentEnd set to a wakeup time (05:00-09:00 range)
- Each NPC's properties initialized (hunger ∈ [0.3, 0.7], fatigue ∈ [0.1, 0.5], etc.)
- Wakeup events queued for all NPCs in correct time order

**Test Steps**:
1. Inject NPC 1-5 creation
2. Expect `npc_created` (x5)
3. Sleep 200ms
4. Expect `node_arrival` (wakeup event) for all 5 NPCs
5. Verify node_arrival timestamps are in range [05:00, 09:00]
6. Action: quit, result: pass

**Duration**: 30s
**Priority**: Critical
**Category**: initialization

---

### Test 03: Spatial Model Loading and Travel Times

**Objective**: Verify that SpatialModel correctly loads locations and computes travel times.

**Preconditions**:
- SpatialModel created with 25 locations (mixed types)
- Cluster size: 1000m
- Location positions: distributed across cluster

**Expected Outcome**:
- All 25 locations accessible by ID
- GetTravelTime(fromLoc, toLoc) returns valid values
- Travel time correlates with distance (closer = faster)
- Travel times are deterministic (same locations → same time)
- Home locations have travel times in expected range (5-30 min)

**Test Steps**:
1. Inject query for location 0 position
2. Expect successful location lookup
3. Sleep 100ms
4. Inject travel time query between 2 locations
5. Verify travel time is positive and reasonable
6. Repeat for 5 different location pairs
7. Verify time correlates with distance
8. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: initialization

---

## Group 2: Event Queue & Scheduling (Tests 04-07)

### Test 04: Event Queue Processing Order

**Objective**: Verify that events process in strict time order, regardless of insertion order.

**Preconditions**:
- Fresh DesSimulation
- 5 NPCs
- Queue processing enabled

**Expected Outcome**:
- Events are processed in ascending timestamp order
- Even if injected out of order, processing is ordered
- No events are skipped
- Event timestamps are monotonically increasing

**Test Steps**:
1. Inject node_arrival for NPC 1 at 2024-01-01 09:00
2. Inject node_arrival for NPC 2 at 2024-01-01 07:00 (earlier)
3. Inject node_arrival for NPC 3 at 2024-01-01 08:00 (middle)
4. Sleep 500ms (let queue process)
5. Expect event (time: 07:00, npc: 2)
6. Expect event (time: 08:00, npc: 3)
7. Expect event (time: 09:00, npc: 1)
8. Action: quit, result: pass

**Duration**: 30s
**Priority**: Critical
**Category**: scheduling

---

### Test 05: Node Arrival Event and Postcondition Application

**Objective**: Verify that NodeArrival events properly trigger location changes, apply postconditions, and schedule next events.

**Preconditions**:
- 2 NPCs created
- Both at home initially
- Storylet selected: "go_to_workplace" (duration: 60 min, postconditions: fatigue +0.2)

**Expected Outcome**:
- NPC location changes from "home" to "workplace"
- Postcondition applied: fatigue increases by 0.2 (clamped to [0, 1])
- Next event scheduled 60 minutes later
- `node_arrival` event logged with npc, location, time
- `relationship_changed` event not triggered (same location)

**Test Steps**:
1. Expect `npc_created` (NPC 1)
2. Sleep 50ms
3. Expect `node_arrival` (wakeup at home)
4. Sleep 50ms
5. Inject storylet completion event (duration: 60 min)
6. Expect postconditions applied (fatigue delta verified)
7. Expect next `node_arrival` event scheduled 60 min later
8. Action: quit, result: pass

**Duration**: 30s
**Priority**: Critical
**Category**: scheduling

---

### Test 06: Day Boundary and Cleanup

**Objective**: Verify that day boundary events trigger cleanup, reset daily counts, and compute metrics.

**Preconditions**:
- 10 NPCs running
- Simulate to 2024-01-01 23:59:00
- Day 1 has metrics (15 encounters, 5 storylets completed)

**Expected Outcome**:
- `day_summary` event fired at 2024-01-02 00:00:00
- Daily presence records cleared
- Daily dedup cache cleared
- Metrics finalized and logged
- Properties drift applied (desperation, morality)
- NPC encounter counts reset to 0
- Next day's metric counters initialized

**Test Steps**:
1. Inject 10 NPCs, advance time to 2024-01-01 23:50:00
2. Sleep 200ms (allow processing)
3. Expect multiple `node_arrival` and `encounter` events
4. Sleep 500ms (advance to next day)
5. Expect `day_summary` event
6. Verify day_summary contains: day=1, encounters_total, storylets_completed
7. Action: quit, result: pass

**Duration**: 35s
**Priority**: High
**Category**: scheduling

---

### Test 07: Long-Running Simulation (30 Days)

**Objective**: Verify that a full 30-day simulation completes without deadlock, memory leak, or data corruption.

**Preconditions**:
- 100 NPCs
- Encounter probabilities: venue 0.07, street 0.015, workplace 0.04
- Randomized storylet selection
- Time: 2024-01-01 to 2024-01-31

**Expected Outcome**:
- Simulation completes cleanly (no exceptions)
- All 30 day_summary events fire (one per day)
- Final event count: npc_created=100, node_arrival ≥ 5000, encounter ≥ 100
- Memory usage stable (no continuous growth)
- Metrics reported: encounters, storylets, properties
- Metrics pass validation (no NaN, Infinity, or negative values)

**Test Steps**:
1. Inject 100 NPCs
2. Expect 100 `npc_created` events
3. Advance time to end of day 1 (sleep 2000ms)
4. Expect `day_summary` for day 1
5. Repeat for days 2-30 (advance 2000ms per day)
6. Expect 30 `day_summary` events total
7. Verify event counts and metrics
8. Action: quit, result: pass

**Duration**: 90s
**Priority**: High
**Category**: scheduling

---

## Group 3: Property Dynamics (Tests 08-10)

### Test 08: Postcondition Application and Clamping

**Objective**: Verify that postconditions update NPC properties atomically and clamp values to [0, 1].

**Preconditions**:
- 2 NPCs with initial properties:
  - NPC 1: wealth=0.9, hunger=0.1
  - NPC 2: fatigue=0.95, anger=0.0

**Expected Outcome**:
- Postcondition "wealth: +0.2" results in wealth=1.0 (clamped from 1.1)
- Postcondition "hunger: -0.3" results in hunger=0.0 (clamped from -0.2)
- Postcondition "fatigue: +0.1" results in fatigue=1.0 (clamped from 1.05)
- Postcondition "anger: +0.5" results in anger=0.5 (no clamp needed)
- Multiple postconditions applied in order (atomically)

**Test Steps**:
1. Expect `npc_created` (NPC 1, NPC 2)
2. Sleep 100ms
3. Inject postcondition for NPC 1 (wealth: +0.2)
4. Verify property delta logged
5. Expect wealth=1.0 (clamped)
6. Inject postcondition for NPC 2 (hunger: -0.3, fatigue: +0.1)
7. Verify both properties updated atomically
8. Verify clamping applied
9. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: properties

---

### Test 09: Desperation Daily Drift

**Objective**: Verify that desperation is computed and drifts correctly based on wealth and hunger.

**Preconditions**:
- 2 NPCs
- NPC 1: wealth=0.8, hunger=0.2 (high wealth, low hunger → low desperation)
- NPC 2: wealth=0.1, hunger=0.9 (low wealth, high hunger → high desperation)
- Run for 5 days

**Expected Outcome**:
- Desperation computed at each day boundary: desperation = (hunger + (1 - wealth)) / 2
- NPC 1 desperation ≈ 0.3 (stable, low)
- NPC 2 desperation ≈ 0.9 (stable, high)
- Desperation values remain stable day-to-day (no drift if wealth/hunger constant)
- If wealth decreases, desperation increases
- If hunger increases, desperation increases

**Test Steps**:
1. Create 2 NPCs with specified properties
2. Expect `npc_created` (x2)
3. Advance to day 1 boundary
4. Expect `day_summary` with computed desperation
5. Verify NPC 1 desperation ≈ 0.3
6. Verify NPC 2 desperation ≈ 0.9
7. Repeat days 2-5 (verify stability)
8. Inject wealth decrease for NPC 1 (0.8 → 0.5)
9. Advance to day 6
10. Verify NPC 1 desperation increased
11. Action: quit, result: pass

**Duration**: 35s
**Priority**: High
**Category**: properties

---

### Test 10: Morality Daily Drift

**Objective**: Verify that morality drifts based on desperation and current value.

**Preconditions**:
- 2 NPCs
- NPC 1: morality=0.8, desperation=0.2 (high morality, low desperation → stable/slight drift down)
- NPC 2: morality=0.3, desperation=0.9 (low morality, high desperation → drift down)
- Run for 5 days

**Expected Outcome**:
- High desperation → morality decreases (drift toward 0)
- Low desperation → morality stable or slight recovery (drift toward 1)
- Drift magnitude ≈ 0.02 per day (desperation-weighted)
- NPC 1 morality drifts slowly (desperation=0.2, drift ≈ -0.004 per day)
- NPC 2 morality drifts faster (desperation=0.9, drift ≈ -0.018 per day)
- Morality always clamped to [0, 1]

**Test Steps**:
1. Create 2 NPCs with specified properties
2. Expect `npc_created` (x2)
3. Advance to day 1 boundary
4. Expect `day_summary` with morality deltas
5. Record morality for both NPCs
6. Repeat days 2-5, verifying drift direction
7. Verify NPC 1 morality drift slower than NPC 2
8. Verify drift correlates with desperation
9. Action: quit, result: pass

**Duration**: 35s
**Priority**: High
**Category**: properties

---

## Group 4: Encounter Detection (Tests 11-14)

### Test 11: Encounter at Shared Location

**Objective**: Verify that two NPCs at the same location trigger an encounter event.

**Preconditions**:
- 2 NPCs (NPC 1, NPC 2)
- Both scheduled to be at "social_venue" during 14:00-16:00 on 2024-01-01
- Trust: neutral (no prior interaction)

**Expected Outcome**:
- Both NPCs arrive at social_venue at overlapping times
- `encounter` event fires with both NPC IDs
- Encounter type determined by trust + anger/morality (default: "greet")
- Trust updated (slightly positive for mutual neutral encounter)
- `relationship_changed` event logged with trust deltas

**Test Steps**:
1. Create 2 NPCs
2. Schedule both to social_venue at 14:00
3. Expect 2 `npc_created` events
4. Sleep 500ms
5. Advance time to 14:00
6. Expect 2 `node_arrival` events (both at social_venue)
7. Expect `encounter` event with npc_pair=[1,2]
8. Expect `relationship_changed` events (for both NPCs)
9. Action: quit, result: pass

**Duration**: 35s
**Priority**: Critical
**Category**: encounters

---

### Test 12: Encounter Avoidance (Different Locations)

**Objective**: Verify that NPCs at different locations do not trigger an encounter.

**Preconditions**:
- 2 NPCs (NPC 1, NPC 2)
- NPC 1 scheduled to workplace at 14:00
- NPC 2 scheduled to home at 14:00
- Same time window, different locations

**Expected Outcome**:
- No `encounter` event is fired
- Both `node_arrival` events fire independently
- No `relationship_changed` events
- Trust values unchanged

**Test Steps**:
1. Create 2 NPCs
2. Schedule NPC 1 to workplace, NPC 2 to home (same time window)
3. Expect 2 `npc_created` events
4. Sleep 500ms
5. Advance time to 14:00
6. Expect 2 `node_arrival` events
7. Verify no `encounter` event is logged
8. Verify relationship unchanged
9. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: encounters

---

### Test 13: Multiple Encounters (3+ NPCs at Same Location)

**Objective**: Verify that when 3+ NPCs meet at the same location, all possible pair encounters are generated.

**Preconditions**:
- 3 NPCs (NPC 1, NPC 2, NPC 3)
- All scheduled to social_venue at 14:00-15:00

**Expected Outcome**:
- All 3 NPCs arrive at social_venue
- 3 pair encounters generated: (1,2), (1,3), (2,3)
- Order of encounters determined by processing logic (may be deterministic or random)
- All 3 encounters logged with correct NPC IDs
- Each NPC has relationship updated with the other 2

**Test Steps**:
1. Create 3 NPCs
2. Schedule all to social_venue at 14:00
3. Expect 3 `npc_created` events
4. Sleep 500ms
5. Advance time to 14:00
6. Expect 3 `node_arrival` events (all at social_venue)
7. Expect 3 `encounter` events
8. Verify encounter pairs: (1,2), (1,3), (2,3)
9. Expect 6 `relationship_changed` events (2 per pair)
10. Action: quit, result: pass

**Duration**: 40s
**Priority**: High
**Category**: encounters

---

### Test 14: Encounter Trust Update

**Objective**: Verify that encounter events correctly update trust values and log relationship changes.

**Preconditions**:
- 2 NPCs with initial trust = 0.5 (neutral)
- NPC 1: anger=0.1, morality=0.8 (positive tendency)
- NPC 2: anger=0.9, morality=0.2 (negative tendency)

**Expected Outcome**:
- Encounter type determined by trust + anger + morality
- Trust updated for both NPCs (may be asymmetric)
- NPC 1 trust may increase (low anger, high morality → positive)
- NPC 2 trust may decrease (high anger, low morality → negative)
- `relationship_changed` event logged with before/after trust values
- Trust always clamped to [0, 1]

**Test Steps**:
1. Create 2 NPCs with specified properties
2. Expect 2 `npc_created` events
3. Sleep 100ms
4. Schedule encounter at social_venue at 14:00
5. Advance time to 14:00
6. Expect `encounter` event
7. Expect 2 `relationship_changed` events
8. Verify trust values in relationship_changed:
   - NPC 1 trust increased or stable
   - NPC 2 trust decreased or stable
9. Verify trust always in [0, 1]
10. Action: quit, result: pass

**Duration**: 35s
**Priority**: Critical
**Category**: encounters

---

## Group 5: Relationship Tracking (Tests 15-17)

### Test 15: Trust Tier Progression (Friendly)

**Objective**: Verify that positive interactions increase trust and trigger tier transitions.

**Preconditions**:
- 2 NPCs with initial trust = 0.3 (strangers)
- NPC 1: anger=0.1, morality=0.9 (positive personality)
- Encounter them 5 times over 5 days

**Expected Outcome**:
- Each positive encounter increases trust
- Trust progresses through tiers:
  - 0.0-0.4: "stranger" (no special interaction)
  - 0.4-0.7: "acquaintance" (neutral)
  - 0.7-1.0: "friendly" (positive interaction)
- After 5 encounters, trust should reach ~0.7-0.8 (friendly tier)
- `relationship_changed` event logs tier transitions

**Test Steps**:
1. Create 2 NPCs, initial trust=0.3 (strangers)
2. Day 1: Schedule encounter at social_venue
3. Expect `encounter` and `relationship_changed`
4. Verify trust increased (e.g., 0.3 → 0.35)
5. Repeat for days 2-5 (schedule encounters)
6. After encounter 5, verify trust in friendly range (0.7+)
7. Verify relationship tier transitioned from stranger → acquaintance → friendly
8. Action: quit, result: pass

**Duration**: 35s
**Priority**: High
**Category**: relationships

---

### Test 16: Trust Tier Progression (Hostile)

**Objective**: Verify that negative interactions decrease trust and trigger hostile tier.

**Preconditions**:
- 2 NPCs with initial trust = 0.5 (neutral)
- NPC 1: anger=0.9, morality=0.1 (negative personality)
- Encounter them 5 times over 5 days

**Expected Outcome**:
- Each negative encounter decreases trust
- Trust progresses: neutral (0.5) → stranger (0.3) → hostile (0.1 or less)
- After 5 encounters, trust should reach ~0.0-0.2 (hostile tier)
- Interaction type changes to "intimidate" or "rob" (based on trust/anger)
- `relationship_changed` event logs trust decline

**Test Steps**:
1. Create 2 NPCs, initial trust=0.5 (neutral)
2. NPC 1 personality: anger=0.9, morality=0.1
3. Day 1: Schedule encounter
4. Expect `encounter` (type may be "intimidate")
5. Expect `relationship_changed` with trust decrease
6. Verify trust decreased (e.g., 0.5 → 0.45)
7. Repeat for days 2-5
8. After encounter 5, verify trust in hostile range (0.1 or less)
9. Verify tier transitions: neutral → acquaintance → hostile
10. Action: quit, result: pass

**Duration**: 35s
**Priority**: High
**Category**: relationships

---

### Test 17: Relationship Persistence Across Day Boundaries

**Objective**: Verify that trust values persist and are not reset at day boundaries.

**Preconditions**:
- 2 NPCs with initial trust = 0.5
- Encounter on day 1 at 14:00 (trust updated to 0.6)
- Day boundary at 2024-01-01 23:59:59

**Expected Outcome**:
- Trust=0.6 after day 1 encounter
- At day boundary (midnight), daily presence cleared, but trust persists
- Day 2 NPC relationship snapshot shows trust=0.6 (unchanged)
- Subsequent encounter on day 2 uses persisted trust value
- No "relationship reset" event at day boundary

**Test Steps**:
1. Create 2 NPCs, initial trust=0.5
2. Day 1, 14:00: Schedule encounter
3. Expect `encounter` and `relationship_changed` (trust → 0.6)
4. Record trust value
5. Advance to day 1 23:50:00
6. Expect `day_summary` for day 1
7. Verify relationship data persisted (trust still 0.6)
8. Advance to day 2 09:00:00
9. Verify trust still 0.6 (unchanged by day boundary)
10. Day 2, 15:00: Schedule second encounter
11. Expect `encounter` to use persisted trust=0.6
12. Action: quit, result: pass

**Duration**: 35s
**Priority**: High
**Category**: relationships

---

## Group 6: Metrics & Logging (Tests 18-20)

### Test 18: Daily Metrics Counting

**Objective**: Verify that daily metrics (storylet completion, encounter counts) are tracked correctly.

**Preconditions**:
- 10 NPCs running for 3 days
- Encounter probabilities: venue 0.07, street 0.015, workplace 0.04
- Measure storylet completion and encounter counts

**Expected Outcome**:
- Daily metrics recorded: day, encounters_total, storylets_completed
- Metrics per NPC: encounters_this_day, storylets_completed_this_day
- Counts are accurate (verified by event log)
- Counts reset to 0 at day boundary
- Day 1 metrics independent of day 2 metrics

**Test Steps**:
1. Create 10 NPCs
2. Expect 10 `npc_created` events
3. Run simulation for 3 days (advance 30s per day)
4. Collect `node_arrival` and `encounter` events
5. At day 1 boundary, expect `day_summary` with counts
6. Manually count from event log for day 1
7. Compare with `day_summary` metrics (should match)
8. Repeat for days 2 and 3
9. Verify counts reset between days
10. Action: quit, result: pass

**Duration**: 35s
**Priority**: Medium
**Category**: metrics

---

### Test 19: JSONL Event Logging

**Objective**: Verify that all events are logged to JSONL format with complete and valid fields.

**Preconditions**:
- 5 NPCs
- Run simulation for 2 days
- Capture JSONL event log

**Expected Outcome**:
- All events logged in valid JSONL format (one event per line)
- Each event has fields: t (timestamp), day, npc (or -1 for global), evt (event type)
- Event-specific fields present (e.g., encounter has `encounter_type`)
- All timestamps are valid (ISO 8601 format)
- Timestamps are monotonically increasing
- No malformed JSON in log

**Test Steps**:
1. Create 5 NPCs
2. Run simulation for 2 days
3. Capture JSONL event log to file
4. Parse JSONL: foreach line, parse as JSON
5. Verify each line is valid JSON
6. Verify fields present: t, day, npc, evt
7. Verify 50+ events logged (expected minimum)
8. Spot check 10 random events for field completeness
9. Verify timestamps in ascending order
10. Action: quit, result: pass

**Duration**: 40s
**Priority**: High
**Category**: metrics

---

### Test 20: Group Detection and Clique Formation

**Objective**: Verify that GroupDetector identifies cliques and social groups after 30-day simulation.

**Preconditions**:
- 100 NPCs
- Run for 30 days (full simulation)
- Measure relationship graph and group formation

**Expected Outcome**:
- GroupDetector identifies cliques (groups of 3+ NPCs with trust > 0.7 among all members)
- At least 1 group formed (likely 5-15 groups total)
- Groups have consistent membership (no fluctuation within simulation)
- Group information logged at day 30
- `day_summary` for day 30 includes group detection results

**Test Steps**:
1. Create 100 NPCs
2. Expect 100 `npc_created` events
3. Run full 30-day simulation
4. Collect all `relationship_changed` events
5. Build relationship graph (trust values between NPCs)
6. At day 30 boundary, expect `day_summary` with group data
7. Verify group count > 0
8. Verify each group has 3+ members
9. Verify group members have high mutual trust (0.7+)
10. Verify no cliques overlap (disjoint sets)
11. Action: quit, result: pass

**Duration**: 90s
**Priority**: Medium
**Category**: metrics

---

## Format Summary

All Phase 0 test scripts follow this JSON structure:

```json
{
  "name": "test-name",
  "description": "One-line description of what this validates",
  "phase": "phase-0",
  "category": "initialization|scheduling|properties|encounters|relationships|metrics",
  "priority": "critical|high|medium",
  "globalTimeout": <seconds>,
  "preconditions": "Initial state description",
  "expectedOutcome": "What should happen",
  "steps": [
    {"expect": {"type": "event.type", "code": "optional"}, "timeout": 30, "comment": "..."},
    {"sleep": 1000, "comment": "..."},
    {"action": "quit", "result": "pass"}
  ]
}
```

---

## Execution

### Run Single Test
```bash
JOYCE_TEST_SCRIPT=models/tests/tale/phase0-des/01-initialization.json \
  dotnet run --project nogame/nogame.csproj
```

### Run All Phase 0 Tests
```bash
for script in models/tests/tale/phase0-des/*.json; do
  echo "Running: $(basename $script)"
  JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj
  if [ $? -ne 0 ]; then
    echo "FAILED: $script"
    exit 1
  fi
done
echo "✓ All 20 Phase 0 tests passed!"
```

---

## Test Priority Summary

| Priority | Tests | Count | Rationale |
|----------|-------|-------|-----------|
| Critical | 01, 02, 04, 05, 11, 14 | 6 | Initialization, event queue, encounters must work |
| High | 03, 06, 07, 08, 09, 10, 12, 13, 19 | 9 | Core DES functionality, properties, metrics |
| Medium | 18, 20 | 2 | Post-simulation analysis, advanced features |

---

## Events Validated

- `npc_created` — NPC initialization
- `node_arrival` — Location arrival and storylet completion
- `encounter` — Two+ NPCs meet at location
- `relationship_changed` — Trust value update
- `day_summary` — Daily metrics and cleanup
- `request_emitted` (Phase 3 prep) — Not in Phase 0, but logged for reference

---

## Next Steps

1. Implement all 20 JSON test scripts based on this specification
2. Run test scripts against Testbed
3. Verify all tests pass
4. Move to Phase 1 (Storylet System) tests

---

**Phase 0 Complete When**: All 20 tests pass and event counts match expected ranges.
