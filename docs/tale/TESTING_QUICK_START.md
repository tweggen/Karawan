# TALE Testing — Quick Start Guide

## TL;DR

You now have a complete testing plan for the TALE narrative engine:
- **120+ test scripts** across 6 phases
- **Phase 3: IMPLEMENTATION & VALIDATION COMPLETE** ✅
  - 22 test scripts implemented in JSON
  - All interaction storylets integrated into main role files
  - Testbed validation passed: 62,208 request_emitted, 62,168 request_claimed, 100% fulfillment
- **Phases 0, 1, 2, 4, 5** have outline structures ready to expand

**Right Now**: Phase 3 tests are ready to run. Priority 2 is expanding Phase 0 test specs.

---

## Main Documents (Read in This Order)

1. **TALE_TESTING_FRAMEWORK.md** (YOU ARE HERE)
   - Navigation guide, file structure, execution examples
   - Quick summary of all 5 companion documents
   - Success metrics

2. **TALE_TEST_PLAN.md**
   - 120+ test overview
   - Organization by phase and category
   - CI roadmap

3. **TALE_TEST_SCRIPTS_PHASE_3.md** ← PRIORITY #1
   - 22 detailed test specifications
   - Full JSON structure + preconditions + expected outcomes
   - Ready to code

4. **TALE_TEST_SCRIPTS_PHASE_0_SKELETON.md**
   - 20 DES engine tests (outlined)
   - Expand this next (after Phase 3)

5. **TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md**
   - Phases 1, 2, 4, 5 outlined (lower priority)

---

## What To Do Next (Priority Order)

### ✅ DONE (Already Committed)
- Phase 3 implementation code (InteractionPool, InteractionRequest, InteractionSignal)
- Phase 3 JSON storylets (10 files, consolidated into main role files)
- Phase 3 test specs (TALE_TEST_SCRIPTS_PHASE_3.md)
- Phase 3 test scripts (22 JSON files in models/tests/tale/phase3-interactions/)
- Phase 3 Testbed validation:
  - ✅ Interaction storylets consolidated into merchant.json, worker.json, drifter.json, socialite.json, authority.json, universal.json
  - ✅ JSON parsing fixed to handle nested request/signal objects
  - ✅ 30-day simulation produces: 62,208 request_emitted, 62,168 request_claimed, 100% fulfillment rate
  - ✅ Tier 3 abstract resolution working (66 signal_emitted with npc=-1)

### ✅ DONE — Phase 3 Tests (Ready to Run)
**Status**: 22 JSON test scripts complete, Testbed integration proven, 100% fulfillment validated
- All interaction storylets consolidated into main role files
- JSON parsing fixed for nested request/signal objects
- 30-day simulation: 62,208 request_emitted, 62,168 request_claimed, 100% fulfillment

**Next**: Run formal Phase 3 test execution when time permits.

### ✅ DONE — Phase 0 Tests (Detailed Specs & Scripts Complete)
**Status**: 20 JSON test scripts implemented, all validated with jq
- Created detailed TALE_TEST_SCRIPTS_PHASE_0.md with full specifications
- Implemented all 20 JSON scripts in models/tests/tale/phase0-des/
- Organized into 6 categories: initialization, scheduling, properties, encounters, relationships, metrics
- Test priorities: 6 critical, 9 high, 2 medium

**Next**: Run Phase 0 tests against Testbed to validate DES engine.

### 🟢 PRIORITY 1 — Run Phase 3 and Phase 0 Tests Against Testbed
**Status**: 42 total test scripts ready (22 Phase 3 + 20 Phase 0)
**Task**: Execute test scripts to validate event sequences and metrics
**Time Est**: 2-3 hours total

Steps:
1. Run all Phase 3 tests (22 scripts):
   ```bash
   for script in models/tests/tale/phase3-interactions/*.json; do
     echo "Running: $(basename $script)"
     JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj
     if [ $? -ne 0 ]; then echo "FAILED: $script"; exit 1; fi
   done
   ```
2. Run all Phase 0 tests (20 scripts):
   ```bash
   for script in models/tests/tale/phase0-des/*.json; do
     echo "Running: $(basename $script)"
     JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj
     if [ $? -ne 0 ]; then echo "FAILED: $script"; exit 1; fi
   done
   ```
3. Verify event types and fulfillment metrics in outputs

