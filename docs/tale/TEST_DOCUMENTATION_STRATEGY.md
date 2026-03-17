# Test Documentation Strategy

## Overview

The TALE test documentation consists of detailed test specifications across 7 phases. This document provides a strategy for maintaining and updating these specifications as the system evolves, without losing context or creating conflicts between specification and implementation.

## Current Documentation Structure

### Planning Documents (`docs/tale/TALE_TEST_PLAN.md`)
- High-level test philosophy
- Framework requirements (ExpectEngine)
- Overall test metrics and success criteria
- Phase-by-phase overview with test counts

### Phase-Specific Specifications (`docs/tale/TALE_TEST_SCRIPTS_PHASE_*.md`)
- Detailed specification for each phase's test scripts
- 7 files total (Phase 0-7, some with sub-phases)
- Structure: test objectives, preconditions, expected outcomes, test steps
- **Problem**: Specifications can become stale as implementation evolves

### Phase Design Documents (`docs/tale/PHASE_*.md`)
- Architectural design for each phase
- Implementation details
- Key classes and methods
- Rationale for design decisions

### Actual Test Implementations (`models/tests/tale/phase*/`)
- 171+ JSON test scripts (60-day regression suite)
- Each script follows ExpectEngine format
- **Source of Truth**: These are the authoritative test specifications

## The Challenge

As of March 2026, we have:
1. **Detailed prose specifications** in `TALE_TEST_SCRIPTS_PHASE_*.md` (written upfront)
2. **Actual JSON test implementations** in `models/tests/tale/` (living, evolving)
3. **Design documentation** in `PHASE_*.md` files

**Issue**: The prose specifications can diverge from implementation as:
- Tests are tweaked for clarity or robustness
- New test cases are added (Phase 6 has 49 tests, many added incrementally)
- Test event types change or evolve
- Preconditions are refined

## Recommended Strategy

### Goal

Maintain documentation-as-source-of-truth by:
1. Treating JSON test scripts as the **primary specification**
2. Using prose documentation for **rationale and context**, not detailed specs
3. Implementing **automated generation** of test counts and basic specs
4. Creating **linkage** between design documents and actual tests

### Phase 1: Establish Single Source of Truth

**For each phase (0-7)**:

1. **Review actual test implementations** in `models/tests/tale/phase*/`
   - Count tests per category
   - Extract test names and descriptions from JSON
   - Identify actual event types used
   - Note actual timeout values

2. **Update `TALE_TEST_SCRIPTS_PHASE_*.md`** with:
   - Accurate test counts and categories
   - References to actual test filenames (e.g., "See `01-initialization.json`")
   - Actual preconditions from test setup
   - Actual expected outcomes from assertions
   - Actual timeout values from implementation

3. **Add cross-references**:
   - Link from prose spec to actual JSON file: `(See implementation: `models/tests/tale/phase0-des/01-initialization.json`)`
   - Link from design doc to test spec: "Validated by Phase X tests 01-03"
   - Link from test spec to design doc: "See PHASE_X.md for design rationale"

### Phase 2: Continuous Maintenance

**When adding new tests**:

1. Create JSON test script in `models/tests/tale/phase*/`
2. Document in relevant `PHASE_*.md` design doc
3. **Don't immediately update `TALE_TEST_SCRIPTS_PHASE_*.md`** prose
   - Instead: Add a note in the phase's `README.md` (auto-generated if needed)
   - Or: Update the count at the top of the spec file
   - Keep detailed specs in prose only for foundational tests

4. **Quarterly update cycle**:
   - Once per quarter (or per major phase), audit `TALE_TEST_SCRIPTS_*.md`
   - Update test counts, categories, descriptions
   - Verify assertions still match implementation
   - Document any significant changes since last update

**When modifying existing tests**:

1. Update JSON test script
2. If change is significant (different preconditions, event types, assertions):
   - Update corresponding section in `TALE_TEST_SCRIPTS_PHASE_*.md`
   - Add note: "Updated MM/DD/YYYY to reflect..."
   - Link to commit that changed test

3. If change is minor (timeout adjustments, wording):
   - No update to prose spec required

### Phase 3: Documentation Linkage

Create a system of cross-references:

#### In `PHASE_X.md` (Design Documents)

Add a "Test Coverage" section:

```markdown
## Test Coverage

This phase is validated by the regression test suite Phase X:

| Test Category | Tests | Files | Validates |
|---|---|---|---|
| Initialization | 3 | `01-initialization.json`, `02-npc-creation.json`, `03-spatial-model.json` | DES core setup |
| Event Queue | 4 | `04-event-queue-order.json` through `07-long-simulation.json` | Event scheduling and ordering |

See `docs/tale/TALE_TEST_SCRIPTS_PHASE_0.md` for detailed specifications.
```

#### In `TALE_TEST_SCRIPTS_PHASE_X.md` (Spec Documents)

Add references to actual implementations:

```markdown
### Test 01: DES Simulation Initialization

**Implementation**: See `models/tests/tale/phase0-des/01-initialization.json`

**Objective**: Verify that DesSimulation.Initialize() correctly creates...

**Test Steps**:
1. Expect `npc_created` event (x10)
2. Sleep 100ms
3. ...

[etc.]
```

#### In test README files (`models/tests/tale/phase*/README.md`)

Include auto-generated summary:

