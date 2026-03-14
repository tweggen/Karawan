# TALE Phase 0 Test Scripts — DES Engine (SKELETON)

## Overview

Phase 0 validates the **discrete event simulation engine**, the foundation for all TALE phases.

**Test Categories**: 20 tests organized into 5 groups
- Initialization & Setup (3 tests)
- Event Queue & Scheduling (4 tests)
- Property Dynamics (3 tests)
- Encounter Detection (4 tests)
- Relationship Tracking & Metrics (3 tests) + Logging (3 tests)

**Script Location**: `models/tests/tale/phase0-des/`

---

## Test Script Outline

### Group 1: Initialization & Setup (3 tests)

| # | Script | Validates | Duration | Priority |
|---|--------|-----------|----------|----------|
| 01 | `01-initialization.json` | DesSimulation.Initialize() creates queue, metrics, encounters | 30s | Critical |
| 02 | `02-npc-creation.json` | NPCs register, get initial wakeup event, properties initialized | 30s | Critical |
| 03 | `03-spatial-model.json` | SpatialModel loads locations, calculates travel times | 30s | High |

**Implementation notes**:
- Test 01: Verify _queue, _encounters, _metrics, _relationships created
- Test 02: Verify NpcSchedule.CurrentEnd set to wakeup time, NPC_CREATED event logged
- Test 03: Verify location positions, GetTravelTime() returns valid values

---

### Group 2: Event Queue & Scheduling (4 tests)

| # | Script | Validates | Duration | Priority |
|---|--------|-----------|----------|----------|
| 04 | `04-event-queue-order.json` | Events process in time order (not insertion order) | 30s | Critical |
| 05 | `05-node-arrival-event.json` | NodeArrival triggers location change, postconditions, next event | 30s | Critical |
| 06 | `06-day-boundary.json` | Day boundary fires at midnight, cleans up presence, resets metrics | 35s | High |
| 07 | `07-long-simulation.json` | 30-day run completes without deadlock/memory leak | 90s | High |

**Implementation notes**:
- Test 04: Emit events out of order, verify processing order by timestamp
- Test 05: Track NPC location change, property deltas, next event scheduled
- Test 06: Verify DayEnd events, ClearDailyDedup() called, metrics reset
- Test 07: Run full 30-day simulation, track event count, memory usage

---

### Group 3: Property Dynamics (3 tests)

| # | Script | Validates | Duration | Priority |
|---|--------|-----------|----------|----------|
| 08 | `08-postcondition-application.json` | Postconditions update NPC properties (e.g., "wealth: +0.05") | 30s | High |
| 09 | `09-desperation-computation.json` | Desperation drifts daily based on wealth/hunger | 35s | High |
| 10 | `10-morality-drift.json` | Morality drifts daily based on desperation (down when desperate) | 35s | High |

**Implementation notes**:
- Test 08: Apply postconditions, verify properties changed atomically, clamped [0,1]
- Test 09: Set wealth/hunger, compute desperation over 5 days, verify drift direction
- Test 10: Set desperation, verify morality decrease; low desperation → slight recovery

---

### Group 4: Encounter Detection (4 tests)

| # | Script | Validates | Duration | Priority |
|---|--------|-----------|----------|----------|
| 11 | `11-encounter-at-venue.json` | Two NPCs at same location trigger encounter | 35s | Critical |
| 12 | `12-encounter-avoidance.json` | NPCs at different locations don't encounter | 30s | High |
| 13 | `13-multiple-encounters.json` | Three+ NPCs at same location generate all pairs | 40s | High |
| 14 | `14-encounter-trust-update.json` | Encounter changes trust values, updates RelationshipTracker | 35s | Critical |

**Implementation notes**:
- Test 11: Place 2 NPCs in same location during same time window, verify encounter event
- Test 12: Place 2 NPCs in different locations, verify NO encounter
- Test 13: Place 3 NPCs at same location, verify 3 pair encounters (A-B, A-C, B-C)
- Test 14: Verify ENCOUNTER event logged with trust before/after

---

### Group 5: Relationship Tracking (3 tests)

| # | Script | Validates | Duration | Priority |
|---|--------|-----------|----------|----------|
| 15 | `15-trust-tier-friendly.json` | Positive interactions increase trust, trigger "friendly" tier | 35s | High |
| 16 | `16-trust-tier-hostile.json` | Negative interactions decrease trust, trigger "hostile" tier | 35s | High |
| 17 | `17-relationship-persistence.json` | Trust values persist across day boundaries | 35s | High |

**Implementation notes**:
- Test 15: Track trust values across encounters, verify tier transitions (stranger → acquaintance → friendly)
- Test 16: Verify negative interactions (determined by trust + anger/morality) decrease trust
- Test 17: Set trust on day 1, verify unchanged on day 2 (persistence)

---

### Group 6: Metrics & Logging (3 tests)

| # | Script | Validates | Duration | Priority |
|---|--------|-----------|----------|----------|
| 18 | `18-metrics-daily-count.json` | Daily storylet and encounter counts tracked correctly | 35s | Medium |
| 19 | `19-jsonl-event-logging.json` | All events logged to JSONL (npc_created, node_arrival, encounter, etc.) | 40s | High |
| 20 | `20-group-detection.json` | GroupDetector identifies cliques every 30 days | 90s | Medium |

**Implementation notes**:
- Test 18: Count completed storylets, encounters per NPC per day; verify metrics
- Test 19: Parse JSONL output, verify event sequence, timestamps, field completeness
- Test 20: Run 30-day simulation, verify group detection on day 30, output cliques

---

## Test Metadata Template

```json
{
  "name": "test-name",
  "description": "What this test validates",
  "phase": "phase-0",
  "category": "initialization|scheduling|properties|encounters|relationships|metrics",
  "priority": "critical|high|medium",
  "globalTimeout": 60,
  "dependencies": [],
  "preconditions": "Initial state needed",
  "expectedOutcome": "What should happen",
  "steps": [
    { "expect": {"type": "event.type", "code": "optional"}, "timeout": 30, "comment": "..." },
    { "sleep": 1000, "comment": "..." },
    { "action": "quit", "result": "pass" }
  ]
}
```

---

## Implementation Status

- [ ] Test scripts created (20 JSON files)
- [ ] Tests run and passing
- [ ] Code coverage > 85% for DesSimulation, EventQueue, RelationshipTracker
- [ ] Documentation updated

---

## Next Phase

Once Phase 0 tests pass, proceed to Phase 1 (Storylet System).
