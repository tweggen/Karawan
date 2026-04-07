# Development Process Guide

This document defines the standard process for making changes to the Karawan TALE system, ensuring documentation stays synchronized with implementation and enabling efficient handoff between Claude Code instances.

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
- Key systems (DES, storylets, interactions, etc.)
- Configuration system or registries

#### b) **docs/TESTING.md** — If changes affect:
- Test runner scripts (`run_tests.sh`, `run_recalibration_tests.sh`)
- Test framework or assertion types
- Simulation configuration (TALE_SIM_DAYS, timeouts, etc.)
- Phase test counts or organization
- Performance metrics or benchmarks

#### c) **docs/TESTBED_PLAN.md** — If changes affect:
- Overall testing strategy or tiers
- Headless simulation infrastructure
- Performance expectations
- Integration with testing workflow

#### d) **docs/tale/TEST_DOCUMENTATION_STRATEGY.md** — If changes affect:
- How tests are specified (JSON format, examples)
- Documentation maintenance process
- Cross-reference patterns
- Quarterly audit cycle

#### e) **docs/tale/PHASE_N.md** — If changes affect:
- Phase design or architecture
- Key classes, methods, or data structures
- Test coverage for the phase
- Known limitations or future work

#### f) **docs/tale/TALE_TEST_SCRIPTS_PHASE_N.md** — If changes affect:
- Test specifications (preconditions, expected outcomes)
- New test categories or significant additions
- **Note**: Keep quaternary update cycle; don't update for every test addition

