# TALE Implementation & Testing — Overview

This directory contains implementation tasks and comprehensive test suites for the TALE narrative system.

## Organization

- **Implementation**: Phase files (`phases/PHASE_0A.md`, `phases/PHASE_3.md`, etc.) with detailed implementation plans
- **Testing**: Detailed test specifications (`tests/PHASE_X.md`) + JSON test scripts (models/tests/tale/phaseX-*)
- **Reference**: REFERENCE.md with shared concepts (properties, verbs, interaction primitives, roles)
- **Running tests**: `PROCESS_TALE.md` (tiers, commands) and `docs/testing/docs/RUN_TESTS_GUIDE.md`

## Reading Order

1. **For Implementation**: Read `REFERENCE.md` first, then phase files in sequence
2. **For Testing**: Read `PROCESS_TALE.md` for the test tiers, then `tests/PHASE_X.md` for test details
3. **For Test Execution**: See `PROCESS_TALE.md` and `docs/testing/docs/RUN_TESTS_GUIDE.md`

## Phase Implementation & Testing Status

> **Live status is in `CLAUDE.md` ("Current State") and the history in
> `docs/main/docs/STATUS-LOG.md`.** The summary below was rewritten on
> 2026-10-12; trust those two over it when they disagree.

### Implementation Status

- ✅ **Phases 0–8** (DES engine, storylets, strategies, NPC–NPC interaction, player intersection,
  escalation, population/lifecycle, spatial grounding, occupations) — complete, including
  7B building role tagging (`../plans/done/PHASE_7B-BUILDING-ROLE-TAGGING.md`).
- ✅ **C1–C4** NPC conversations — complete (`../plans/done/TALE-NPC-CONVERSATION-SYSTEM-C1-C4.md`).
  C5/C6 are proposals in `../plans/proposed/`.
- ✅ **TALE-SOCIAL D1–D5** (scenario pre-computation) and **E** (living factions) — complete
  (`phases/PHASE_D_SOCIAL.md`, `phases/PHASE_E_SOCIAL.md`). Acting on the five D5 tuning
  concerns is still in flight.
- ⚠️ **Routing Phase D is a separate workstream** with the same letter
  (`docs/navigation/docs/MULTI-OBJECTIVE-ROUTING.md`): goals and preferences exist, D2
  (multi-objective A*) and D4 (role-based preferences) are in flight.

### Testing Status

| Suite | Where | Count | Run by |
|-------|-------|-------|--------|
| Phases 0–6 | `models/tests/tale/phase0-des/` … `phase6-population/` | 171 JSON | `./run_tests.sh all` / `full` |
| C1–C4 | `models/tests/tale/phaseC1-infrastructure/` … `phaseC4-trust/` | 29 JSON | `./run_tests.sh all` / `full` |
| Phase 7 | `models/tests/tale/phase7-spatial/` | 102 JSON | **no tier** — open, tracked separately |
| TALE-SOCIAL D/E | `tests/JoyceCode.Tests/engine/tale/` (incl. `bake/`) | xUnit | `dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj` |

A green TALE run is 200 scripts (the 171 + 29 above, 60-day simulations). Phase 8 and 7B have no
test directory of their own.

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

**Current Status**: see `CLAUDE.md` and `docs/main/docs/STATUS-LOG.md`.

## Documentation Map

### Implementation & Concepts
- **REFERENCE.md** — Shared concepts (properties, verbs, roles, interaction primitives, simulation tiers, seed generation, deviation tracking)
- **phases/PHASE_0A.md … PHASE_0C.md** — DES engine implementation (complete)
- **phases/PHASE_3.md** — NPC-NPC interaction system implementation (complete)
- **phases/PHASE_1.md, PHASE_2.md, PHASE_4.md, PHASE_5.md** — Phases 1, 2, 4, 5
- **phases/PHASE_6.md** — Production integration: seed-based population, cluster lifecycle, spawn rework, deviation persistence (complete)
- **phases/PHASE_7.md** — Spatial grounding & navigation: per-cluster models, entry points, street pathfinding, building occupancy, fragment position tracking (complete)
- **`../plans/done/PHASE_7B-BUILDING-ROLE-TAGGING.md`** — Building role tagging: semantic building types, explicit tag system, generator operators (complete)
- **phases/PHASE_8.md** — Realistic occupation schedules: role-based shifts, district-based character distribution (complete)
- **phases/PHASE_D_SOCIAL.md, PHASE_E_SOCIAL.md** — TALE-SOCIAL: scenario pre-computation (D1–D5) and living factions (E) (complete)
- **`docs/navigation/docs/MULTI-OBJECTIVE-ROUTING.md`** (formerly PHASE_D.md) — *routing* Phase D, a separate workstream: NPC goals (Fast/OnTime/Scenic/Safe), role-based preferences, deadline-aware urgency (D2/D4 in flight)

### Testing Framework
- **tests/STRATEGY.md** — How the test docs are maintained (quarterly audit)
- **tests/PHASE_C.md** — C1–C4 test specifications
- Superseded planning docs (framework, test plan, quick start) are in `../archive/TALE_TESTS_*.md`
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
- **Phase 7 Tests**: `models/tests/tale/phase7-spatial/` (102 JSON scripts, not run by any tier)
- **C1–C4 Tests**: `models/tests/tale/phaseC1-infrastructure/` … `phaseC4-trust/` (29 JSON scripts)

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

See `PROCESS_TALE.md` and `docs/testing/docs/RUN_TESTS_GUIDE.md` for the full execution guide.
