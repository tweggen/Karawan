# TALE Testing — Quick Start Guide

## TL;DR

You now have a complete testing plan for the TALE narrative engine:
- **120+ test scripts** across 6 phases
- **Phase 3 (just completed)** has 22 detailed test specs ready to implement
- **Phases 0, 1, 2, 4, 5** have outline structures to expand

**Right Now**: Read the main docs, then implement Phase 3 tests.

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
- Phase 3 implementation code (InteractionPool, etc.)
- Phase 3 JSON storylets (9 files)
- This test plan

### 🔴 PRIORITY 1 — Implement Phase 3 Tests
**Status**: 22 detailed specs exist (TALE_TEST_SCRIPTS_PHASE_3.md)
**Task**: Create 22 JSON test scripts
**Time Est**: 4-6 hours

Steps:
1. Read TALE_TEST_SCRIPTS_PHASE_3.md (all 22 test specs)
2. Create `models/tests/tale/phase3-interactions/` directory
3. For each spec, write corresponding JSON file:
   - `01-request-postcondition-emission.json`
   - `02-multiple-requests-from-different-npcs.json`
   - ... (20 more)
4. Run test suite:
   ```bash
   for script in models/tests/tale/phase3-interactions/*.json; do
     JOYCE_TEST_SCRIPT="$script" dotnet run --project nogame/nogame.csproj
     if [ $? -ne 0 ]; then echo "FAILED: $(basename $script)"; exit 1; fi
   done
   ```
5. All tests should pass ✓

### 🟡 PRIORITY 2 — Expand & Implement Phase 0 Tests
**Status**: 20 outlines exist (PHASE_0_SKELETON.md)
**Task**: Detail specs, create 20 JSON scripts
**Time Est**: 5-7 hours
**Rationale**: Foundation for all other phases

Steps:
1. Read PHASE_0_SKELETON.md
2. Expand each test outline to detailed spec (like PHASE_3.md)
3. Create 20 JSON files in `models/tests/tale/phase0-des/`
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

- [ ] Phase 3 tests implemented (22 JSON files)
- [ ] Phase 3 tests all passing
- [ ] Phase 0 tests implemented (20 JSON files)
- [ ] Phase 0 tests all passing
- [ ] Phase 1 tests implemented (20 JSON files)
- [ ] Phase 1 tests all passing
- [ ] Phase 2 tests implemented (20 JSON files)
- [ ] Phase 2 tests all passing
- [ ] GitHub Actions CI configured
- [ ] All 120+ tests passing on main
- [ ] Coverage > 80%
- [ ] Testbed metrics validate (fulfillment ≥85%)

---

## Related Files

- **Implementation**:
  - `JoyceCode/engine/tale/InteractionPool.cs` (Phase 3 code)
  - `models/tale/interactions/` (Phase 3 storylets)

- **Existing Test Framework**:
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

**Ready to implement?** → **Read TALE_TEST_SCRIPTS_PHASE_3.md and start coding!**
