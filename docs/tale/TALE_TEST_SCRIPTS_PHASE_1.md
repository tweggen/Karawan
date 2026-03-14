# Phase 1 Detailed Test Scripts — Storylet System

## Overview

Phase 1 validates the **Storylet System**, the JSON-driven story selection engine. This document provides detailed specifications for 20 test scripts organized into 5 categories.

**Test Categories**:
1. Library Loading (3 tests)
2. Precondition Matching (4 tests)
3. Selection & Duration (3 tests)
4. Postconditions (3 tests)
5. Complex Scenarios (4 tests)

---

## Group 1: Library Loading (Tests 01-03)

### Test 01: Storylet Library Loading

**Objective**: Verify that StoryletLibrary.LoadFromDirectory() correctly loads all .json files from directory.

**Preconditions**:
- Directory: models/tale/
- Contains: 6 role-specific JSON files (merchant.json, worker.json, drifter.json, socialite.json, authority.json, universal.json, desperation.json)
- Each file contains 3-10 storylets

**Expected Outcome**:
- All JSON files loaded into library
- Total storylet count: 40+ storylets
- No duplicate IDs across files
- All storylets accessible via GetById() or GetCandidates()
- Load completes without exceptions

**Test Steps**:
1. Inject library load request
2. Expect library initialization
3. Sleep 100ms (allow loading)
4. Query library for total count
5. Verify count ≥ 40
6. Query specific storylet by ID (e.g., "work_manual")
7. Verify storylet found with correct role, duration, postconditions
8. Action: quit, result: pass

**Duration**: 30s
**Priority**: Critical
**Category**: library

---

### Test 02: Library Indexing and Role-Based Retrieval

**Objective**: Verify that GetCandidates() returns role-specific and universal storylets correctly.

**Preconditions**:
- Library loaded with storylets for all roles
- Test NPC with role="merchant"

**Expected Outcome**:
- GetCandidates(NPC) returns merchant-specific storylets
- GetCandidates(NPC) includes universal storylets
- Non-matching roles excluded from candidates
- Universal storylets in every role's candidate list
- Candidates indexed and searchable

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create test NPC with role="merchant"
4. Query GetCandidates(NPC)
5. Verify candidate list includes "open_shop" (merchant-specific)
6. Verify candidate list includes "sleep" (universal)
7. Verify candidate list does NOT include "beg" (drifter-specific)
8. Verify candidate count ≥ 5
9. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: library

---

### Test 03: Fallback Storylet Selection

**Objective**: Verify that GetFallback() returns rest/wander at appropriate times.

**Preconditions**:
- Library loaded
- Test NPC with no matching candidates (e.g., extreme hunger, fatigue)
- Time: 14:00 (day) and 02:00 (night)

**Expected Outcome**:
- When no candidates available, GetFallback() returns safe fallback
- Night (20:00-06:00): fallback is "sleep"
- Day (08:00-20:00): fallback is "wander" or "rest"
- Fallback always available (never null)
- Fallback is from universal storylets

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create test NPC
4. Clear candidate list (simulate no matches)
5. Query GetFallback(time=02:00)
6. Verify fallback is "sleep"
7. Query GetFallback(time=14:00)
8. Verify fallback is "wander" or "rest"
9. Verify fallback not null
10. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: library

---

## Group 2: Precondition Matching (Tests 04-07)

### Test 04: Property Precondition Matching

**Objective**: Verify that preconditions with property ranges match correctly.

**Preconditions**:
- Storylet "eat_at_home": preconditions { hunger: { min: 0.5 } }
- Test NPC with hunger=0.8 (matches) and hunger=0.3 (no match)

**Expected Outcome**:
- NPC with hunger=0.8 matches precondition
- NPC with hunger=0.3 does NOT match precondition
- Precondition evaluation returns boolean (match/no match)
- Range boundaries inclusive: hunger >= 0.5 matches 0.5 exactly

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create NPC with hunger=0.8
4. Query GetCandidates(NPC)
5. Verify "eat_at_home" in candidates
6. Create second NPC with hunger=0.3
7. Query GetCandidates(NPC2)
8. Verify "eat_at_home" NOT in candidates
9. Action: quit, result: pass

