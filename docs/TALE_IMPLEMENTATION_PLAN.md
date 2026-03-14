# TALE Implementation Plan
*Agile phases — story generation through emergent NPC interaction*

---

## Current Status (2026-03-14)

| Phase | Status | Notes |
|-------|--------|-------|
| 0A — Testbed Infra | **Complete** | Headless bootstrap, ClusterViewer, SpatialModel, NpcAssigner |
| 0B — DES Engine | **Complete** | EventQueue, NpcSchedule, EncounterResolver, StoryletSelector, RelationshipTracker, DesSimulation |
| 0C — Output & Automation | **Complete** | JSONL logger, metrics JSON, target validation, traces, graph, CLI |
| 1 — Stories + Content | **Complete** | JSON storylet library, data-driven selection, emergent crime/groups, morality drift |
| 2 — Strategies | **Complete** | TaleManager, GoTo/StayAt strategy parts, TaleEntityStrategy, TaleSpawnOperator, all 20 tests passing |
| 3 — Interactions | **Complete** | Interaction pool, request/signal events, Tier-2 direct + Tier-3 abstract resolution, all 22 tests passing |
| 4 — Player | Not started | Player enters NPC storylines |
| 5 — Escalation | Not started | Branching, interrupts, escalation storylets |

### Phase 0 Results

**Performance** (500 NPCs, seed 42):
- 7 days: ~1,000ms wall-clock
- 365 days: ~5,300ms wall-clock (Release mode) — target was <10s

**Key metrics (7-day run)**:
- 67,514 interactions (all "greet" — trust starts at 0, 7 days too short for acquaintance tier)
- 20,491 unique relationships
- Graph: clustering_coefficient=0.532, degree_gini=0.255, largest_component=1.0
- Hunger mean=0.947 (daily gain exceeds lunch reduction — needs Phase 1 balance work)

**Production code** lives in `JoyceCode/engine/tale/`:
- `EventQueue.cs` — min-heap priority queue
- `NpcSchedule.cs` — per-NPC state
- `EncounterResolver.cs` — probabilistic encounter detection with inline compaction
- `StoryletSelector.cs` — placeholder role-based schedule templates
- `RelationshipTracker.cs` — trust tracking, interaction types, tier progression
- `SimMetrics.cs` — graph analysis (BFS components, clustering, Gini)
- `DesSimulation.cs` — main DES loop with day boundaries, traces
- `IEventLogger.cs` / `JsonlEventLogger.cs` — structured event output
- `SpatialModel.cs` — location graph extracted from ClusterDesc

**Testbed** in `Testbed/`:
- `TestbedMain.cs` — CLI harness with `--days`, `--seed`, `--npcs`, `--quiet`, etc.
- `testbed_targets.json` — metric bounds for pass/fail

### Phase 1 Results

**JSON-driven storylet system** replacing hardcoded schedule templates:
- `StoryletDefinition.cs` / `StoryletLibrary.cs` — loads storylets from `models/tale/*.json`
- `GroupDetector.cs` — Bron-Kerbosch clique detection on high-trust subgraph
- 7 JSON storylet files: universal, worker, merchant, socialite, drifter, authority, desperation
- Data-driven preconditions: time_of_day, property ranges, desperation, morality gates
- Morality drift: desperation pushes morality down; low desperation allows recovery
- Emergent behavior (90-day, 500 NPCs): 39k argues, 5.7k intimidations, 105 robberies, 50 recruits

**Key fixes:**
- InvariantCulture float parsing (critical on German-locale systems)
- Bron-Kerbosch performance: trust threshold 0.75, MaxCliques=500 cap, 30-day detection interval

### Phase 2 Results

**Story-to-strategy bridge** translating DES storylets into visible NPC behavior:

**Engine-level** (`JoyceCode/engine/tale/`):
- `TaleManager.cs` — runtime manager advancing storylets at game time, location resolution, spawn API

**Game-level** (`nogameCode/nogame/characters/citizen/`):
- `GoToStrategyPart.cs` — walk-to-destination via 2-point SegmentRoute, arrival detection in Sync
- `StayAtStrategyPart.cs` — idle at location for real-time duration (game-time converted via RealSecondsPerGameDay)
- `IdleBehavior.cs` — stationary behavior with idle animation and collision handling
- `TaleEntityStrategy.cs` — AOneOfStrategy: travel → activity → advance → repeat; reuses citizen flee/recover
- `TaleSpawnOperator.cs` — ISpawnOperator spawning TALE-driven NPCs via SpawnController

**Module** (`nogameCode/nogame/modules/tale/`):
- `TaleModule.cs` — bootstraps StoryletLibrary, registers TaleManager service

**Integration**: Scene.cs registers TaleSpawnOperator alongside existing citizen SpawnOperator

### Phase 3 Results

**NPC-NPC interaction system** with request emission, claiming, and signal fulfillment:

