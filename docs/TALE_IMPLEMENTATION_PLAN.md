# TALE Implementation Plan
*Agile phases — story generation through emergent NPC interaction*

---

## Current Status (2026-03-13)

| Phase | Status | Notes |
|-------|--------|-------|
| 0A — Testbed Infra | **Complete** | Headless bootstrap, ClusterViewer, SpatialModel, NpcAssigner |
| 0B — DES Engine | **Complete** | EventQueue, NpcSchedule, EncounterResolver, StoryletSelector, RelationshipTracker, DesSimulation |
| 0C — Output & Automation | **Complete** | JSONL logger, metrics JSON, target validation, traces, graph, CLI |
| 1 — Stories + Content | Not started | Storylet authoring format, NpcNarrativeState, property balancing |
| 2 — Strategies | Not started | Strategy executors for spatial verbs |
| 3 — Interactions | Not started | Interaction pool, encounter probability tuning |
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
0A (Testbed Infra)  ──→  0B (DES Engine)  ──→  0C (Output)     ← ALL COMPLETE
                                ↕
1 (Stories + Content)  ──→  2 (Strategies)  ──→  3 (Interactions)  ──→  4 (Player)
                                                        ↓
                                                  5 (Escalation)
```

Phases 0A-0C and Phase 1 can be developed in parallel. The DES code (0B) is production Tier 3 simulation — it lives in JoyceCode, not in the Testbed project.
