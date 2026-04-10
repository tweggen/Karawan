# Documentation Organization Guide (PROCESS_DOCS)

This document defines the standard structure for Karawan documentation, ensuring consistency and discoverability as the project grows.

---

## Current Problem

The `docs/` directory has grown organically and is now difficult to navigate:

- **Root docs/** has 14 unrelated files: testing, TALE, rendering, world generation, platforms, persistence mixed together
- **docs/tale/** has 48 mixed files: phases, tests, architecture, proposals, and challenges all at one level
- **Inconsistent naming**: some use `THING_PLAN.md`, others `IMPLEMENTATION-PLAN-THING.md`, etc.
- **Hard to find**: no clear index or hierarchy

**Goal:** Organize by **subsystem/domain** first, then by **purpose** (architecture, tests, design, phases).

---

## Proposed Structure

```
docs/
├── PROCESS.md                          # Generic development process (exists)
├── PROCESS_TALE.md                     # TALE-specific process (exists)
├── PROCESS_DOCS.md                     # This file (documentation organization)
│
├── ARCHITECTURE/                       # High-level system architecture
│   ├── ENGINE.md                       # Joyce engine overview
│   ├── RENDERING.md                    # Rendering pipeline
│   ├── PHYSICS.md                      # Physics engine
│   ├── AUDIO.md                        # Audio system
│   └── INPUT.md                        # Input handling
│
├── SYSTEMS/                            # Feature & system design (non-TALE)
│   ├── NARRATION/
│   │   ├── EXPECT_ENGINE.md           # Narration/Expect engine concept
│   │   └── EXPECT_IMPLEMENTATION.md   # Implementation details
│   ├── WORLD_GEN/
│   │   ├── LSYSTEM.md                 # L-system features
│   │   └── LSYSTEM_EDITOR.md          # L-system editor plan
│   ├── QUEST/
│   │   └── QUEST_SYSTEM.md            # Quest system (moved from root)
│   ├── PERSISTENCE/
│   │   └── LITEDB.md                  # LiteDB storage
│   └── PLATFORMS/
│       └── ANDROID.md                 # Android/MAUI specific
│
├── TESTING/                            # Testing infrastructure (all subsystems)
│   ├── TESTING_STRATEGY.md            # Overall testing strategy
│   ├── TESTBED.md                     # Headless testbed architecture
│   └── TIERS.md                       # Testing tiers proposal
│
├── tale/                               # TALE narrative subsystem (comprehensive)
│   ├── README.md                      # TALE index & overview
│   ├── OVERVIEW.md                    # TALE system overview
│   ├── ARCHITECTURE.md                # TALE architecture
│   ├── REFERENCE.md                   # Class/system reference
│   │
│   ├── phases/                        # Phase documentation (0-8, C1-C4, D)
│   │   ├── PHASE_0.md
│   │   ├── PHASE_1.md
│   │   ├── ...
│   │   ├── PHASE_C.md
│   │   └── PHASE_D.md
│   │
│   ├── tests/                         # Test documentation & specs
│   │   ├── STRATEGY.md               # Test documentation maintenance (quarterly)
│   │   ├── QUICK_START.md            # Quick reference for test commands
│   │   ├── PHASE_0.md                # Test specs for Phase 0
│   │   ├── PHASE_1.md                # Test specs for Phase 1
│   │   ├── ...
│   │   └── PHASE_C.md                # Test specs for Phase C
│   │
│   ├── design/                        # Design documents & problem analysis
│   │   ├── NPC_COMMUTING.md          # Design challenge/solution docs
│   │   ├── NPC_STORIES.md            # Story/character design
│   │   └── (future design docs)
│   │
│   └── concepts/                      # Conceptual/foundational docs
│       ├── NARRATION.md              # Narration system concept (TALE-specific)
│       ├── ROLES.md                  # Role abstraction concept
│       ├── INTERACTIONS.md           # Interaction types concept
│       └── (future concept docs)
│
└── roadmap/                            # Project planning & progress
    ├── proposed/                      # Not yet started
    ├── planned/                       # Scheduled, in queue
    └── done/                          # Completed, for reference
```

---

## Decision Tree: Where to Put a New Document

Use this flowchart to decide where your new documentation belongs:

```
Is it about Joyce engine, rendering, physics, audio, or input?
  ├─ YES → docs/ARCHITECTURE/ or docs/SYSTEMS/SUBSYSTEM/
  └─ NO

Is it a plan, proposal, or roadmap item?
  ├─ YES → docs/roadmap/{proposed,planned,done}/
  └─ NO

Is it about TALE narrative system?
  ├─ YES → docs/tale/{architecture,phases,tests,design,concepts}/ (see TALE decision below)
  └─ NO

Is it about testing infrastructure (applies to multiple subsystems)?
  ├─ YES → docs/TESTING/
  └─ NO

Otherwise:
  └─ Create a new subsystem folder under docs/SYSTEMS/ if it's a significant system
```

### TALE-Specific Decision Tree

If your document is about TALE, use this tree:

```
Is it a phase design doc (Phase 0-8, C1-C4, D)?
  ├─ YES → docs/tale/phases/PHASE_N.md

Is it test specs or test infrastructure for a phase?
  ├─ YES → docs/tale/tests/
  │        (PHASE_N.md for specs, STRATEGY.md for maintenance)
  │
Is it overall TALE architecture or overview?
  ├─ YES → docs/tale/ARCHITECTURE.md or OVERVIEW.md

Is it a design problem/solution or analysis doc?
  ├─ YES → docs/tale/design/THING_NAME.md

Is it a conceptual/foundational doc (not tied to a phase)?
  ├─ YES → docs/tale/concepts/CONCEPT_NAME.md

Is it a quick-reference or tutorial?
  ├─ YES → docs/tale/tests/QUICK_START.md (if test-related)
  │        or docs/tale/REFERENCE.md (if general)

Otherwise:
  └─ Unclear. Ask: Is it foundational (→ concepts/) or problem-specific (→ design/)?
```

---

## Naming Conventions

Use **snake_case** for file names:

✅ **Good:**
- `PHASE_0.md`
- `NPC_COMMUTING.md`
- `EXPECT_ENGINE.md`
- `TESTING_STRATEGY.md`

❌ **Bad:**
- `Phase-0.md` (use PHASE_0)
- `IMPLEMENTATION-PLAN-THING.md` (too long, inconsistent)
- `phase_0.md` (capitalize subsystem names)

**Special case**: Top-level process files are uppercase without underscores:
- `PROCESS.md`
- `PROCESS_TALE.md`
- `PROCESS_DOCS.md`

---

## Document Conventions

### Every document should have:

1. **Title** (H1, `# Title`)
   ```markdown
   # TALE Phase 5: Escalation System
   ```

2. **Status line** (for work-in-progress docs)
   ```markdown
   **Status**: 📋 Proposed | 🔨 In Progress | ✅ Complete
   **Last Updated**: 2026-04-10
   ```

3. **Purpose statement** (1-2 sentences)
   ```markdown
   Defines the escalation mechanic for NPC conflict spirals.
   ```

4. **Clear sections** (use H2/H3 hierarchy)

5. **Cross-references** to related documents
   ```markdown
   See also: docs/tale/phases/PHASE_4.md, docs/tale/design/NPC_STORIES.md
   ```

### Phase documentation (docs/tale/phases/PHASE_N.md) should include:

- Phase goals and objectives
- Key systems/classes affected
- Test coverage (reference to docs/tale/tests/PHASE_N.md)
- Known limitations or future work

### Test documentation (docs/tale/tests/PHASE_N.md) should include:

- Test count and organization
- Test categories or groupings
- Expected outcomes per test
- Links to actual test files (models/tests/tale/phaseN-*/)