**Engine-level** (`JoyceCode/engine/tale/`):
- `InteractionRequest.cs` — Request data structure (ID, type, requester, timeout, claimer)
- `InteractionSignal.cs` — Signal data structure (ID, type, source NPC, timestamp)
- `InteractionPool.cs` — Cluster-scoped pool managing request lifecycle
- `DesSimulation.cs` — Integration points: emit requests from postconditions, claim during encounters, resolve unclaimed via Tier-3 abstract
- `IEventLogger.cs` extensions — LogRequestEmitted, LogRequestClaimed, LogSignalEmitted
- `TestEventLogger.cs` (TestRunner) — Bridges DES events to test framework

**Test Infrastructure**:
- `TestRunner/TestRunner.csproj` — Dedicated test CLI harness
- `TestRunner/TestRunnerMain.cs` — Full engine initialization, background DES simulation, test script execution
- `models/tests/tale/phase3-interactions/` — 22 JSON test scripts (all passing)

**Test Results** (2026-03-14):
- ✅ **22/22 Phase 3 tests passing**
- ✅ **62/62 total tests passing** (Phases 0, 1, 3)
- **Key fixes**:
  - Fixed `node_arrival` Code field to use storylet ID instead of location ID
  - Added signal emission on direct (Tier-2) claim in CheckAndClaimRequests
  - Removed debug console output from TestEventLogger

## Implementation Task Files

The full plan is split into self-contained files in `docs/tale/`. Each file can be read by Claude Code alongside `REFERENCE.md` to implement that phase.

| File | Phase | Summary |
|------|-------|---------|
| [OVERVIEW.md](tale/OVERVIEW.md) | — | Phase map, dependencies, reading order |
| [REFERENCE.md](tale/REFERENCE.md) | — | Shared concepts: properties, verbs, interaction primitives, roles, codebase files |
| [PHASE_0A_TESTBED_INFRA.md](tale/PHASE_0A_TESTBED_INFRA.md) | 0A | Headless bootstrap, ClusterViewer, spatial model extraction |
| [PHASE_0B_DES_ENGINE.md](tale/PHASE_0B_DES_ENGINE.md) | 0B | Discrete event simulation: event queue, NpcSchedule, encounter resolver |
| [PHASE_0C_OUTPUT.md](tale/PHASE_0C_OUTPUT.md) | 0C | Event log schema, metrics JSON, target metrics, CLI, automated iteration |
| [PHASE_1_STORIES.md](tale/PHASE_1_STORIES.md) | 1 | Storylet authoring format, roles, initial library, NpcNarrativeState, property balancing |
| [PHASE_2_STRATEGIES.md](tale/PHASE_2_STRATEGIES.md) | 2 | Strategy executors for spatial verbs, visual daily routines |
| [PHASE_3_INTERACTIONS.md](tale/PHASE_3_INTERACTIONS.md) | 3 | Interaction pool, interaction storylets, encounter probability tuning |
| [PHASE_4_PLAYER.md](tale/PHASE_4_PLAYER.md) | 4 | Player enters NPC storylines, overhear/dialogue, social capital |
| [PHASE_5_ESCALATION.md](tale/PHASE_5_ESCALATION.md) | 5 | Branching, interrupts, escalation storylets, emergent power structures |

## The Iteration Loop

Every phase follows the same cycle:

```
Write/adjust story content (storylets, roles, preconditions, probabilities)
  → Run testbed DES (seconds for a simulated year)
  → Read metrics + interaction graph + text traces
  → Identify problems (dead routines, chaotic interrupts, missing escalation)
  → Adjust content and parameters
  → Re-run
```

Claude Code can execute this loop autonomously. See [PHASE_0C_OUTPUT.md](tale/PHASE_0C_OUTPUT.md) for the structured output format, target metrics, and automated iteration protocol.

## Design Documents

For architectural context and rationale behind the plan:
- [NPC_STORIES_DESIGN.md](NPC_STORIES_DESIGN.md) — NPC-centric narrative vision, social capital, simulation tiers, transport tubes
- [TESTBED_PLAN.md](TESTBED_PLAN.md) — Testbed architecture, DES design, spatial model, encounter probability model
- [TALE_CONCEPT.md](TALE_CONCEPT.md) — Storylet templates, emotional arcs, graph rewriting

## Phase Dependencies

```
0A (Testbed Infra)  ──→  0B (DES Engine)  ──→  0C (Output)         ← ALL COMPLETE
                                ↕
1 (Stories + Content)  ──→  2 (Strategies)                          ← BOTH COMPLETE
                                  ↓
                          3 (Interactions)  ──→  4 (Player)
                                  ↓                  ↓
                          5 (Escalation)  ←──────┘
```

**Status**: Phases 0A-0C, 1, 2, and 3 are complete. All test suites passing (62/62).
- Phase 3 test infrastructure fixed and fully operational (TestRunner harness)
- Next: Phase 2 strategy tests + Phase 4 player integration tests
- The DES code (0B, 1, 3) is production Tier 3 simulation — it lives in JoyceCode, not in the Testbed project.
