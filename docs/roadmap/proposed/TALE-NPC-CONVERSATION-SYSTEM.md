# Plan: TALE NPC Conversation System (Phases C1–C4)

**Status**: 📋 Proposed

## Context

TALE NPCs have rich simulation state (role, hunger, anger, fatigue, wealth, morality, current storylet) but are completely mute. The narration engine runs scripts for player-NPC conversations (see `niceday` NPC), but TALE and Narration are two fully independent systems with zero integration today.

This plan bridges them in four phases: from a minimal "any NPC can speak" MVP to property-reactive, trust-sensitive, storyline-connected dialogue.

## Goals

- Every TALE NPC can be approached and spoken to by the player
- Dialogue reflects the NPC's current simulation state (hungry, angry, tired, wealthy, etc.)
- Each storylet can declare which conversation script to use
- Conversations deepen over time: trust accumulates, NPCs remember the player
- Foundation for full storyline integration (Phase C6+)

## Phase Summary

| Phase | Key Deliverable |
|-------|----------------|
| C1 | Infrastructure: any outdoor NPC can be talked to, generic property-aware dialogue |
| C2 | Storylet-specific dialogue: lunch break → food talk, begging → poverty talk |
| C3 | Property-reactive tone: NPC properties color *how* things are said |
| C4 | Trust & memory: NPCs remember player, trust gates dialogue, quest hooks |

## Architecture

```
Player presses E near TALE NPC
  → TaleConversationBehavior.OnAction()
    → TaleNarrationBindings.InjectNpcProps(schedule)   ← TALE props into narration Props
    → TaleNarrationBindings.ResolveScript(...)         ← 4-level script selection
    → Narration.TriggerConversation(scriptName, npcId)
      → NarrationManager.TriggerScript(...)
        → NarrationRunner plays script with {props.npc.*} interpolation
    → ScriptEndedEvent → TaleNarrationBindings.ClearNpcProps()
                        → trust += 0.02 for player-NPC relationship
```

## Key Design Decisions

- **Property injection**: NPC properties injected as `props.npc.*` into global Props before script; cleared after. Safe because conversations are serial (one active script at a time).
- **Script selection (5-level fallback)**:
  1. `StoryletDefinition.ConversationScript` (explicit override)
  2. `tale.{storyletId}` (auto-named by storylet id)
  3. `tale.tag.{firstMatchingTag}` (first tag in storylet's `tags[]`)
  4. `tale.role.{role}` (role default)
  5. `tale.generic` (unconditional catch-all)
- **Script location**: `models/tale/conversations/*.json`, loaded via `__include__` in `nogame.narration.json`
- **Player-NPC trust**: stored in `schedule.Trust[-1]` (player pseudo-ID = -1, never a valid TALE NpcId)
- **Behavior attachment**: `TaleConversationBehavior` set as entity `Behavior` component in `_setupActivity()`, cleared in `_advanceAndTravel()`. Indoor NPCs excluded (`IsIndoorActivity` check).

## Files to Create

| File | Phase | Purpose |
|------|-------|---------|
| `nogameCode/nogame/characters/citizen/TaleConversationBehavior.cs` | C1 | ANearbyBehavior subclass; triggers conversation |
| `nogameCode/nogame/modules/tale/TaleNarrationBindings.cs` | C1 | Prop injection, script resolution, cleanup |
| `models/tale/conversations/tale.generic.json` | C1 | Catch-all script |
| `models/tale/conversations/tale.role.worker.json` | C1 | Worker default lines |
| `models/tale/conversations/tale.role.drifter.json` | C1 | Drifter default lines |
| `models/tale/conversations/tale.role.merchant.json` | C1/C3 | Merchant lines |
| `models/tale/conversations/tale.role.socialite.json` | C1/C3 | Socialite lines |
| `models/tale/conversations/tale.role.authority.json` | C1/C4 | Authority lines + trust branches |
| `models/tale/conversations/tale.tag.routine.json` | C2 | Tag-based routine activity fallback |
| `models/tale/conversations/tale.tag.rest.json` | C2 | Tag-based rest fallback |
| `models/tale/conversations/tale.tag.eating.json` | C2 | Tag-based eating fallback |
| `models/tale/conversations/tale.tag.economic.json` | C2 | Tag-based work/money fallback |
| `models/tale/conversations/tale.lunch_break.json` | C2 | Example storylet-specific script |
| `docs/tale/PHASE_C.md` | — | Design doc (this plan's companion) |

## Files to Modify

| File | Phase | Change |
|------|-------|--------|
| `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` | C1 | Attach/detach TaleConversationBehavior in `_setupActivity()` / `_advanceAndTravel()` |
| `nogameCode/nogame/modules/tale/TaleModule.cs` | C1 | Call `TaleNarrationBindings.Register()` at activation |
| `JoyceCode/engine/tale/TaleManager.cs` | C1 | Expose `public NpcSchedule? GetSchedule(int npcId)` |
| `models/nogame.narration.json` | C1 | Add `__include__` for `models/tale/conversations/` |
| `JoyceCode/engine/tale/StoryletDefinition.cs` | C2 | Add `string? ConversationScript` field + parser |
| `models/tale/*.json` | C2 | Add `conversation_script` to relevant storylets |
| `nogameCode/nogame/modules/tale/TaleNarrationBindings.cs` | C3/C4 | Add mood functions, trust logic, memory event handler |

## Future Phases (not in this plan)

- **C5 — Ambient NPC-NPC Dialogue**: Overheard lines emitted during TALE encounters; spatial audio/floating text; no player interaction required.
- **C6 — Storyline Conversation Trees**: Full multi-turn branching conversations tied to NPC backstory arcs. Requires C4 trust/memory foundation.
