# TALE Phase 4: Player Integration (Quests & Navigation) — Test Specifications

## Overview

Phase 4 tests validate the **player quest system** that integrates the NPC interaction system (Phase 3) with player-facing gameplay. Players can receive quests from NPCs with active requests, navigate to quest locations via satnav, and complete objectives.

**Test Framework**: ExpectEngine with JSON format (same as Phases 0, 1, 2, 3)
**Test Location**: `models/tests/tale/phase4-player/` (20 JSON scripts)
**Execution**: TestRunner CLI harness
**Dependencies**: Phase 3 (interaction system) + Phase 2 (strategy foundation)

---

## Test Categories (20 Scripts)

### Category 1: Quest System Basics (3 tests)

#### Test 01: Player Quest Trigger
**File**: `01-player-quest-trigger.json`
**Priority**: Critical
**Objective**: Verify player meets NPC with active request → quest triggered

**Preconditions**:
- DES running with NPCs and player character spawned
- NPC has active interaction request (from Phase 3 interaction pool)
- Player can navigate and encounter NPCs

**Steps**:
1. Wait for NPC creation (requester NPC)
2. Wait for request emission (request_emitted event)
3. Wait for player spawned event (or verified via game state)
4. Simulate player movement to NPC location (encounter)
5. Verify quest triggered (quest_triggered event or equivalent)

**Expected Outcome**:
- Quest created with ID, title, description from NPC request
- Quest phase set to initial phase (typically waiting or navigating)
- Player notified of quest trigger (event logged)

**Implementation Notes**:
- Quest title derived from request type (e.g., "Deliver Package" for food_delivery)
- Quest reward set based on NPC reputation, request urgency
- Player location/position affects encounter trigger

---

#### Test 02: Quest Data Structure
**File**: `02-quest-data-structure.json`
**Priority**: High
**Objective**: Verify quest has ID, title, description, phase, reward

**Preconditions**:
- Quest created (test 01 passes)

**Steps**:
1. Trigger quest (player meets NPC with request)
2. Query quest data: ID (unique), title (non-empty), description, phase (0), reward
3. Verify all fields populated correctly
4. Verify quest can be queried by ID

**Expected Outcome**:
- Quest has unique ID (distinct from all other quests)
- Title and description match NPC request context
- Phase starts at 0
- Reward >= 1 (gold, XP, or reputation)

---

#### Test 03: Quest Registry
**File**: `03-quest-registry.json`
**Priority**: High
**Objective**: Verify QuestFactory maintains active quest list

**Preconditions**:
- Multiple NPCs with requests active

**Steps**:
1. Trigger quest 1 (NPC A)
2. Trigger quest 2 (NPC B)
3. Query active quest list
4. Verify both quests in list
5. Complete quest 1
6. Query active quest list
7. Verify quest 1 removed, quest 2 still active

**Expected Outcome**:
- Quest registry tracks active quests
- Completed quests removed from active list
- Can query quest by ID or list all active

---

### Category 2: Quest Phases (3 tests)

#### Test 04: Quest Phase 0 — Wait
**File**: `04-quest-phase-0-wait.json`
**Priority**: High
**Objective**: Verify Phase 0: player waits for event/signal

**Preconditions**:
- Quest triggered and in phase 0
- Quest objective: wait for NPC to complete their task

**Steps**:
1. Quest triggered, phase 0 (waiting for signal)
2. NPC executing strategy to fulfill request (phase 2)
3. Wait for signal emission (signal_emitted event, request_fulfilled)
4. Verify quest receives signal and transitions to phase 1

**Expected Outcome**:
- Phase 0 blocks player from immediate action
- NPC works on task (visible in DES events)
- Signal causes phase transition

---

#### Test 05: Quest Phase 1 — Navigate
**File**: `05-quest-phase-1-navigate.json`
**Priority**: High
**Objective**: Verify Phase 1: player navigates to location

**Preconditions**:
- Quest in phase 1 (after receiving signal)
- Quest objective: go to destination location

**Steps**:
1. Quest transitions to phase 1 (navigate to destination)
2. Destination set from NPC location or request metadata
3. Satnav creates route to destination
4. Simulate player movement toward destination
5. Monitor distance-to-goal decrease
6. Player reaches destination (distance < threshold)

**Expected Outcome**:
- Phase 1 sets clear navigation goal
- Satnav active with route displayed
- Player can follow satnav to destination
- Progress tracked via distance metric

---

#### Test 06: Quest Phase 2 — Complete
**File**: `06-quest-phase-2-complete.json`
**Priority**: High
**Objective**: Verify Phase 2: player interacts with NPC, receives reward

