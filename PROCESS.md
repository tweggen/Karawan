# Development Process Guide

This document defines the standard process for making changes to Karawan, ensuring documentation stays synchronized with implementation and enabling efficient handoff between Claude Code instances.

**Note:** This is the generic process for any Karawan subsystem (Joyce engine, rendering, audio, TALE, etc.). For TALE-specific paths and test organization, see `PROCESS_TALE.md`.

---

## Workflow Overview

### 1. Planning Phase

For non-trivial changes (new phases, major refactors, architectural changes):

- Use `EnterPlanMode` to create a structured implementation plan
- Store the plan in `docs/roadmap/proposed/` with descriptive filename
- Plan should include:
  - Clear objectives and success criteria
  - Files that will be modified/created
  - Implementation approach
  - Test strategy and affected test phases
  - Documentation changes needed

**Example**: `docs/roadmap/proposed/PHASE-8-REPUTATION-SYSTEM.md`

### 2. Execution Phase

Before writing code:

- **Read existing code first** — Never propose changes to unread code
- **Keep it minimal** — Only implement what's explicitly requested
- **Follow existing patterns** — Match style, architecture, naming conventions
- **Test as you go** — Verify changes don't break existing tests
- **Use proper tools** — Read/Edit/Write for files, bash only for git/build commands

After implementation completes:

- Move plan file: `docs/roadmap/proposed/` → `docs/roadmap/done/`
- Update any `docs/roadmap/planned/` files if completed

### 3. Documentation Updates (MANDATORY)

**After implementing changes, update documentation in this order:**

#### a) **CLAUDE.md** — If changes affect:
- Architecture or major design decisions
- Build/test commands
- Project phases or completion status
- Key systems or subsystems
- Configuration system or registries

#### b) **Subsystem-specific documentation** — Update design/spec docs for the affected area:
- If modifying **TALE narrative system**: See `PROCESS_TALE.md` for paths
- If modifying **Joyce engine core**: Update `docs/engine/*.md` if they exist
- If modifying **rendering/audio/physics**: Update subsystem-specific docs
- Principle: Keep design docs synchronized with code

#### c) **Testing documentation** — If changes affect:
- Test organization or structure
- Test runner behavior
- Test configuration or timeouts
- Test counts or categories
- Performance metrics or benchmarks

**Rule of thumb:** If you created/modified tests, update the corresponding test documentation. If you changed test infrastructure, update testing strategy docs.

#### d) **Roadmap documentation**:
- Move plan file from `docs/roadmap/proposed/` → `docs/roadmap/done/` when complete
- Update `docs/roadmap/planned/` if scheduling changes

#### e) **Inline code comments** (sparingly):
- Only where logic isn't self-evident
- Don't add comments just to restate the code

**Documentation Update Checklist**:
- [ ] Searched for all references to changed systems/files
- [ ] Updated all affected documentation files
- [ ] Verified cross-references are accurate
- [ ] Checked for outdated examples or commands
- [ ] Updated test counts and metrics if applicable

### 4. Testing Phase

The testing strategy varies by subsystem. For your area:

**TALE Narrative System**: See `PROCESS_TALE.md` for test commands and tier structure.

**Other Subsystems**:
- Run the test suite for the area you modified
- If no test suite exists, verify your changes don't break existing tests
- Update test documentation if you added/modified tests

**General Principle**:
- Before committing: Run tests for the area you changed
- Before pushing: Run the full test suite if available
- If tests fail, fix the issue (never ignore failures)

### 5. Commit & Cleanup

When all changes and documentation are complete:

- Run `git status` to verify expected changes
- Create commit with clear message:
  ```
  <action> <system>: <brief description>

  - Specific change 1
  - Specific change 2
  - Updated docs/TESTING/TESTING_STRATEGY.md, docs/tale/phases/PHASE_N.md
  - All 171 regression tests passing
  - Moved plan: proposed/ → done/

  Co-Authored-By: Claude Haiku 4.5 <noreply@anthropic.com>
  ```
- **Examples**:
  - `Implement Phase 8: Reputation System`
  - `Fix: Cross-cluster encounter probability calculation`
  - `Refactor: RelationshipTracker configuration abstraction`
  - `Update: Test documentation and recalibration suite`

- **Never force push** to main unless explicitly authorized
- **Never commit secrets** (.env, API keys, credentials)

---

## Key Principles

### Documentation Discipline

**Out-of-date documentation is worse than no documentation.**

- Documentation updates are **MANDATORY**, not optional
- Keep documentation synchronized with code changes
- When in doubt about what to update, update more rather than less
- Search for references when changing systems (use Grep tool)

### Testing Discipline

- **Always run regression tests** before committing
- **Always pass all tests** — No "will fix later" commits
- If a test fails, fix the issue or adjust the test (never ignore)
- Document any test timeout/metric changes in commit message

### Code Quality

- Prefer existing patterns in codebase
- Don't add error handling for impossible scenarios
- Don't create utilities for one-time operations
- Trust framework guarantees; only validate at system boundaries

### Tool Usage

- Use `Glob`, `Grep`, `Read` for exploration (not bash `find`, `grep`, `cat`)
- Use `Edit`/`Write` for modifications (not bash `sed`, `awk`, `echo`)
- Reserve bash for terminal operations (git, npm, build, tests)
- For broad exploration, use `Agent` with `subagent_type=Explore`

### File Organization

- Never create files unless absolutely necessary
- Prefer editing existing files
- Follow existing directory structure and naming
- Place roadmap files in `docs/roadmap/proposed/` first

