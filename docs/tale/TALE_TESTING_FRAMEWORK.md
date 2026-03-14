# TALE Testing Framework — Complete Reference

## Overview

A comprehensive testing strategy for the **TALE narrative engine** using the **ExpectEngine** framework. This document provides navigation and summary of all testing documents.

**Total Coverage**: 120+ JSON test scripts across 6 implementation phases
**Test Framework**: ExpectEngine (lock-free event channels, ~100 LOC per test)
**Execution**: JSON scripts in `models/tests/tale/phaseX-*/`, run via `JOYCE_TEST_SCRIPT=...`
**Success Criteria**: All tests pass, coverage > 80%, metrics validate (fulfillment ≥85%, etc.)

---

## Documents in This Series

### 1. `TALE_TEST_PLAN.md` — Master Strategy
**Read this first**. Contains:
- 120+ test overview across all 6 phases
- Organization by feature category
- Test metadata format
- Cross-phase integration strategy
- CI roadmap

**Key section**: "Test Execution Strategy" — shows how to validate each phase.

---

### 2. `TALE_TEST_SCRIPTS_PHASE_3.md` — Detailed Phase 3 Specs
**Read this for Phase 3 implementation** (the phase just completed). Contains:
- 22 detailed test script specifications (not just outline)
- Full JSON structure for each test
- Preconditions, steps, expected outcomes
- Example: `07-claim-during-encounter.json` shows full spec with all fields

**Key sections**:
- Request Emission Tests (3 scripts)
- Request Pool Lifecycle (3 scripts)
- Request Claiming (4 scripts)
- Signal Emission (3 scripts)
- Tier 3 Abstract Resolution (4 scripts)
- Event Integration & Metrics (3 scripts)
- Advanced Scenarios (2 scripts)

**Action items**: Implement 22 JSON files from these specs.

---

### 3. `TALE_TEST_SCRIPTS_PHASE_0_SKELETON.md` — Phase 0 Outline
**Reference for DES engine testing**. Contains:
- 20 test script **outlines** (not detailed like Phase 3)
- Organized into 6 categories
- Table of test # / name / validator / duration / priority
- Implementation notes (what to verify in code)
- Test metadata template

**Key categories**:
1. Initialization (3)
2. Event Queue & Scheduling (4)
3. Property Dynamics (3)
4. Encounter Detection (4)
5. Relationship Tracking (3)
6. Metrics & Logging (3)

**Next action**: Expand outlines to detailed specs (like Phase 3), implement JSON files.

---

### 4. `TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md` — Phases 1, 2, 4, 5 Outline
**Reference for remaining phases**. Contains:
- Phase 1 (Storylets): 20 tests in 5 categories
- Phase 2 (Strategies): 20 tests in 5 categories
- Phase 4 (Player Integration): 20 tests in 6 categories
- Phase 5 (Escalation): 20 tests in 6 categories

**Same structure as Phase 0**: outlines with tables, implementation notes, no JSON detail.

**Next action**: Pick a phase (likely Phase 1), expand to detailed specs like Phase 3, implement.

---

## Quick Navigation

### By Phase
```
Phase 0: DES Engine
  → See: TALE_TEST_SCRIPTS_PHASE_0_SKELETON.md
  → Status: Outline only
  → Next: Expand to detail, implement

Phase 1: Storylets
  → See: TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md
  → Status: Outline only
  → Next: Expand to detail, implement

Phase 2: Strategies
  → See: TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md
  → Status: Outline only
  → Next: Expand to detail, implement

Phase 3: Interactions (JUST IMPLEMENTED)
  → See: TALE_TEST_SCRIPTS_PHASE_3.md
  → Status: DETAILED (22 specs, ready to implement)
  → Next: Create 22 JSON files in models/tests/tale/phase3-interactions/

Phase 4: Player Integration
  → See: TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md
  → Status: Outline only
  → Next: Expand to detail, implement

Phase 5: Escalation
  → See: TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md
  → Status: Outline only
  → Next: Expand to detail, implement
```

### By Purpose
```
If you want to...

→ Understand overall test strategy:
  Read TALE_TEST_PLAN.md "Test Execution Strategy"

→ Implement Phase 3 tests (highest priority):
  Read TALE_TEST_SCRIPTS_PHASE_3.md
  Create 22 JSON files in models/tests/tale/phase3-interactions/
  Run: for script in models/tests/tale/phase3-interactions/*.json; do JOYCE_TEST_SCRIPT=$script dotnet run ...; done

→ Plan Phase 0 tests (foundation):
  Read TALE_TEST_SCRIPTS_PHASE_0_SKELETON.md
  Expand outlines to detailed specs (copy Phase 3 structure)
  Create 20 JSON files

→ Plan Phase 1/2/4/5 tests:
  Read TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md
  Pick a phase, expand outlines to detailed specs
  Create 20+ JSON files

→ Add test to CI/CD:
  Read TALE_TEST_PLAN.md "CI Integration"
  Create GitHub Actions workflow
  Run all 120+ tests on push
```

---

## Test File Structure

```
models/tests/tale/
├── README.md (this file points here)
├── phase0-des/
│   ├── 01-initialization.json
│   ├── 02-npc-creation.json
│   ├── ...
│   └── 20-group-detection.json        [20 files total]
│
├── phase1-storylets/
│   ├── 01-library-loading.json
│   ├── 02-library-indexing.json
│   ├── ...
│   └── 20-combined-candidates.json    [20 files total]
│
├── phase2-strategies/
│   ├── 01-strategy-creation.json
│   ├── ...
│   └── 20-strategy-failure-recovery.json [20 files total]
│
├── phase3-interactions/
│   ├── 01-request-postcondition-emission.json
│   ├── 02-multiple-requests-from-different-npcs.json
│   ├── ...
│   └── 22-request-timeout-before-claim.json   [22 files total]
│
├── phase4-player/
│   ├── 01-player-quest-trigger.json
│   ├── ...
│   └── 20-npc-dialogue-integration.json       [20 files total]
│
└── phase5-escalation/
    ├── 01-crime-detection.json
    ├── ...
    └── 20-wave-narrative-integration.json     [20 files total]
```

