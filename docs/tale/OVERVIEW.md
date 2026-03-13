# TALE Implementation — Overview

This directory contains self-contained implementation tasks for the TALE narrative system. Each phase file can be read by Claude Code alongside `REFERENCE.md` to understand what to implement.

## Reading Order

1. **Read `REFERENCE.md` first** — shared concepts (properties, verbs, interaction primitives, roles) referenced by all phases.
2. **Read one phase file** — contains everything needed to implement that step: what to build, where it goes, what existing code to reference, and the deliverable.

## Phase Map

```
PHASE_0A  Testbed Infrastructure     ──┐
PHASE_0B  DES Engine                   ├── COMPLETE (2026-03-13)
PHASE_0C  Output & Automation        ──┘
PHASE_1   Story Generation + Content ──── First storylets, NpcNarrativeState
PHASE_2   Strategy Translation       ──── Spatial verbs → physical behavior
PHASE_3   NPC-NPC Interaction        ──── Interaction pool, encounter tuning
PHASE_4   Player Intersection        ──── Player enters NPC storylines
PHASE_5   Branching & Escalation     ──── Interrupts, power structures
```

## Dependencies

| Phase | Requires | Status |
|-------|----------|--------|
| 0A | Nothing (first step) | **Complete** |
| 0B | 0A (spatial model) | **Complete** |
| 0C | 0B (DES produces output) | **Complete** |
| 1 | 0A or 0B (storylets run in DES or standalone) | Not started |
| 2 | 1 (storylets exist to translate) | Not started |
| 3 | 1 + 0B (interaction pool needs DES + storylets) | Not started |
| 4 | 2 + 3 (player needs visible NPCs + interaction pool) | Not started |
| 5 | 3 (branching needs interaction pool) | Not started |

Phase 1 is the next step — it can build on the existing placeholder StoryletSelector and replace it with a real storylet library.

## Where Code Lives

- **Engine narrative code**: `JoyceCode/engine/tale/` — production DES engine (10 files)
- **Testbed driver**: `Testbed/` project — thin CLI harness
- **Story content**: JSON data files in `models/` — storylet definitions, role templates, parameters (Phase 1+)
- **Design documents**: `docs/NPC_STORIES_DESIGN.md`, `docs/TESTBED_PLAN.md`, `docs/TALE_CONCEPT.md`

## The Iteration Loop

Every phase follows: **write content -> run testbed -> read metrics -> adjust -> re-run**. The testbed (Phase 0) exists to make this loop fast. Claude Code can execute this loop autonomously — see `PHASE_0C.md` for the automated iteration protocol.

## Running the Testbed

```bash
# Quick 7-day run with all output
dotnet run --project Testbed -- --days 7

# Year-long performance run (Release mode recommended)
dotnet run -c Release --project Testbed -- --days 365 --quiet --events-file none

# Full output with traces and graph
dotnet run --project Testbed -- --days 30 --traces 5 --events-file events.jsonl --trace-file traces.log --graph-file graph.json
```
