# Multi-Tier Testing Strategy Proposal

**Status:** 📋 Proposed
**Created:** 2026-04-07
**Purpose:** Enable rapid feedback cycles during development while maintaining comprehensive validation

---

## Current State

- **Regression Tests**: ~5 minutes (171 tests, 60-day simulations)
- **Recalibration Tests**: ~2-4 hours (365-day simulations)
- **Gap**: No fast smoke test for rapid iteration

---

## Parallel Runner (2026-07-14)

`./run_tests_parallel.sh` accepts the same tier/phase/script filters as
`run_tests.sh` but fans TestRunner processes out across all CPU cores
(worker: `tools/run_one_tale_test.sh`). Each test is an independent process,
so no engine-side isolation is required. Measured on a 10-core machine:
smoke 12s → 4s, standard ~5min → ~28s, identical pass sets.

Environment knobs:

| Variable | Default | Meaning |
|----------|---------|---------|
| `JOYCE_TEST_JOBS` | `hw.ncpu` | Concurrent test processes |
| `TEST_TIMEOUT` | 60 | Per-test timeout (s), forwarded as `JOYCE_TEST_TIMEOUT` to TestRunner |
| `JOYCE_TEST_TIMEOUT_SCALE` | 1.0 | Stretches in-script expect timeouts on saturated machines |
| `JOYCE_TEST_SLEEP_SCALE` | 0.1 | Multiplier for in-script `"sleep"` steps (the DES sim runs at CPU speed; sleeps only delay reading buffered events). Set 1.0 for original pacing |
| `JOYCE_TURBO` | unset | `1` = engine logical thread free-runs with fixed dt=1/60 instead of wall-clock 60 Hz. Opt-in; only useful for frame-driven tests, pins one core per process |

Results land in a per-run `mktemp -d /tmp/tale_tests.XXXXXX` directory:
one `.log` + `.status` per test, failing log tails in `failures.txt`.

Supporting changes (all backward compatible; `run_tests.sh` still works):
- `TestDriverModule` exposes `SessionReady` / `Completed` + `ExitCode` events;
  TestRunner waits on those instead of `Thread.Sleep(500)` / `Sleep(60000)`
  (~2.5 s fixed overhead removed per test).
- `ScriptRunner` honors `JOYCE_TEST_TIMEOUT_SCALE` / `JOYCE_TEST_SLEEP_SCALE`.
- `Engine._logicalThreadFunction` has an opt-in turbo branch
  (`engine.Turbo=true` GlobalSetting).

---

## Proposed Multi-Tier Testing Pyramid

```
TIER 4: EXTENDED (2-4 hours)
═════════════════════════════════════════════════════════════
• All phases: 365-day recalibration tests
• Equilibrium, emergence, long-term stability
• When: Nightly builds, before release, param tuning

TIER 3: FULL (15-20 minutes)
═════════════════════════════════════════════════════════════
• All 171 regression tests, 120-day simulations
• Comprehensive coverage, emergent behavior checks
• When: Before feature branch merge, pre-commit CI

TIER 2: STANDARD (5 minutes)
═════════════════════════════════════════════════════════════
• All 171 regression tests, 60-day simulations
• Core functional validation (current implementation)
• When: Post-commit CI, before PR approval

TIER 1: SMOKE (1 minute)
═════════════════════════════════════════════════════════════
• 10 critical tests, 10-day simulations
• Sanity check, quick feedback loop
• When: Local dev, quick validation before pushing
```

---

## Tier Specifications

### Tier 1: SMOKE TEST (~1 minute)

**Purpose:** Rapid sanity check during active development
**Invocation:** `./run_tests.sh smoke`

**Test Selection (10 critical tests):**
```
Phase 0 (DES Engine):
  - phase0-des/01-initialization.json
  - phase0-des/02-property-tracking.json

Phase 1 (Storylet System):
  - phase1-storylets/01-library-loading.json
  - phase1-storylets/02-fallback-selection.json

Phase 2 (Strategy System):
  - phase2-strategies/01-simple-strategy.json

Phase 3 (Interaction System):
  - phase3-interactions/01-request-lifecycle.json

Phase 4 (Quest/Player):
  - phase4-player/01-quest-trigger.json

Phase 5 (Escalation/Interrupts):
  - phase5-escalation/01-interrupt-nest-scope.json

Phase 6 (Population Management):
  - phase6-population/01-tier1-generation.json
  - phase6-population/02-tier1-persistence.json
```

