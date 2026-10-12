# TALE Implementation & Testing — Overview

This directory contains implementation tasks and comprehensive test suites for the TALE narrative system.

## Organization

- **Implementation**: Phase files (`phases/PHASE_0A.md`, `phases/PHASE_3.md`, etc.) with detailed implementation plans
- **Testing**: Detailed test specifications (`tests/PHASE_X.md`) + JSON test scripts (models/tests/tale/phaseX-*)
- **Reference**: REFERENCE.md with shared concepts (properties, verbs, interaction primitives, roles)
- **Quick Start**: `tests/QUICK_START.md` for test execution and current status

## Reading Order

1. **For Implementation**: Read `REFERENCE.md` first, then phase files in sequence
2. **For Testing**: Read `tests/QUICK_START.md` for current status, then `tests/PHASE_X.md` for test details
3. **For Test Execution**: See `tests/QUICK_START.md` for bash scripts and execution examples

## Phase Implementation & Testing Status

### Implementation Status

```
PHASE_0A  Testbed Infrastructure     ──┐
PHASE_0B  DES Engine                   ├── ✅ COMPLETE (2026-03-13)
PHASE_0C  Output & Automation        ──┘
PHASE_1   Story Generation + Content ──── ✅ COMPLETE (2026-03-14)
PHASE_2   Strategy Translation       ──── ✅ COMPLETE (2026-03-14)
PHASE_3   NPC-NPC Interaction        ──── ✅ COMPLETE (2026-03-14)
PHASE_4   Player Intersection        ──── ✅ COMPLETE (2026-03-14)
PHASE_5   Branching & Escalation     ──── ✅ COMPLETE (2026-03-14)
PHASE_6A  Seed-Based Population Gen  ──── ✅ COMPLETE (2026-03-15)
PHASE_6B  Cluster Lifecycle Hook     ──── ✅ COMPLETE (2026-03-15)
PHASE_6C  SpawnOperator Rework       ──── ✅ COMPLETE (2026-03-15)
PHASE_6D  Deviation Tracking         ──── ✅ COMPLETE (2026-03-15)
PHASE_6E  Deviation Persistence      ──── ✅ COMPLETE (2026-03-15)
PHASE_6F  TaleManager Rework         ──── ✅ COMPLETE (2026-03-15)
PHASE_7A  SpatialModel Wire-up        ──┐
PHASE_7B  Entry Points                 ├── ✅ COMPLETE (2026-03-16)
PHASE_7C  Street Pathfinding           ├── ✅ COMPLETE (2026-03-26)
PHASE_7D  Building Occupancy           ├──
PHASE_7E  Fragment-Accurate Position ──┘
PHASE_7B  Building Role Tagging (Plan) ──── 📋 PLANNED (2026-03-16)
PHASE_8   Realistic Occupation Schedules ── ✅ COMPLETE (2026-03-20)
PHASE_D   Multi-Objective Routing        ── ✅ COMPLETE (2026-03-25)
```

### Testing Status

| Phase | Specs | Scripts | Validation | Status |
|-------|-------|---------|-----------|--------|
| **Phase 0** | ✅ tests/PHASE_0.md | ✅ 20 JSON (phase0-des/) | ✅ **20/20 PASS** | **COMPLETE** |
| **Phase 1** | ✅ tests/PHASE_1.md | ✅ 20 JSON (phase1-storylets/) | ✅ **20/20 PASS** | **COMPLETE** |
| **Phase 2** | ✅ tests/PHASE_2.md | ✅ 20 JSON (phase2-strategies/) | ✅ **20/20 PASS** | **COMPLETE** |
| **Phase 3** | ✅ tests/PHASE_3.md | ✅ 22 JSON (phase3-interactions/) | ✅ **22/22 PASS** | **COMPLETE** |
| **Phase 4** | ✅ tests/PHASE_4.md | ✅ 20 JSON (phase4-player/) | ✅ **20/20 PASS** | **COMPLETE** |
| **Phase 5** | ✅ tests/PHASE_5.md | ✅ 20 JSON (phase5-escalation/) | ✅ **20/20 PASS** | **COMPLETE** |
| **Phase 6** | ✅ tests/PHASE_6.md | ✅ 49 JSON (phase6-population/) | ✅ **49/49 PASS** | **COMPLETE** |
| **Phase 7** | ✅ tests/PHASE_7.md | ✅ 102 JSON (phase7-spatial/) | ✅ **102/102 PASS** | **COMPLETE** |
| **Phase 8** | ✅ Documentation | ✅ Integrated in Phase 7 tests | ✅ **49/49 PASS** | **COMPLETE** |
| **Phase D** | ✅ `docs/navigation/docs/MULTI-OBJECTIVE-ROUTING.md` | Integrated (no new tests) | ✅ **171/171 PASS** | **COMPLETE** |