---

## Execution Examples

### Run Single Test
```bash
JOYCE_TEST_SCRIPT=models/tests/tale/phase3-interactions/01-request-postcondition-emission.json \
  dotnet run --project nogame/nogame.csproj
```

### Run All Phase 3 Tests
```bash
cd /Users/tweggen/coding/github/Karawan
for script in models/tests/tale/phase3-interactions/*.json; do
  echo "Running: $(basename $script)"
  JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj
  if [ $? -ne 0 ]; then
    echo "FAILED: $script"
    exit 1
  fi
done
echo "✓ All Phase 3 tests passed!"
```

### Run Phase 0 + Phase 3 (Sequential Phases)
```bash
for phase in phase0-des phase3-interactions; do
  for script in models/tests/tale/$phase/*.json; do
    JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj || exit 1
  done
done
```

### Integration Test: 30-Day Simulation with Metrics
```bash
# After individual tests pass, run full integration
dotnet run --project Testbed -- --days 30 --events-file events.jsonl \
  --expect-fulfillment-rate 0.85 \
  --expect-mean-interrupts 1.5
```

---

## Test Metadata Format

Every JSON test script includes:
```json
{
  "name": "descriptive-test-name",
  "description": "Detailed description of what this validates",
  "phase": "phase-N",
  "category": "feature-category",
  "priority": "critical|high|medium|low",
  "globalTimeout": 60,
  "dependencies": ["other-test-id"],
  "preconditions": "Required initial state",
  "expectedOutcome": "What should happen",
  "steps": [
    {
      "expect": { "type": "event.type", "code": "optional.code" },
      "timeout": 30,
      "comment": "Description"
    },
    {
      "inject": { "type": "event.type", "code": "value" },
      "comment": "Send event to engine"
    },
    {
      "sleep": 1000,
      "comment": "Wait 1s"
    },
    {
      "action": "quit",
      "result": "pass"
    }
  ]
}
```

---

## Implementation Roadmap

### Immediate (Next)
- [ ] Implement 22 Phase 3 JSON test scripts (TALE_TEST_SCRIPTS_PHASE_3.md)
- [ ] Run Phase 3 tests, validate interaction pool functionality
- [ ] Update memory with test framework structure

### Short Term (1-2 weeks)
- [ ] Expand Phase 0 skeleton → detailed specs
- [ ] Implement 20 Phase 0 JSON test scripts
- [ ] Validate DES engine through tests
- [ ] Build GitHub Actions CI workflow

### Medium Term (Ongoing)
- [ ] Expand Phase 1 skeleton → detailed specs, implement
- [ ] Expand Phase 2 skeleton → detailed specs, implement
- [ ] Run integration tests (Phase 0 + Phase 1 + Phase 2)

### Long Term
- [ ] Expand Phase 4 skeleton → detailed specs, implement
- [ ] Expand Phase 5 skeleton → detailed specs, implement
- [ ] Full suite (120+ tests) passing, documented

---

## Success Metrics

| Metric | Target | Method |
|--------|--------|--------|
| Test Count | 120+ | Count JSON files |
| Coverage | >80% | Code coverage tool |
| Phase 0 Pass Rate | 100% | Run all 20 tests |
| Phase 1 Pass Rate | 100% | Run all 20 tests |
| Phase 2 Pass Rate | 100% | Run all 20 tests |
| Phase 3 Pass Rate | 100% | Run all 22 tests |
| Phase 4 Pass Rate | 100% | Run all 20 tests |
| Phase 5 Pass Rate | 100% | Run all 20 tests |
| Fulfillment Rate | ≥85% | Testbed metrics |
| Mean Interrupts/Day | 1.0-3.0 | Testbed metrics |
| Group Detection | Day 30 | Testbed output |

---

## Related Documents

- `TALE_TEST_PLAN.md` — Master strategy (read first)
- `TALE_TEST_SCRIPTS_PHASE_0_SKELETON.md` — Phase 0 outline
- `TALE_TEST_SCRIPTS_PHASE_3.md` — Phase 3 detailed specs (reference implementation)
- `TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md` — Phases 1, 2, 4, 5 outlines
- `EXPECT_ENGINE_IMPLEMENTATION.md` — TestSession API, ExpectEngine framework

---

## Summary Table

| Phase | Status | Scripts | Spec Detail | Location |
|-------|--------|---------|------------|----------|
| 0 | Planned | 20 | Outline | PHASE_0_SKELETON.md |
| 1 | Planned | 20 | Outline | PHASES_1_2_4_5_SKELETON.md |
| 2 | Planned | 20 | Outline | PHASES_1_2_4_5_SKELETON.md |
| 3 | Ready | 22 | **Detailed** | **PHASE_3.md** ← Start here |
| 4 | Planned | 20 | Outline | PHASES_1_2_4_5_SKELETON.md |
| 5 | Planned | 20 | Outline | PHASES_1_2_4_5_SKELETON.md |
| **Total** | **Roadmap** | **122** | **Mixed** | **This series** |

---

**Next Action**: Implement 22 Phase 3 JSON test scripts from `TALE_TEST_SCRIPTS_PHASE_3.md`.