**Configuration:**
- Simulation duration: 10 days per test
- Timeout per test: 30-40 seconds
- Total time: ~1-2 minutes
- Purpose: Verify core initialization, no catastrophic failures

---

### Tier 2: STANDARD REGRESSION (~5 minutes)

**Purpose:** Pre-commit validation, CI pipeline
**Invocation:** `./run_tests.sh all` or `./run_tests.sh standard`

**Test Selection:** All 171 tests (phases 0-6)
**Configuration:**
- Simulation duration: 60 days per test
- Timeout per test: 65 seconds
- Total time: ~5 minutes
- Purpose: Functional validation of all systems

**Status:** ✅ Current implementation — no changes required

---

### Tier 3: FULL REGRESSION (~15-20 minutes)

**Purpose:** Pre-merge validation, deeper behavior coverage
**Invocation:** `./run_tests.sh full` or `TALE_SIM_DAYS=120 ./run_tests.sh all`

**Test Selection:** All 171 tests (phases 0-6)
**Configuration:**
- Simulation duration: 120 days per test
- Timeout per test: 130-150 seconds
- Total time: ~15-20 minutes
- Purpose: Verify emergent behavior over longer horizon

**Implementation:**
- Add `full` filter to `run_tests.sh`
- When `full` filter is used, override `TALE_SIM_DAYS=120`
- All other logic identical to standard tier

---

### Tier 4: EXTENDED / RECALIBRATION (~2-4 hours)

**Purpose:** Equilibrium validation, emergent structures, parameter verification
**Invocation:** `./run_recalibration_tests.sh full` or `./run_recalibration_tests.sh all`

**Test Selection:** Phase-specific recalibration suites
- Phase 4: Quest patterns, completion rates
- Phase 5: Gang formation, escalation chains
- Phase 6: Population equilibrium, role distribution
- Phase 7: Movement patterns, location utilization
- (Phase 7B, 8 when stable)

**Configuration:**
- Simulation duration: 365 days per test
- Timeout per test: 90-180 seconds (varies by phase)
- Total time: ~2-4 hours
- Purpose: Verify long-term equilibrium and emergence

**Status:** ✅ Current implementation (`run_recalibration_tests.sh`) — no changes required

---

## Development Workflow

### RECOMMENDED: Local Development (Rapid Iteration)
```bash
# Quick sanity check during development
./run_tests.sh smoke
# → 1 minute

# BEFORE COMMITTING (pre-commit hook / manual)
./run_tests.sh smoke
# → 1 minute — catches 95% of issues with rapid feedback

# After commit, before pushing to shared branch
./run_tests.sh standard
# → 5 minutes
```

**Note:** Smoke tests are recommended as the **standard pre-commit validation**. This workflow is documented in `PROCESS.md`.

### Pre-Push Validation
```bash
# Full validation before pushing to shared branch
./run_tests.sh full
# → 15-20 minutes
```

### Nightly / Pre-Release
```bash
# Deep equilibrium validation
./run_recalibration_tests.sh full
# → 2-4 hours
```

---

## Implementation Tasks

### Phase 1: Create Smoke Test Manifest
1. Create `models/tests/tale/.smoke-tests` file
   - 10-line file listing critical test paths (one per line)
   - No JSON, just paths relative to `models/tests/tale/`

### Phase 2: Modify run_tests.sh
1. Add `smoke` filter handling:
   ```bash
   elif [ "$FILTER" = "smoke" ]; then
       read_manifest "models/tests/tale/.smoke-tests"
       TALE_SIM_DAYS=10 run_filtered_tests "$MANIFEST"
   ```

2. Add `full` filter handling:
   ```bash
   elif [ "$FILTER" = "full" ]; then
       TALE_SIM_DAYS=120 run_all_tests
   ```

3. Update help text and comments

### Phase 3: Optional Helper Scripts
1. Create convenience scripts:
   - `./run_smoke_tests.sh` → `./run_tests.sh smoke`
   - `./run_full_tests.sh` → `./run_tests.sh full`

### Phase 4: Update Documentation
1. Update `docs/TESTING.md`:
   - Add "Testing Tiers" section
   - Add workflow examples
   - Link to this proposal

2. Update `CLAUDE.md`:
   - Add to testing workflow section