### Design documents (docs/tale/design/THING.md) should include:

- Problem statement
- Design approach/solution
- Trade-offs considered
- References to phases or systems that implement this

---

## Migration Guide (Current → Proposed)

This shows where existing documents will move:

### From docs/ root → docs/SYSTEMS/ or docs/ARCHITECTURE/

| Current | New |
|---------|-----|
| `RENDERING_ARCHITECTURE.md` | `docs/ARCHITECTURE/RENDERING.md` |
| `LSYSTEM_EDITOR_PLAN.md` | `docs/SYSTEMS/WORLD_GEN/LSYSTEM_EDITOR.md` |
| `LSYSTEM_FEATURES.md` | `docs/SYSTEMS/WORLD_GEN/LSYSTEM.md` |
| `BIRD_SWARM_FRAGMENT_OPERATOR.md` | `docs/SYSTEMS/WORLD_GEN/FRAGMENT_OPERATORS.md` |
| `ANDROID_KEYBOARD.md` | `docs/SYSTEMS/PLATFORMS/ANDROID.md` |
| `LITEDB_STORAGE.md` | `docs/SYSTEMS/PERSISTENCE/LITEDB.md` |
| `EXPECT_ENGINE_CONCEPT.md` | `docs/SYSTEMS/NARRATION/EXPECT_ENGINE.md` |
| `EXPECT_ENGINE_IMPLEMENTATION.md` | `docs/SYSTEMS/NARRATION/EXPECT_IMPLEMENTATION.md` |
| `QUEST_REFACTOR.md` | `docs/SYSTEMS/QUEST/QUEST_SYSTEM.md` |

