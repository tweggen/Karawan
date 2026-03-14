# TALE Implementation & Testing — Overview

This directory contains implementation tasks and comprehensive test suites for the TALE narrative system.

## Organization

- **Implementation**: Phase files (PHASE_0.md, PHASE_3.md, etc.) with detailed implementation plans
- **Testing**: Detailed test specifications (TALE_TEST_SCRIPTS_PHASE_X.md) + JSON test scripts (models/tests/tale/phaseX-*)
- **Reference**: REFERENCE.md with shared concepts (properties, verbs, interaction primitives, roles)
- **Quick Start**: TESTING_QUICK_START.md for test execution and current status

## Reading Order

1. **For Implementation**: Read `REFERENCE.md` first, then phase files in sequence
2. **For Testing**: Read `TESTING_QUICK_START.md` for current status, then TALE_TEST_SCRIPTS_PHASE_X.md for test details
3. **For Test Execution**: See TESTING_QUICK_START.md for bash scripts and execution examples

## Phase Implementation & Testing Status

### Implementation Status

```
PHASE_0A  Testbed Infrastructure     ──┐
PHASE_0B  DES Engine                   ├── ✅ COMPLETE (2026-03-13)
PHASE_0C  Output & Automation        ──┘
PHASE_1   Story Generation + Content ──── ⏳ NEXT (Phase 1 outlines → Phase 2 specs)
PHASE_2   Strategy Translation       ──── ⏳ Queued
PHASE_3   NPC-NPC Interaction        ──── ✅ COMPLETE (2026-03-14)
PHASE_4   Player Intersection        ──── ⏳ Queued
PHASE_5   Branching & Escalation     ──── ⏳ Queued
```

### Testing Status

| Phase | Specs | Scripts | Validation | Status |
|-------|-------|---------|-----------|--------|
| **Phase 0** | ✅ TALE_TEST_SCRIPTS_PHASE_0.md | ✅ 20 JSON (phase0-des/) | ✅ **20/20 PASS** | **COMPLETE** |
| **Phase 1** | ✅ TALE_TEST_SCRIPTS_PHASE_1.md | ✅ 20 JSON (phase1-storylets/) | ✅ **20/20 PASS** | **COMPLETE** |
| **Phase 2** | ⏳ Outlined (PHASES_1_2_4_5_SKELETON.md) | ⏳ Need specs → scripts | ⏳ NEXT | In Progress |
| **Phase 3** | ✅ TALE_TEST_SCRIPTS_PHASE_3.md | ✅ 22 JSON (phase3-interactions/) | ✅ **22/22 PASS** | **COMPLETE** |
| **Phase 4** | ⏳ Outlined | ⏳ Need specs → scripts | ⏳ Queued | Pending |
| **Phase 5** | ⏳ Outlined | ⏳ Need specs → scripts | ⏳ Queued | Pending |

### Test Suite Summary

- **Total Test Scripts**: 62 completed and all passing
  - Phase 0: **20/20 PASS** (DES engine, initialization, scheduling, encounters, relationships)
  - Phase 1: **20/20 PASS** (Storylet library, preconditions, selection, postconditions)
  - Phase 3: **22/22 PASS** (NPC-NPC interactions, requests, signals, abstract resolution)
- **Total Test Specifications**: 3 detailed docs covering 62 tests
- **Test Categories**: 18+ categories covering initialization, scheduling, properties, encounters, relationships, metrics, library loading, preconditions, selection, postconditions, request emission, claiming, signal emission, and complex scenarios
- **Framework**: ExpectEngine with JSON format, lock-free event channels, event injection/monitoring
- **Execution**: TestRunner CLI (Phase-agnostic test harness)

### Dependencies for Implementation

| Phase | Requires | Status |
|-------|----------|--------|
| 0A | Nothing (first step) | **Complete** |
| 0B | 0A (spatial model) | **Complete** |
| 0C | 0B (DES produces output) | **Complete** |
| 1 | 0A or 0B (storylets run in DES or standalone) | Ready (implementation not started) |
| 2 | 1 (storylets exist to translate) | Awaiting Phase 1 completion |
| 3 | 1 + 0B (interaction pool needs DES + storylets) | **Complete** (implementation-wise) |
| 4 | 2 + 3 (player needs visible NPCs + interaction pool) | Awaiting Phase 2 completion |
| 5 | 3 (branching needs interaction pool) | Awaiting Phase 2 completion |

**Current Focus**: All 62 tests passing (Phases 0, 1, 3). Phase 3 test infrastructure fixed (node_arrival Code field, signal emission on direct claim). Phase 2 test implementation is next priority.

## Documentation Map

