# Strategy System Architecture
*How entities decide what to do and how to do it*

---

## Overview

The strategy system is a **hierarchical state machine** framework built on DefaultEcs. It separates two concerns:

- **Strategy** — decides *which* behavior is active and *when* to transition (long-lived, event-driven)
- **Behavior** — implements *how* the entity acts each frame (short-lived, per-frame execution)

---

## Core Interfaces

### IStrategyPart
A single phase or state within a strategy machine:
- `Controller` — reference to parent `IStrategyController`
- `OnEnter()` — called when this phase activates
- `OnExit()` — called when this phase deactivates

### IStrategyController
Manages transitions between strategy parts:
- `GetActiveStrategy()` — returns current active part
- `GiveUpStrategy(IStrategyPart)` — child signals it wants to exit; parent decides what happens next

### IEntityStrategy
Lifecycle hooks for strategies attached to ECS entities:
- `OnAttach(engine, entity)` — called when strategy component is set on entity
- `OnDetach(entity)` — called when strategy component is removed
- `Sync(entity)` — re-synchronize after inactivity

---

## Base Classes

### AStrategyPart
Abstract base implementing `IStrategyPart`. Provides empty virtual `OnEnter()`/`OnExit()`.

### AEntityStrategyPart
Extends `AStrategyPart` + implements `IEntityStrategy`. Stores `_engine` and `_entity` references for subclasses. This is the base for leaf strategy parts (walk, flee, recover, etc.).

### AOneOfStrategy
**The key composite class.** Implements all three interfaces — it is simultaneously:
- A `IStrategyController` (manages child strategies)
- A `IStrategyPart` (acts as a phase within a parent)
- A `IEntityStrategy` (attaches to entities)

Core API:
- `SortedDictionary<string, IStrategyPart> Strategies` — named child strategies
- `TriggerStrategy(string name)` — change active child (calls `OnExit` on old, `OnEnter` on new)
- `GetStartStrategy()` — abstract; subclass defines initial child
- `GiveUpStrategy(IStrategyPart)` — abstract; subclass decides what to do when child gives up

---

## ECS Integration

### Components

**`Strategy`** component — holds an `IEntityStrategy` instance. One per entity. Watched by `StrategyManager`.

**`Behavior`** component — holds an `IBehavior` instance. One per entity. Set/removed by strategy parts in `OnEnter()`/`OnExit()`. Executed per-frame by `BehaviorSystem`.

### Managers

**`StrategyManager`** (extends `AComponentWatcher<Strategy>`):
- On component add: calls `strategy.OnAttach()`, then `OnEnter()`
- On component change: old `OnExit()` + `OnDetach()`, new `OnAttach()` + `OnEnter()`
- On component remove: `OnExit()` + `OnDetach()`

**`BehaviorManager`** (extends `AComponentWatcher<Behavior>`):
- Same lifecycle pattern for behaviors.

**`BehaviorSystem`** (DefaultEcs system):
- Iterates all entities with `Behavior` component each frame
- Calls `behavior.Behave(entity, dt)`
- Handles distance-based activation/deactivation relative to player
- Estimates `Motion` from transform deltas

### Entity Composition (typical)

```
Entity (e.g., citizen)
├── Transform3                    — position/rotation
├── Strategy { EntityStrategy }   — state machine
├── Behavior { WalkBehavior }     — current per-frame logic (set by strategy)
├── Body                          — physics
├── GPUAnimationState             — animation
└── ...
```

---

## Transition Mechanics

### How Transitions Work

1. A strategy part decides it's done (timer, event, goal reached)
2. It calls `Controller.GiveUpStrategy(this)`
3. The parent `AOneOfStrategy` receives the call in its `GiveUpStrategy()` override
4. The parent decides the next state and calls `TriggerStrategy("nextPhase")`
5. `TriggerStrategy()` calls `OnExit()` on current → `OnEnter()` on new
6. `OnEnter()` typically sets a new `Behavior` component; `OnExit()` removes it

### Transition Triggers

| Trigger | Example |
|---------|---------|
| **Timer** | `FleeStrategy` sets 10s timer → expires → `GiveUpStrategy()` |
| **Event** | Crash event → `EntityStrategy` catches it → `TriggerStrategy("recover")` |
| **Goal reached** | `PickupStrategy` creates `ToLocation` → player arrives → callback → `GiveUpStrategy()` |
| **External** | `QuestFactory.DeactivateQuest()` removes Strategy component → `OnDetach()` cascade |

---

## Concrete Implementations

### Citizen (NPC)

```
citizen.EntityStrategy (AOneOfStrategy)
├── "walk"    → WalkStrategy    — sets Navigator speed, adds WalkBehavior
├── "flee"    → FleeStrategy    — 2x speed, 10s timer, adds WalkBehavior
└── "recover" → RecoverStrategy — 5s timer, adds RecoverBehavior

Transitions:
  start → "walk"
  crash event → "recover"
  hit event → "flee"
  flee/recover timer expires → "walk"
```

### NPC Nice Day (Stationary)

```
niceday.EntityStrategy (AOneOfStrategy)
└── "rest" → RestStrategy — adds NearbyBehavior, subscribes to hit events

Transitions:
  start → "rest" (stays indefinitely)
```

### Taxi Quest

```
TaxiQuestStrategy (AOneOfStrategy)
├── "pickup"  → PickupStrategy  — creates ToLocation at guest position
└── "driving" → DrivingStrategy — creates ToLocation at destination, spawns passenger

Transitions:
  start → "pickup"
  player reaches pickup → "driving"
  player reaches destination → deactivate quest
```

---

## Key Design Patterns

### 1. Controller Callback (GiveUpStrategy)
Children don't know their siblings. They call `Controller.GiveUpStrategy(this)` and the parent decides the next state. Decouples child from transition logic.

### 2. Behavior-per-Phase
Each strategy phase installs a different behavior in `OnEnter()` and removes it in `OnExit()`. Same behavior class (e.g., `WalkBehavior`) can be reused with different parameters.

### 3. Quest as Entity
Quests are regular ECS entities with `QuestInfo` + `Strategy` components. The strategy is the quest state machine. No separate quest subsystem — same pattern as NPC strategies.

### 4. Event-Driven Transitions
Strategies subscribe to engine events in `OnEnter()`, unsubscribe in `OnExit()`. Event handlers use `_engine.QueueEventHandler()` to avoid threading issues.

---

## File Locations

**Core framework** (`JoyceCode/engine/behave/`):
- `IEntityStrategy.cs`, `IStrategyPart.cs`, `IStrategyController.cs`
- `AStrategyPart.cs`, `AEntityStrategyPart.cs`
- `strategies/AOneOfStrategy.cs`
- `StrategyManager.cs`, `BehaviorManager.cs`
- `components/Strategy.cs`, `components/Behavior.cs`
- `systems/BehaviorSystem.cs`

**Citizen strategies** (`nogameCode/nogame/characters/citizen/`):
- `EntityStrategy.cs`, `WalkStrategy.cs`, `FleeStrategy.cs`, `RecoverStrategy.cs`

**Quest strategies** (`nogameCode/nogame/quests/`):
- `Taxi/TaxiQuestStrategy.cs`, `Taxi/PickupStrategy.cs`, `Taxi/DrivingStrategy.cs`
- `HelloFishmonger/HelloFishmongerStrategy.cs`
- `VisitAgentTwelve/VisitAgentTwelveStrategy.cs`