### Test Suite Summary

- **Total Test Scripts**: 273 completed (all passing)
  - Phase 0: **20/20 PASS** (DES engine, initialization, scheduling, encounters, relationships)
  - Phase 1: **20/20 PASS** (Storylet library, preconditions, selection, postconditions)
  - Phase 2: **20/20 PASS** (Strategy system, phase transitions, DES integration, multi-NPC coordination)
  - Phase 3: **22/22 PASS** (NPC-NPC interactions, requests, signals, abstract resolution)
  - Phase 4: **20/20 PASS** (Player quest integration, navigation, state synchronization)
  - Phase 5: **20/20 PASS** (Interrupts, conditional postconditions, gang formation, escalation)
  - Phase 6: **49/49 PASS** (Seed-based population, cluster lifecycle, materialization, deviation tracking, persistence)
  - Phase 7: **102/102 PASS** (SpatialModel extraction, location assignment, entry positions, pathfinding, building occupancy, fragment position tracking)
  - Phase 8: Included in Phase 7 tests (occupancy-based scheduling)
  - Phase D: **171/171 PASS** (Multi-objective routing integrated, no breakage)
- **Total Test Specifications**: 8 detailed docs covering 273 tests
- **Test Categories**: 40+ categories covering initialization, scheduling, properties, encounters, relationships, metrics, library loading, preconditions, selection, postconditions, strategy phases, transitions, requests, signals, player interaction, interrupts, conditional branching, gang formation, escalation, spatial grounding, navigation, and fragment-aware spawning
- **Framework**: ExpectEngine with JSON format, lock-free event channels, event injection/monitoring
- **Execution**: TestRunner CLI (Phase-agnostic test harness)

### Dependencies for Implementation

| Phase | Requires | Status |
|-------|----------|--------|
| 0A | Nothing (first step) | **Complete** |
| 0B | 0A (spatial model) | **Complete** |
| 0C | 0B (DES produces output) | **Complete** |
| 1 | 0A or 0B (storylets run in DES or standalone) | **Complete** |
| 2 | 1 (storylets exist to translate) | **Complete** |
| 3 | 1 + 0B (interaction pool needs DES + storylets) | **Complete** |
| 4 | 2 + 3 (player needs visible NPCs + interaction pool) | **Complete** |
| 5 | 3 (branching needs interaction pool) | **Complete** |
| 6A | 0-5 (needs TaleManager, StoryletLibrary, NpcSchedule) | **Complete** |
| 6B | 6A (needs population generator to hook into cluster) | **Complete** |
| 6C | 6B (needs populated TaleManager to query) | **Complete** |
| 6D | 6C (needs working spawn/despawn to track interactions) | **Complete** |
| 6E | 6D (needs deviation flags to know what to persist) | **Complete** |
| 6F | 6A-6E (reworks TaleManager role based on full pipeline) | **Complete** |

**Current Status**: Phases 0-8 + Phase D complete, Phase 7C (street pathfinding) complete. All 171+ regression tests passing (phases 0-8, Phase D). Phase 7C fix: identified and resolved critical _currentPosition tracking bug causing 0m distance calculations (commit f9b3a88d). Next phases available: Phase 7D (building occupancy), Phase 7B (building role tagging), Phase E (dynamic safety zones), Phase F (learned routes), Phase G (traffic modeling).

## Documentation Map

### Implementation & Concepts
- **REFERENCE.md** — Shared concepts (properties, verbs, roles, interaction primitives, simulation tiers, seed generation, deviation tracking)
- **phases/PHASE_0A.md … PHASE_0C.md** — DES engine implementation (complete)
- **phases/PHASE_3.md** — NPC-NPC interaction system implementation (complete)
- **phases/PHASE_1.md, PHASE_2.md, PHASE_4.md, PHASE_5.md** — Phases 1, 2, 4, 5
- **phases/PHASE_6.md** — Production integration: seed-based population, cluster lifecycle, spawn rework, deviation persistence (complete)
- **phases/PHASE_7.md** — Spatial grounding & navigation: per-cluster models, entry points, street pathfinding, building occupancy, fragment position tracking (complete)
- **`../plans/done/PHASE_7B-BUILDING-ROLE-TAGGING.md`** — Building role tagging: semantic building types, explicit tag system, generator operators (planned)
- **phases/PHASE_8.md** — Realistic occupation schedules: role-based shifts, district-based character distribution (complete)
- **`docs/navigation/docs/MULTI-OBJECTIVE-ROUTING.md`** (formerly PHASE_D.md) — Multi-objective routing: NPC goals (Fast/OnTime/Scenic/Safe), role-based preferences, deadline-aware urgency (complete)

