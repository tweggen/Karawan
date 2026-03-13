# TALE Implementation — Overview

This directory contains self-contained implementation tasks for the TALE narrative system. Each phase file can be read by Claude Code alongside `REFERENCE.md` to understand what to implement.

## Reading Order

1. **Read `REFERENCE.md` first** — shared concepts (properties, verbs, interaction primitives, roles) referenced by all phases.
2. **Read one phase file** — contains everything needed to implement that step: what to build, where it goes, what existing code to reference, and the deliverable.

## Phase Map

```
PHASE_0A  Testbed Infrastructure     ──┐
PHASE_0B  DES Engine                   ├── Testbed (parallel with Phase 1)
PHASE_0C  Output & Automation        ──┘
PHASE_1   Story Generation + Content ──── First storylets, NpcNarrativeState
PHASE_2   Strategy Translation       ──── Spatial verbs → physical behavior
PHASE_3   NPC-NPC Interaction        ──── Interaction pool, encounter tuning
PHASE_4   Player Intersection        ──── Player enters NPC storylines
PHASE_5   Branching & Escalation     ──── Interrupts, power structures
```

## Dependencies

| Phase | Requires |
|-------|----------|
| 0A | Nothing (first step) |
| 0B | 0A (spatial model) |
| 0C | 0B (DES produces output) |
| 1 | 0A or 0B (storylets run in DES or standalone) |
| 2 | 1 (storylets exist to translate) |
| 3 | 1 + 0B (interaction pool needs DES + storylets) |
| 4 | 2 + 3 (player needs visible NPCs + interaction pool) |
| 5 | 3 (branching needs interaction pool) |

Phases 0A-0C and Phase 1 can be developed in parallel.

## Where Code Lives

- **Engine narrative code**: `JoyceCode/` or `nogameCode/` — shared between testbed and game
- **Testbed driver**: `Testbed/` project — thin harness, no narrative logic
- **Story content**: JSON data files in `models/` — storylet definitions, role templates, parameters
- **Design documents**: `docs/NPC_STORIES_DESIGN.md`, `docs/TESTBED_PLAN.md`, `docs/TALE_CONCEPT.md`

## The Iteration Loop

Every phase follows: **write content → run testbed → read metrics → adjust → re-run**. The testbed (Phase 0) exists to make this loop fast. Claude Code can execute this loop autonomously — see `PHASE_0C.md` for the automated iteration protocol.
