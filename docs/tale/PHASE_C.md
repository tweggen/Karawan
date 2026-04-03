# TALE Phase C: NPC Conversation System

**Status:** 📋 Proposed

**Overview:** Dialogue-driven personality for TALE NPCs. Players can approach any outdoor NPC and trigger contextual conversations shaped by the NPC's simulation state (role, hunger, anger, wealth, etc.). Conversations accumulate trust and can trigger quests, laying groundwork for deep storyline integration in future phases.

---

## Architecture

### The Bridge: TaleConversationBehavior

`nogameCode/nogame/characters/citizen/TaleConversationBehavior.cs`

Extends `ANearbyBehavior` (same pattern as `niceday` NPC):
- **Distance:** 12 meters
- **Prompt:** "E to Talk"
- **OnAction():** Get NPC schedule from TaleManager, inject TALE properties into narration Props, resolve conversation script, call narration engine

Attached as `Behavior` component on entity during `TaleEntityStrategy._setupActivity()`, cleared during `_advanceAndTravel()`.

Only attaches when `StayAtStrategyPart.IsIndoorActivity == false` (outdoor NPCs only).

### The Bindings: TaleNarrationBindings

`nogameCode/nogame/modules/tale/TaleNarrationBindings.cs`

Central helper (parallel to `NarrationBindings.cs`) that:

1. **Register()** — Called from `TaleModule.OnModuleActivate()` after NarrationManager is available
   - Registers narration functions: `func.npcMood()`, `func.npcRole()`, `func.npcWealthLabel()`
   - Subscribes to `ScriptEndedEvent` to clear injected props and update trust

2. **InjectNpcProps(NpcSchedule)** — Writes TALE properties into narration `Props`:
   - `npc.hunger`, `npc.anger`, `npc.fatigue`, `npc.health`
   - `npc.wealth`, `npc.happiness`, `npc.reputation`, `npc.morality`, `npc.fear`
   - `npc.role`, `npc.storylet`, `npc.group_id`, `npc.met_player`, `npc.trust_player` (Phase C4)

3. **ClearNpcProps()** — Removes all `npc.*` keys after script finishes; called on `ScriptEndedEvent`

4. **ResolveScript(storyletId, role, tags[])** — 5-level fallback:
   1. `schedule.CurrentStorylet` looked up in library → `StoryletDefinition.ConversationScript` if set
   2. `tale.{storyletId}` (auto-named by id if registered)
   3. First matching `tale.tag.{tag}` from storylet's `tags[]` array
   4. `tale.role.{role}`
   5. `tale.generic` (unconditional catch-all)

### Property Injection Timing

```
OnAction() is called by user pressing E
  ↓
TaleConversationBehavior.OnAction():
  1. Call InjectNpcProps(schedule)          ← Writes props.npc.* to Props
  2. Determine script name via ResolveScript()
  3. Call Narration.TriggerConversation(scriptName, npcId.ToString())
  4. NarrationRunner.Start() uses props in conditions/interpolation
  5. ... conversation runs ...
  6. On ScriptEndedEvent: ClearNpcProps()   ← Removes props.npc.*
```

Safe because:
- Conversations are serial (only one active NarrationRunner)
- `ScriptEndedEvent` fires whether script completes or is interrupted
- `npc.` prefix won't collide with existing props

---

## Phase C1: Infrastructure & Generic Dialogue

**Goal:** Any outdoor TALE NPC can be talked to. One-liner dialogue shaped by their most extreme property.

### Changes

| File | Change |
|------|--------|
| Create | `TaleConversationBehavior.cs` |
| Create | `TaleNarrationBindings.cs` |
| Create | `models/tale/conversations/tale.generic.json` |
| Create | `models/tale/conversations/tale.role.*.json` (5 files) |
| Modify | `TaleEntityStrategy._setupActivity()` — set Behavior component |
| Modify | `TaleEntityStrategy._advanceAndTravel()` — clear Behavior component |
| Modify | `TaleModule.OnModuleActivate()` — call `TaleNarrationBindings.Register()` |
| Modify | `models/nogame.narration.json` — add `__include__` for conversations dir |
| Modify | `TaleManager.cs` — expose `GetSchedule(int npcId)` public |

### JSON: Generic Dialogue

**`models/tale/conversations/tale.generic.json`** — 5-level branch on most extreme property:

```json
{
  "start": "entry",
  "nodes": {
    "entry": {
      "flow": [
        { "goto": [
            { "if": "props.npc.hunger > 0.7",  "goto": "hungry" },
            { "if": "props.npc.anger > 0.7",   "goto": "angry" },
            { "if": "props.npc.fatigue > 0.7", "goto": "tired" },
            { "else": "default" }
          ]
        }
      ]
    },
    "hungry": {
      "flow": [
        { "texts": [
            "I could really use something to eat.",
            "Haven't eaten properly in a while."
          ]
        }
      ]
    },
    "angry": {
      "flow": [
        { "texts": [
            "Not now.",
            "What do you want."
          ]
        }
      ]
    },
    "tired": {
      "flow": [
        { "texts": [
            "I'm exhausted. Can we make this quick?",
            "Haven't slept properly in days."
          ]
        }
      ]
    },
    "default": {
      "flow": [
        { "texts": [
            "Hey.",
            "Nice enough day.",
            "You heading somewhere?"
          ]
        }
      ]
    }
  }
}
```

**`models/tale/conversations/tale.role.worker.json`** — Worker defaults:

```json
{
  "start": "entry",
  "nodes": {
    "entry": {
      "flow": [
        { "texts": [
            "Just trying to get through the day.",
            "Work's been steady, I guess.",
            "Not complaining. Yet."
          ]
        }
      ]
    }
  }
}
```

Similar files for `drifter`, `merchant`, `socialite`, `authority`.

### Test for C1

- Walk up to any outdoor TALE NPC
- See "E to Talk" prompt
- Press E
- NPC says a line reflecting their most extreme current property
- Prompt hides during conversation

---

## Phase C2: Storylet-Specific Dialogue

**Goal:** Workers on lunch break talk about food. Drifters begging talk about poverty. Storylet tags auto-map to conversation scripts.

### Changes

| File | Change |
|------|--------|
| Modify | `StoryletDefinition.cs` — add `public string? ConversationScript;` |
| Modify | `StoryletLibrary.LoadFrom()` — parse `"conversation_script"` field |
| Modify | `TaleNarrationBindings.ResolveScript()` — check StoryletDefinition first |
| Modify | `models/tale/*.json` — add `"conversation_script"` to relevant storylets |
| Create | `models/tale/conversations/tale.tag.routine.json` |
| Create | `models/tale/conversations/tale.tag.rest.json` |
| Create | `models/tale/conversations/tale.tag.eating.json` |
| Create | `models/tale/conversations/tale.tag.economic.json` |
| Create | `models/tale/conversations/tale.lunch_break.json` (example storylet-specific) |

### StoryletDefinition Extension

```csharp
public class StoryletDefinition
{
    // ... existing fields ...

    /// <summary>
    /// Optional explicit conversation script name. If set, overrides 4-level fallback.
    /// </summary>
    public string? ConversationScript { get; set; }
}
```

### Script Selection (Updated)

**New 5-level fallback:**
1. `StoryletDefinition.ConversationScript` (explicit override, takes precedence)
2. `tale.{storyletId}` (auto-named by id)
3. First matching `tale.tag.{tag}` from `StoryletDefinition.Tags[]`
4. `tale.role.{role}`
5. `tale.generic`

### Storylet Extension Example

In any storylet file (e.g., `worker.json`):

```json
{
  "id": "lunch_break",
  "name": "Lunch Break",
  "roles": ["worker"],
  "conversation_script": "tale.lunch_break",
  // ... rest of storylet definition ...
}
```

### Conversation Scripts by Tag

**`models/tale/conversations/tale.tag.routine.json`** — Generic routine/work lines:

```json
{
  "start": "entry",
  "nodes": {
    "entry": {
      "flow": [
        { "texts": [
            "Just getting through the day.",
            "Same as always.",
            "Nothing exciting. Which is fine."
          ]
        }
      ]
    }
  }
}
```

Similar files for `rest`, `eating`, `economic`.

**`models/tale/conversations/tale.lunch_break.json`** — Wealth-gated lunch dialogue:

```json
{
  "start": "entry",
  "nodes": {
    "entry": {
      "flow": [
        { "goto": [
            { "if": "props.npc.wealth < 0.2", "goto": "poor_lunch" },
            { "else": "normal_lunch" }
          ]
        }
      ]
    },
    "normal_lunch": {
      "flow": [
        { "texts": [
            "Just grabbing something to eat. Break's almost over.",
            "This place does decent noodles. Not great, but decent."
          ]
        }
      ]
    },
    "poor_lunch": {
      "flow": [
        { "texts": [
            "I bring my own food now. Lunch costs too much.",
            "Everything's getting more expensive."
          ]
        }
      ]
    }
  }
}
```

### Test for C2

- Worker NPC at `lunch_break` storylet → lunch-specific dialogue
- Drifter at `beg` storylet → drifter role fallback (or tag match)
- NPC at `work_manual` → `tale.tag.routine` fallback
- NPC at `sleep` → `tale.tag.rest` fallback

---

## Phase C3: Property-Reactive Tone