### From docs/ root → docs/TESTING/

| Current | New |
|---------|-----|
| `TESTING.md` | `docs/TESTING/TESTING_STRATEGY.md` |
| `TESTBED_PLAN.md` | `docs/TESTING/TESTBED.md` |
| `TESTING_TIERS_PROPOSAL.md` | `docs/TESTING/TIERS.md` |

### From docs/ root → docs/tale/

| Current | New |
|---------|-----|
| `NPC_STORIES_DESIGN.md` | `docs/tale/design/NPC_STORIES.md` |
| `STRATEGY_ARCHITECTURE.md` | `docs/tale/design/STRATEGY_ARCHITECTURE.md` |

### From docs/tale/ → docs/tale/phases/

| Current | New | Note |
|---------|-----|------|
| `PHASE_0A_TESTBED_INFRA.md` | `docs/tale/phases/PHASE_0A.md` | Flatten names |
| `PHASE_0B_DES_ENGINE.md` | `docs/tale/phases/PHASE_0B.md` | |
| `PHASE_1_STORIES.md` | `docs/tale/phases/PHASE_1.md` | |
| ... | ... | |

### From docs/tale/ → docs/tale/tests/

| Current | New |
|---------|-----|
| `TALE_TEST_SCRIPTS_PHASE_0.md` | `docs/tale/tests/PHASE_0.md` |
| `TALE_TEST_SCRIPTS_PHASE_1.md` | `docs/tale/tests/PHASE_1.md` |
| ... | ... |
| `TEST_DOCUMENTATION_STRATEGY.md` | `docs/tale/tests/STRATEGY.md` |
| `TESTING_QUICK_START.md` | `docs/tale/tests/QUICK_START.md` |

### From docs/tale/ → docs/tale/concepts/

| Current | New |
|---------|-----|
| `ROLE_ABSTRACTION_PROPOSAL.md` | `docs/tale/concepts/ROLES.md` |
| `INTERACTION_TYPE_ABSTRACTION_PROPOSAL.md` | `docs/tale/concepts/INTERACTIONS.md` |
| `TALE_CONCEPT.md` | Keep as-is or split into phases/concepts |

### From docs/tale/ → docs/tale/design/

| Current | New |
|---------|-----|
| `NPC_COMMUTING_DESIGN_CHALLENGE.md` | `docs/tale/design/NPC_COMMUTING.md` |

### Keep as-is