```markdown
# Phase 0 DES Engine Tests

**Total Tests**: 20
**Categories**: Initialization (3), Event Queue (4), Property Dynamics (3), ...
**Regression Duration**: 60 days
**Recalibration Duration**: 365 days

See `docs/tale/TALE_TEST_SCRIPTS_PHASE_0.md` for detailed specifications.
```

### Phase 4: Automated Verification

Create a simple script to verify consistency:

```bash
# verify_test_docs.sh - Check that documented test counts match reality

for phase in 0 1 2 3 4 5 6 7; do
  actual=$(ls models/tests/tale/phase$phase/*.json 2>/dev/null | wc -l)
  documented=$(grep -E "^Total.*:.*[0-9]" docs/tale/PHASE_$phase.md 2>/dev/null | head -1)
  echo "Phase $phase: $actual tests"
done
```

Result: Easy to spot documentation drift at a glance.

## Maintenance Schedule

### Daily/Per-Commit
- Update JSON test script
- Commit message explains change: "Fix timeout in test 05" or "Add test 22-request-edge-case"
- No prose doc update required for minor changes

### Weekly
- If multiple tests added/changed: update relevant `PHASE_*.md` file
- Add test coverage section if missing

### Monthly
- Review `TALE_TEST_SCRIPTS_PHASE_*.md` for any major divergences
- If discrepancies found: decide whether to:
  - Update prose spec to match implementation (common case)
  - Update implementation to match spec (rare, indicates spec was intentional)

### Quarterly (Major Update Cycle)
- Full audit of test documentation
- Regenerate test count summaries
- Verify cross-references are accurate
- Update `Test Coverage` sections in design docs

## Documentation Templates

### Template: Updated Test Specification

When significant changes are made, update the spec like this:

```markdown
### Test 08: Phase Storylets

**Implementation**: `models/tests/tale/phase2-strategies/08-phase-storylets.json`

**Last Updated**: 2026-03-17 (added support for multi-phase transitions)

**Objective**: Verify that strategy phases...

**Preconditions**: [from actual test]
- Strategy with 2 phases created
- Phase 1 configured with duration constraint
- Phase 2 unlocked on phase 1 completion

[etc.]

**Changes Since Last Release**:
- ✅ 2026-03-17: Added validation for pre-computed routes
```

### Template: New Phase's Documentation

When a new phase is added:

1. Create `docs/tale/PHASE_N.md` — Architecture & Design
2. Create `docs/tale/TALE_TEST_SCRIPTS_PHASE_N.md` — Detailed Specs
3. Create `models/tests/tale/phaseN-*/README.md` — Test Summary
4. Link from `docs/tale/TALE_TEST_PLAN.md`
5. Update `TESTBED_PLAN.md` with new phase

## Handling Documentation Gaps

### For Phases Already Implemented (0-7)

**Action**: Don't try to create perfect retroactive specs. Instead:

1. Keep existing prose specs as-is (they document the intent)
2. Add a "Cross-Reference" section:
   ```markdown
   ## Cross-Reference to Implementation

   Actual tests: `models/tests/tale/phase6-population/`

   | Spec Section | Actual Tests | Matches |
   |---|---|---|
   | Group 1 | 01-10 | ✅ Yes |
   | Group 2 | 11-30 | ⚠️ Expanded (now 11-30, was 11-25) |
   | Group 3 | 31-49 | ✅ Yes |
   ```

3. Note any discrepancies and update as time permits

### For New Phases (8+)

**Action**: Write spec and implementation **in parallel**:

1. Design phase (PHASE_N.md): What we want to validate
2. Spec phase (TALE_TEST_SCRIPTS_PHASE_N.md): How we'll test it
3. Implementation (models/tests/tale/phaseN-*/): JSON test scripts
4. Keep all three in sync

## Benefits of This Approach

✅ **Single Source of Truth**: JSON tests are authoritative
✅ **Rationale Preserved**: Design docs explain why
✅ **Maintainability**: No dual-maintenance of identical info
✅ **Scalability**: Easy to add phases without creating documentation debt
✅ **Discoverability**: Clear links between layers
✅ **Automation-Ready**: Scripts can verify consistency

## Example Workflow

### Developer adds test

```bash
# 1. Add test
cp models/tests/tale/phase6-population/49-save-file-size-bounded.json \
   models/tests/tale/phase6-population/50-load-restore-verify.json
# Edit JSON...

# 2. Update design doc
# Edit docs/tale/PHASE_6.md - add bullet under "Persistence" section

# 3. Commit
git add models/tests/tale/phase6-population/50-load-restore-verify.json
git add docs/tale/PHASE_6.md
git commit -m "Add test 50: Load/restore verification

Tests that saved deviations correctly restore state.
Covers edge case where deviated NPC list is empty."

# 4. Quarterly: update spec doc
# (Wait for 3-4 tests to accumulate, then batch update TALE_TEST_SCRIPTS_PHASE_6.md)
```

## Conclusion

By treating JSON test implementations as the primary spec and prose documentation as supporting rationale, we maintain:
- **Accuracy**: Specs match implementation by definition
- **Clarity**: Rationale is clear and searchable
- **Scalability**: Easy to add tests and phases
- **Maintainability**: Reduced documentation burden

This strategy balances the need for detailed specification with the reality of evolving test suites.