**Goal:** Properties color *how* NPCs speak, not just which branch runs. Dialogue fully reflects the NPC's emotional and economic state.

### Changes

| File | Change |
|------|--------|
| Modify | `TaleNarrationBindings.Register()` — add `func.npcMood()`, `func.npcRole()`, `func.npcWealthLabel()` |
| Modify | `models/tale/conversations/tale.role.*.json` — expand with property branches + text interpolation |
| Modify | `models/tale/conversations/tale.tag.*.json` — add emotional tone variants |

### Narration Functions

Three new functions registered in `TaleNarrationBindings.Register()`:

**`func.npcMood()`** — Returns emotional descriptor based on combined state:
- `"frustrated"` — if anger > 0.7
- `"weary"` — if fatigue > 0.7
- `"desperate"` — if hunger > 0.7 or wealth < 0.2
- `"struggling"` — if wealth < 0.35 and not desperate
- `"neutral"` — otherwise

**`func.npcRole()`** — Returns `props.npc.role` (for use in `{ "speaker": "{func.npcRole()}" }`)

**`func.npcWealthLabel()`** — Returns wealth descriptor:
- `"broke"` — < 0.15
- `"tight"` — 0.15–0.35
- `"comfortable"` — 0.35–0.65
- `"doing well"` — > 0.65

### Example Script: Merchant with Property-Reactive Tone

**`models/tale/conversations/tale.role.merchant.json`** — Now with wealth-gated branches AND interpolation:

```json
{
  "start": "entry",
  "nodes": {
    "entry": {
      "flow": [
        { "speaker": "{func.npcRole()}" },
        { "goto": [
            { "if": "props.npc.wealth > 0.7",  "goto": "wealthy" },
            { "if": "props.npc.wealth < 0.25", "goto": "struggling" },
            { "else": "midrange" }
          ]
        }
      ]
    },
    "wealthy": {
      "flow": [
        { "texts": [
            "Business has been good. Can't complain.",
            "Things are moving. That's all you can ask for.",
            "I'm {func.npcMood()} today, but business-wise things are strong."
          ]
        }
      ]
    },
    "struggling": {
      "flow": [
        { "texts": [
            "Slow week. Slow month. People aren't buying.",
            "I don't know how much longer I can keep this going.",
            "Frankly, I'm {func.npcMood()}. Can't hide it."
          ]
        }
      ]
    },
    "midrange": {
      "flow": [
        { "texts": [
            "Staying afloat. Day by day.",
            "Could be worse. Could be better.",
            "I'm {func.npcMood()}, if I'm being honest. You probably can tell."
          ]
        }
      ]
    }
  }
}
```

### Test for C3

- Merchant NPC with wealth 0.8 → upbeat "business is good" lines
- Same role with wealth 0.1 → resigned "can't keep going" lines
- Text `{func.npcMood()}` renders correct emotion label
- `{func.npcRole()}` shows in speaker label

---

## Phase C4: Trust, Memory & Quest Hooks

**Goal:** NPCs remember the player. Trust gates dialogue tone. Conversations can trigger quests. Lay groundwork for full storyline integration.

### Changes

| File | Change |
|------|--------|
| Modify | `TaleNarrationBindings.Register()` — add trust read/write, cooldown dict |
| Modify | `TaleNarrationBindings.InjectNpcProps()` — add `npc.met_player`, `npc.trust_player` |
| Modify | `TaleNarrationBindings._onScriptEnded()` — increment player-NPC trust |
| Modify | `TaleNarrationBindings.Register()` — add `tale.npc.remember` event handler |
| Modify | `models/tale/conversations/tale.role.authority.json` — trust-gated branches |
| Modify | `models/tale/conversations/tale.role.merchant.json` — add "I remember you" variant |

### Player-NPC Trust Mechanics

**Storage:** Trust stored in `NpcSchedule.Trust[-1]`, where -1 is the player pseudo-ID.

**Injection (in `InjectNpcProps()`):**
```csharp
float trustForPlayer = schedule.Trust.GetValueOrDefault(-1, 0.5f);
Props.Set("npc.trust_player", trustForPlayer);

bool hasMetPlayer = schedule.Trust.ContainsKey(-1);
Props.Set("npc.met_player", hasMetPlayer ? "true" : "false");
```

**Update (in `_onScriptEnded()`):**
```csharp
schedule.Trust[-1] = Math.Min(1f, current + 0.02f);
schedule.HasPlayerDeviation = true;  // Persists NPC on save
```

**Cooldown:** Suppress "E to Talk" prompt if same NPC was talked to < 30 real-seconds ago, stored in `Dictionary<int, DateTime> _lastConversationTime`.

### Trust-Gated Dialogue Example

**`models/tale/conversations/tale.role.authority.json`** — Three dialogue paths based on trust:

```json
{
  "start": "entry",
  "nodes": {
    "entry": {
      "flow": [
        { "goto": [
            { "if": "props.npc.met_player == 'false'",    "goto": "first_meeting" },
            { "if": "props.npc.trust_player > 0.65",      "goto": "trusted" },
            { "else": "acquaintance" }
          ]
        }
      ]
    },
    "first_meeting": {
      "flow": [
        { "texts": [
            "Move along. Nothing here for you.",
            "Keep walking. This area is monitored."
          ]
        }
      ]
    },
    "acquaintance": {
      "flow": [
        { "texts": [
            "You again. What is it?",
            "I remember you. Make it quick."
          ]
        }
      ]
    },
    "trusted": {
      "flow": [
        { "texts": [
            "Quiet night. For now.",
            "Between you and me, there's been activity two blocks east."
          ]
        }
      ]
    }
  }
}
```

### Memory Flag Event

New narration event type `tale.npc.remember` allows scripts to set flags on the NPC:

```json
{ "events": [
    { "type": "tale.npc.remember", "fact": "gave_quest_hint" }
  ]
}
```

Handler in `TaleNarrationBindings.Register()`:
```csharp
manager.RegisterEventHandler("tale.npc.remember", async desc =>
{
    if (desc.Params.TryGetValue("fact", out var factObj))
    {
        string factKey = factObj.ToString();
        schedule.Properties[$"player_fact.{factKey}"] = 1f;
        schedule.HasPlayerDeviation = true;
    }
    await Task.CompletedTask;
});
```

These flags can later be read as storylet preconditions or drive different script branches.

### Quest Trigger

No new code needed. Existing `quest.trigger` narration event (registered in `NarrationBindings.cs`) works:

```json
{ "events": [
    { "type": "quest.trigger", "quest": "nogame.quests.SomeQuest.Quest" }
  ]
}
```

### Test for C4

- Walk up to authority NPC for first time → cold dismissal
- Approach same NPC again → "I remember you"
- After several conversations (trust > 0.65) → warmer, may share info
- `tale.npc.remember` flag persists across encounters
- Script can `quest.trigger` to start a quest from dialogue

---

## Properties Injected Into Narration System

All as `props.npc.*` and available to both conditions and text interpolation:

**Phase C1 (always):**
- `hunger`, `anger`, `fatigue`, `health`, `wealth`, `happiness`, `reputation`, `morality`, `fear`
- `role`, `storylet`

**Phase C3:**
- Additional: functions `func.npcMood()`, `func.npcRole()`, `func.npcWealthLabel()`

**Phase C4:**
- `met_player` ("true" or "false")
- `trust_player` (0.0–1.0)
- `group_id`

---

## Test Coverage

See `docs/tale/TALE_TEST_SCRIPTS_PHASE_C.md` (to be created during implementation).

Expected test categories:
- C1: Generic dialogue property branching (5 tests)
- C2: Storylet-specific and tag-based script selection (6 tests)
- C3: Property-reactive tone and interpolation (5 tests)
- C4: Trust accumulation, memory flags, quest trigger (6 tests)
- Integration: Multi-NPC conversations, outdoor/indoor, Tier 1 spawning (5 tests)

---

## Implementation Notes

### Avoid Dual-Behavior Problem

`TaleEntityBehavior` (if it exists as a separate class) is a monolithic strategy behavior. `TaleConversationBehavior` is attached as the entity's `Behavior` component only during activity phase (in `_setupActivity()`), then cleared (in `_advanceAndTravel()`). No conflict because they don't run simultaneously.

### Conversation Only Outdoor

`StayAtStrategyPart.IsIndoorActivity` flag is checked in `_setupActivity()` before attaching `TaleConversationBehavior`. Indoor NPCs are invisible to player anyway, so this is both efficient and correct.

### Props Isolation

Injected `npc.*` props are cleared on `ScriptEndedEvent`. The `npc.` prefix avoids collision with all existing game props keys. Safe because narration scripts execute serially (one active NarrationRunner at a time).

### Storylet Precondition for Talking

Future phases can add a `"allow_conversation": false` flag to storylets if desired (e.g., NPCs in the middle of a crime shouldn't chat). Not included in C1–C4.

---

## Future Integration

**Phase C5 — Ambient NPC-NPC Dialogue**: When two NPCs encounter each other (probabilistic detection in `DesSimulation.ProcessEncounter()`), they exchange brief overheard lines. No player interaction. Scripts selected based on interaction type, roles, and relationship trust.

**Phase C6 — Storyline Conversation Trees**: Full multi-turn branching conversations tied to individual NPC backstory arcs. Player choices affect NPC properties and storylet selection. Builds on C4's trust/memory foundation. Requires quest/narrative framework integration.
