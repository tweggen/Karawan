# Phase C6: Storyline Conversation Trees

**Status**: 📋 Proposed
**Created**: 2026-04-10
**Dependency**: Phase C4 (complete ✅), Phase C5 (proposed)
**Estimated Effort**: ~3-4 weeks
**Complexity**: High

---

## Context

Phases C1-C4 create **single-encounter dialogues**: short, reactive conversations between player and NPC. C5 adds **ambient multi-NPC dialogue**: organic chatter between NPCs.

Phase C6 builds **multi-turn narrative conversations**: branching dialogue trees that tell NPC backstories, enable quest acceptance, reveal secrets, and create meaningful player-NPC relationships. Conversations can span multiple encounters and tie into NPC life arcs.

---

## Goals

- NPCs have rich backstories told through dialogue
- Player can unlock different conversation paths based on trust/choices
- Conversations branch on player decisions (not just NPC state)
- Dialogue persists across encounters (continuing a thread weeks later)
- Foundation for NPC character development and player agency
- Support for quest-critical dialogue sequences

---

## Design Concepts (TBD)

### Conversation Structure

How are multi-turn conversations organized?

- **Option A**: Branching tree (player picks from multiple dialogue options each turn)
  - Pros: Player agency, replayability
  - Cons: Complex to author, exponential branches

- **Option B**: Linear with conditional branches (single path, but gate some content on trust/choices)
  - Pros: Simpler to author, still feels dynamic
  - Cons: Less player agency

- **Option C**: Hybrid (mostly linear, key decision points branch to alternates)
  - Pros: Balance between authoring complexity and player choice
  - Cons: Requires careful design to avoid branching explosion

### Persistence & Continuity

How does dialogue persist across time?

- **Option A**: Per-conversation state (did player ask about X? Do NPCs remember player chose Y?)
- **Option B**: Relationship milestones (conversaton unlock → new dialogue available forever)
- **Option C**: Both (ongoing dialogue thread + unlocked topics)

### Conversation Triggers

What initiates a multi-turn conversation?

- **Option A**: Player E-press (like C1-C4, but longer)
- **Option B**: Quest accept/milestone (tied to story progression)
- **Option C**: NPC-initiated (NPC approaches player with news/request)
- **Option D**: All of the above

### Dialogue Options/Choices

How many dialogue options per turn?

