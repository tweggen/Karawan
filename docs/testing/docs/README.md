# Testing Infrastructure Documentation

Documentation for the Karawan testing system, which currently focuses on TALE narrative simulation testing.

## Test Documentation

### 📖 **Testing Strategy** — [TESTING_STRATEGY.md](TESTING_STRATEGY.md)
Overall testing approach, simulation duration, test tiers, when to run tests

### 🏗️ **Testbed** — [TESTBED.md](TESTBED.md)
Headless simulation infrastructure, how tests work

### 📊 **Testing Tiers** — [TIERS.md](TIERS.md)
Multi-tier testing strategy proposal (smoke, standard, full, recalibration)

## For TALE-Specific Testing

See [../tale/tests/](../tale/tests/) for:
- Test specifications per phase
- Test documentation strategy & audit cycle
- Quick start guide for running tests

## Common Commands

```bash
# Run tests
./run_tests.sh all          # All 192 tests (60 days)
./run_tests.sh phaseN       # Specific phase
./run_tests.sh smoke        # Quick validation (~1 min)

# For recalibration
./run_recalibration_tests.sh phaseN  # 365+ day simulations
```

## Test Organization

**Location:** `models/tests/tale/phaseN-*/`
**Format:** JSON test specifications
**Framework:** TestRunner.cs (C#, headless)

## Test Tiers

| Tier | Duration | Count | When | Time |
|------|----------|-------|------|------|
| Smoke | 10 days | 10 | Before commit | ~1 min |
| Standard | 60 days | 192 | Before push | ~5 min |
| Full | 120 days | 192 | Pre-merge | ~15-20 min |
| Recalibration | 365+ days | (phase-specific) | Parameter tuning | 30 min - 2 hrs |

## See Also

- [../tale/](../tale/) — TALE narrative system & phases
- [PROCESS_TALE.md](../PROCESS_TALE.md) — TALE development process (includes test commands)
- [PROCESS.md](../PROCESS.md) — Generic development process

---

**Last Updated:** 2026-04-10