**Preconditions**:
- Quest phase 1 complete, player at destination
- Destination NPC present

**Steps**:
1. Player at destination, encounters destination NPC
2. Interaction triggered (dialogue or automatic)
3. Quest phase 2: complete interaction
4. NPC confirms quest completion
5. Verify reward applied (gold, XP, reputation)
6. Quest marked as completed

**Expected Outcome**:
- Phase 2 requires player-NPC interaction
- Reward applied to player inventory/stats
- Quest moved from active to completed list
- Event logged: quest_completed with success flag

---

### Category 3: Satnav Integration (3 tests)

#### Test 07: Satnav Route Creation
**File**: `07-satnav-route-creation.json`
**Priority**: High
**Objective**: Verify active quest creates route marker + satnav path

**Preconditions**:
- Quest in phase 1 (navigate)
- Destination location known

**Steps**:
1. Quest phase 1 active
2. Verify route marker created at destination (satnav shows target)
3. Verify satnav path calculated from player position to destination
4. Verify route visible in game (marker at destination)

**Expected Outcome**:
- Route marker placed at destination location
- Satnav calculates shortest path
- Player can see destination on satnav
- Route updates if player moves

---

#### Test 08: Satnav Progress
**File**: `08-satnav-progress.json`
**Priority**: High
**Objective**: Verify satnav shows distance, updates on player movement

**Preconditions**:
- Satnav active with route to destination
- Player moving toward destination

**Steps**:
1. Check distance-to-destination (initial distance D)
2. Simulate player moving toward destination
3. Check distance again (should decrease)
4. Verify satnav updates distance/heading in real-time
5. Continue movement until distance < arrival threshold

**Expected Outcome**:
- Distance metric accurate
- Satnav updates as player moves
- Heading/direction correct
- No lag in satnav updates

---

#### Test 09: Satnav Arrival
**File**: `09-satnav-arrival.json`
**Priority**: High
**Objective**: Verify satnav clears when player reaches destination

**Preconditions**:
- Satnav active, player moving toward destination
- Player reaches destination

**Steps**:
1. Monitor satnav active state
2. Simulate player reaching destination (position ~ destination)
3. Verify arrival event triggered
4. Verify satnav cleared/hidden
5. Verify route marker removed
6. Quest transitions to phase 2 (complete) or auto-completes

**Expected Outcome**:
- Arrival detected within threshold distance
- Satnav deactivated
- Route markers removed
- Quest phase advances automatically

---

### Category 4: Quest Log UI (3 tests)

#### Test 10: Quest Log Display
**File**: `10-quest-log-display.json`
**Priority**: High
**Objective**: Verify Quest Log shows active, completed, failed quests

**Preconditions**:
- Player has active, completed, and failed quests
- Quest Log UI accessible

**Steps**:
1. Trigger multiple quests (at least 2 active)
2. Complete one quest
3. Fail another quest (timeout or condition)
4. Open Quest Log
5. Verify active quests listed in tab
6. Verify completed quests listed in tab
7. Verify failed quests listed in tab
8. Verify quest count accurate for each tab

**Expected Outcome**:
- Quest Log has 3 tabs: Active, Completed, Failed
- Each tab shows correct quests
- Quest titles, descriptions visible
- Quest progress indicators present

---

#### Test 11: Quest Follow/Unfollow
**File**: `11-quest-follow-unfollow.json`
**Priority**: High
**Objective**: Verify player can follow/unfollow (max 1 followed at a time)

**Preconditions**:
- Multiple active quests in Quest Log
- Follow/unfollow UI available

**Steps**:
1. Open Quest Log
2. Quest A active, unfollowed
3. Click Follow on Quest A
4. Verify Quest A highlighted as followed
5. Verify satnav shows Quest A's route
6. Click Follow on Quest B
7. Verify Quest A unfollowed, Quest B now followed
8. Verify satnav shows Quest B's route
9. Click Unfollow on Quest B
10. Verify no quest followed, satnav cleared

**Expected Outcome**:
- Only one quest can be followed at a time
- Followed quest shows marker and route
- Unfollowing quest clears satnav
- UI shows followed status clearly

---

#### Test 12: Quest Auto-Advance
**File**: `12-quest-auto-advance.json`
**Priority**: High
**Objective**: Verify on quest completion, next quest auto-follows

**Preconditions**:
- Player has 3 active quests
- Quest 1 currently followed
- No other quest explicitly followed

**Steps**:
1. Quest 1 followed (satnav active)
2. Complete Quest 1
3. Verify Quest 1 moved to completed list
4. Verify Quest 2 auto-follows (satnav updates)
5. Complete Quest 2
6. Verify Quest 3 auto-follows
7. Complete Quest 3
8. Verify no quest auto-followed (all completed)
9. Verify satnav cleared

