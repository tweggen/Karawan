# TALE Development Process (TALE-Specific)

This document supplements `PROCESS.md` with TALE-specific paths, test organization, and documentation structure.

**Reference this when:**
- Adding a new TALE test
- Modifying TALE narrative or simulation code
- Updating TALE documentation
- Working with phases (Phase 0-8, C1-C4, etc.)

---

## TALE Test Organization

### Test Tiers

Run tests at different levels depending on your change:

**Before Committing (Quick Validation)**:
```bash
./run_tests.sh smoke  # ~1 minute
# 10 critical tests from smoke manifest
```

**Before Pushing (Standard Regression)**:
```bash
./run_tests.sh all    # ~5 minutes (same as 'standard')
# All 192 tests with 60-day simulations
# Currently: Phase 0-6 (183) + C1-C4 (29) = 212 total test files
# See docs/TESTING/TESTING_STRATEGY.md for full list
```

**Pre-Merge (Full Regression)**:
```bash
./run_tests.sh full   # ~15-20 minutes
# All tests with 120-day simulations
```

**Parameter Tuning (Recalibration)**:
```bash
./run_recalibration_tests.sh phaseN  # 30 min - 2 hours
# 365+ day simulations for equilibrium validation
```

### Test File Structure

```
models/tests/tale/
├── phase0-des/              # 20 tests
├── phase1-storylets/        # 20 tests
├── phase2-strategies/       # 20 tests
├── phase3-interactions/     # 22 tests
├── phase4-player/           # 20 tests
├── phase5-escalation/       # 20 tests
├── phase6-population/       # 49 tests
├── phaseC1-infrastructure/  # 8 tests
├── phaseC2-storylet/        # 6 tests
├── phaseC3-tone/            # 6 tests
├── phaseC4-trust/           # 9 tests
└── (other phases)
```

Each test is a JSON file: `NN-descriptive-name.json`

### Running Specific Tests

```bash
./run_tests.sh phase0       # Run all Phase 0 tests
./run_tests.sh phase5       # Run all Phase 5 tests
./run_tests.sh phaseC1      # Run all C1 tests
./run_tests.sh 01-init.json # Run specific test (finds it anywhere in models/tests/tale/)
```

---

## TALE Documentation Paths

### Design & Architecture

```
docs/tale/
├── PHASE_0.md                          # Phase 0: DES Engine
├── PHASE_1.md                          # Phase 1: Storylets
├── PHASE_2.md                          # Phase 2: Strategies
├── PHASE_3.md                          # Phase 3: Interactions
├── PHASE_4.md                          # Phase 4: Quests
├── PHASE_5.md                          # Phase 5: Escalation
├── PHASE_6.md                          # Phase 6: Population
├── PHASE_7.md                          # Phase 7: Spatial Grounding
├── PHASE_7B.md                         # Phase 7B: Building Role Tagging
├── PHASE_8.md                          # Phase 8: Realistic Occupations
├── PHASE_C.md                          # Phase C: NPC Conversations
├── TEST_DOCUMENTATION_STRATEGY.md      # How to maintain test docs (quarterly audit cycle)
├── TALE_TEST_SCRIPTS_PHASE_N.md        # Detailed test specifications per phase
├── TESTING_QUICK_START.md              # Quick reference for test commands
└── README.md                           # Overview
```

### When to Update Which Docs

| Change | File(s) to Update |
|--------|-------------------|
| New TALE phase | Create `docs/tale/phases/PHASE_N.md` |
| Phase design/architecture changes | Update `docs/tale/phases/PHASE_N.md` |
| New test cases | Update `docs/tale/phases/PHASE_N.md` + (quarterly) `docs/tale/tests/PHASE_N.md` |
| Test infrastructure changes | Update `docs/TESTING/TESTING_STRATEGY.md` + `docs/tale/tests/STRATEGY.md` |
| Overall TALE status | Update `CLAUDE.md` section on TALE phases |

---

## Common TALE Tasks

### Adding a Test to Phase N