### Testing Framework
- **tests/QUICK_START.md** — Current status, execution guide, priorities (start here)
- **tests/FRAMEWORK.md** — Framework design and structure
- **tests/TEST_PLAN.md** — Master strategy for all tests
- **tests/PHASE_0.md** — 20 detailed DES engine test specifications
- **tests/PHASE_1.md** — 20 detailed Storylet system test specifications
- **tests/PHASE_3.md** — 22 detailed NPC-NPC interaction test specifications
- **tests/PHASE_6.md** — 49 detailed population/lifecycle/persistence test specifications
- **tests/PHASE_7.md** — 102 detailed spatial grounding & navigation test specifications
- **`docs/testing/docs/EXPECT_IMPLEMENTATION.md`** — ExpectEngine framework details

### Test Scripts
- **Phase 0 Tests**: `models/tests/tale/phase0-des/` (20 JSON scripts)
- **Phase 1 Tests**: `models/tests/tale/phase1-storylets/` (20 JSON scripts)
- **Phase 3 Tests**: `models/tests/tale/phase3-interactions/` (22 JSON scripts)
- **Phase 6 Tests**: `models/tests/tale/phase6-population/` (49 JSON scripts)
- **Phase 7 Tests**: `models/tests/tale/phase7-spatial/` (102 JSON scripts)

## Where Code Lives

- **Engine narrative code**: `JoyceCode/engine/tale/` — production DES engine
  - Phase 0: DesSimulation, EventQueue, RelationshipTracker, JsonlEventLogger
  - Phase 3: InteractionPool, InteractionRequest, InteractionSignal
  - Phase 6: TaleManager (schedule registry), NpcSchedule, StoryletSelector
- **Production integration**: `nogameCode/nogame/`
  - TALE module: `modules/tale/TaleModule.cs` — bootstraps library + TaleManager
  - TALE spawn: `characters/citizen/TaleSpawnOperator.cs` — materializes Tier 3 → Tier 2/1
  - TALE strategy: `characters/citizen/TaleEntityStrategy.cs` — drives NPC behavior from schedule
  - Scene hook: `scenes/root/Scene.cs` — registers TaleSpawnOperator with SpawnController
- **Testbed driver**: `Testbed/` project — thin CLI harness for simulation
- **Story content**: JSON data files in `models/tale/` — storylet definitions, role-specific content
  - Main files: merchant.json, worker.json, drifter.json, socialite.json, authority.json, universal.json
  - Interaction storylets consolidated into above files
- **Design documents**: `docs/tale/docs/` — design/NPC_STORIES.md, CONCEPT.md, ARCHITECTURE.md, etc.; testbed in `docs/testing/docs/TESTBED.md`

## The Iteration Loop

### Implementation Phase
Every phase follows: **write content -> run testbed -> read metrics -> adjust -> re-run**. The testbed (Phase 0) exists to make this loop fast. Claude Code can execute this loop autonomously — see `phases/PHASE_0C.md` for the automated iteration protocol.

### Testing Phase (Current)
The testing framework validates each phase's implementation:

1. **Read test specification** (`tests/PHASE_X.md`)
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
    ./TestRunner/bin/Release/net10.0/TestRunner || exit 1
done

# Run all Phase 1 tests
for script in models/tests/tale/phase1-storylets/*.json; do
  JOYCE_TEST_SCRIPT="tests/tale/phase1-storylets/$(basename $script)" \
    ./TestRunner/bin/Release/net10.0/TestRunner || exit 1
done

# Run all Phase 3 tests
for script in models/tests/tale/phase3-interactions/*.json; do
  JOYCE_TEST_SCRIPT="tests/tale/phase3-interactions/$(basename $script)" \
    ./TestRunner/bin/Release/net10.0/TestRunner || exit 1
done

# Or run all tests at once
./run_tests.sh all
```

See `tests/QUICK_START.md` for full execution guide and current status.