- **Option A**: Player always has multiple dialogue options (branching)
- **Option B**: Single path (no choices, but feel of conversation)
- **Option C**: Variable (some turns have choices, some don't)

### Tone & Personality

How much do NPC personality and player history affect dialogue?

- **Option A**: Reuse C3 mood/tone functions (dialogue colors on NPC emotional state)
- **Option B**: Trust-dependent variants (same topic, different tone based on trust level)
- **Option C**: Both + memory (NPC references past conversations, player choices)

---

## Proposed Approach

### Architecture

```
Storyline Conversation System:
  1. Player initiates conversation or NPC triggers story beat
  2. Check conversation preconditions (trust, quest state, seen other dialogue?)
  3. Load conversation tree: tale.story.{npcId}.{conversationId}
  4. Enter conversation loop:
     a. Display NPC dialogue
     b. (If branching) Show player dialogue options
     c. Process player choice (if any)
     d. Execute effects (update quest state, trust, flags)
     e. Advance to next node
  5. On conversation end:
     a. Mark conversation as "seen" in NPC schedule
     b. Trigger any post-conversation effects (quest trigger, NPC behavior change)
     c. Return to world
```

### Key Systems to Add

1. **ConversationTree** — Data structure representing multi-turn dialogue
2. **ConversationEngine** — Executes tree: displays dialogue, processes choices, manages state
3. **ConversationPersistence** — Tracks which conversations seen, per-conversation flags
4. **DialogueOptionUI** — Renders player choice menu with trust/consequence hints
5. **ConversationScriptLoader** — Load & cache conversation trees from JSON

### New Files to Create

| File | Purpose |
|------|---------|
| `nogameCode/nogame/modules/tale/ConversationTree.cs` | Tree data structure |
| `nogameCode/nogame/modules/tale/ConversationEngine.cs` | Tree execution engine |
| `nogameCode/nogame/modules/tale/DialogueOptionUI.cs` | Player choice rendering |
| `nogameCode/nogame/characters/citizen/TaleStorylineBehavior.cs` | Behavior for long-form conversations |
| `models/tale/conversations/tale.story.{role}.json` | NPC-specific storyline conversations |
| `docs/tale/design/CONVERSATION_TREES.md` | Design document |
| `docs/tale/concepts/BRANCHING_DIALOGUE.md` | Branching dialogue concept doc |

### Files to Modify

| File | Change |
|------|--------|
| `nogameCode/nogame/modules/tale/TaleModule.cs` | Register ConversationEngine, load conversation trees |
| `nogameCode/nogame/modules/tale/TaleNarrationBindings.cs` | Adapt for branching (player choice injection) |
| `nogameCode/nogame/characters/citizen/TaleConversationBehavior.cs` | Trigger storyline conversations (vs short encounters) |
| `engine/tale/NpcSchedule.cs` | Add conversation state tracking |
| `nogameCode/nogame/modules/tale/TaleManager.cs` | Query conversation seen state |

---

## Conversation Format (Proposed JSON)

```json
{
  "id": "merchant_backstory_arc_1",
  "title": "Merchant's Tale: The Old Shop",
  "trigger": {
    "precondition": "trust > 0.7",
    "oneTimeOnly": false,
    "requiresQuest": null
  },
  "nodes": {
    "start": {
      "speaker": "{npc.name}, the {func.npcRole()}",
      "text": "You know, I didn't always run this shop...",
      "flow": [
        { "goto": "backstory" }
      ]
    },
    "backstory": {
      "speaker": "{npc.name}",
      "text": "Came here with nothing. Now... well, still not much.",
      "flow": [
        { "if": "trust > 0.8", "goto": "trust_high" },
        { "else": "neutral_response" }
      ]
    },
    "trust_high": {
      "speaker": "{npc.name}",
      "text": "You seem like someone I can trust. The truth is...",
      "flow": [{ "goto": "reveal" }]
    },
    "neutral_response": {
      "speaker": "{npc.name}",
      "text": "Anyway, life goes on.",
      "flow": [{ "goto": "end" }]
    },
    "reveal": {
      "speaker": "{npc.name}",
      "text": "I'm looking to expand. But I need capital...",
      "playerOptions": [
        {
          "text": "I can help. What do you need?",
          "effects": ["questAccept:merchant_expansion"],
          "goto": "accept_quest"
        },
        {
          "text": "That's rough. Good luck.",
          "effects": [],
          "goto": "end"
        }
      ]
    },
    "accept_quest": {
      "speaker": "{npc.name}",
      "text": "Really? Thank you! I need to find a supplier...",
      "flow": [{ "action": "end_conversation", "effects": ["questStart:merchant_expansion"] }]
    },
    "end": {
      "speaker": "{npc.name}",
      "text": "Take care.",
      "flow": [{ "action": "end_conversation" }]
    }
  }
}
```

---

## Test Strategy

**Test Suite: `models/tests/tale/phaseC6-storylines/`** (TBD count, ~20-25 tests)

Proposed test categories:

1. **Tree Loading & Execution** (3-4 tests)
   - Conversation tree loads and executes sequentially
   - Nodes resolve in correct order
   - Properties interpolate in dialogue text

2. **Branching & Conditionals** (4-5 tests)
   - Branching on trust level
   - Branching on quest state
   - Branching on prior conversation flags
   - Dead-end paths (no goto)

3. **Player Choices** (3-4 tests)
   - Player can choose from multiple dialogue options
   - Choice affects conversation path
   - Choice effects (quest trigger, flag set) execute

4. **Persistence** (3 tests)
   - Conversation marked "seen" after completion
   - Can re-initiate one-time-only conversations (blocked)
   - Conversation state persists across saves

5. **Integration** (3-4 tests)
   - Storyline conversations don't interfere with short encounters
   - NPC mood affects storyline tone (C3 functions work)
   - Quest hooks trigger correctly from dialogue

6. **Edge Cases** (2-3 tests)
   - Infinite loop protection (if misconfigured)
   - Missing nodes handled gracefully
   - Empty player options list handled

---

## Known Unknowns / Decisions Needed

- [ ] Branching strategy: Full tree or linear with gates?
- [ ] Player choice UI: Dialogue wheel, text menu, or integrated into narration?
- [ ] One-time vs repeatable conversations?
- [ ] Can NPCs initiate conversations (vs player always presses E)?
- [ ] Memory: Do NPCs reference specific past choices?
- [ ] Conversation length: How many turns before player fatigue?

---

## Success Criteria

✅ All tests pass (phaseC6 suite)
✅ Player can branch dialogue trees based on trust
✅ Conversations persist (mark as seen, effects persist)
✅ Quest integration works (accept quests from dialogue)
✅ NPC backstories feel coherent and rewarding to discover
✅ Player choice matters (visible consequences)
✅ No dead-end bugs (all paths lead somewhere)

---

## Relationship to Other Phases

- **Depends on**: Phase C4 (trust/memory) and C5 (ambient dialogue foundation)
- **Complements**: Phase 4 (quests triggered from storylines)
- **Impacts**: Phase 8 (occupation-based arcs can be told via C6)
- **Enables**: Future phases (romance, faction reputation, deep lore)

---

## Risk Notes

- **Complexity**: Highest of C-phases; requires UI, state machine, complex JSON
- **Authoring**: NPC backstories & branching trees require careful narrative work
- **Performance**: Large conversation trees in memory; need streaming/caching for many NPCs
- **Testing**: Branching explosion (N choices per turn = N^depth paths); need smart test selection
- **Balance**: Too many choices = confusion; too few = feels on-rails

---

## Future Extensions

Post-C6, possible enhancements:

- **C6.5 — Conversation Consequences**: NPC behavior changes based on dialogue choices (betrayal, loyalty, romance)
- **C7 — Faction Dialogue**: NPC conversation trees tied to faction reputation/quests
- **C8 — Dynamic Narratives**: Procedural conversation generation based on NPC properties
- **Romance/Rivalry**: Relationship arcs driven entirely through conversation trees

---

## Appendix: Design Philosophy

Conversation trees should:

1. **Reveal character** — Each NPC has personality, history, motivations
2. **Enable agency** — Player choices matter (not cosmetic)
3. **Respect time** — Conversations don't outstay welcome (5-10 turns max)
4. **Gate content** — Trust/choices unlock new dialogue, not new mechanics
5. **Persist naturally** — NPCs remember, but don't obsess over past conversations
6. **Integrate seamlessly** — Stories don't interrupt simulation, they emerge from it

---

**Next Phase**: Post-C6 systems (faction dynamics, romance/rivalry, procedural narratives)
