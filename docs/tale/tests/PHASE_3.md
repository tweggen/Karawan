# TALE Phase 3 Test Scripts — NPC-NPC Interaction System

## Overview

Phase 3 validates the **request/signal interaction system** where NPCs create work for each other. Tests are split into **20+ JSON scripts** that verify:

1. **Request Emission** — Storylet postconditions create requests in the pool
2. **Request Claiming** — NPCs claim requests during encounters based on roles
3. **Signal Flow** — Requests resolved, signals sent back to requesters
4. **Tier 3 Abstract Resolution** — Unclaimed requests fulfilled by background NPCs
5. **Event Logging** — All interactions logged to JSONL
6. **Pool Lifecycle** — Expiration, cleanup, metrics

## Test Script Specifications

Each script is **self-contained**, has a 60-second timeout, and validates one key aspect. Scripts are located in `models/tests/tale/phase3-interactions/`.

---

### 1. Request Emission Tests (3 scripts)

#### `01-request-postcondition-emission.json`
**Priority**: Critical
**Duration**: 30s
**Validates**: Storylet with `RequestPostcondition` emits request to pool

**Preconditions**:
- DES simulation initialized with 2 NPCs (Npc1, Npc2)
- Storylet "order_food" loaded with:
  ```json
  {
    "id": "order_food",
    "roles": ["worker"],
    "preconditions": {"hunger": {"min": 0.6}},
    "location": "home",
    "duration_minutes": 2,
    "postconditions": {
      "request": {
        "type": "food_delivery",
        "location": "current",
        "urgency": 0.7,
        "timeout_minutes": 60
      }
    }
  }
  ```

**Steps**:
1. Expect `npc_created` for Npc1 (role: worker)
2. Expect `npc_created` for Npc2
3. Npc1 arrives at home, hunger set to 0.8
4. Expect `node_arrival` with storylet="order_food"
5. Expect `request_emitted` with `type: "food_delivery", urgency: 0.7`
6. Verify request ID assigned (auto-increment)
7. Action: quit, result: pass

**Expected Outcome**: Request appears in InteractionPool with correct type and urgency.

---

#### `02-multiple-requests-from-different-npcs.json`
**Priority**: High
**Duration**: 40s
**Validates**: Multiple NPCs emit different request types

**Preconditions**:
- DES with 3 NPCs: Worker (home), Merchant (workplace), Drifter (street)
- Loaded storylets: "order_food", "merchant_needs_restock", "ask_for_help"

**Steps**:
1. Npc1 (Worker, hunger=0.8) at home → emits "food_delivery" request (ID=1)
2. Wait 2 minutes (storylet duration)
3. Npc2 (Merchant, wealth=0.2) at workplace → emits "restock_supply" request (ID=2)
4. Wait 2 minutes
5. Npc3 (Drifter, desperation=0.7) at street → emits "help_request" request (ID=3)
6. Expect 3 `request_emitted` events in order
7. Verify IDs are 1, 2, 3 (auto-incremented)
8. Action: quit, result: pass

**Expected Outcome**: InteractionPool contains 3 distinct requests with sequential IDs.

---

#### `03-request-timeout-parameter.json`
**Priority**: Medium
**Duration**: 20s
**Validates**: Request timeout calculated correctly from emission time + TimeoutMinutes

**Preconditions**:
- Storylet "order_food" with timeout_minutes: 60

**Steps**:
1. Npc1 emits "food_delivery" at T=0:00
2. Expect `request_emitted` event (note timestamp)
3. Parse event: `timeout_minutes: 60`
4. Expected request expiration time: T + 60 minutes
5. Sleep 5s (advance game time)
6. Verify request still in active pool (not expired yet)
7. Action: quit, result: pass

**Expected Outcome**: Request timeout is correctly calculated as EmittedAt + TimeoutMinutes.

---

### 2. Request Pool Lifecycle (3 scripts)

#### `04-active-requests-list.json`
**Priority**: High
**Duration**: 30s
**Validates**: GetActiveRequests() returns only unclaimed, non-expired requests

**Preconditions**:
- 3 requests emitted (IDs 1, 2, 3)
- Request 1: claimed, timeout=60min
- Request 2: unclaimed, timeout=60min
- Request 3: unclaimed, timeout=expired

**Steps**:
1. Emit 3 requests as above
2. Claim request 1
3. Expire request 3 (advance time beyond timeout)
4. Expect pool metrics: active=1 (request 2 only)
5. Verify pending=1 (request 1 claimed but unfulfilled)
6. Action: quit, result: pass

**Expected Outcome**: GetActiveRequests() returns only request 2 (unclaimed, non-expired).

---

#### `05-claimed-pending-requests.json`
**Priority**: High
**Duration**: 30s
**Validates**: GetPendingRequests() returns claimed, unfulfilled requests

