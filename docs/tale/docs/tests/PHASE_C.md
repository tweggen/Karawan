# Phase C Test Scripts: NPC Conversation System

**Last Audit:** 2026-04-07
**Phase Status:** Proposed (tests defined before implementation)
**Total Tests:** 29 across 4 phases

---

## Test Organization

Tests are organized into 4 phase directories under `models/tests/tale/`:

| Phase | Directory | Test Count | Focus |
|-------|-----------|-----------|-------|
| **C1** | `phaseC1-infrastructure` | 8 | Behavior attachment, generic dialogue, role fallback |
| **C2** | `phaseC2-storylet` | 6 | Storylet override, tag fallback, precedence |
| **C3** | `phaseC3-tone` | 6 | Property-reactive functions, interpolation |
| **C4** | `phaseC4-trust` | 9 | Trust mechanics, memory, quest integration |

---

## Phase C1: Infrastructure & Generic Dialogue (8 tests)

### Purpose
Verify that:
- TaleConversationBehavior attaches/detaches correctly
- Generic dialogue script evaluates property conditions
- Role-specific fallbacks work
- Indoor NPCs are excluded

### C1 Tests

#### 01-conversation-behavior-attaches.json
- **Objective:** Outdoor NPC has Behavior component set to TaleConversationBehavior
- **Setup:** Create outdoor NPC in activity phase
- **Expect:** `npc_created` (11), `node_arrival` (≥10)
- **Validation:** Behavior component visible in UI/logs; E-prompt appears at 12m

#### 02-generic-dialogue-hungry.json
- **Objective:** NPC with hunger > 0.7 triggers hungry branch of tale.generic
- **Setup:** Initialize NPC with hunger=0.8, balanced other properties
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Script selects "hungry" node; outputs food-related line

#### 03-generic-dialogue-angry.json
- **Objective:** NPC with anger > 0.7 triggers angry branch
- **Setup:** Initialize NPC with anger=0.8, balanced other properties
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Script selects "angry" node; outputs dismissive line

#### 04-generic-dialogue-tired.json
- **Objective:** NPC with fatigue > 0.7 triggers tired branch
- **Setup:** Initialize NPC with fatigue=0.8, balanced other properties
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Script selects "tired" node; outputs exhaustion line

#### 05-generic-dialogue-default.json
- **Objective:** NPC with balanced properties triggers default branch
- **Setup:** Initialize NPC with all properties < 0.7
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Script selects "default" node; outputs neutral line ("Hey", "Nice enough day", etc.)

#### 06-role-fallback-worker.json
- **Objective:** Worker NPC at generic storylet falls back to tale.role.worker
- **Setup:** Worker at storylet with no conversation_script or tags
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Script from tale.role.worker plays; worker-typical lines ("Just trying to get through the day")

#### 07-role-fallback-merchant.json
- **Objective:** Merchant NPC at generic storylet falls back to tale.role.merchant
- **Setup:** Merchant at storylet with no conversation_script or tags
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Script from tale.role.merchant plays; business-focused lines

#### 08-indoor-npc-no-conversation.json
- **Objective:** Indoor NPC (IsIndoorActivity=true) does NOT have conversation behavior
- **Setup:** NPC at StayAtStrategyPart with IsIndoorActivity=true
- **Expect:** `npc_created` (11), `node_arrival` (≥10)
- **Validation:** No E-prompt visible; TaleConversationBehavior NOT attached

---

## Phase C2: Storylet-Specific Dialogue (6 tests)

### Purpose
Verify that:
- ConversationScript field on storylets takes precedence
- Tag-based fallback works for unspecified storylets
- 5-level resolution order is respected

### C2 Tests

#### 01-storylet-conversation-script-override.json
- **Objective:** Storylet with `conversation_script` field uses that script
- **Setup:** Create storylet with `"conversation_script": "tale.custom_script"` and tags=[],  TaleNarrationBindings.ResolveScript() checks ConversationScript first
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Narration runs tale.custom_script, not role fallback

#### 02-tag-fallback-routine.json
- **Objective:** Storylet with tags=["routine"] but no conversation_script falls back to tale.tag.routine
- **Setup:** Storylet with no ConversationScript, tags=["routine"]
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Narration runs tale.tag.routine script

#### 03-tag-fallback-eating.json
- **Objective:** Storylet with tags=["eating"] but no conversation_script falls back to tale.tag.eating
- **Setup:** Storylet with no ConversationScript, tags=["eating"]
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Narration runs tale.tag.eating script

#### 04-storylet-script-precedence.json
- **Objective:** ConversationScript takes precedence over tag/role fallback
- **Setup:** Storylet with `conversation_script="tale.specific"` AND tags=["routine"]
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Narration runs tale.specific, NOT tale.tag.routine

#### 05-multiple-tags-first-match.json
- **Objective:** Multiple tags: first existing script wins
- **Setup:** Storylet with tags=["nonexistent_tag", "routine"] and no conversation_script
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Narration tries tale.tag.nonexistent_tag, falls through, uses tale.tag.routine

#### 06-lunch-break-wealth-gated.json
- **Objective:** tale.lunch_break script branches on wealth (poor < 0.2 vs normal)
- **Setup:** Worker at lunch_break storylet with explicit `conversation_script="tale.lunch_break"`
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** If wealth < 0.2, uses poor_lunch node; otherwise normal_lunch node

---

## Phase C3: Property-Reactive Tone (6 tests)

### Purpose
Verify that:
- func.npcMood() returns correct descriptor
- func.npcRole() interpolates role name
- func.npcWealthLabel() returns correct label
- Text interpolation with {func.*} works

### C3 Tests