3. Create `.github/workflows/ci.yml` (if applicable):
   - Smoke tier on PR
   - Standard on commit
   - Full on merge queue
   - Extended nightly

---

## Expected Timing (on fast machine: 4-core i7, 16GB RAM)

| Tier | Tests | Days | Per-Test | Total | Use Case |
|------|-------|------|----------|-------|----------|
| **Smoke** | 10 | 10 | ~8-10s | **1-2 min** | Local dev, rapid iteration |
| **Standard** | 171 | 60 | ~2-4s | **5-7 min** | Pre-commit CI, PR checks |
| **Full** | 171 | 120 | ~7-10s | **15-20 min** | Pre-merge, feature branch |
| **Extended** | 40-50 | 365 | ~30-60s | **2-4 hours** | Nightly, release validation |

---

## CI/CD Integration Example

```yaml
# .github/workflows/test.yml (suggested)
name: TALE Tests

on: [push, pull_request]

jobs:
  smoke:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - uses: actions/checkout@v3
      - name: Build TestRunner
        run: dotnet build TestRunner/TestRunner.csproj -c Release
      - name: Smoke Tests
        run: ./run_tests.sh smoke

  standard:
    runs-on: ubuntu-latest
    timeout-minutes: 15
    needs: smoke
    if: success()
    steps:
      - uses: actions/checkout@v3
      - name: Build TestRunner
        run: dotnet build TestRunner/TestRunner.csproj -c Release
      - name: Standard Regression
        run: ./run_tests.sh standard

  full:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    if: github.event_name == 'push' && github.ref == 'refs/heads/master'
    steps:
      - uses: actions/checkout@v3
      - name: Build TestRunner
        run: dotnet build TestRunner/TestRunner.csproj -c Release
      - name: Full Regression
        run: ./run_tests.sh full

  extended:
    runs-on: ubuntu-latest
    timeout-minutes: 300
    if: github.event_name == 'workflow_dispatch' || github.event_name == 'schedule'
    steps:
      - uses: actions/checkout@v3
      - name: Build TestRunner
        run: dotnet build TestRunner/TestRunner.csproj -c Release
      - name: Extended Recalibration
        run: ./run_recalibration_tests.sh full
```

---

## Design Rationale

### Why 10-Day Smoke Tests?
- Sufficient to initialize DES engine, place NPCs, trigger 1-2 storylets
- Below threshold for emergent behavior (gangs, escalations)
- Each test runs in ~8-10 seconds
- Catches initialization bugs, crashes, basic state machine failures

### Why 120-Day Full Tests?
- Double standard duration without doubling cost (not linear)
- Captures 2-month emergence window
- Enough for gangs to form, some escalations to fire
- Still completes in 15-20 minutes

### Why Keep 60-Day Standard?
- Current baseline, proven reliable in CI/CD
- Sufficient for functional validation
- Fast enough for pre-commit gates
- Well-calibrated for system maturity

### Why Preserve 365-Day Extended?
- True equilibrium behavior (1 year)
- Population demographics stabilize
- Economic patterns emerge
- Necessary for parameter tuning

---

## Open Questions for Implementation

1. **Smoke test selection** — Are these 10 tests the right critical path?
   - Could prioritize differently (e.g., population-heavy tests)
   - Feedback welcome

2. **Manifest format** — Keep `.smoke-tests` as simple text list?
   - Alternative: JSON array for metadata (tags, categories)
   - Trade-off: simplicity vs. flexibility

3. **Tier naming** — Prefer smoke/standard/full/extended or other?
   - Alternatives: quick/fast/complete/exhaustive
   - Alternatives: level1/level2/level3/level4

4. **Recalibration phases** — Include 7/7B/8 once stable?
   - Currently excluded (phases 0-6 only in extended)
   - Should expand when those phases mature

5. **Scripting approach** — Bash enhancements or new orchestration?
   - Keep `run_tests.sh` as single source of truth?
   - Or build tooling in TestRunner itself?

---

## Future Extensions

- [ ] Per-developer caching of smoke test results
- [ ] Parallel execution of independent tests (framework limitation today)
- [ ] Benchmarking tier (same tests, measure performance regression)
- [ ] Profiling tier (identify hotspots in Phase 6 population generation)
- [ ] Stress test tier (1000+ NPCs, 5-year simulations)
