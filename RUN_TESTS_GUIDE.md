# TALE Test Suite Execution Guide

Quick start guide for running all 62 TALE test scripts (Phases 0, 1, 3).

## Prerequisites

1. **Build the TestRunner** (one-time setup):
   ```bash
   dotnet build TestRunner/TestRunner.csproj -c Release -p:EnableSourceLink=false
   ```

2. **Make script executable** (one-time):
   ```bash
   chmod +x run_tests.sh
   ```

## Quick Start

### Run All 62 Tests
```bash
./run_tests.sh all
```

Expected output:
```
=== TALE Test Suite ===
Started: ...

Phase: phase0-des
  [phase0-des] 01-initialization.json ... ✓ PASS
  [phase0-des] 02-npc-creation.json ... ✓ PASS
  ...

Phase: phase1-storylets
  [phase1-storylets] 01-library-loading.json ... ✓ PASS
  ...

Phase: phase3-interactions
  [phase3-interactions] 01-request-postcondition-emission.json ... ✓ PASS
  ...

=== Summary ===
Passed: 62/62
Failed: 0/62
All tests passed!
```

### Run Single Phase
```bash
./run_tests.sh phase0    # Phase 0: DES Engine (20 tests)
./run_tests.sh phase1    # Phase 1: Storylets (20 tests)
./run_tests.sh phase3    # Phase 3: Interactions (22 tests)
```

### Run Single Test
```bash
./run_tests.sh 01-initialization.json
```

## Understanding Results

### Status Indicators

| Symbol | Meaning | Interpretation |
|--------|---------|-----------------|
| ✓ PASS | Test passed | Event stream confirmed, validation successful |
| ✗ FAIL | Test failed | Validation error or unexpected event sequence |
| ⏱ TIMEOUT | Test exceeded 30 seconds | May indicate slow system or test issue |
| ✗ ERROR | Test crashed | Check `/tmp/tale_test_results.txt` for details |

### Example Output
```
[phase0-des] 01-initialization.json ... ✓ PASS (event stream confirmed)
[phase0-des] 02-npc-creation.json ... ✓ PASS (event stream confirmed)
[phase3-interactions] 15-abstract-food-delivery-merchant.json ... ✓ PASS
```

## Debugging

### View Full Error Details
```bash
cat /tmp/tale_test_results.txt
```

### Run Single Test with Full Output
```bash
JOYCE_TEST_SCRIPT="tests/tale/phase0-des/01-initialization.json" \
  ./TestRunner/bin/Release/net9.0/TestRunner 2>&1 | tail -100
```

### Check TestRunner Binary
```bash
ls -lh TestRunner/bin/Release/net9.0/TestRunner
# Should show the executable with correct permissions
```

### Verify Test Scripts Exist
```bash
find models/tests/tale -name "*.json" | wc -l
# Should show 62 test files
```

## Test Organization

### Phase 0 - DES Engine (20 tests)
- Initialization and core data structures
- NPC creation and scheduling
- Spatial model and event queue
- Encounters and relationships
- Metrics and logging

**Location**: `models/tests/tale/phase0-des/`

### Phase 1 - Storylet System (20 tests)
- Library loading and indexing
- Precondition matching
- Weighted selection and duration
- Location resolution
- Postconditions and property updates

**Location**: `models/tests/tale/phase1-storylets/`

### Phase 3 - NPC-NPC Interaction (22 tests)
- Request emission and pooling
- Request claiming during encounters
- Signal emission and fulfillment
- Abstract (Tier 3) resolution
- Event logging and metrics

**Location**: `models/tests/tale/phase3-interactions/`

## Performance Notes

- **Total Runtime**: ~2-5 minutes for all 62 tests (depends on system)
- **Per-Test Timeout**: 30 seconds
- **Resource Usage**: Minimal (headless, no graphics)

## Troubleshooting

### "TestRunner not found" Error
```bash
# Rebuild TestRunner
dotnet build TestRunner/TestRunner.csproj -c Release -p:EnableSourceLink=false
```

### All Tests Timeout
- Check TestRunner binary is executable: `ls -lx TestRunner/bin/Release/net9.0/TestRunner`
- Verify test scripts exist: `ls models/tests/tale/phase*/`
- Try single test: `./run_tests.sh 01-initialization.json`

### Graphics Resource Warnings
```
Warning: Asset not found: LIghtingVS.vert
```
**This is expected and non-critical.** The test harness gracefully handles missing graphics resources.

### Different Results on Repeated Runs
Test results should be deterministic. If you see inconsistency:
1. Check system load
2. Review `/tmp/tale_test_results.txt` for specific failures
3. Run single problematic test multiple times
4. Report issue with test name and system info

## Integration with CI/CD

To run tests in CI/CD pipeline:

```bash
# GitHub Actions example
- name: Build TestRunner
  run: dotnet build TestRunner/TestRunner.csproj -c Release -p:EnableSourceLink=false

- name: Run TALE Tests
  run: ./run_tests.sh all

- name: Upload Results
  if: failure()
  uses: actions/upload-artifact@v3
  with:
    name: test-results
    path: /tmp/tale_test_results.txt
```

## Next Steps

After running tests successfully:

1. **Review Test Specifications**:
   - `docs/tale/TALE_TEST_SCRIPTS_PHASE_0.md`
   - `docs/tale/TALE_TEST_SCRIPTS_PHASE_1.md`
   - `docs/tale/TALE_TEST_SCRIPTS_PHASE_3.md`

2. **Expand Phase 2-5 Tests**:
   - Read `docs/tale/PHASES_1_2_4_5_SKELETON.md`
   - Create detailed specs for Phase 2-5
   - Implement 20+ JSON test scripts per phase

3. **Validate Event Logging**:
   - Check JSONL event output with Testbed
   - Verify event types match test expectations

## Support

- **Test Framework**: See `docs/tale/EXPECT_ENGINE_IMPLEMENTATION.md`
- **Test Harness**: See `TestRunner/README.md`
- **Documentation**: See `docs/tale/OVERVIEW.md`

---

**Status**: All test infrastructure ready for execution ✅