---

## Documentation Maintenance Schedule

### Daily/Per-Commit
- Implement changes
- Run tests
- Update directly affected documentation only

### Weekly (If Multiple Changes)
- Update subsystem design docs for changes
- Update testing docs if test infrastructure changes
- Verify commit messages reference doc updates

### Monthly (Quick Review)
- Scan documentation for outdated commands
- Check design docs for drift from implementation
- Update metrics if they've changed significantly

### Quarterly (Major Audit)
- Audit test documentation against actual tests
- Verify cross-references between design/spec/implementation
- Update test counts and categories
- Review `docs/roadmap/done/` for any outdated files
- Check `PROCESS.md` itself — update if workflow has changed

---

## Roadmap Structure

Plans are organized chronologically and by status:

```
docs/roadmap/
├── proposed/              # Not yet started
│   ├── PHASE-8-REPUTATION-SYSTEM.md
│   └── REFACTOR-ROLE-REGISTRY.md
├── planned/               # Scheduled, in queue
│   └── (usually empty; plans move proposed → done)
└── done/                  # Completed, for reference
    ├── PHASE-0-TESTBED-INFRA.md
    ├── PHASE-1-DES-ENGINE.md
    └── ...
```

When starting work on a plan:
1. It lives in `docs/roadmap/proposed/`
2. During work, reference it in commit messages
3. When complete, move it to `docs/roadmap/done/`

---

## Checklist for Future Claude Instances

Before committing changes, verify:

- [ ] All requested features implemented and tested
- [ ] Code follows existing patterns and style
- [ ] `CLAUDE.md` updated (if applicable)
- [ ] Subsystem design/spec docs updated (if applicable)
- [ ] Test documentation updated (if tests changed)
- [ ] Other affected documentation updated (search for references)
- [ ] Plan file moved from proposed/ to done/ (if applicable)
- [ ] Tests passing for affected subsystem
- [ ] Full test suite passing (if available)
- [ ] No debug code, console logs, or commented-out code
- [ ] No unnecessary files created
- [ ] `git status` shows only expected changes
- [ ] Commit message is clear, references docs changes
- [ ] Documentation changes included in commit

---

## Common Scenarios

### Adding a Test

```
1. Create test file in appropriate test directory for your subsystem
2. Update test documentation (count, description)
3. Run tests for the affected subsystem
4. Commit: "Add test: Description"
5. Update comprehensive docs (quarterly cycle)
```

### Modifying Test Infrastructure

```
1. Update test script or test framework code
2. Update testing documentation
3. Run full test suite
4. Update docs if architecture changes
5. Commit: "Update: Test infrastructure changes"
```

### Implementing a New Feature/System

```
1. EnterPlanMode: Create docs/roadmap/proposed/FEATURE-NAME.md
2. Create/update design documentation
3. Implement code + tests
4. Update CLAUDE.md (project status)
5. Update testing documentation (if new tests added)
6. Move plan: proposed/ → done/
7. Commit: "Implement: Feature description"
```

### Updating Documentation Only

```
1. Update affected doc files
2. Run: git status (verify no code changes)
3. Commit: "Docs: Description of updates"
```

---

## Prevention of Documentation Drift

**The Four-Part System**:

1. **Process**: This document (PROCESS.md) — defines mandatory steps
2. **Discipline**: Documentation updates in commit message — makes intent explicit
3. **Audit**: Quarterly review cycle — catches drift across subsystems
4. **Visibility**: Commit history shows when docs were last updated

**When documentation gets out of sync**:

- Quarterly audit catches it (for TALE: see TEST_DOCUMENTATION_STRATEGY.md)
- Clear process means next developer knows what to fix
- Commit history shows when docs were last updated
- Search for references when changing systems (catches missed docs)

**Rule of Thumb**:
- If you're unsure whether to update a doc, **do update it**
- Better to have redundant info than missing updates
- Errors in docs get caught in audits
- Errors in code get caught in tests

---

## Examples

### Good Commit Message
```
Implement Phase C4: Trust, Memory & Quest Hooks

- Added Trust[-1] tracking for player-NPC relationships
- Injected npc.met_player, npc.trust_player into narration Props
- Added conversation cooldown (30s)
- Created 9 new test scripts for Phase C4
- Updated docs/TESTING/TESTING_STRATEGY.md with phase summary
- All 192 regression tests passing

See docs/roadmap/done/PHASE-C4-TRUST-MEMORY.md
```

### Good Documentation Update
```
1. Code change: Add new property to NpcSchedule
2. Update: JoyceCode/engine/tale/NpcSchedule.cs (code)
3. Update: docs/tale/phases/PHASE_C.md (add to class docs)
4. Update: docs/TESTING/TESTING_STRATEGY.md (if affects tests)
5. Update: CLAUDE.md (if architectural)
6. Commit: "Feature: Add property to NpcSchedule"
```

### Good Quarterly Audit (TALE-specific)

```
- Check: Test counts in TALE_TEST_SCRIPTS_PHASE_*.md match models/tests/tale/
- Check: Cross-references between PHASE_*.md and test specs are valid
- Check: TESTING.md commands are accurate (run examples)
- Check: TEST_DOCUMENTATION_STRATEGY.md reflects current strategy
- Fix: Update any outdated sections
- Commit: "Docs: Quarterly audit and updates"
```

---

## Document History

- **2026-03-17**: Created initial PROCESS.md
- **Consolidated**: (This date will be updated quarterly)