**Preconditions**:
- Requests in pool: 2 unclaimed, 2 claimed but no signal

**Steps**:
1. Emit 4 requests
2. Claim requests 1 and 2
3. Expect `request_claimed` x2
4. Query pending requests: should be 2 (requests 1, 2)
5. Emit signal for request 1 ("request_fulfilled")
6. Query pending again: should be 1 (request 2 only)
7. Action: quit, result: pass

**Expected Outcome**: GetPendingRequests() correctly filters claimed + unfulfilled.

---

#### `06-expired-request-purge.json`
**Priority**: High
**Duration**: 45s
**Validates**: Daily cleanup removes requests past timeout

**Preconditions**:
- 3 requests emitted at T=0:00
- Request 1: timeout=10min (will expire)
- Request 2: timeout=90min (won't expire)
- Request 3: timeout=5min (will expire)

**Steps**:
1. Emit 3 requests at T=0:00
2. Expect `request_emitted` x3
3. Advance time to T=20:00 (past 10min and 5min timeouts)
4. Expect day boundary processing
5. Verify pool metrics: only 1 active request (request 2)
6. Verify 2 expired (requests 1, 3)
7. Action: quit, result: pass

**Expected Outcome**: Requests 1 and 3 purged; request 2 remains.

---

### 3. Request Claiming Tests (4 scripts)

#### `07-claim-during-encounter.json`
**Priority**: Critical
**Duration**: 35s
**Validates**: When Npc1 and Npc2 meet, Npc2 can claim Npc1's request

**Preconditions**:
- Npc1 (Worker) at location=social_venue, has active "food_delivery" request
- Npc2 (Merchant) incoming to social_venue
- Storylet "deliver_food" with claim_trigger: {request_type: "food_delivery", role_match: ["merchant"]}

**Steps**:
1. Npc1 emits "food_delivery" request (ID=1)
2. Expect `request_emitted`
3. Npc2 arrives at social_venue (same as Npc1)
4. Expect `encounter` event (Npc1, Npc2)
5. Expect `request_claimed` (request_id=1, npc=Npc2)
6. Verify request 1 ClaimerId = Npc2
7. Action: quit, result: pass

**Expected Outcome**: Npc2 claims request during encounter; request moves to "pending" state.

---

#### `08-claim-role-matching.json`
**Priority**: High
**Duration**: 40s
**Validates**: Claiming NPC must have role in RoleMatch array

**Preconditions**:
- Request type: "food_delivery"
- Claim trigger: role_match: ["merchant", "drifter"]
- Npc1 (Worker) at venue with request
- Npc2 (Worker) arriving at venue
- Npc3 (Merchant) arriving at venue

**Steps**:
1. Npc1 (Worker) emits "food_delivery"
2. Npc2 (Worker) meets Npc1 at venue
3. Expect encounter but NO `request_claimed` (Worker not in role_match)
4. Npc3 (Merchant) meets Npc1 at different time
5. Expect encounter AND `request_claimed` (Merchant in role_match)
6. Action: quit, result: pass

**Expected Outcome**: Only Merchant (matching role_match) successfully claims; Worker cannot.

---

#### `09-claim-request-type-match.json`
**Priority**: High
**Duration**: 40s
**Validates**: ClaimTrigger must match request type

**Preconditions**:
- Npc1 emits two requests: "food_delivery" and "help_request"
- Npc2 (Merchant) can claim only "food_delivery" (has claim_trigger for it)

**Steps**:
1. Npc1 emits "food_delivery" (ID=1)
2. Npc1 emits "help_request" (ID=2)
3. Npc2 meets Npc1
4. Expect `request_claimed` for ID=1 only
5. Verify request 2 still unclaimed
6. Action: quit, result: pass

**Expected Outcome**: Only matching request type claimed; other request remains unclaimed.

---

#### `10-claim-once-per-request.json`
**Priority**: High
**Duration**: 45s
**Validates**: Request can only be claimed once

**Preconditions**:
- Npc1 emits "food_delivery" request
- Npc2, Npc3, Npc4 can all claim (all merchants)

**Steps**:
1. Npc1 emits "food_delivery" (ID=1)
2. Npc2 meets Npc1 → claims request 1
3. Expect `request_claimed` (npc=Npc2)
4. Npc3 meets Npc1 → attempts to claim request 1
5. Expect encounter but NO second `request_claimed`
6. Verify request 1 still claims=Npc2
7. Action: quit, result: pass

**Expected Outcome**: Second NPC cannot claim already-claimed request.

---

### 4. Signal Emission Tests (3 scripts)

#### `11-signal-on-fulfill.json`
**Priority**: High
**Duration**: 35s
**Validates**: Claimer emits "request_fulfilled" signal after completing request

**Preconditions**:
- Npc2 claims Npc1's "food_delivery" request
- Npc2 completes "deliver_food" storylet (duration=30min)

**Steps**:
1. Npc1 emits "food_delivery" (ID=1)
2. Npc2 claims request 1
3. Expect `request_claimed`
4. Npc2 completes "deliver_food" storylet (travels + completes activity)
5. Wait for node arrival (30min later)
6. Expect `signal_emitted` with:
   - signal_type: "request_fulfilled"
   - request_id: 1
   - source_npc: Npc2
7. Action: quit, result: pass

**Expected Outcome**: Signal emitted when claimer completes request execution.

---

#### `12-signal-logging.json`
**Priority**: Medium
**Duration**: 30s
**Validates**: signal_emitted event logged with all metadata

**Preconditions**:
- Signal emitted (from previous test)

**Steps**:
1. Capture `signal_emitted` event from JSONL
2. Verify fields:
   - `signal_id`: unique auto-increment
   - `request_id`: matches original request
   - `signal_type`: "request_fulfilled"
   - `source_npc`: Npc2 ID
   - `timestamp`: after request claim
3. Action: quit, result: pass

**Expected Outcome**: All fields present, consistent with request lifecycle.

---

#### `13-signal-abstract-source.json`
**Priority**: High
**Duration**: 35s
**Validates**: Tier 3 abstract resolution emits signal with SourceNpcId = -1

**Preconditions**:
- Unclaimed "food_delivery" request at end of day
- No merchant/drifter available to claim during encounters

**Steps**:
1. Npc1 (Worker) emits "food_delivery"
2. Advance time to day boundary (no encounters occur to claim it)
3. Daily cleanup triggers abstract resolution
4. Expect `signal_emitted` with:
   - source_npc: -1 (abstract/system)
   - signal_type: "request_fulfilled"
5. Action: quit, result: pass

**Expected Outcome**: Abstract resolution emits signal with SourceNpcId=-1.

---

### 5. Tier 3 Abstract Resolution Tests (4 scripts)

#### `14-abstract-resolution-daily-cleanup.json`
**Priority**: Critical
**Duration**: 40s
**Validates**: Unclaimed requests matched to capable roles during daily cleanup

**Preconditions**:
- 3 unclaimed requests at day boundary:
  - "food_delivery" (Tier 3: no merchant available, but abstract pool exists)
  - "help_request" (Tier 3: worker available)
  - "unknown_type" (no capable roles)

**Steps**:
1. Emit 3 requests
2. Reach day boundary (no encounters claim them)
3. Expect day boundary event
4. Expect `signal_emitted` x2 (for "food_delivery" and "help_request")
5. Verify "unknown_type" still unclaimed/active
6. Action: quit, result: pass

**Expected Outcome**: 2 requests resolved abstractly; 1 remains unclaimed.

---

#### `15-abstract-food-delivery-merchant.json`
**Priority**: High
**Duration**: 40s
**Validates**: "food_delivery" request matched to merchant/drifter Tier 3 pool

**Preconditions**:
- GetCapableRoles("food_delivery") = ["merchant", "drifter"]
- No merchants/drifters available during day (all in other activities)

**Steps**:
1. Worker emits "food_delivery" request
2. No Merchant/Drifter encounter Worker (no claiming during day)
3. Day boundary reached
4. Abstract resolution checks capable roles for "food_delivery"
5. Expect `signal_emitted` (request fulfilled abstractly)
6. Action: quit, result: pass

**Expected Outcome**: Request fulfilled by abstract Tier 3 match.

---

#### `16-abstract-help-request-worker.json`
**Priority**: High
**Duration**: 40s
**Validates**: "help_request" matched to worker/socialite Tier 3 pool

**Preconditions**:
- GetCapableRoles("help_request") = ["worker", "socialite"]
- Drifter emits help_request
- No Workers/Socialites encounter Drifter during day

**Steps**:
1. Drifter emits "help_request"
2. No encounters claim it
3. Day boundary
4. Abstract resolution matches to Worker/Socialite pool
5. Expect `signal_emitted`
6. Action: quit, result: pass

**Expected Outcome**: Help request fulfilled by abstract worker match.

---

#### `17-abstract-no-capable-role.json`
**Priority**: Medium
**Duration**: 40s
**Validates**: Request stays in pool if no capable roles exist

**Preconditions**:
- Custom request type: "exotic_service" with no role mappings

**Steps**:
1. Npc emits "exotic_service" request
2. Day boundary reached
3. Abstract resolution: GetCapableRoles("exotic_service") returns empty set
4. Expect NO `signal_emitted` for this request
5. Query pool: request still active
6. Action: quit, result: pass

**Expected Outcome**: Request remains unclaimed, waiting for Tier 2 encounter.

---

### 6. Event Integration & Metrics Tests (3 scripts)

#### `18-request-claim-fulfill-same-day.json`
**Priority**: High
**Duration**: 50s
**Validates**: Complete lifecycle in single day: emit → claim → fulfill → signal

**Preconditions**:
- Npc1 (Worker, hunger=0.8) at social_venue
- Npc2 (Merchant) can arrive at same venue
- Storylets: "order_food", "deliver_food"

**Steps**:
1. Npc1 at social_venue, emits "food_delivery" (T=0:30)
2. Expect `request_emitted` (ID=1, T=0:30)
3. Npc2 arrives at social_venue (T=1:00)
4. Expect `encounter`, `request_claimed` (ID=1, T=1:00)
5. Npc2 completes delivery (T=1:35, after 35min activity)
6. Expect `signal_emitted` (ID=1, "request_fulfilled", T=1:35)
7. Verify pool: request 1 fulfilled, signal created
8. Action: quit, result: pass

**Expected Outcome**: Full lifecycle completes within same game day.

---

#### `19-interaction-pool-metrics.json`
**Priority**: High
**Duration**: 35s
**Validates**: Pool metrics (active, claimed, expired, fulfilled) tracked correctly

**Preconditions**:
- Multiple requests at various states

**Steps**:
1. Emit 5 requests
2. Claim 2 requests
3. Fulfill 1 of the claimed
4. Expire 1 request
5. Query metrics:
   - active: 2 (unclaimed, non-expired)
   - claimed: 1 (claimed, unfulfilled)
   - fulfilled: 1
   - expired: 1
6. Verify counts sum correctly
7. Action: quit, result: pass

**Expected Outcome**: Metrics accurately reflect pool state.

---

#### `20-daily-boundary-does-not-clear-pool.json`
**Priority**: Medium
**Duration**: 45s
**Validates**: Daily boundary doesn't clear active pool; only cleanup purges

**Preconditions**:
- Active request from day 1 (not expired)
- Day boundary at T=24:00

**Steps**:
1. Emit request (ID=1, timeout=48h)
2. Advance to day 2 boundary
3. Expect day_summary events, metrics reset
4. Query active requests on day 2
5. Verify request 1 still in pool (not cleared)
6. Verify request 1 timeout still valid (not reset)
7. Action: quit, result: pass

**Expected Outcome**: Active requests persist across day boundaries.

---

### 7. Advanced Scenarios (2+ scripts)

#### `21-multiple-requesters-same-claimer.json`
**Priority**: Medium
**Duration**: 45s
**Validates**: One NPC can claim multiple different requests from different requesters

**Preconditions**:
- Npc1 emits "food_delivery"
- Npc3 emits "restock_supply"
- Npc2 (Merchant) can claim both types

**Steps**:
1. Npc1 emits "food_delivery" (ID=1)
2. Npc3 emits "restock_supply" (ID=2)
3. Npc2 meets Npc1 → claims request 1
4. Npc2 meets Npc3 → claims request 2
5. Expect 2x `request_claimed`
6. Npc2 completes both storylets
7. Expect 2x `signal_emitted`
8. Action: quit, result: pass

**Expected Outcome**: One NPC fulfills requests from multiple requesters.

---

#### `22-request-timeout-before-claim.json`
**Priority**: Medium
**Duration**: 40s
**Validates**: Request expires even if not claimed

**Preconditions**:
- Request with short timeout (5 min)
- No encounters occur

**Steps**:
1. Npc1 emits request with timeout_minutes: 5
2. No encounters occur (no claiming)
3. Advance time to T=10:00 (past timeout)
4. Day boundary triggers cleanup
5. Verify request purged (not in pool)
6. No signal emitted (timeout, not fulfillment)
7. Action: quit, result: pass

**Expected Outcome**: Expired requests purged without signal.

---

## Test Execution

### Run All Phase 3 Tests
```bash
cd /Users/tweggen/coding/github/Karawan
for script in models/tests/tale/phase3-interactions/*.json; do
  echo "Running: $(basename $script)"
  JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj
  if [ $? -ne 0 ]; then
    echo "FAILED: $script"
    exit 1
  fi
done
echo "All Phase 3 tests passed!"
```

### Run Single Test
```bash
JOYCE_TEST_SCRIPT=models/tests/tale/phase3-interactions/01-request-postcondition-emission.json \
  dotnet run --project nogame/nogame.csproj
```

---

## Next Steps

1. **Implement JSON test script files** (22 scripts in models/tests/tale/phase3-interactions/)
2. **Validate against Phase 3 code** (verify InteractionPool, DesSimulation integration)
3. **Run Testbed integration test** (30-day simulation with metrics validation)
4. **Build Phase 1, 2, 4, 5 tests** (use same pattern)