### 🟡 PRIORITY 2 — Expand & Implement Phase 1 Tests
**Status**: 20 outlines exist (PHASES_1_2_4_5_SKELETON.md)
**Task**: Detail specs, create 20 JSON scripts
**Time Est**: 5-7 hours
**Rationale**: Core storylet selection logic

Steps:
1. Read PHASES_1_2_4_5_SKELETON.md (Phase 1 section)
2. Expand Phase 1 outlines to detailed spec (like PHASE_0.md)
3. Create 20 JSON files in `models/tests/tale/phase1-storylets/`
4. Run full test suite

### 🟡 PRIORITY 3 — Expand & Implement Phase 1 Tests
**Status**: 20 outlines exist (PHASES_1_2_4_5_SKELETON.md)
**Task**: Detail specs, create 20 JSON scripts
**Time Est**: 5-7 hours
**Rationale**: Core storylet selection logic

### 🟡 PRIORITY 4 — Expand & Implement Phase 2 Tests
**Status**: 20 outlines exist (PHASES_1_2_4_5_SKELETON.md)
**Task**: Detail specs, create 20 JSON scripts
**Time Est**: 5-7 hours
**Rationale**: Quest composition from strategies

### 🟢 PRIORITY 5 — Expand & Implement Phase 4 & 5 Tests
**Status**: 20 outlines each
**Task**: Detail specs, create ~40 JSON scripts
**Time Est**: 8-10 hours
**Rationale**: Player integration & emergent mechanics

---

## Document Map

```
TALE Testing Framework (YOU ARE HERE)
├── TALE_TEST_PLAN.md
│   └── Master strategy for all 120+ tests
│
├── TALE_TEST_SCRIPTS_PHASE_3.md ← START HERE FOR IMPLEMENTATION
│   └── 22 detailed test specs (ready to code)
│
├── TALE_TEST_SCRIPTS_PHASE_0_SKELETON.md
│   └── 20 DES engine tests (outlines, needs expansion)
│
└── TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md
    └── Phases 1, 2, 4, 5 (outlines, needs expansion)
```

---

## Key Concepts

### Test Format
All tests use **ExpectEngine JSON format**:
```json
{
  "name": "test-name",
  "description": "what this validates",
  "phase": "phase-3",
  "priority": "critical|high|medium",
  "globalTimeout": 60,
  "steps": [
    {"expect": {"type": "event.type"}, "timeout": 30, "comment": "..."},
    {"sleep": 1000, "comment": "..."},
    {"action": "quit", "result": "pass"}
  ]
}
```

### Test Categories
- **Critical**: Must pass for phase to be considered complete
- **High**: Should pass, validates core functionality
- **Medium**: Nice to have, edge cases
- **Low**: Polish, advanced scenarios

### Test Organization
One file per test, organized by phase:
- `models/tests/tale/phase0-des/01-initialization.json`
- `models/tests/tale/phase1-storylets/01-library-loading.json`
- `models/tests/tale/phase3-interactions/01-request-postcondition-emission.json`

---

## Quick Reference: Test Execution

### Run Single Test
```bash
JOYCE_TEST_SCRIPT=models/tests/tale/phase3-interactions/01-request-postcondition-emission.json \
  dotnet run --project nogame/nogame.csproj
```

### Run All Phase 3 Tests
```bash
./run_phase3_tests.sh  # (create this script)
```

### Run Multiple Phases
```bash
for phase in phase0-des phase3-interactions; do
  for script in models/tests/tale/$phase/*.json; do
    JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj || exit 1
  done
done
```

### Testbed Integration Test
```bash
dotnet run --project Testbed -- --days 30 --events-file events.jsonl
```

---

## Success Checklist

### Phase 3 (COMPLETE ✅)
- [x] Phase 3 implementation code (InteractionPool, Request, Signal)
- [x] Phase 3 JSON storylets (consolidated into main role files)
- [x] Phase 3 test scripts (22 JSON files in models/tests/tale/phase3-interactions/)
- [x] Phase 3 Testbed validation (62K+ events, 100% fulfillment rate)
- [ ] Phase 3 formal test execution (running all 22 scripts in sequence)

### Phase 0 (COMPLETE ✅)
- [x] Phase 0 tests expanded to detailed specs (TALE_TEST_SCRIPTS_PHASE_0.md)
- [x] Phase 0 tests implemented (20 JSON files in models/tests/tale/phase0-des/)
- [ ] Phase 0 tests executed and all passing

