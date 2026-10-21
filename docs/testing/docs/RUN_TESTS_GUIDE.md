# TALE Test Suite Execution Guide

How to run the TALE scenario scripts (`models/tests/tale/`) through the headless `TestRunner`.
For why the suite is split into regression and recalibration runs, see
[TESTING_STRATEGY.md](TESTING_STRATEGY.md).

## Prerequisites

1. **Publish the build tools, then build the TestRunner** (again after any change to `Chushi/`, `Mazu/` or `Tooling/Cmdline/`):
   ```bash
   bash Tooling/Cmdline/build.sh && bash Chushi/build.sh
   dotnet build TestRunner/TestRunner.csproj -c Release
   ```
   The runners look for `TestRunner/bin/Release/net9.0/TestRunner.dll` and fall back to
   whatever Release TFM is on disk (currently `net10.0`).

2. Run every script from the repository root.

## Quick Start

```bash
./run_tests.sh all              # standard tier: 133 tests, 60-day sims (the gate before commit)
./run_tests_parallel.sh all     # same tests, one TestRunner process per test across all cores
```

A green run ends with:
```
=== Summary ===
Passed: 133/133
Failed: 0/133
All tests passed!
```

## Filters

`run_tests.sh` and `run_tests_parallel.sh` take the same first argument (default `all`):

| Filter | What runs | `TALE_SIM_DAYS` |
|--------|-----------|-----------------|
| `smoke` | the 10 scripts listed in `models/tests/tale/.smoke-tests` | 10 |
| `standard` / `all` | every `*.json` in the 11 regression phase dirs (200) | 60 |
| `full` | the same 200 | 120 |
| `bugfix` | entity-level behaviour checks (`BehaviorBugfixTests`), one process, not DES scripts | — |
| `phase0` … `phase6`, `phaseC1` … `phaseC4` | one phase dir | `$TALE_SIM_DAYS`, else 60 |
| `<file>.json` | the first file under `models/tests/tale/` whose name ends in the argument | `$TALE_SIM_DAYS`, else 60 |

The tiers override `TALE_SIM_DAYS`; phase and single-file filters take it from the environment.

### Regression phases (counts from `models/tests/tale/`)

| Filter | Directory | Tests |
|--------|-----------|-------|
| `phase0` | `phase0-des` | 20 |
| `phase1` | `phase1-storylets` | 20 |
| `phase2` | `phase2-strategies` | 20 |
| `phase3` | `phase3-interactions` | 22 |
| `phase4` | `phase4-player` | 20 |
| `phase5` | `phase5-escalation` | 20 |
| `phase6` | `phase6-population` | 49 |
| `phaseC1` | `phaseC1-infrastructure` | 8 |
| `phaseC2` | `phaseC2-storylet` | 6 |
| `phaseC3` | `phaseC3-tone` | 6 |
| `phaseC4` | `phaseC4-trust` | 9 |
| | **total** | **200** |

`phase7-spatial` (102 scripts; 302 JSON scripts in all) is in no tier of these two runners;
only `run_recalibration_tests.sh` names it.

## Parallel Runner

`run_tests_parallel.sh` fans `tools/run_one_tale_test.sh` out with `xargs -P`. Knobs:

| Variable | Default | Effect |
|----------|---------|--------|
| `JOYCE_TEST_JOBS` | CPU count | concurrent tests |
| `TEST_TIMEOUT` | 60 | per-test timeout in seconds (passed to TestRunner as `JOYCE_TEST_TIMEOUT`) |
| `JOYCE_TEST_TIMEOUT_SCALE` | 1.0 | stretches in-script `expect` timeouts on a saturated machine |
| `JOYCE_TEST_SLEEP_SCALE` | 0.1 | scales in-script `sleep` steps (the DES runs at CPU speed; 1.0 = real time) |

Per-test logs and `.status` files go to a fresh `/tmp/tale_tests.XXXXXX/`; failures are
collected in its `failures.txt`.

## Recalibration Runner

`run_recalibration_tests.sh [all|phase4|phase5|phase6|phase7|<file>]` runs each test with
`TALE_SIM_DAYS=365` and a 20-minute timeout; `all` means phases 4–7. As checked in it
hard-codes the `net9.0` TestRunner path with no fallback and executes the `.dll` without
`dotnet`, so it stops at "TestRunner not found" — open, tracked separately. To run a long
simulation now, call TestRunner directly (see Debugging) with `TALE_SIM_DAYS=365`.

## Understanding Results

A run is classified from the TestRunner output, not from its exit code:

| Shown | Meaning |
|-------|---------|
| `✓ PASS` | output contains `[TEST] PASS` |
| `✗ FAIL` | output contains `[TEST] FAIL` |
| `✗ ERROR` | neither, but the output mentions `Error` / `error` / `Exception` |
| `✓ PASS (event stream confirmed)` | neither, and no error text — counted as a pass (`PASS_HEURISTIC` in the parallel runner) |
| `✗ TIMEOUT` | still running after 65 s (`run_tests.sh`) or `TEST_TIMEOUT` + 5 s (parallel) |

## Debugging

```bash
# Sequential runner: tail of each failing log
cat /tmp/tale_test_results.txt

# One test with full output (JOYCE_TEST_SCRIPT is relative to models/)
JOYCE_TEST_SCRIPT="tests/tale/phase0-des/01-initialization.json" \
  dotnet TestRunner/bin/Release/net10.0/TestRunner.dll 2>&1 | tail -100
```

`JOYCE_TEST_TIMEOUT` (default 60 s) is how long TestRunner waits for the script to report.

### Graphics Resource Warnings
```
Warning: Asset not found: LIghtingVS.vert
```
Expected and non-critical: TestRunner runs a headless engine.

### Different Results on Repeated Runs
The DES runs with a fixed seed (42), so results should be deterministic. Under heavy load the
parallel runner can time out; raise `TEST_TIMEOUT` / `JOYCE_TEST_TIMEOUT_SCALE` or lower
`JOYCE_TEST_JOBS`, then re-run the single test.

## CI

CI (`.github/workflows/ci.yml`) builds and runs the xUnit suite only. The TALE suite is not in
CI: run `./run_tests.sh all` locally before committing.

## See Also

- Test specifications per phase: `docs/tale/docs/tests/PHASE_*.md`; maintenance strategy in `docs/tale/docs/tests/STRATEGY.md`
- Script format and the test driver: [EXPECT_IMPLEMENTATION.md](EXPECT_IMPLEMENTATION.md)
- The harness itself: `TestRunner/README.md`
- TALE overview: `docs/tale/docs/OVERVIEW.md`
