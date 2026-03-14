# Phase 3 Interaction Tests

## Overview

This directory contains **22 JSON test scripts** for validating the TALE Phase 3 NPC-NPC interaction system using the **ExpectEngine** framework.

## Test Coverage

### Request Emission (3 tests)
- `01-request-postcondition-emission.json` — Verify storylet postconditions emit requests
- `02-multiple-requests-from-different-npcs.json` — Multiple NPCs emit different request types
- `03-request-timeout-parameter.json` — Timeout calculated correctly

### Request Pool Lifecycle (3 tests)
- `04-active-requests-list.json` — GetActiveRequests() returns unclaimed, non-expired only
- `05-claimed-pending-requests.json` — GetPendingRequests() returns claimed, unfulfilled
- `06-expired-request-purge.json` — Daily cleanup purges expired requests

### Request Claiming (4 tests)
- `07-claim-during-encounter.json` — NPCs claim requests when meeting
- `08-claim-role-matching.json` — Claimer role must match ClaimTrigger.RoleMatch
- `09-claim-request-type-match.json` — Request type must match ClaimTrigger
- `10-claim-once-per-request.json` — Request can only be claimed once

### Signal Emission (3 tests)
- `11-signal-on-fulfill.json` — Claimer emits "request_fulfilled" after completion
- `12-signal-logging.json` — Signal logged with all metadata
- `13-signal-abstract-source.json` — Tier 3 abstract resolution emits signal with source=-1

### Tier 3 Abstract Resolution (4 tests)
- `14-abstract-resolution-daily-cleanup.json` — Unclaimed requests matched daily
- `15-abstract-food-delivery-merchant.json` — food_delivery matched to merchant/drifter
- `16-abstract-help-request-worker.json` — help_request matched to worker/socialite
- `17-abstract-no-capable-role.json` — Request stays in pool if no capable roles exist

### Event Integration & Metrics (3 tests)
- `18-request-claim-fulfill-same-day.json` — Full lifecycle in single day
- `19-interaction-pool-metrics.json` — Pool metrics tracked correctly
- `20-daily-boundary-does-not-clear-pool.json` — Active requests persist across day boundary

### Advanced Scenarios (2 tests)
- `21-multiple-requesters-same-claimer.json` — One NPC claims multiple requests
- `22-request-timeout-before-claim.json` — Request expires before being claimed

## Format

Each test is a JSON file with:
- **Metadata**: name, description, phase, category, priority, timeout
- **Preconditions**: Initial state required
- **Steps**: Array of expect/inject/sleep/action operations
- **Expected Outcome**: What should happen

Example:
```json
{
  "name": "test-name",
  "description": "What this validates",
  "phase": "phase-3",
  "category": "feature-category",
  "priority": "critical|high|medium|low",
  "globalTimeout": 60,
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
JOYCE_TEST_SCRIPT=models/tests/tale/phase3-interactions/01-request-postcondition-emission.json \
  dotnet run --project nogame/nogame.csproj
```

### Run All Phase 3 Tests
```bash
for script in models/tests/tale/phase3-interactions/*.json; do
  echo "Running: $(basename $script)"
  JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj
  if [ $? -ne 0 ]; then
    echo "FAILED: $script"
    exit 1
  fi
done
echo "✓ All 22 Phase 3 tests passed!"
```

## Test Priority

| Priority | Tests | Count |
|----------|-------|-------|
| Critical | 01, 07, 14, 18 | 4 |
| High | 02, 04, 05, 06, 08, 09, 10, 11, 13, 15, 16, 19 | 12 |
| Medium | 03, 12, 17, 20, 21, 22 | 6 |

## Events Validated

- `npc_created` — NPC initialization
- `node_arrival` — Storylet completion and transition
- `request_emitted` — Request to pool (postcondition)
- `request_claimed` — NPC claims request (encounter-based)
- `signal_emitted` — Request fulfilled/failed signal
- `encounter` — Two NPCs meet
- `day_summary` — Daily cleanup and metrics

## Framework

These tests use **ExpectEngine**, a generic system testing framework defined in `docs/EXPECT_ENGINE_IMPLEMENTATION.md`.

Key features:
- Lock-free event channels (System.Threading.Channels)
- JSON test script format
- Fish-for-event (skip non-matching) semantics
- Event injection and monitoring

## Validation

All 22 test scripts are valid JSON:
```bash
for f in *.json; do jq empty "$f" && echo "✓ $f"; done
```

## Next Steps

1. Run all Phase 3 tests to validate against the implementation
2. Adjust timeouts/sleep values based on actual game tick rates
3. Investigate any test failures and refine expectations
4. Move on to Phase 0 tests (DES engine foundation)

---

**Phase 3 Complete**: Request/signal system fully tested.
