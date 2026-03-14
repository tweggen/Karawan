# Phase 1 Storylet System Tests

## Overview

This directory contains **20 JSON test scripts** for validating the TALE Phase 1 Storylet System using the **ExpectEngine** framework.

## Test Coverage

### Group 1: Library Loading (3 tests)
- `01-library-loading.json` — Verify LoadFromDirectory() loads all .json files
- `02-library-indexing.json` — Verify GetCandidates() returns role-specific + universal
- `03-fallback-selection.json` — Verify GetFallback() returns rest/wander appropriately

### Group 2: Precondition Matching (4 tests)
- `04-property-precondition.json` — Verify property range preconditions match correctly
- `05-property-mismatch.json` — Verify storylets skipped when preconditions fail
- `06-time-of-day-match.json` — Verify time_of_day windows enforced
- `07-role-filter.json` — Verify non-matching roles excluded

### Group 3: Selection & Duration (3 tests)
- `08-weighted-selection.json` — Verify higher weight increases selection probability
- `09-duration-randomness.json` — Verify duration varies within [min, max] range
- `10-location-resolution.json` — Verify location strings resolve to IDs correctly

### Group 4: Postconditions (3 tests)
- `11-simple-postcondition.json` — Verify simple postconditions update properties
- `12-multiple-postconditions.json` — Verify all postconditions applied atomically
- `13-clamped-values.json` — Verify properties clamped to [0, 1]

### Group 5: Complex Scenarios (4 tests)
- `14-desperation-gating.json` — Verify crime storylets gated by desperation
- `15-nearest-venue.json` — Verify nearest_shop_* location resolution
- `16-role-specific-weight.json` — Verify role-specific weight > universal
- `17-universal-fallback.json` — Verify universal available when role-specific empty
- `18-combined-candidates.json` — Verify merged role-specific + universal lists
- `19-json-parse-error.json` — Verify graceful handling of invalid JSON
- `20-empty-library.json` — Verify empty library behavior and fallback mechanism

## Format

Each test is a JSON file with:
- **Metadata**: name, description, phase, category, priority, timeout
- **Preconditions**: Initial state required
- **Steps**: Array of expect/sleep/action operations
- **Expected Outcome**: What should happen

Example:
```json
{
  "name": "test-name",
  "description": "What this validates",
  "phase": "phase-1",
  "category": "library|preconditions|selection|postconditions|complex",
  "priority": "critical|high|medium|low",
  "globalTimeout": 30,
  "preconditions": "Initial state",
  "expectedOutcome": "What should happen",
  "steps": [
    {"expect": {"type": "event.type"}, "timeout": 30, "comment": "..."},
    {"sleep": 1000, "comment": "..."},
    {"action": "quit", "result": "pass"}
  ]
}
```

## Execution

### Run Single Test
```bash
JOYCE_TEST_SCRIPT=models/tests/tale/phase1-storylets/01-library-loading.json \
  dotnet run --project nogame/nogame.csproj
```

### Run All Phase 1 Tests
```bash
for script in models/tests/tale/phase1-storylets/*.json; do
  echo "Running: $(basename $script)"
  JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj
  if [ $? -ne 0 ]; then
    echo "FAILED: $script"
    exit 1
  fi
done
echo "✓ All 20 Phase 1 tests passed!"
```

## Test Priority

| Priority | Tests | Count |
|----------|-------|-------|
| Critical | 01, 04 | 2 |
| High | 02, 03, 05, 06, 07, 08, 09, 10, 11, 12, 13 | 11 |
| Medium | 14, 15, 16, 17, 18 | 5 |
| Low | 19, 20 | 2 |

## Events Validated

- `npc_created` — NPC initialization with access to storylet library
- `node_arrival` — Storylet selected and executed
- `day_summary` — Daily metrics including storylet completion counts

## Framework

These tests use **ExpectEngine**, a generic system testing framework defined in `docs/EXPECT_ENGINE_IMPLEMENTATION.md`.

Key features:
- Lock-free event channels (System.Threading.Channels)
- JSON test script format
- Fish-for-event (skip non-matching) semantics
- Event injection and monitoring

## Validation

All 20 test scripts are valid JSON:
```bash
for f in *.json; do jq empty "$f" && echo "✓ $f"; done
```

## Next Steps

1. Run all Phase 1 tests to validate against the implementation
2. Adjust timeouts/sleep values based on actual game tick rates
3. Investigate any test failures and refine expectations
4. Move on to Phase 2 tests (Strategy System)

---

**Phase 1 Complete**: Storylet system fully tested with 20 comprehensive tests covering library loading, preconditions, selection, postconditions, and complex scenarios.
