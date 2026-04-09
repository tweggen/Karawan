# TALE Testing Strategy

## Overview

The TALE test suite is split into two categories:
1. **Regression Tests** - Fast, functional validation (~5 minutes)
2. **Recalibration Tests** - Long-running, emergent behavior verification (extensive)

This separation allows rapid feedback during development while maintaining comprehensive validation for parameter tuning.

## Regression Tests (`run_tests.sh`)

**Purpose**: Quick functional validation of core systems
**Simulation Duration**: 60 days (fast feedback)
**Run Time**: ~5 minutes for all 183 tests
**When to Run**: After code changes, CI/CD pipeline

### Usage

```bash
# Run all regression tests (all phases)
./run_tests.sh all

# Run specific phase
./run_tests.sh phase0           # DES Engine
./run_tests.sh phase1           # Storylet System
./run_tests.sh phase2           # Strategy System
./run_tests.sh phase3           # Interaction System
./run_tests.sh phase4           # Quest/Player System
./run_tests.sh phase5           # Escalation/Interrupts
./run_tests.sh phase6           # Population Management
./run_tests.sh phaseC1          # Conversation Infrastructure
./run_tests.sh phaseC2          # Storylet-Specific Dialogue
./run_tests.sh phaseC3          # Mood/Tone Branches

# Run specific test
./run_tests.sh 01-initialization.json
```

### Test Phases

| Phase | Name | Tests | Focus | Duration |
|-------|------|-------|-------|----------|
| 0 | DES Engine | 20 | Event queue, scheduling, properties | 60 days |
| 1 | Storylet System | 20 | Selection, preconditions, postconditions | 60 days |
| 2 | Strategy System | 20 | Multi-phase state machines, transitions | 60 days |
| 3 | Interaction System | 22 | Request/signal lifecycle, claiming | 60 days |
| 4 | Quest/Player System | 20 | Quest mechanics, satnav, follow/unfollow | 60 days |
| 5 | Escalation/Interrupts | 20 | Interrupt scopes, conditional branches | 60 days |
| 6 | Population Management | 49 | Generation, deviance, persistence | 60 days |
| C1 | Conversation Infrastructure | 8 | Behavior attachment, generic/role/tag fallback, indoor NPCs | 60 days |
| C2 | Storylet-Specific Dialogue | 6 | Explicit override, tag fallback, precedence, wealth gating | 60 days |
| C3 | Mood/Tone Branches | 6 | NPC mood functions, wealth labels, tone-aware dialogue | 60 days |

**Total**: 183 regression tests

## Recalibration Tests (`run_recalibration_tests.sh`)

**Purpose**: Verify emergent structures and equilibrium behavior
**Simulation Duration**: 365 days (1 year, configurable)
**Run Time**: ~2-4 hours for all tests (longer per test)
**When to Run**: When adjusting NPC parameters, verifying economic models, social structure emergence

### Usage

```bash
# Run all recalibration tests (phases 4-7)
./run_recalibration_tests.sh all

# Run specific phase
./run_recalibration_tests.sh phase4      # Quest mechanics over long term
./run_recalibration_tests.sh phase5      # Escalation chains, gang stability
./run_recalibration_tests.sh phase6      # Population equilibrium, deviance patterns
./run_recalibration_tests.sh phase7      # Spatial stability, movement patterns

# Run specific test with 365-day simulation
./run_recalibration_tests.sh 01-population-equilibrium.json
```

### Recalibration Test Phases

| Phase | Name | Focus | Simulation |
|-------|------|-------|-----------|
| 4 | Quest/Player | Long-term quest patterns, completion rates | 365 days |
| 5 | Escalation | Gang formation stability, conflict escalation | 365 days |
| 6 | Population | Population equilibrium, role distribution | 365 days |
| 7 | Spatial | Movement patterns, location utilization | 365 days |

**Typical Results**: Population/economic models reach equilibrium, social structures stabilize, conflict patterns emerge

## Configuring Simulation Duration

The test runner uses the `TALE_SIM_DAYS` environment variable:

```bash
# Default: 60 days (regression)
JOYCE_TEST_SCRIPT="tests/tale/phase0-des/01-init.json" \
  dotnet TestRunner/bin/Release/net9.0/TestRunner.dll

# Custom duration (e.g., 365 days for recalibration)
TALE_SIM_DAYS=365 JOYCE_TEST_SCRIPT="tests/tale/phase6/..." \
  dotnet TestRunner/bin/Release/net9.0/TestRunner.dll

# Full year-plus analysis
TALE_SIM_DAYS=730 JOYCE_TEST_SCRIPT="tests/tale/phase6/..." \
  dotnet TestRunner/bin/Release/net9.0/TestRunner.dll
```

## Testing Workflow

### Development (Fast Feedback)
```bash
# After code changes, run regression tests
./run_tests.sh all

# If all pass, focus on affected phase
./run_tests.sh phase3  # if you changed interactions
```

### Parameter Tuning (Emergent Behavior Verification)
```bash
# When adjusting NPC properties, wealth dynamics, etc.
./run_recalibration_tests.sh all

# Or focus on specific aspect
./run_recalibration_tests.sh phase6  # population equilibrium
```

### Continuous Integration
- **Push/PR triggers**: `./run_tests.sh all` (5 minutes)
- **Nightly/Weekly jobs**: `./run_recalibration_tests.sh all` (4+ hours)
- **Before release**: Both suites, plus manual testing

## Test Structure

### Regression Test Format
```json
{
  "name": "test-name",
  "globalTimeout": 30,
  "steps": [
    { "expect": { "type": "event_type" }, "timeout": 5 },
    { "action": "quit", "result": "pass" }
  ]
}
```

### Recalibration Test Format
Same format, but:
- Run with `TALE_SIM_DAYS=365` (or longer)
- Timeout values adjusted for longer simulation
- May assert on equilibrium metrics (e.g., population stability)

## Results and Logging

### Regression Tests
```
=== TALE Test Suite ===
Phase: phase0-des
  [phase0-des] 01-initialization.json ... ✓ PASS
  ...
=== Summary ===
Passed: 171/171
Failed: 0/171
All tests passed!
```

### Recalibration Tests
```
=== TALE Recalibration Test Suite ===
Simulation Duration: 365 days
Phase: phase6-population
  [phase6-population] 01-population-growth.json ... ✓ PASS (1m 23s)
  ...
=== Recalibration Summary ===
Passed: 20/49
Failed: 0/49
Duration: ~46 minutes estimated
```

## Performance Notes

- **Regression**: 60 days × 10 NPCs = ~16,900 events per test
- **Recalibration**: 365 days × 50 NPCs = ~100,000+ events per test
- Single test typically takes: 2-5 seconds (60 days), 15-60 seconds (365 days)
- Parallel execution: Tests run sequentially (test framework limitation)

## Troubleshooting

### Tests Timeout
- Increase `globalTimeout` in test script if simulation is complex
- Reduce `TALE_SIM_DAYS` for debugging
- Check system load

### Inconsistent Results
- Verify random seed is set in TestRunner (`seed: 42`)
- Check for non-deterministic event ordering
- Run multiple times to verify flakiness

### Memory Usage
- Large population tests (500+ NPCs) use ~1GB
- Ensure sufficient system memory before running recalibration suite

## Future Extensions

- [ ] Stress tests (1000+ NPCs, 5 years simulation)
- [ ] Economic equilibrium validation tests
- [ ] Social network emergence tests
- [ ] Conflict spiral tests
- [ ] Performance benchmarks with profiling