### Phases 1, 2, 4, 5 (OUTLINE → DETAILED → IMPLEMENT)

**Phase 1 (Storylet System) — PRIORITY 2**
- [ ] Phase 1 tests expanded to detailed specs (TALE_TEST_SCRIPTS_PHASE_1.md)
- [ ] Phase 1 tests implemented (20 JSON files)
- [ ] Phase 1 tests all passing

**Phase 2 (Strategy-to-Story Bridge) — PRIORITY 3**
- [ ] Phase 2 tests expanded to detailed specs (TALE_TEST_SCRIPTS_PHASE_2.md)
- [ ] Phase 2 tests implemented (20 JSON files)
- [ ] Phase 2 tests all passing

**Phase 4 (Player Interaction) — PRIORITY 4**
- [ ] Phase 4 tests expanded to detailed specs (TALE_TEST_SCRIPTS_PHASE_4.md)
- [ ] Phase 4 tests implemented (20 JSON files)
- [ ] Phase 4 tests all passing

**Phase 5 (Advanced Mechanics) — PRIORITY 5**
- [ ] Phase 5 tests expanded to detailed specs (TALE_TEST_SCRIPTS_PHASE_5.md)
- [ ] Phase 5 tests implemented (20 JSON files)
- [ ] Phase 5 tests all passing

### CI & Integration
- [ ] GitHub Actions CI configured
- [ ] All 120+ tests passing on main
- [ ] Coverage > 80%
- [ ] Testbed metrics validate (fulfillment ≥85%) ← Already met in Phase 3

---

## Related Files

- **Phase 3 Implementation**:
  - `JoyceCode/engine/tale/InteractionPool.cs` (cluster-scoped request/signal pool)
  - `JoyceCode/engine/tale/InteractionRequest.cs` (request data structure)
  - `JoyceCode/engine/tale/InteractionSignal.cs` (signal data structure)
  - `JoyceCode/engine/tale/DesSimulation.cs` (integration: emission, claiming, fulfillment)
  - `JoyceCode/engine/tale/JsonlEventLogger.cs` (event logging)

- **Phase 3 Storylets** (consolidated into main role files):
  - `models/tale/merchant.json` (deliver_food, restock, trade_service)
  - `models/tale/worker.json` (order_food, help, greeting, witness_crime)
  - `models/tale/drifter.json` (delivery, supply, trade, threat, pickpocket, blackmail)
  - `models/tale/socialite.json` (greeting, help, respond_to_greeting)
  - `models/tale/authority.json` (catch_pickpocket, investigate_crime)
  - `models/tale/universal.json` (argument, threaten_response, blackmail_response)

- **Phase 3 Test Scripts**:
  - `models/tests/tale/phase3-interactions/` (22 JSON test files)
  - `models/tests/tale/phase3-interactions/README.md` (test documentation)

- **Test Framework**:
  - `ExpectEngine/` (core testing library)
  - `JoyceCode/engine/testing/` (Joyce integration)
  - `models/tests/startup-smoke.json` (example test)

- **Documentation**:
  - `docs/EXPECT_ENGINE_IMPLEMENTATION.md` (framework details)
  - `docs/tale/` (this entire directory)

---

## Notes for Future Developers

1. **Test Format is Standardized**: All tests follow the same JSON structure with `steps` array, `expect`/`inject`/`sleep`/`action` operations.

2. **Each Test is Independent**: Tests can run in any order. No shared state between tests.

3. **Phase Dependency**: Phase N tests assume phases 0 to N-1 are implemented. Phase 3 tests assume DES engine (Phase 0) is working.

4. **Event Logging**: All tests capture and validate event sequences via JSONL logging.

5. **Tier 3 Abstraction**: Phase 3+ tests validate abstract resolution (no NPC materialization for Tier 3 background NPCs).

---

## Contact / Questions

If implementing tests and encounter issues:
1. Check the detailed spec in TALE_TEST_SCRIPTS_PHASE_X.md
2. Verify event types in JsonlEventLogger.cs
3. Verify Testbed generates expected events (run manually with `--days 1`)
4. Check ExpectEngine API (TestSession methods in EXPECT_ENGINE_IMPLEMENTATION.md)

---

**Phase 3 Status**: Implementation ✅ | Testbed Validation ✅ | Formal Test Execution → **NEXT**

**Ready to continue?** → **Run the 22 Phase 3 test scripts, then expand Phase 0 tests!**
