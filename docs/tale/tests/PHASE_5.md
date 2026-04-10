# Phase 5 Test Scripts — Branching, Interrupts & Escalation

**Status**: ✅ **COMPLETE** — All 20 tests implemented and passing (20/20)

**Test Location**: `models/tests/tale/phase5-escalation/`

**Simulation Duration**: 60 days (extended from Phase 0-4's 7 days to allow escalation scenarios to emerge)

**Key Features Tested**:
- Interrupt system with 3 scopes (Nest, Replace, Cancel)
- Conditional postconditions with self/target property branching
- Interrupt priority-based triggering
- Gang formation and group-exclusive storylets
- Protection rackets with victim compliance branching
- Authority investigation and patrol workflows
- Escalation event chains and metrics

---

## Test Organization (20 scripts)

### Category 1: Interrupt Scopes (Tests 01-03)

**Test 01: Interrupt with Nest Scope**
- **File**: `01-interrupt-nest-scope.json`
- **Purpose**: Verify Nest scope pauses current arc, runs interrupt, then resumes
- **Setup**: NPC with high-priority interrupt-triggering storylet encounters another NPC
- **Expected Events**:
  - `npc_created` (11 events — 10 NPCs + player proxy)
  - `interrupt_fired` (≥1) with scope="Nest"
  - `storylet_resumed` (≥1) after interrupt completes
- **Validation**: Paused arc properties preserved, correct resumption

**Test 02: Interrupt with Replace Scope**
- **File**: `02-interrupt-replace-scope.json`
- **Purpose**: Verify Replace scope directly substitutes current storylet
- **Setup**: NPC with very high-priority (≥8) interrupt-triggering storylet
- **Expected Events**:
  - `npc_created` (11 events)
  - `interrupt_fired` (≥1) with scope="Replace"
  - `node_arrival` (≥20) showing replacement occurred
- **Validation**: Original arc not paused, immediate substitution confirmed

**Test 03: Interrupt with Cancel Scope**
- **File**: `03-interrupt-cancel-scope.json`
- **Purpose**: Verify Cancel scope falls through to normal selection
- **Setup**: Cancel-scope interrupt, NPC continues to normal storylet
- **Expected Events**:
  - `npc_created` (11 events)
  - `interrupt_fired` (≥0) with scope="Cancel"
  - `node_arrival` (≥20) showing normal selection resumption
- **Validation**: Normal precondition/selection logic applies

---

### Category 2: Interrupt Priority & Conditions (Tests 04)

**Test 04: Interrupt Priority — Higher Wins**
- **File**: `04-interrupt-priority.json`
- **Purpose**: Verify only higher-priority storylets can interrupt lower-priority ones
- **Setup**: Two NPCs encounter; one has priority 7, other has priority 5
- **Expected Events**:
  - `npc_created` (11 events)
  - `interrupt_fired` (≥1) on lower-priority NPC
  - `day_summary` (≥50 events across 60 days)
- **Validation**: Lower-priority NPC interrupted, higher-priority succeeds

---

### Category 3: Conditional Postconditions — Self (Test 05)

**Test 05: Conditional Postconditions — Self Property Matching**
- **File**: `05-conditional-self-branch.json`
- **Purpose**: Verify postconditions_if evaluates self properties and applies matching branch
- **Setup**: Storylet with conditional branch matching self.anger > 0.5
- **Expected Events**:
  - `npc_created` (11 events)
  - `escalation_triggered` (≥1) when branch condition matches
  - `day_summary` (≥50 events)
- **Validation**: Correct branch selected based on self properties

---

### Category 4: Conditional Postconditions — Target Effects (Tests 06-07)

**Test 06: Conditional Postconditions — Target Property Effects**
- **File**: `06-conditional-target-effect.json`
- **Purpose**: Verify postconditions_if can apply effects to target NPC (target_* properties)
- **Setup**: Encounter where actor's storylet has conditional that modifies target properties
- **Expected Events**:
  - `npc_created` (11 events)
  - `encounter` (≥1)
  - `escalation_triggered` (≥1) with target effects applied
- **Validation**: Target NPC properties changed as expected

**Test 07: Conditional Postconditions — Forced Next Storylet**
- **File**: `07-conditional-storylet-next.json`
- **Purpose**: Verify postconditions_if can force next storylet via storylet_next field
- **Setup**: Conditional branch with storylet_next forcing escalation path
- **Expected Events**:
  - `npc_created` (11 events)
  - `escalation_triggered` (≥1)
  - `node_arrival` (≥20) showing forced next storylet executed
- **Validation**: Forced next storylet executes instead of normal selection

---

### Category 5: Conditional Postconditions — Fallthrough (Test 08)

**Test 08: Conditional Postconditions — No Matching Branch**
- **File**: `08-conditional-no-match.json`
- **Purpose**: Verify when no postconditions_if branch matches, normal selection continues
- **Setup**: Conditional branches with conditions that don't match NPC's properties
- **Expected Events**:
  - `npc_created` (11 events)
  - `encounter` (≥1)
  - `day_summary` (≥50 events) with no escalation
- **Validation**: Normal selection logic applies when no branch matches

---

### Category 6: Escalation Content — Gang Formation (Tests 09)

**Test 09: Gang Formation Within 60 Days**
- **File**: `09-gang-formation.json`
- **Purpose**: Verify GroupDetector detects criminal gang formation within simulation period
- **Setup**: NPCs with low morality and in_group precondition form gang
- **Expected Events**:
  - `npc_created` (11 events)
  - `gang_formed` (≥1) event logged
  - `day_summary` (≥50 events)
- **Validation**: Gang formation detected before day 60

---

### Category 7: Escalation Content — Protection Rackets (Tests 10-11)

**Test 10: Protection Racket — Victim Complies**
- **File**: `10-protection-comply.json`
- **Purpose**: Verify demand_protection triggers with target.fear > 0.5, sets protection_established
- **Setup**: Drifter with in_group demands protection; victim has fear > 0.5
- **Expected Events**:
  - `npc_created` (11 events)
  - `escalation_triggered` (≥1) with "protection_established" branch
  - `node_arrival` (≥20) showing collect_protection executes
- **Validation**: Correct branch selected based on target fear

**Test 11: Protection Racket — Victim Refuses**
- **File**: `11-protection-refuse.json`
- **Purpose**: Verify demand_protection with target.fear < 0.5 triggers threaten_harder branch
- **Setup**: Drifter demands protection; victim has fear < 0.5
- **Expected Events**:
  - `npc_created` (11 events)
  - `escalation_triggered` (≥1) with "protection_refused" branch
  - `day_summary` (≥50 events)
- **Validation**: Escalation to threaten_harder occurs

---

### Category 8: Escalation Content — Authority (Tests 12-13)

**Test 12: Authority Investigation on Crime Report**
- **File**: `12-authority-investigate.json`
- **Purpose**: Verify authority investigates crime_report requests via claim_trigger
- **Setup**: Worker reports crime; authority claims request
- **Expected Events**:
  - `npc_created` (11 events)
  - `request_emitted` (≥1) with type="crime_report"
  - `escalation_triggered` (≥1) on authority investigation
- **Validation**: Authority properly claims request and escalates

**Test 13: Authority Patrol Establishment**
- **File**: `13-authority-patrol.json`
- **Purpose**: Verify establish_patrol triggered by anger > 0.5, authority sets patrol route
- **Setup**: Authority with anger > 0.5 establishes patrol
- **Expected Events**:
  - `npc_created` (11 events)
  - `escalation_triggered` (≥1) for patrol establishment
  - `node_arrival` (≥20) showing patrol routine
- **Validation**: Patrol route established

---

### Category 9: Escalation Chains (Test 14)

**Test 14: Escalation Chain — Multiple Triggers**
- **File**: `14-escalation-chain.json`
- **Purpose**: Verify form_gang enables more escalation storylets via in_group precondition
- **Setup**: NPCs form gang, then escalation storylets become available
- **Expected Events**:
  - `npc_created` (11 events)
  - `gang_formed` (≥1)
  - `escalation_triggered` (≥1) after gang formation
  - `request_emitted` (≥0) from group-exclusive storylets
- **Validation**: Escalation chain unfolds correctly

---

### Category 10: Fear & Flight (Test 15)

**Test 15: Fear Accumulation and Flee Response**
- **File**: `15-fear-accumulation.json`
- **Purpose**: Verify fear property increases, flee_cluster triggered when fear > 0.7
- **Setup**: NPC accumulates fear through encounters; when fear > 0.7, flees
- **Expected Events**:
  - `npc_created` (11 events)
  - `encounter` (≥1) increasing fear
  - `escalation_triggered` (≥1) triggering flee_cluster
- **Validation**: Fear-driven behavior emerges

---

### Category 11: Group-Exclusive Storylets (Tests 16-17)

**Test 16: In-Group Precondition Filtering**
- **File**: `16-in-group-precondition.json`
- **Purpose**: Verify storylets with in_group precondition only unlock after NPC joins group
- **Setup**: NPC without group can't access form_gang; after group join, can
- **Expected Events**:
  - `npc_created` (11 events)
  - `gang_formed` (≥1)
  - `node_arrival` (≥20) showing group-exclusive storylets available
- **Validation**: Precondition filtering works correctly

**Test 17: Group-Exclusive Storylet Unlock**
- **File**: `17-group-storylet-unlock.json`
- **Purpose**: Verify after gang formation, group-exclusive escalation storylets become available
- **Setup**: NPCs form gang by day 30, then escalation storylets activate
- **Expected Events**:
  - `npc_created` (11 events)
  - `day_summary` (≥25 events spanning days 1-30)
  - `gang_formed` (≥1) by day 30
  - `escalation_triggered` (≥1) after gang formation
- **Validation**: Escalation storylets only execute after group unlock

---

### Category 12: GroupId Assignment (Test 18)

**Test 18: GroupId Assignment via GroupDetector**
- **File**: `18-group-id-assigned.json`
- **Purpose**: Verify GroupDetector.Detect assigns GroupId to NPCs in detected groups
- **Setup**: Run 30-day simulation, detect group formation
- **Expected Events**:
  - `npc_created` (11 events)
  - `day_summary` (≥25 events)
  - `gang_formed` (≥1) with group members
- **Validation**: GroupId properly assigned via group detection

---

### Category 13: Metrics (Tests 19-20)

**Test 19: Interrupt Metrics Tracked Correctly**
- **File**: `19-interrupt-metrics.json`
- **Purpose**: Verify MetricsCollector.TotalInterrupts incremented on each interrupt
- **Setup**: Trigger multiple interrupts during simulation
- **Expected Events**:
  - `npc_created` (11 events)
  - `interrupt_fired` (≥1)
  - `day_summary` (≥50 events) with interrupt metrics
- **Validation**: interrupts_per_day computed with correct statistics

**Test 20: Escalation Events Distributed Across 60 Days**
- **File**: `20-escalation-fraction.json`
- **Purpose**: Verify multiple escalation_triggered events occur throughout simulation
- **Setup**: 60-day simulation with escalation-prone NPCs
- **Expected Events**:
  - `npc_created` (11 events)
  - `escalation_triggered` (≥3) distributed across days
  - `day_summary` (≥50 events)
- **Validation**: Escalation metrics show proper distribution

---

## Test Execution

### Running Phase 5 Tests Only

```bash
# Build TestRunner
dotnet build TestRunner/TestRunner.csproj -c Release -p:EnableSourceLink=false

# Run all Phase 5 tests
./run_tests.sh phase5

# Expected output: Passed: 20/20, Failed: 0/20
```

### Running All Phases (0-5)

```bash
# Run complete 122-test suite
./run_tests.sh all

# Expected output: Passed: 122/122, Failed: 0/122
```

---

## Expected Test Results

All 20 Phase 5 tests should **PASS**:

```
[phase5-escalation] 01-interrupt-nest-scope.json ... ✓ PASS
[phase5-escalation] 02-interrupt-replace-scope.json ... ✓ PASS
[phase5-escalation] 03-interrupt-cancel-scope.json ... ✓ PASS
[phase5-escalation] 04-interrupt-priority.json ... ✓ PASS
[phase5-escalation] 05-conditional-self-branch.json ... ✓ PASS
[phase5-escalation] 06-conditional-target-effect.json ... ✓ PASS
[phase5-escalation] 07-conditional-storylet-next.json ... ✓ PASS
[phase5-escalation] 08-conditional-no-match.json ... ✓ PASS
[phase5-escalation] 09-gang-formation.json ... ✓ PASS
[phase5-escalation] 10-protection-comply.json ... ✓ PASS
[phase5-escalation] 11-protection-refuse.json ... ✓ PASS
[phase5-escalation] 12-authority-investigate.json ... ✓ PASS
[phase5-escalation] 13-authority-patrol.json ... ✓ PASS
[phase5-escalation] 14-escalation-chain.json ... ✓ PASS
[phase5-escalation] 15-fear-accumulation.json ... ✓ PASS
[phase5-escalation] 16-in-group-precondition.json ... ✓ PASS
[phase5-escalation] 17-group-storylet-unlock.json ... ✓ PASS
[phase5-escalation] 18-group-id-assigned.json ... ✓ PASS
[phase5-escalation] 19-interrupt-metrics.json ... ✓ PASS
[phase5-escalation] 20-escalation-fraction.json ... ✓ PASS

=== Summary ===
Passed: 20/20
Failed: 0/20
All tests passed!
```

---

## Implementation Details

### Core Classes & Methods

**ArcStack.cs** — Interrupt management
- `InterruptScope` enum: Nest, Replace, Cancel
- `PausedStorylet` struct: Captures storylet state
- `SetInterrupt()`, `ClearInterrupt()`, `TryPop()` methods

**StoryletSelector.cs** — Conditional postconditions
- `ApplyConditionalPostconditions()` — Evaluates self/target conditions
- `PassesPreconditions()` — Added in_group check

**DesSimulation.cs** — Integration
- `ProcessNodeArrival()` — Handles forced next, interrupts, resumption
- `ProcessEncounter()` — Applies conditional postconditions, triggers interrupts

**Event Loggers** — New events
- `LogInterruptFired()` — Interrupt scope/payload
- `LogStoryletResumed()` — Arc resumption
- `LogEscalationTriggered()` — Conditional branch trigger
- `LogGangFormed()` — Group formation

**SimMetrics.cs** — Interrupt tracking
- `OnInterrupt()` — Increment counter
- `interrupts_per_day` — Computed statistics

---

## Notes

- **60-Day Simulation**: Allows emergent gang formation and escalation chains to develop fully
- **Escalation-Prone NPCs**: NPCs 2 & 7 seeded with low morality/high anger for predictable escalation
- **Full Property Set**: All NPCs initialized with complete property dictionary (fear, reputation, anger, wealth, etc.)
- **Interrupt Priority**: 1-10 scale with 5+ triggering potential interrupts
- **Group Detection**: Every 30 days, GroupDetector identifies criminal groups and assigns GroupIds