**Expected Outcome**:
- Next available active quest auto-follows on completion
- Satnav updates automatically
- No manual follow/unfollow needed for progression
- Intelligent: skips failed quests, only offers active

---

### Category 5: Multiple Quests (3 tests)

#### Test 13: Multiple Active Quests
**File**: `13-multiple-active-quests.json`
**Priority**: High
**Objective**: Verify player can have multiple active quests simultaneously

**Preconditions**:
- Multiple NPCs with active requests
- Player can encounter multiple NPCs

**Steps**:
1. Encounter NPC A, trigger Quest A
2. Encounter NPC B, trigger Quest B
3. Encounter NPC C, trigger Quest C
4. Query active quest list
5. Verify 3 quests in list, all active
6. Verify can navigate between them (follow/unfollow)
7. Complete Quest A
8. Verify Quests B, C still active

**Expected Outcome**:
- No hard limit on active quests
- Player can manage multiple objectives
- Quest Log shows all active quests
- Progress tracked per quest

---

#### Test 14: Quest Priority
**File**: `14-quest-priority.json`
**Priority**: Medium
**Objective**: Verify high-priority quest can interrupt lower-priority

**Preconditions**:
- Player following low-priority quest
- High-priority urgent quest becomes available

**Steps**:
1. Follow Quest A (low priority, e.g., fetch item)
2. Encounter NPC with high-priority quest (urgent, e.g., emergency)
3. Verify high-priority quest notification
4. Verify auto-follow switches to high-priority quest
5. Low-priority quest still in active list (not removed)
6. After high-priority quest complete, can resume low-priority

**Expected Outcome**:
- High-priority quests can interrupt lower-priority
- Player notified of interruption
- Interrupted quest remains active (not failed)
- Can resume interrupted quest later

---

#### Test 15: Quest Abandonment
**File**: `15-quest-abandonment.json`
**Priority**: Medium
**Objective**: Verify player can abandon active quest

**Preconditions**:
- Player has active quest
- Quest Log UI available

**Steps**:
1. Open Quest Log
2. Select active quest
3. Click Abandon button
4. Confirm abandonment
5. Verify quest moved to abandoned/failed list
6. Verify quest no longer in active list
7. If that quest was followed, verify next active quest auto-follows

**Expected Outcome**:
- Player can abandon quests without penalty (or with reputation cost)
- Abandoned quest tracked separately
- No soft-lock from bad quest choices
- Next quest auto-follows if needed

---

### Category 6: Feedback & Rewards (5 tests)

#### Test 16: Quest Triggered Toast
**File**: `16-quest-triggered-toast.json`
**Priority**: Medium
**Objective**: Verify toast appears when quest triggered

**Preconditions**:
- Player encounters NPC with request
- Quest triggered

**Steps**:
1. Encounter NPC with active request
2. Verify quest_triggered event
3. Verify toast UI appears (text + duration)
4. Toast displays quest title and short description
5. Toast appears for 3-5 seconds then fades
6. Multiple quests triggered show multiple toasts

**Expected Outcome**:
- Toast feedback for quest trigger
- Text readable and relevant
- Duration appropriate (not too fast, not too slow)
- Doesn't block gameplay

---

#### Test 17: Quest Completion Toast
**File**: `17-quest-completion-toast.json`
**Priority**: Medium
**Objective**: Verify toast appears on completion with reward display

**Preconditions**:
- Quest in final phase, ready to complete
- Destination reached, interaction ready