**Duration**: 30s
**Priority**: Critical
**Category**: preconditions

---

### Test 05: Property Mismatch and Rejection

**Objective**: Verify that storylets are skipped when preconditions fail.

**Preconditions**:
- Storylet "work_office": preconditions { fatigue: { max: 0.85 } }
- Test NPC with fatigue=0.9 (doesn't match)

**Expected Outcome**:
- NPC with fatigue=0.9 fails precondition check
- "work_office" excluded from candidates
- NPC can still select other storylets without fatigue precondition
- Rejection is silent (no error, just skip)

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create NPC with fatigue=0.9
4. Query GetCandidates(NPC)
5. Verify "work_office" NOT in candidates (precondition failed)
6. Verify other non-fatigue storylets still available
7. Verify candidate list not empty
8. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: preconditions

---

### Test 06: Time-of-Day Matching

**Objective**: Verify that storylets respect time_of_day windows.

**Preconditions**:
- Storylet "work_manual": time_of_day { min: "07:00", max: "17:00" }
- Test at 09:00 (matches) and 20:00 (no match)

**Expected Outcome**:
- At 09:00, "work_manual" is in candidates
- At 20:00, "work_manual" NOT in candidates
- Time window boundaries inclusive
- Time ranges evaluated against current time

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Set simulation time to 09:00
4. Create worker NPC
5. Query GetCandidates(NPC)
6. Verify "work_manual" in candidates
7. Set simulation time to 20:00
8. Query GetCandidates(NPC)
9. Verify "work_manual" NOT in candidates
10. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: preconditions

---

### Test 07: Role-Based Filtering

**Objective**: Verify that non-matching roles are excluded from candidates.

**Preconditions**:
- Storylet "beg": roles ["drifter"]
- Test NPCs with roles: "drifter", "worker", "merchant"

**Expected Outcome**:
- Drifter NPC: "beg" in candidates
- Worker NPC: "beg" NOT in candidates
- Merchant NPC: "beg" NOT in candidates
- Role filtering applied before returning candidates
- Empty role array [] = universal (all roles)

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create drifter NPC
4. Query GetCandidates(drifter)
5. Verify "beg" in candidates
6. Create worker NPC
7. Query GetCandidates(worker)
8. Verify "beg" NOT in candidates
9. Verify universal storylets still available for worker
10. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: preconditions

---

## Group 3: Selection & Duration (Tests 08-10)

### Test 08: Weighted Selection

**Objective**: Verify that higher weight increases selection probability.

**Preconditions**:
- 2 similar storylets with different weights:
  - "rest" with weight 1.0
  - "wander" with weight 0.3
- Test NPC with 100 selections

**Expected Outcome**:
- "rest" selected approximately 77% of the time (1.0 / 1.3)
- "wander" selected approximately 23% of the time (0.3 / 1.3)
- Statistical distribution matches weight ratios (±10% variance acceptable)
- Higher weight = higher probability

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create test NPC with matching preconditions for both
4. Select storylet 100 times
5. Count occurrences of "rest" and "wander"
6. Verify "rest" count ≈ 77 (±10)
7. Verify "wander" count ≈ 23 (±10)
8. Action: quit, result: pass

**Duration**: 35s
**Priority**: High
**Category**: selection

---

### Test 09: Duration Randomness

**Objective**: Verify that storylet duration varies within specified min/max range.

**Preconditions**:
- Storylet with duration_minutes_min=240, duration_minutes_max=300
- Select storylet 20 times, record duration each time

**Expected Outcome**:
- All durations in range [240, 300]
- Min duration ≥ 240
- Max duration ≤ 300
- Not all durations identical (randomness present)
- Duration follows uniform or normal distribution

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create test NPC
4. Select "work_manual" storylet 20 times
5. Record duration for each
6. Verify all durations in [240, 300]
7. Verify min_duration ≥ 240
8. Verify max_duration ≤ 300
9. Verify durations vary (std dev > 0)
10. Action: quit, result: pass

**Duration**: 35s
**Priority**: High
**Category**: selection

---

### Test 10: Location Resolution

**Objective**: Verify that location strings resolve to actual location IDs.

**Preconditions**:
- Storylet "go_to_workplace": location: "workplace"
- Test NPC with WorkplaceLocationId=25

**Expected Outcome**:
- Location string "workplace" resolves to NPC.WorkplaceLocationId
- Resolved location ID is valid (≥ 0, < total locations)
- Location resolution happens before execution
- Special locations resolved: "current", "home", "workplace", "random_street", "nearest_shop_*"

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create test NPC with WorkplaceLocationId=25
4. Query storylet preconditions/execution for "go_to_workplace"
5. Verify location resolves to 25
6. Create storylet with location="random_street"
7. Verify location resolved to valid street location
8. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: selection

---

## Group 4: Postconditions (Tests 11-13)

### Test 11: Simple Postcondition Application

**Objective**: Verify that simple postconditions ("wealth: +0.05") update properties correctly.

**Preconditions**:
- Storylet with postconditions { "wealth": "+0.05" }
- NPC with initial wealth=0.50

**Expected Outcome**:
- After storylet execution, wealth=0.55
- Property updated atomically (not read-modify-write race condition)
- Postcondition string parsed correctly ("+" operation recognized)
- Numeric value extracted and applied

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create NPC with wealth=0.50
4. Select and execute storylet with wealth postcondition
5. Verify wealth updated to 0.55
6. Create NPC with fatigue=0.30
7. Select storylet with fatigue postcondition "fatigue: +0.15"
8. Verify fatigue updated to 0.45
9. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: postconditions

---

### Test 12: Multiple Postcondition Application

**Objective**: Verify that all postconditions are applied atomically.

**Preconditions**:
- Storylet with postconditions { "wealth": "+0.05", "fatigue": "+0.1", "hunger": "-0.3" }
- NPC with initial: wealth=0.50, fatigue=0.30, hunger=0.80

**Expected Outcome**:
- All 3 postconditions applied in order (atomic transaction)
- Final state: wealth=0.55, fatigue=0.40, hunger=0.50
- Intermediate states not visible (all-or-nothing)
- No partial application if one fails

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create NPC with specified initial properties
4. Select and execute storylet with multiple postconditions
5. Verify all properties updated correctly
6. Verify no partial application occurred
7. Verify update order matches postcondition order
8. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: postconditions

---

### Test 13: Property Clamping in Postconditions

**Objective**: Verify that property values are clamped to [0, 1] after postcondition application.

**Preconditions**:
- Postcondition: "wealth: +0.2" on NPC with wealth=0.9 (would overflow to 1.1)
- Postcondition: "hunger: -0.3" on NPC with hunger=0.2 (would underflow to -0.1)

**Expected Outcome**:
- NPC wealth clamped to 1.0 (not 1.1)
- NPC hunger clamped to 0.0 (not -0.1)
- Clamping applied after arithmetic
- Values always in [0, 1] after any postcondition

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create NPC with wealth=0.9
4. Execute storylet with "wealth: +0.2" postcondition
5. Verify wealth=1.0 (clamped, not 1.1)
6. Create NPC with hunger=0.2
7. Execute storylet with "hunger: -0.3" postcondition
8. Verify hunger=0.0 (clamped, not -0.1)
9. Action: quit, result: pass

**Duration**: 30s
**Priority**: High
**Category**: postconditions

---

## Group 5: Complex Scenarios (Tests 14-20)

### Test 14: Desperation-Based Gating

**Objective**: Verify that crime storylets only available to desperate NPCs (desperation > 0.4).

**Preconditions**:
- Storylet "attempt_pickpocket": available only if desperation > 0.4
- NPC 1: desperation=0.2 (not desperate)
- NPC 2: desperation=0.8 (desperate)

**Expected Outcome**:
- NPC 1 cannot access "attempt_pickpocket" (desperation too low)
- NPC 2 can access "attempt_pickpocket" (desperation high enough)
- Gating checked as precondition (desperation_min: 0.4 or similar)
- Access control based on computed desperation value

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create NPC with desperation=0.2
4. Query GetCandidates(NPC)
5. Verify "attempt_pickpocket" NOT in candidates
6. Create NPC with desperation=0.8
7. Query GetCandidates(NPC)
8. Verify "attempt_pickpocket" in candidates
9. Action: quit, result: pass

**Duration**: 30s
**Priority**: Medium
**Category**: complex

---

### Test 15: Nearest Venue Location Resolution

**Objective**: Verify that location: "nearest_shop_Eat" resolves to closest social_venue.

**Preconditions**:
- SpatialModel with multiple social_venues at different distances
- NPC current location at position <0, 0>
- Nearest social_venue at distance 100m, farthest at 500m

**Expected Outcome**:
- "nearest_shop_Eat" resolved to social_venue at 100m (closest)
- Not to farther venues
- Resolution uses spatial proximity calculation
- Location lookup returns valid venue ID

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create NPC at known position
4. Query storylet with location: "nearest_shop_Eat"
5. Verify resolved location is closest social_venue
6. Verify distance calculation accurate
7. Action: quit, result: pass

**Duration**: 30s
**Priority**: Medium
**Category**: complex

---

### Test 16: Role-Specific Weight Priority

**Objective**: Verify that role-specific storylet weight > universal weight in candidate selection.

**Preconditions**:
- Universal storylet "wander" with weight=1.0
- Role-specific storylet "work_manual" (merchant) with weight=2.0
- Test merchant NPC with both available

**Expected Outcome**:
- "work_manual" selected more frequently than "wander"
- Role-specific weight takes precedence
- Selection probability reflects combined weights
- Statistical validation: work_manual ~66%, wander ~33%

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create merchant NPC
4. Select 100 times from candidates
5. Count "work_manual" and "wander" occurrences
6. Verify "work_manual" count ≈ 66 (±10)
7. Verify "wander" count ≈ 33 (±10)
8. Action: quit, result: pass

**Duration**: 35s
**Priority**: Medium
**Category**: complex

---

### Test 17: Universal Fallback for All Roles

**Objective**: Verify that universal storylets available to all roles when role-specific empty.

**Preconditions**:
- NPC with role="worker"
- All worker-specific storylets have failed preconditions
- Universal storylets ("rest", "wander") always available

**Expected Outcome**:
- When role-specific candidates empty, universal candidates returned
- Worker can select "rest" or "wander" as fallback
- No dead-lock (candidates never completely empty)
- Fallback mechanism prevents story stall

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create NPC with properties that fail all worker-specific preconditions
4. Query GetCandidates(NPC)
5. Verify role-specific candidates empty or minimal
6. Verify universal candidates returned ("rest", "wander", "sleep")
7. Verify candidate list not empty
8. Action: quit, result: pass

**Duration**: 30s
**Priority**: Medium
**Category**: complex

---

### Test 18: Combined Candidate List (Role-Specific + Universal)

**Objective**: Verify that GetCandidates() merges role-specific and universal storylets correctly.

**Preconditions**:
- Test merchant NPC
- Merchant-specific: "open_shop", "serve_customers", "close_shop" (3 storylets)
- Universal: "wander", "rest", "sleep", "eat_at_home" (4 storylets)

**Expected Outcome**:
- Combined candidate list has 7 storylets (3 + 4)
- No duplicates (if overlap, counted once)
- All storylets accessible via GetCandidates()
- List properly indexed for selection

**Test Steps**:
1. Inject library load
2. Sleep 100ms
3. Create merchant NPC
4. Query GetCandidates(NPC)
5. Verify count = 7 (no duplicates)
6. Verify merchant-specific storylets present
7. Verify universal storylets present
8. Verify no duplicate IDs in list
9. Action: quit, result: pass

**Duration**: 30s
**Priority**: Medium
**Category**: complex

---

### Test 19: JSON Parse Error Handling

**Objective**: Verify that invalid JSON files are handled gracefully (logged, skipped).

**Preconditions**:
- Directory with valid JSON files
- One file has invalid JSON syntax: "{ incomplete json"

**Expected Outcome**:
- Invalid file logged as error (but doesn't crash)
- Valid files still loaded
- Library partially populated (skipping bad file)
- No exception thrown to caller

**Test Steps**:
1. Inject library load from directory with mixed valid/invalid JSON
2. Verify load completes without exception
3. Verify valid storylets loaded
4. Verify invalid file skipped (not in candidate list)
5. Verify error logged (can be checked in logs)
6. Action: quit, result: pass

**Duration**: 30s
**Priority**: Low
**Category**: complex

---

### Test 20: Empty Library Behavior

**Objective**: Verify that empty library returns empty candidates but uses fallbacks.

**Preconditions**:
- Empty directory (no JSON files)
- Library load on empty directory

**Expected Outcome**:
- Library initialized but empty (0 storylets)
- GetCandidates() returns empty list
- GetFallback() still returns valid fallback ("sleep", "rest", "wander")
- No crash, graceful degradation

**Test Steps**:
1. Attempt to load library from empty directory
2. Verify library initializes
3. Query GetCandidates(NPC)
4. Verify candidates empty
5. Query GetFallback(NPC, time)
6. Verify fallback returned (not null)
7. Verify system doesn't crash
8. Action: quit, result: pass

**Duration**: 30s
**Priority**: Low
**Category**: complex

---

## Format Summary

All Phase 1 test scripts follow this JSON structure:

```json
{
  "name": "test-name",
  "description": "One-line description of what this validates",
  "phase": "phase-1",
  "category": "library|preconditions|selection|postconditions|complex",
  "priority": "critical|high|medium|low",
  "globalTimeout": <seconds>,
  "preconditions": "Initial state description",
  "expectedOutcome": "What should happen",
  "steps": [
    {"expect": {"type": "event.type"}, "timeout": 30, "comment": "..."},
    {"sleep": 1000, "comment": "..."},
    {"action": "quit", "result": "pass"}
  ]
}
```

---

## Execution

### Run Single Test
```bash
JOYCE_TEST_SCRIPT=models/tests/tale/phase1-storylets/01-library-loading.json \
  dotnet run --project nogame/nogame.csproj
```

### Run All Phase 1 Tests
```bash
for script in models/tests/tale/phase1-storylets/*.json; do
  echo "Running: $(basename $script)"
  JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj
  if [ $? -ne 0 ]; then
    echo "FAILED: $script"
    exit 1
  fi
done
echo "✓ All 20 Phase 1 tests passed!"
```

---

## Test Priority Summary

| Priority | Tests | Count | Rationale |
|----------|-------|-------|-----------|
| Critical | 01, 04 | 2 | Library loading and precondition matching are core |
| High | 02, 03, 05, 06, 07, 08, 09, 10, 11, 12, 13 | 11 | Storylet selection and postconditions essential |
| Medium | 14, 15, 16, 17, 18 | 5 | Complex scenarios and edge cases |
| Low | 19, 20 | 2 | Error handling and graceful degradation |

---

## Events Validated

- `node_arrival` — Storylet selected and executed
- `day_summary` — Daily metrics include storylet completion counts
- `encounter` — (Phase 2+) Storylet selection during encounters

---

## Next Steps

1. Implement all 20 JSON test scripts based on this specification
2. Run test scripts against Testbed
3. Verify all tests pass
4. Move to Phase 2 (Strategy System) tests

---

**Phase 1 Complete When**: All 20 tests pass and storylet selection metrics match expected distributions.
