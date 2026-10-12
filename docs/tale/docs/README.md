# TALE Narrative System Documentation

The TALE system is a comprehensive narrative simulation engine that powers NPC behavior, dialogue, relationships, and quest systems in Silicon Desert 2.

## Quick Navigation

### For New Developers
1. Start with [OVERVIEW.md](OVERVIEW.md) — System overview
2. Read [ARCHITECTURE.md](ARCHITECTURE.md) — System design and components
3. See [PROCESS_TALE.md](PROCESS_TALE.md) for development workflow

### By Category

#### 📚 **Phase Documentation** — [phases/](phases/)
Implementation roadmap across phases 0-8, C1-C4 and TALE-SOCIAL D/E

- [PHASE_0A.md](phases/PHASE_0A.md) … [PHASE_0C.md](phases/PHASE_0C.md) — DES Engine (testbed infrastructure)
- [PHASE_1.md](phases/PHASE_1.md) — Storylet System
- [PHASE_2.md](phases/PHASE_2.md) — Strategies
- [PHASE_C.md](../plans/done/PHASE_C-NPC-CONVERSATIONS.md) — NPC Conversations
- [Full phase list](phases/)

#### 🧪 **Test Documentation** — [tests/](tests/)
Test specifications and testing infrastructure

- [STRATEGY.md](tests/STRATEGY.md) — Test documentation maintenance (quarterly audit)
- [PHASE_C.md](tests/PHASE_C.md) — Test specs for C1–C4
- Test commands: [PROCESS_TALE.md](PROCESS_TALE.md); superseded test planning docs are in [../archive/](../archive/) (`TALE_TESTS_*.md`)
- [PHASE_0.md](tests/PHASE_0.md) — Test specs for Phase 0
- [Full test list](tests/)

#### 🎯 **Design Documents** — [design/](design/)
Problem analysis, design solutions, and implementation challenges

- [NPC_COMMUTING.md](design/NPC_COMMUTING.md) — NPC pathfinding & movement
- [NPC_STORIES.md](design/NPC_STORIES.md) — Character backstory & narrative arcs
- [Full design docs](design/)

#### 💡 **Concepts** — [concepts/](concepts/)
Foundational, theoretical, and architectural concepts

- [NARRATION.md](concepts/NARRATION.md) — Narration system concept
- [ROLES.md](../plans/done/ROLE-REGISTRY.md) — Role abstraction
- [INTERACTIONS.md](../plans/done/INTERACTION-TYPE-REGISTRY.md) — Interaction types
- [Full concepts](concepts/)

## Key Documents

| Document | Purpose |
|----------|---------|
| [OVERVIEW.md](OVERVIEW.md) | What is TALE? System goals and scope |
| [ARCHITECTURE.md](ARCHITECTURE.md) | How TALE works: components, lifecycle, interactions |
| [REFERENCE.md](REFERENCE.md) | Class and system reference |

## Development Workflow

**To implement a new feature:**
1. Read [PROCESS_TALE.md](PROCESS_TALE.md) for TALE-specific process
2. Check relevant [PHASE_*.md](phases/) design doc
3. Review [tests/PHASE_*.md](tests/) for test specifications
4. Add/modify tests in `models/tests/tale/phaseN-*/`
5. Update documentation as needed

**To add a test:**
```bash
1. Create: models/tests/tale/phaseN-*/NN-name.json
2. Update: docs/tale/docs/phases/PHASE_N.md
3. Run: ./run_tests.sh phaseN
4. (Quarterly) Update: docs/tale/docs/tests/PHASE_N.md
```

## Current Status

Live status: `CLAUDE.md` ("Current State"); history: `docs/main/docs/STATUS-LOG.md`.

- ✅ **Phase 0-8**: Core simulation engine complete (273 test files, 102 of them in `phase7-spatial/`, which no `run_tests.sh` tier runs — open, tracked separately)
- ✅ **Phase C1-C4**: NPC conversation system complete (29 tests)
- ✅ **TALE-SOCIAL D1-D5 and E**: scenario pre-computation and living factions complete ([PHASE_D_SOCIAL.md](phases/PHASE_D_SOCIAL.md), [PHASE_E_SOCIAL.md](phases/PHASE_E_SOCIAL.md)); tested by xUnit in `tests/JoyceCode.Tests/engine/tale/`. Acting on the five D5 tuning concerns is in flight.
- ⚠️ **Routing Phase D** (multi-objective pathfinding, [MULTI-OBJECTIVE-ROUTING.md](../../navigation/docs/MULTI-OBJECTIVE-ROUTING.md)) is a *separate* workstream that shares the letter: D2 (multi-objective A*) and D4 (role-based preferences) are in flight.

**Total:** 302 test files, 200 in standard regression suite (60-day simulations)

## Where Things Are

| What | Where |
|------|-------|
| Phase implementation code | `JoyceCode/engine/tale/` (engine), `nogameCode/nogame/modules/tale/` (game module) |
| Test specifications | `models/tests/tale/phaseN-*/` |
| Test runner script | `./run_tests.sh` |
| Development process | `PROCESS_TALE.md` |
| Generic process | `PROCESS.md` |
| Doc organization | `docs/main/docs/PROCESS_DOCS.md` |

## See Also

- [../../main/docs/architecture/](../../main/docs/architecture/) — Other system architectures (rendering, physics, audio)
- [../../SYSTEMS/](../../SYSTEMS/) — Other feature systems (world generation, quests, persistence)
- [../../testing/docs/](../../testing/docs/) — Testing infrastructure for all subsystems
- [../../roadmap/](../../roadmap/) — Project planning and progress

---

**Last Updated:** 2026-10-12