**Steps**:
1. Complete quest (reach destination and interact)
2. Verify quest_completed event
3. Verify toast appears with completion message
4. Toast displays reward (gold: +50, XP: +100, etc.)
5. Toast displays for 4-6 seconds
6. Player can proceed immediately (toast doesn't block)

**Expected Outcome**:
- Completion toast feedback
- Reward clearly displayed
- Toast duration allows reading
- No gameplay blocking

---

#### Test 18: Quest Reward Application
**File**: `18-quest-reward-application.json`
**Priority**: Medium
**Objective**: Verify inventory/gold/XP updated on completion

**Preconditions**:
- Quest with reward defined
- Player inventory and stats accessible

**Steps**:
1. Check initial player gold, XP, reputation
2. Complete quest with known reward (gold: 100, XP: 50, reputation: +10 with NPC)
3. Query player stats after quest
4. Verify gold: +100
5. Verify XP: +50
6. Verify reputation with NPC: +10
7. Verify inventory updated if reward includes items

**Expected Outcome**:
- Rewards applied correctly
- Stats updated in real-time
- Inventory reflects new items
- Rewards persist (save/load preserves them)

---

#### Test 19: Quest Failure Conditions
**File**: `19-quest-failure-conditions.json`
**Priority**: Medium
**Objective**: Verify quest can fail (timeout, dialogue choice, etc.)

**Preconditions**:
- Quest with failure condition (timeout or condition-based)
- Quest timeout set to reasonable value

**Steps**:
1. Trigger quest with timeout (e.g., 1 hour sim-time)
2. Simulate waiting beyond timeout without completing
3. Verify quest fails (quest_failed event)
4. Verify quest moved to failed list
5. Verify player can abandon and retry
6. Alternatively: quest with condition (e.g., "must not be detected")
7. Player violates condition
8. Verify quest fails

**Expected Outcome**:
- Quests can fail for valid reasons
- Failure tracked and visible
- Player can retry (new quest instance from same NPC)
- Failure doesn't block game progression

---

#### Test 20: NPC Dialogue Integration
**File**: `20-quest-dialogue-integration.json`
**Priority**: Low
**Objective**: Verify quest triggered via NPC dialogue, not just encounters

**Preconditions**:
- Player can interact with NPC via dialogue system
- NPC has active request

**Steps**:
1. Encounter NPC (no auto-trigger)
2. Open dialogue with NPC
3. NPC offers quest in conversation
4. Player accepts quest via dialogue choice
5. Verify quest triggered and tracked
6. Verify dialogue informs player of quest details
7. Quest can be accepted or declined in dialogue

**Expected Outcome**:
- Dialogue system integrates with quest system
- Player agency in quest acceptance
- Quest details provided in dialogue
- Declined quests don't create false entries

---

## Execution Guide

### Prerequisites
```bash
# Build TestRunner
dotnet build TestRunner/TestRunner.csproj -c Release -p:EnableSourceLink=false
```

### Run All Phase 4 Tests
```bash
for script in models/tests/tale/phase4-player/*.json; do
  JOYCE_TEST_SCRIPT="tests/tale/phase4-player/$(basename $script)" \
    ./TestRunner/bin/Release/net9.0/TestRunner
  if [ $? -ne 0 ]; then
    echo "FAILED: $script"
    exit 1
  fi
done
```

### Run Single Test
```bash
JOYCE_TEST_SCRIPT="tests/tale/phase4-player/01-player-quest-trigger.json" \
  ./TestRunner/bin/Release/net9.0/TestRunner
```

### Expected Output
```
[TEST] PASS: Quit action: pass
[TEST] Elapsed: 00:00:XX.XXXXXXXX
```

---

## Test Metadata

Each Phase 4 test JSON includes:
```json
{
  "name": "test-name",
  "description": "What this validates",
  "phase": "phase-4",
  "category": "player-category",
  "priority": "critical|high|medium|low",
  "globalTimeout": 60,
  "steps": [
    {"expect": {"type": "event.type", "code": "optional"}, "timeout": 30},
    {"action": "quit", "result": "pass"}
  ]
}
```

---

## Summary

| Test # | Name | Priority | Category |
|--------|------|----------|----------|
| 01 | player-quest-trigger | Critical | Basics |
| 02 | quest-data-structure | High | Basics |
| 03 | quest-registry | High | Basics |
| 04 | quest-phase-0-wait | High | Phases |
| 05 | quest-phase-1-navigate | High | Phases |
| 06 | quest-phase-2-complete | High | Phases |
| 07 | satnav-route-creation | High | Satnav |
| 08 | satnav-progress | High | Satnav |
| 09 | satnav-arrival | High | Satnav |
| 10 | quest-log-display | High | UI |
| 11 | quest-follow-unfollow | High | UI |
| 12 | quest-auto-advance | High | UI |
| 13 | multiple-active-quests | High | Multiple |
| 14 | quest-priority | Medium | Multiple |
| 15 | quest-abandonment | Medium | Multiple |
| 16 | quest-triggered-toast | Medium | Feedback |
| 17 | quest-completion-toast | Medium | Feedback |
| 18 | quest-reward-application | Medium | Feedback |
| 19 | quest-failure-conditions | Medium | Feedback |
| 20 | quest-dialogue-integration | Low | Story |

**Total**: 20 tests covering player quest lifecycle, satnav navigation, quest log UI, and integration with NPC dialogue system.

---

## Dependencies

- **Phase 3 Required**: Interaction system must emit request_emitted and signal_emitted events
- **Phase 2 Required**: NPC strategies provide quest context and fulfillment timeline
- **Phase 1 Required**: Storylet system provides NPC behavior and context
- **Phase 0 Required**: DES engine provides time progression and encounter events
