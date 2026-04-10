# TALE Narrative System Documentation

The TALE system is a comprehensive narrative simulation engine that powers NPC behavior, dialogue, relationships, and quest systems in Silicon Desert 2.

## Quick Navigation

### For New Developers
1. Start with [OVERVIEW.md](OVERVIEW.md) — System overview
2. Read [ARCHITECTURE.md](ARCHITECTURE.md) — System design and components
3. See [../PROCESS_TALE.md](../PROCESS_TALE.md) for development workflow

### By Category

#### 📚 **Phase Documentation** — [phases/](phases/)
Implementation roadmap across 19 phases (0-8, C1-C4, D)

- [PHASE_0.md](phases/PHASE_0.md) — DES Engine (testbed infrastructure)
- [PHASE_1.md](phases/PHASE_1.md) — Storylet System
- [PHASE_2.md](phases/PHASE_2.md) — Strategies
- [PHASE_C.md](phases/PHASE_C.md) — NPC Conversations
- [Full phase list](phases/)

#### 🧪 **Test Documentation** — [tests/](tests/)
Test specifications and testing infrastructure

- [STRATEGY.md](tests/STRATEGY.md) — Test documentation maintenance (quarterly audit)
- [QUICK_START.md](tests/QUICK_START.md) — Quick reference for test commands
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
- [ROLES.md](concepts/ROLES.md) — Role abstraction
- [INTERACTIONS.md](concepts/INTERACTIONS.md) — Interaction types
- [Full concepts](concepts/)

## Key Documents

| Document | Purpose |
|----------|---------|
| [OVERVIEW.md](OVERVIEW.md) | What is TALE? System goals and scope |
| [ARCHITECTURE.md](ARCHITECTURE.md) | How TALE works: components, lifecycle, interactions |
| [REFERENCE.md](REFERENCE.md) | Class and system reference |

## Development Workflow

**To implement a new feature:**
1. Read [../PROCESS_TALE.md](../PROCESS_TALE.md) for TALE-specific process
2. Check relevant [PHASE_*.md](phases/) design doc
3. Review [tests/PHASE_*.md](tests/) for test specifications
4. Add/modify tests in `models/tests/tale/phaseN-*/`
5. Update documentation as needed

**To add a test:**
```bash
1. Create: models/tests/tale/phaseN-*/NN-name.json
2. Update: docs/tale/phases/PHASE_N.md
3. Run: ./run_tests.sh phaseN
4. (Quarterly) Update: docs/tale/tests/PHASE_N.md
```

## Current Status

- ✅ **Phase 0-8**: Core simulation engine complete (179 tests)
- ✅ **Phase C1-C4**: NPC conversation system complete (29 tests)
- 🔄 **Phase D**: Multi-objective pathfinding & behavioral variety (in progress)

**Total:** 212 test files, 192 in standard regression suite (60-day simulations)

## Where Things Are

| What | Where |
|------|-------|
| Phase implementation code | `nogameCode/nogame/modules/tale/` |
| Test specifications | `models/tests/tale/phaseN-*/` |
| Test runner script | `./run_tests.sh` |
| Development process | `PROCESS_TALE.md` |
| Generic process | `PROCESS.md` |
| Doc organization | `PROCESS_DOCS.md` |

## See Also

- [../ARCHITECTURE/](../ARCHITECTURE/) — Other system architectures (rendering, physics, audio)
- [../SYSTEMS/](../SYSTEMS/) — Other feature systems (world generation, quests, persistence)
- [../TESTING/](../TESTING/) — Testing infrastructure for all subsystems
- [../roadmap/](../roadmap/) — Project planning and progress

---

**Last Updated:** 2026-04-10