| File | Reason |
|------|--------|
| `docs/tale/OVERVIEW.md` | Already well-placed |
| `docs/tale/ARCHITECTURE.md` | Already well-placed |
| `docs/tale/REFERENCE.md` | Already well-placed |
| `docs/tale/NARRATION.md` | Concept doc, move to concepts/ |
| `docs/roadmap/` | Already well-organized |

---

## How to Create a New Document

1. **Decide where it belongs** using the decision tree above
2. **Create the directory** if it doesn't exist (e.g., `docs/SYSTEMS/NEW_SYSTEM/`)
3. **Use snake_case naming**: `DOCUMENT_NAME.md`
4. **Add the required sections**:
   - H1 title
   - Status line (if applicable)
   - Purpose statement
   - Content with clear H2/H3 structure
   - Cross-references
5. **Update the appropriate index**:
   - If in `docs/tale/`, update `docs/tale/README.md`
   - If in `docs/SYSTEMS/`, consider an index for that subsystem
6. **Commit with doc**: `Docs: Add DOCUMENT_NAME to docs/PATH/`

---

## Index Files

Each major subdirectory should have a README.md that serves as an index:

### docs/tale/README.md

```markdown
# TALE Narrative System Documentation

**Quick Links:**
- [Overview](OVERVIEW.md) — System overview
- [Architecture](ARCHITECTURE.md) — System design
- [Phases](phases/) — Phase documentation (0-8, C1-C4, D)
- [Tests](tests/) — Test specs and strategy
- [Design](design/) — Problem analysis and solutions
- [Concepts](concepts/) — Foundational concepts

**For new developers:**
1. Read [OVERVIEW.md](OVERVIEW.md)
2. See [../PROCESS_TALE.md](../PROCESS_TALE.md) for development process
3. Pick a phase: [phases/PHASE_0.md](phases/PHASE_0.md)
```

### docs/SYSTEMS/README.md (future)

Similar structure for other subsystems as they grow.

---

## Quarterly Review

Like the TALE test documentation audit, run a **doc structure audit** quarterly:

**Checklist:**
- [ ] No documents in docs/ root that should be in subsystem folders
- [ ] Naming conventions followed (snake_case, no hyphens for subsystems)
- [ ] Cross-references between docs are valid
- [ ] Index files (README.md) are up-to-date
- [ ] Status lines show current state (not stale)
- [ ] Orphaned docs identified and classified
- [ ] New documents in right place

**Commit:** `Docs: Quarterly structure audit [date]`

---

## Anti-Patterns to Avoid

❌ **Don't:**
- Mix planning docs with finished design docs (separate into roadmap/ vs. docs/)
- Create a new subsystem folder for one-off documents (keep in root until pattern emerges)
- Use inconsistent naming (phase_0.md vs. PHASE_0.md vs. PHASE-0.md)
- Leave status lines stale ("Proposed" when work is complete)
- Create nested subdirectories deeper than 2-3 levels (gets hard to navigate)

✅ **Do:**
- Use this decision tree for placement
- Keep H1/H2 hierarchy consistent
- Link to related documents
- Update status lines when work completes
- Consolidate related docs (one concepts/ folder vs. scattered proposals)

---

## FAQ

**Q: Where should I put a design document about a feature that might become a phase?**
A: In `docs/roadmap/proposed/` until it's approved, then move to `docs/tale/design/` or `docs/tale/phases/` when implementation starts.

**Q: What if a document relates to multiple subsystems?**
A: Place it in the primary subsystem, cross-reference from others. Example: a document about NPC AI primarily lives in `docs/tale/` but references `docs/ARCHITECTURE/AI.md` if one exists.

**Q: Should I keep old/superseded docs?**
A: Keep them in their category but mark as "Superseded by [new doc]" at the top. Useful for history.

**Q: How deeply should I nest subdirectories?**
A: Maximum 3 levels (docs/SUBSYSTEM/category/doc.md). Beyond that, it's hard to navigate.

---

## Document History

- **2026-04-10**: Created initial PROCESS_DOCS.md with proposed structure