```bash
1. Create: models/tests/tale/phaseN-*/NN-descriptive-name.json
2. Update: docs/tale/phases/PHASE_N.md (add test to "Test Coverage" section)
3. Run: ./run_tests.sh phaseN
4. Commit: "Add test Phase N: Description"
5. (Quarterly) Update: docs/tale/tests/PHASE_N.md (detailed spec)
```

### Implementing a New TALE Phase

```bash
1. EnterPlanMode: Create docs/roadmap/proposed/PHASE-N-NAME.md
2. Create: docs/tale/phases/PHASE_N.md (design document)
3. Create: models/tests/tale/phaseN-*/  (test directory)
4. Create: docs/tale/tests/PHASE_N.md (test specifications)
5. Implement code + tests
6. Update: docs/TESTING/TESTING_STRATEGY.md (add phase to test phases table)
7. Update: CLAUDE.md (update phase status)
8. Move: docs/roadmap/proposed/ → docs/roadmap/done/
9. Commit: "Implement Phase N: Description"
```

### Modifying an Existing Phase

```bash
1. Make code changes
2. Update: docs/tale/phases/PHASE_N.md (if design changed)
3. Add/modify tests in: models/tests/tale/phaseN-*/
4. Run: ./run_tests.sh phaseN
5. Update: docs/TESTING/TESTING_STRATEGY.md (if test counts changed)
6. Commit: "Update Phase N: Description"
```

---

## TALE-Specific Test Configuration

### Environment Variables

```bash
# Simulation duration (days) — adjustable for different test tiers
TALE_SIM_DAYS=10    # Smoke tests
TALE_SIM_DAYS=60    # Standard regression (default)
TALE_SIM_DAYS=120   # Full regression
TALE_SIM_DAYS=365   # Recalibration
```

### Test Timeout

Default timeout: 60 seconds per test (60-day simulation)
- Smoke: ~2-5 seconds
- Standard: ~2-5 seconds
- Full: ~5-10 seconds
- Recalibration: ~30-120 seconds

---

## Documentation Audit Cycle (Quarterly)

The TALE system maintains detailed test documentation that needs periodic refresh. See `docs/tale/tests/STRATEGY.md` for:

- How to verify test counts match actual files
- Cross-reference validation
- Examples and consistency checks
- Automation scripts

**Quarterly tasks:**
```bash
1. Verify test counts: models/tests/tale/phaseN-*/*.json vs TALE_TEST_SCRIPTS_PHASE_N.md
2. Check cross-references in PHASE_N.md point to valid docs
3. Run TESTING.md commands to verify they're accurate
4. Update metrics if they've drifted
5. Commit: "Docs: Quarterly audit for Phase N"
```

---

## Key Files to Understand

| File | Purpose |
|------|---------|
| `TestRunner/` | Headless test runner (C#) |
| `run_tests.sh` | Bash script that invokes TestRunner |
| `models/tests/tale/` | All test specifications (JSON) |
| `docs/TESTING.md` | Generic testing strategy (all subsystems) |
| `docs/tale/tests/STRATEGY.md` | TALE-specific test maintenance |

---

## Phase Summary

| Phase | Domain | Tests | Status |
|-------|--------|-------|--------|
| 0 | DES Engine | 20 | ✅ Complete |
| 1 | Storylets | 20 | ✅ Complete |
| 2 | Strategies | 20 | ✅ Complete |
| 3 | Interactions | 22 | ✅ Complete |
| 4 | Quests | 20 | ✅ Complete |
| 5 | Escalation | 20 | ✅ Complete |
| 6 | Population | 49 | ✅ Complete |
| 7 | Spatial Grounding | (integrated into 0-6) | ✅ Complete |
| 7B | Building Role Tagging | (integrated) | ✅ Complete |
| 8 | Occupations | (integrated) | ✅ Complete |
| C1 | Conversation Infrastructure | 8 | ✅ Complete |
| C2 | Storylet-Specific Dialogue | 6 | ✅ Complete |
| C3 | Mood/Tone Branches | 6 | ✅ Complete |
| C4 | Trust, Memory & Hooks | 9 | ✅ Complete |

**Total TALE tests: 212 test files**
**Standard regression suite: 192 tests (60-day simulations)**