#### 01-npcmood-frustrated.json
- **Objective:** func.npcMood() returns "frustrated" when anger > 0.7
- **Setup:** NPC with anger=0.8, hunger/fatigue < 0.7, wealth ≥ 0.35
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** func.npcMood() in text evaluates to "frustrated"

#### 02-npcmood-desperate.json
- **Objective:** func.npcMood() returns "desperate" for hunger > 0.7 or wealth < 0.2
- **Setup:** Test both: (a) hunger=0.8, others < 0.7; (b) wealth=0.1, anger/hunger < 0.7
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** func.npcMood() evaluates to "desperate" in both cases

#### 03-npcwealthlabel-broke.json
- **Objective:** func.npcWealthLabel() returns "broke" when wealth < 0.15
- **Setup:** Merchant NPC with wealth=0.1
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** func.npcWealthLabel() in text evaluates to "broke"

#### 04-npcwealthlabel-comfortable.json
- **Objective:** func.npcWealthLabel() returns "comfortable" when wealth 0.35–0.65
- **Setup:** Merchant NPC with wealth=0.5
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** func.npcWealthLabel() evaluates to "comfortable"

#### 05-text-interpolation-with-mood.json
- **Objective:** Text with {func.npcMood()} placeholder renders actual mood descriptor
- **Setup:** Script with line "I'm {func.npcMood()}, if I'm being honest"
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Final text reads e.g., "I'm frustrated, if I'm being honest" (not literal "{func.npcMood()}")

#### 06-speaker-interpolation-role.json
- **Objective:** Speaker field with {func.npcRole()} renders as role name
- **Setup:** Script with speaker: "{func.npcRole()}"
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Speaker label shows actual role (e.g., "merchant", "worker")

---

## Phase C4: Trust, Memory & Quest Hooks (9 tests)

### Purpose
Verify that:
- Trust is initialized and incremented per conversation
- Trust gates dialogue branches
- Memory facts persist
- Quest triggers work from dialogue

### C4 Tests

#### 01-first-conversation-initializes-trust.json
- **Objective:** First conversation with NPC initializes schedule.Trust[-1]
- **Setup:** NPC that hasn't been talked to yet
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** After dialogue: schedule.Trust[-1] ≥ 0.5 (initial default); npc.met_player injected as "true"

#### 02-trust-increments-per-conversation.json
- **Objective:** Each conversation increments trust by +0.02
- **Setup:** Multiple conversations with same NPC (schedule.Trust[-1] starts at 0.5)
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥3)
- **Validation:** After 1st: ~0.52; after 2nd: ~0.54; capped at 1.0

#### 03-trust-gates-dialogue-low.json
- **Objective:** Trust < 0.65 gates dialogue to acquaintance branch
- **Setup:** Authority NPC with schedule.Trust[-1] = 0.5
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Dialog uses "acquaintance" branch ("You again. What is it?"), not "trusted"

#### 04-trust-gates-dialogue-high.json
- **Objective:** Trust > 0.65 gates dialogue to trusted branch
- **Setup:** Authority NPC with schedule.Trust[-1] = 0.7
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Dialog uses "trusted" branch ("Between you and me..."), not "acquaintance"

#### 05-met-player-flag-persists.json
- **Objective:** npc.met_player flag persists across save/load
- **Setup:** Trigger conversation to set met_player=true, then save and load game
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** After load: schedule.Trust contains -1 key; flag restored

#### 06-npc-remember-event-sets-flag.json
- **Objective:** tale.npc.remember event sets persistent memory flag
- **Setup:** Script with `{ "type": "tale.npc.remember", "fact": "gave_quest_hint" }`
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** schedule.Properties["player_fact.gave_quest_hint"] set to 1.0; persists on save

#### 07-quest-trigger-from-dialogue.json
- **Objective:** quest.trigger event fires quest from dialogue
- **Setup:** Script with `{ "type": "quest.trigger", "quest": "nogame.quests.TestQuest.Quest" }`
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Quest is activated/triggered after dialogue ends

#### 08-conversation-cooldown-suppresses-prompt.json
- **Objective:** 30-second cooldown suppresses "E to Talk" prompt after conversation
- **Setup:** Conversation with NPC, then attempt second conversation immediately
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥1)
- **Validation:** Prompt hidden for ~30s; reappears after cooldown expires

#### 09-integration-multi-npc-conversations.json
- **Objective:** Multiple NPCs have independent trust; no state leakage
- **Setup:** Cycle conversations with 3+ different NPCs
- **Expect:** `npc_created` (11), `node_arrival` (≥10), `dialogue_started` (≥3)
- **Validation:** Each NPC accumulates trust independently; no cross-contamination in props injection

---

## Running Phase C Tests

```bash
# Run all Phase C tests
./run_tests.sh phaseC1
./run_tests.sh phaseC2
./run_tests.sh phaseC3
./run_tests.sh phaseC4

# Run all together
./run_tests.sh phaseC1 phaseC2 phaseC3 phaseC4
```

---

## Test Infrastructure Requirements

- **NPC Property Tracking:** Tests initialize NPCs with specific properties (hunger, anger, wealth, trust)
- **Dialogue Event Logging:** `dialogue_started` event fired when narration script begins
- **Property Injection Verification:** Can inspect `props.npc.*` during script execution
- **Save/Load Cycle:** Tests 05–06 require game save/restore
- **Cooldown Tracking:** Tests 08 require DateTime tracking in behavior

---

## Notes

- All tests spawn 11 NPCs via standard tier-1 population
- Conversations are modal (one active narration script at a time)
- Props injection is cleaned up on ScriptEndedEvent
- Trust is persisted via `schedule.HasPlayerDeviation = true`
- Memory flags stored in `schedule.Properties` with `player_fact.` prefix