### Implementation & Concepts
- **REFERENCE.md** — Shared concepts (properties, verbs, roles, interaction primitives)
- **PHASE_0.md** — DES engine implementation (complete)
- **PHASE_3.md** — NPC-NPC interaction system implementation (complete)
- **PHASES_1_2_4_5_SKELETON.md** — Outlines for Phases 1, 2, 4, 5

### Testing Framework
- **TESTING_QUICK_START.md** — Current status, execution guide, priorities (start here)
- **TALE_TESTING_FRAMEWORK.md** — Framework design and structure
- **TALE_TEST_PLAN.md** — Master strategy for all 120+ tests
- **TALE_TEST_SCRIPTS_PHASE_0.md** — 20 detailed DES engine test specifications
- **TALE_TEST_SCRIPTS_PHASE_1.md** — 20 detailed Storylet system test specifications
- **TALE_TEST_SCRIPTS_PHASE_3.md** — 22 detailed NPC-NPC interaction test specifications
- **EXPECT_ENGINE_IMPLEMENTATION.md** — ExpectEngine framework details

### Test Scripts
- **Phase 0 Tests**: `models/tests/tale/phase0-des/` (20 JSON scripts)
- **Phase 1 Tests**: `models/tests/tale/phase1-storylets/` (20 JSON scripts)
- **Phase 3 Tests**: `models/tests/tale/phase3-interactions/` (22 JSON scripts)

## Where Code Lives

- **Engine narrative code**: `JoyceCode/engine/tale/` — production DES engine
  - Phase 0: DesSimulation, EventQueue, RelationshipTracker, JsonlEventLogger
  - Phase 3: InteractionPool, InteractionRequest, InteractionSignal
- **Testbed driver**: `Testbed/` project — thin CLI harness for simulation
- **Story content**: JSON data files in `models/tale/` — storylet definitions, role-specific content
  - Main files: merchant.json, worker.json, drifter.json, socialite.json, authority.json, universal.json
  - Interaction storylets consolidated into above files
- **Design documents**: `docs/` — NPC_STORIES_DESIGN.md, TESTBED_PLAN.md, TALE_CONCEPT.md, etc.

## The Iteration Loop

### Implementation Phase
Every phase follows: **write content -> run testbed -> read metrics -> adjust -> re-run**. The testbed (Phase 0) exists to make this loop fast. Claude Code can execute this loop autonomously — see `PHASE_0C.md` for the automated iteration protocol.

### Testing Phase (Current)
The testing framework validates each phase's implementation:

1. **Read test specification** (`TALE_TEST_SCRIPTS_PHASE_X.md`)
2. **Run test suite** (20+ JSON scripts per phase)
3. **Verify event sequences** (JSONL logging)
4. **Check metrics** (fulfillment rates, encounter distribution, etc.)
5. **Refine implementation** if tests fail
6. **Repeat** with Testbed for 30-day validation

## Running the Testbed

```bash
# Quick 7-day run with all output
dotnet run --project Testbed -- --days 7

# Year-long performance run (Release mode recommended)
dotnet run -c Release --project Testbed -- --days 365 --quiet --events-file none

# Full output with traces and graph
dotnet run --project Testbed -- --days 30 --traces 5 --events-file events.jsonl --trace-file traces.log --graph-file graph.json

# Run a specific phase test
JOYCE_TEST_SCRIPT=models/tests/tale/phase0-des/01-initialization.json \
  dotnet run --project nogame/nogame.csproj
```

## Running Test Suites

Build TestRunner first:
```bash
dotnet build TestRunner/TestRunner.csproj -c Release -p:EnableSourceLink=false
```

Run all tests:
```bash
# Run all Phase 0 tests
for script in models/tests/tale/phase0-des/*.json; do
  JOYCE_TEST_SCRIPT="tests/tale/phase0-des/$(basename $script)" \
    ./TestRunner/bin/Release/net9.0/TestRunner || exit 1
done

# Run all Phase 1 tests
for script in models/tests/tale/phase1-storylets/*.json; do
  JOYCE_TEST_SCRIPT="tests/tale/phase1-storylets/$(basename $script)" \
    ./TestRunner/bin/Release/net9.0/TestRunner || exit 1
done

# Run all Phase 3 tests
for script in models/tests/tale/phase3-interactions/*.json; do
  JOYCE_TEST_SCRIPT="tests/tale/phase3-interactions/$(basename $script)" \
    ./TestRunner/bin/Release/net9.0/TestRunner || exit 1
done

# Or run all 62 tests at once
./run_tests.sh all
```

See `TESTING_QUICK_START.md` for full execution guide and current status.
