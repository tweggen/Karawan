# Narration System

The narration system drives in-game dialogue, monologues, and scripted scenes. Scripts are defined in JSON (loaded via the Mix system at `/narration`) and executed by `NarrationManager` and `NarrationRunner`.

## JSON Structure

Narration data lives in `models/nogame.narration.json` and is structured as:

```json
{
  "bindings": { },
  "startup": "main",
  "triggers": {
    "interact.niceguy": { "script": "niceguy", "mode": "conversation" }
  },
  "scripts": {
    "scriptName": {
      "start": "nodeId",
      "nodes": { ... }
    }
  }
}
```

- **bindings** — key/value pairs available for text interpolation
- **startup** — script name to run automatically after a game load
- **triggers** — maps event paths to script activations (`mode`: `"conversation"`, `"narration"`, or `"scriptedScene"`)
- **scripts** — named scripts, each with a `start` node and a `nodes` map

## Flow-Based Nodes

Each node contains a `flow` array — an ordered list of **statements** that the runner steps through one at a time. The player must acknowledge each text line (e.g. press SPACE) before the next statement appears.

### Statement Types

| Type | JSON | Behavior |
|------|------|----------|
| **text** | `{ "text": "..." }` | Display text, wait for acknowledgment |
| **texts** | `{ "texts": ["a", "b", "c"] }` | Pick one at random, display, wait for acknowledgment |
| **choices** | `{ "choices": [...] }` | Display choices, wait for selection |
| **events** | `{ "events": [...] }` | Fire events, auto-advance (no wait) |
| **speaker** | `{ "speaker": "name" }` | Set current speaker, auto-advance |
| **goto** | `{ "goto": "nodeId" }` | Transition to another node |

### Example

```json
"intro": {
  "flow": [
    { "speaker": "narrator" },
    { "text": "Again I looked around. This was all? This was it?" },
    { "text": "Rumours are this is a vast space." },
    { "text": "For now, I need something that runs the inside of me." },
    { "choices": [
      { "text": "Find a Ramen shop", "goto": "ramen1" },
      { "text": "Go get a drink.", "goto": "drink1" }
    ]},
    { "events": [{ "type": "quest.trigger", "quest": "some.Quest" }] }
  ]
}
```

The player sees each text line one at a time. The speaker set before a text line carries through to that line's display. Events fire silently and the flow continues. Choices pause until the player picks one.

### Conditions on Statements

Any statement can have a `"condition"` field. If the condition evaluates to false, the statement is skipped:

```json
{ "text": "You have the key!", "condition": "hasKey" }
```

## Legacy Format (Backward Compatible)

Nodes without a `flow` array still work. The system synthesizes a flow from the legacy fields in this order:

1. `speaker` / `animation` -> Speaker statement
2. `text` or `texts` -> Text statement
3. `events` -> Events statement
4. `choices` -> Choices statement
5. `goto` -> Goto statement

```json
"greet": {
  "speaker": "niceguy",
  "texts": [
    "It's a bit foggy today, isn't it?",
    "I like to hang out in these islands of green."
  ]
}
```

## Goto Variants

The `goto` field (on nodes, choices, or goto statements) supports several forms:

**Simple string:**
```json
"goto": "nodeId"
```

**Conditional array:**
```json
"goto": [
  { "if": "someCondition", "goto": "targetA" },
  { "if": "otherCondition", "goto": "targetB" },
  { "else": "fallback" }
]
```

**Weighted random:**
```json
"goto": {
  "random": [
    { "weight": 3, "goto": "common" },
    { "weight": 1, "goto": "rare" }
  ]
}
```

**Sequential (varies by visit count):**
```json
"goto": {
  "sequence": ["first", "second", "third"],
  "overflow": "cycle"
}
```
Overflow modes: `"cycle"` (default), `"clamp"`, `"random"`.

## Events

Event descriptors fire handlers registered via `NarrationManager.RegisterEventHandler()`. Built-in event types:

- `quest.trigger` — triggers a quest: `{ "type": "quest.trigger", "quest": "fully.qualified.QuestName" }`
- `props.set` — sets a property: `{ "type": "props.set", "key": "myKey", "value": "myValue" }`

Unhandled event types are emitted as generic engine events at `narration.event.{type}`.

## Text Interpolation

Narration text supports `{func.name(arg1, arg2)}` syntax. Register functions via `NarrationManager.RegisterFunction()` or `RegisterAsyncFunction()`. Built-in:

- `{func.propValue(key)}` — returns a property value

## Triggers

Triggers map engine event paths to script activations:

```json
"triggers": {
  "interact.niceguy": { "script": "niceguy", "mode": "conversation" }
}
```

When the event `interact.niceguy` fires, the `niceguy` script starts in conversation mode.

## State Machine

The narration manager enforces a state machine for script priority:

- **Idle** -> any state
- **Conversation** -> any state (interruptible)
- **Narration** -> Narration or ScriptedScene only
- **ScriptedScene** -> ScriptedScene only