#### g) **Other docs** — Update any referenced documentation:
- `docs/tale/TESTING_QUICK_START.md` if quick-start commands change
- `models/tests/tale/phaseN-*/README.md` if test organization changes
- Inline code comments (sparingly, only where logic isn't obvious)

**Documentation Update Checklist**:
- [ ] Searched for all references to changed systems/files
- [ ] Updated all affected documentation files
- [ ] Verified cross-references are accurate
- [ ] Checked for outdated examples or commands
- [ ] Updated test counts and metrics if applicable

### 4. Testing Phase

**Pre-Commit (Quick Validation)**:
- **Smoke Tests**: `./run_tests.sh smoke` (~1 minute) — Run before committing locally
  - 10 critical tests validating core initialization and state machines
  - Catches most catastrophic failures with rapid feedback

**Before Pushing to Shared Branch**:
- **Standard Regression**: `./run_tests.sh standard` or `./run_tests.sh all` (~5 minutes)
  - All 171 tests with 60-day simulations
  - Full functional validation, must pass before pushing
- **Affected Phase** (optional): `./run_tests.sh phaseN` — Quick focus on changed system

**Before Merge/Release**:
- **Full Regression**: `./run_tests.sh full` (~15-20 minutes)
  - All 171 tests with 120-day simulations
  - Deeper behavior coverage, recommended pre-merge
- **Recalibration** (if parameter changes): `./run_recalibration_tests.sh phaseN` (~30 min-2 hours)
  - Long-term equilibrium validation for parameter tuning

See `docs/TESTING_TIERS_PROPOSAL.md` for full multi-tier testing strategy.

### 5. Commit & Cleanup

When all changes and documentation are complete:

- Run `git status` to verify expected changes
- Create commit with clear message:
  ```
  <action> <system>: <brief description>

  - Specific change 1
  - Specific change 2
  - Updated docs/TESTING.md, docs/tale/PHASE_N.md
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
- Keep PROCESS.md synchronized if workflow changes
- Quarterly audit: Run `docs/tale/TEST_DOCUMENTATION_STRATEGY.md` consistency checks
- When in doubt about what to update, update more rather than less

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
- Update `docs/tale/PHASE_*.md` for design changes
- Update `docs/TESTING.md` if test infrastructure changes
- Verify commit messages reference doc updates

### Monthly (Quick Review)
- Scan `docs/TESTING.md` for outdated commands
- Check `docs/tale/TEST_DOCUMENTATION_STRATEGY.md` for drift
- Update metrics if they've changed significantly

### Quarterly (Major Audit)
- Full audit of `docs/tale/TALE_TEST_SCRIPTS_PHASE_*.md` against actual tests
- Verify cross-references between design/spec/implementation
- Update test counts, categories, examples
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
- [ ] `docs/TESTING.md` updated (if applicable)
- [ ] `docs/tale/PHASE_*.md` updated (if applicable)
- [ ] `docs/tale/TEST_DOCUMENTATION_STRATEGY.md` updated (if applicable)
- [ ] Other affected documentation updated (search for references)
- [ ] Plan file moved from proposed/ to done/ (if applicable)
- [ ] Smoke tests passing: `./run_tests.sh smoke` (~1 min)
- [ ] Standard regression tests passing: `./run_tests.sh all` (~5 min)
- [ ] Affected phase tests passing: `./run_tests.sh phaseN` (optional)
- [ ] No debug code, console logs, or commented-out code
- [ ] No unnecessary files created
- [ ] `git status` shows only expected changes
- [ ] Commit message is clear, references docs changes
- [ ] Documentation changes included in commit

---

## Common Scenarios

### Adding a New Test

```
1. Create JSON in models/tests/tale/phaseN-*/NN-name.json
2. Update docs/tale/PHASE_N.md (add to design section)
3. Run: ./run_tests.sh phaseN
4. Commit: "Add test PhaseN: Description"
5. (Quarterly) Update docs/tale/TALE_TEST_SCRIPTS_PHASE_N.md
```

### Modifying Test Infrastructure

```
1. Update run_tests.sh or TestRunner code
2. Update docs/TESTING.md (usage, configuration)
3. Run: ./run_tests.sh all
4. Update docs/TESTBED_PLAN.md if architecture changes
5. Commit: "Update: Test infrastructure changes"
```

### Implementing a New Phase

```
1. EnterPlanMode: Create docs/roadmap/proposed/PHASE-N-*.md
2. Create docs/tale/PHASE_N.md (design)
3. Create models/tests/tale/phaseN-*/
4. Create docs/tale/TALE_TEST_SCRIPTS_PHASE_N.md (detailed specs)
5. Implement code + tests
6. Update docs/TESTING.md (add phase to summary)
7. Update CLAUDE.md (project status)
8. Move plan: proposed/ → done/
9. Commit: "Implement Phase N: ..."
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
3. **Audit**: Quarterly review cycle in TEST_DOCUMENTATION_STRATEGY.md — catches drift
4. **Automation**: Scripts in TEST_DOCUMENTATION_STRATEGY.md — verify consistency

**When documentation gets out of sync**:

- Quarterly audit catches it (TEST_DOCUMENTATION_STRATEGY.md)
- Consistency checks can be automated (verify test counts, cross-references)
- Clear process means next developer knows what to fix
- Commit history shows when docs were last updated

**Rule of Thumb**:
- If you're unsure whether to update a doc, **do update it**
- Better to have redundant info than missing updates
- Errors in docs get caught in quarterly audits
- Errors in code get caught in tests

---

## Examples

### Good Commit Message
```
Implement Phase 8: Reputation System

- Added RepuationTracker registry for configurable reputation decay
- Integrated with encounter postconditions
- Created 20 new test scripts for Phase 8
- Updated docs/tale/PHASE_8.md with design
- Updated docs/TESTING.md with phase summary
- All 191 regression tests passing

See docs/roadmap/done/PHASE-8-REPUTATION-SYSTEM.md
```

### Good Documentation Update
```
1. Code change: Add new parameter to RoleDefinition
2. Update: JoyceCode/engine/tale/RoleDefinition.cs (code)
3. Update: docs/tale/PHASE_1.md (add to class docs)
4. Update: docs/TESTING.md (if affects tests)
5. Update: CLAUDE.md (if architectural)
6. Commit: "Feature: Add parameter to RoleDefinition"
```

### Good Quarterly Audit

```
- Check: Test counts in TALE_TEST_SCRIPTS_PHASE_*.md match models/tests/tale/
- Check: Cross-references between PHASE_*.md and test specs are valid
- Check: TESTING.md commands are accurate (run examples)
- Check: TESTBED_PLAN.md reflects current testing strategy
- Fix: Update any outdated sections
- Commit: "Docs: Quarterly audit and updates"
```

---

## Document History

- **2026-03-17**: Created initial PROCESS.md
- **Consolidated**: (This date will be updated quarterly)
