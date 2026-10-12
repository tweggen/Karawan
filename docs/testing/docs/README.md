# Testing Infrastructure Documentation

Documentation for the Karawan testing system, which currently focuses on TALE narrative simulation testing.

## Test Documentation

### 📖 **Testing Strategy** — [TESTING_STRATEGY.md](TESTING_STRATEGY.md)
Overall testing approach, simulation duration, test tiers, when to run tests

### 🏗️ **Testbed** — [TESTBED.md](TESTBED.md)
Headless simulation infrastructure, how tests work

### 📊 **Testing Tiers** — [TIERS.md](../plans/done/TIERS.md)
Multi-tier testing strategy proposal (smoke, standard, full, recalibration)

## For TALE-Specific Testing

See [../../tale/docs/tests/](../../tale/docs/tests/) for:
- Test specifications per phase
- Test documentation strategy & audit cycle

Running the suite: [RUN_TESTS_GUIDE.md](RUN_TESTS_GUIDE.md). The earlier TALE test plan, framework and quick-start docs are archived in [../../tale/archive/](../../tale/archive/) (`TALE_TESTS_*.md`).

### 🧪 **Test driver** — [EXPECT_IMPLEMENTATION.md](EXPECT_IMPLEMENTATION.md) (as built), [EXPECT_ENGINE.md](EXPECT_ENGINE.md) (original proposal)

## Common Commands

```bash
# Run tests
./run_tests.sh all          # All 200 tests (60 days)
./run_tests.sh phaseN       # Specific phase
./run_tests.sh smoke        # Quick validation (~1 min)
./run_tests_parallel.sh all # Same filters, all cores

# For recalibration
./run_recalibration_tests.sh phaseN  # 365+ day simulations (currently does not start — open, tracked separately)
```

CI (`.github/workflows/ci.yml`) runs the xUnit suite `tests/JoyceCode.Tests` only; the TALE suite is run locally.

## Test Organization

**Location:** `models/tests/tale/phaseN-*/`
**Format:** JSON test specifications
**Framework:** `TestRunner/` (C#, headless) + ExpectEngine

## Test Tiers

| Tier | Duration | Count | When | Time |
|------|----------|-------|------|------|
| Smoke | 10 days | 10 | Before commit | ~1 min |
| Standard | 60 days | 200 | Before push | ~5 min |
| Full | 120 days | 200 | Pre-merge | ~15-20 min |
| Recalibration | 365+ days | (phase-specific) | Parameter tuning | 30 min - 2 hrs |

## See Also

- [../../tale/](../../tale/) — TALE narrative system & phases
- [PROCESS_TALE.md](../../tale/docs/PROCESS_TALE.md) — TALE development process (includes test commands)
- [PROCESS.md](../../../PROCESS.md) — Generic development process

---

**Last Updated:** 2026-10-12
