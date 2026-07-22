# Phase C5: Ambient NPC-NPC Dialogue

**Status**: 📋 Proposed
**Created**: 2026-04-10
**Dependency**: Phase C4 (complete ✅)
**Estimated Effort**: ~2-3 weeks
**Complexity**: Medium

---

## Context

Phases C1-C4 enable **player-NPC dialogue**: NPCs recognize the player, remember past conversations, and respond in character. But NPCs don't talk to each other. The world feels static outside of player interaction.

Phase C5 adds **ambient NPC-NPC dialogue**: organic conversations between NPCs that occur during encounters, enriching the world simulation without requiring player input. NPCs discuss recent events, argue, flirt, make deals—overheard by the player.

---

## Goals

- NPCs engage in multi-NPC conversations during encounters
- Dialogue is organic and driven by NPC relationships/storylets
- Player can overhear without forcing interaction
- Spatial audio or floating text displays dialogue location
- Foundation for NPC communities and group dynamics

---

## Design Points (TBD)

### Trigger Mechanism
When do ambient dialogues happen?
- Option A: During any multi-NPC encounter (any location with 2+ NPCs)
- Option B: Specific storylet flags (e.g., `ambient_dialogue: true`)
- Option C: NPC proximity + relationship state (trust, group membership)

### Script Resolution
How are ambient dialogue scripts selected?
- Option A: New `tale.ambient.*` script hierarchy (fallback like C1-C4)
- Option B: Reuse player conversation scripts, filter for "overheard" variants
- Option C: Hybrid (dedicated ambient scripts, fall back to general)

### Display & Audio
How does the player perceive dialogue?
- Option A: Floating text above NPCs (3D world space)
- Option B: Spatial audio (directional sound) only
- Option C: Both (text for readability, audio for ambiance)

### Conversation Length
How long are ambient dialogues?
- Option A: Single exchange (2-4 lines per NPC)
- Option B: Multi-turn (5-10 lines, full micro-conversation)
- Option C: Variable (depends on relationship state, storylet)

### NPC Roles in Dialogue
Can NPCs play different roles in ambient conversations?
- Option A: Same as player conversations (speaker is role-dependent)
- Option B: Simplified roles (just character name, no role prefix)
- Option C: Role-less (generic format different from player dialogue)

---

## Proposed Approach

### Architecture

```
Ambient Dialogue System:
  1. Encounter trigger (multi-NPC in same location)
  2. Select which NPCs participate (optional: max 3-4 NPCs)
  3. Query ambient dialogue pool for pair(s)
  4. Resolve script: tale.ambient.{role1}_{role2} (fallback: tale.ambient.generic)
  5. Inject participating NPCs' props into narration
  6. Execute script with spatial positioning
  7. Display/audio per player proximity & settings
```

### Key Systems to Add

1. **AmbientDialoguePool** — Registry of ambient dialogue scenarios by role pair/group type
2. **AmbientDialogueSelector** — Choose which NPC pairs in encounter get dialogue (based on relationship, storylet flags)
3. **SpatialAudioRenderer** — Position dialogue in 3D space (if using audio)
4. **DialogueDisplayManager** — Floating text UI that fades with distance
5. **NPC Pair Relationship State** — Track "recently talked to each other" to prevent spam

### New Files to Create

| File | Purpose |
|------|---------|
| `nogameCode/nogame/modules/tale/AmbientDialoguePool.cs` | Registry and selection logic |
| `nogameCode/nogame/modules/tale/AmbientDialogueSelector.cs` | Choose participating NPC pairs |
| `nogameCode/nogame/modules/tale/SpatialDialogueRenderer.cs` | Render dialogue in 3D space |
| `models/tale/conversations/tale.ambient.generic.json` | Generic ambient dialogue |
| `models/tale/conversations/tale.ambient.{role}.json` | Role-specific ambient dialogues |
| `docs/tale/design/AMBIENT_DIALOGUE.md` | Design document |

### Files to Modify

| File | Change |
|------|--------|
| `nogameCode/nogame/modules/tale/TaleModule.cs` | Register AmbientDialoguePool, wire into encounter lifecycle |
| `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` | Trigger ambient dialogue during encounters |
| `nogameCode/nogame/modules/tale/TaleNarrationBindings.cs` | Adapt prop injection for multi-NPC dialogue |

---

## Test Strategy

**Test Suite: `models/tests/tale/phaseC5-ambient/`** (TBD count, ~15-20 tests)

Proposed test categories:

1. **Trigger & Selection** (3-4 tests)
   - Ambient dialogue triggers on multi-NPC encounter
   - Dialogue selected based on NPC relationship state
   - Spam prevention (cooldown between conversations)

2. **Script Resolution** (3 tests)
   - Fallback chain (role_pair → role_generic → generic)
   - Props injection for 2+ NPCs
   - Speaker interpolation with multiple participants

3. **Spatial Rendering** (2-3 tests)
   - Dialogue displays at correct 3D position
   - Text fades/disappears with distance
   - Audio positioning (if implemented)

4. **Interaction** (2-3 tests)
   - Player can initiate follow-up with participants
   - Dialogue state persists (NPCs remember they talked)
   - Group dialogue (3+ NPCs) if supported

5. **Integration** (3-4 tests)
   - Ambient dialogue coexists with player dialogue
   - Different locations trigger different dialogues
   - Relationship/trust affects dialogue tone

---

## Known Unknowns / Decisions Needed

- [ ] Trigger mechanism: Encounter-based or storylet-flagged?
- [ ] Max participants: 2 NPCs (pairs) or allow groups (3+)?
- [ ] Script storage: New `tale.ambient.*` or reuse player scripts?
- [ ] Spam prevention: Cooldown duration (5s? 30s? 1m)?
- [ ] Display: Text, audio, or both?
- [ ] Long-form or single-exchange only?

---

## Success Criteria

✅ All tests pass (phaseC5 suite)
✅ NPCs naturally converse during encounters
✅ Player can discover NPC relationships through overhearing
✅ Dialogue tone reflects NPC mood/trust
✅ No performance impact on multi-NPC scenarios
✅ Spatial audio or text positioning works correctly

---

## Relationship to Other Phases

- **Depends on**: Phase C4 (trust/memory foundation)
- **Enables**: Phase C6 (multi-turn storytelling)
- **Complements**: Phase 6 (population simulation with richer NPC dynamics)

---

## Risk Notes

- **Complexity**: Multi-NPC prop injection + spatial rendering is higher complexity than C1-C4
- **Performance**: Ambient dialogue in high-population areas could spike CPU; need selective triggering
- **Audio**: Spatial audio positioning may require engine audio system enhancements
- **Narrative**: Ambient dialogue could break immersion if poorly written; needs careful authoring

---

## Appendix: Example Ambient Dialogue

```json
{
  "start": "greeting",
  "nodes": {
    "greeting": {
      "speaker": "{npc0.name} (the {func.npcRole(0)})",
      "text": "Saw you at the market yesterday.",
      "flow": [{ "goto": "response" }]
    },
    "response": {
      "speaker": "{npc1.name} (the {func.npcRole(1)})",
      "text": "Yeah, prices are getting out of hand.",
      "flow": [{ "action": "end" }]
    }
  }
}
```

---

**Next Phase**: Phase C6 — Storyline Conversation Trees (multi-turn, player-driven narratives)
