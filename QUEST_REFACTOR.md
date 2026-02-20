# Quest System Refactor: From Subsystem to ECS Components + Strategy

## Goal

Replace the bespoke quest subsystem (`IQuest`, `quest.Manager`, `AModule`-based quests) with a pure ECS approach: a `QuestComponent` for quest identity/metadata, the existing `Strategy` component for state machines, and the existing hierarchy system for quest-to-sub-objective relationships.

## Why

The current quest system reimplements patterns the engine already provides generically:

| Concern | Current quest approach | Engine already has |
|---|---|---|
| Lifecycle | `IModule.ModuleActivate/Deactivate` | `Strategy` component + `StrategyManager` (OnAttach/OnEnter/OnExit/OnDetach) |
| State machine | Ad-hoc (Taxi quest has comments describing states but no state machine) | `AOneOfStrategy` with named child strategies and `TriggerStrategy()` |
| Persistence | Custom `ICreator` per quest class | `[IsPersistable]` + `Creator` component on any entity |
| Registry/lookup | `quest.Manager` singleton with `ObjectFactory` | ECS query: `world.GetEntities().With<QuestInfo>()` |
| Hierarchy | Not supported | `Parent`/`Children` components via `HierarchyApi` |
| Event response | Quests manually subscribe in `OnModuleActivate` | Strategy parts subscribe in `OnEnter`, unsubscribe in `OnExit` |

The result: less code, one fewer abstraction layer, and quest hierarchies for free.

---

## Architecture Overview

### New Components

#### `QuestInfo` component
Replaces the `IQuest` interface and the old `Quest` component. Lives in `JoyceCode/engine/quest/components/`.

```csharp
namespace engine.quest.components;

[engine.IsPersistable]
public struct QuestInfo
{
    [JsonInclude] public string QuestId;        // unique identifier (was IQuest.Name)
    [JsonInclude] public string Title;          // display title
    [JsonInclude] public string ShortDescription;
    [JsonInclude] public string LongDescription;
    [JsonInclude] public bool IsActive;         // shown in quest log when true
    [JsonInclude] public byte State;            // quest-specific state enum cast to byte
    [JsonInclude] public float Progress;        // 0..1 optional progress indicator
}
```

Key design choices:
- **Struct, not class.** Follows the engine's ECS convention (see `Transform3`, `Behavior`, `Creator`).
- **`[IsPersistable]` + `[JsonInclude]`** on all fields. Serialization works identically to every other component. No custom `ICreator` needed per quest.
- **`State` as byte.** Each quest type defines its own enum and casts. The component doesn't need to know the enum — it just persists the value.
- **No `IQuest` reference.** The component is pure data. Behavior comes from the `Strategy` component.

### Entity Structure

A quest is a regular entity with these components:

```
Quest Entity
├── QuestInfo          — identity, description, state, progress
├── Strategy           — IEntityStrategy (typically an AOneOfStrategy subclass)
├── Creator            — for persistence (points to a quest creator/factory)
└── EntityName         — for lookup by name
```

A multi-phase quest with sub-objectives uses hierarchy:

```
Quest Entity ("Taxi Fare")
├── QuestInfo { Title="Be a good cab driver", IsActive=true }
├── Strategy  → TaxiQuestStrategy (AOneOfStrategy)
├── Creator
│
├── Child Entity ("Pick up rider")
│   ├── QuestInfo { Title="Pick up the passenger", State=Pending }
│   └── (optional: own Strategy for sub-objective state)
│
└── Child Entity ("Drive to destination")
    ├── QuestInfo { Title="Drive to the destination", State=Pending }
    └── (optional: own Strategy)
```

The hierarchy system handles parent-child lifetime automatically (`HierarchyApi.Delete` is recursive).

### Quest State Machine

Each quest type implements an `AOneOfStrategy` subclass. Example for the Taxi quest:

```
TaxiQuestStrategy : AOneOfStrategy
├── "pickup"   → PickupPhaseStrategy : AEntityStrategyPart
├── "driving"  → DrivingPhaseStrategy : AEntityStrategyPart
└── "complete" → CompletePhaseStrategy : AEntityStrategyPart
```

Each phase strategy follows the established NPC pattern:
- `OnEnter()`: create `ToLocation`/`TrailVehicle` marker, subscribe to events, attach `Behavior`
- `OnExit()`: remove markers, unsubscribe, remove `Behavior`
- Transition: call `Controller.GiveUpStrategy(this)` or parent calls `TriggerStrategy("nextPhase")`

### Quest Log

The quest log UI queries ECS directly:

```csharp
var activeQuests = engine.GetEcsWorld().GetEntities()
    .With<QuestInfo>()
    .AsEnumerable()
    .Where(e => e.Get<QuestInfo>().IsActive);
```

Sub-objectives: query children of a quest entity that also have `QuestInfo`.

### Quest Triggering

Replace `quest.Manager.TriggerQuest(name)` with a helper that:
1. Checks if a quest entity with that `QuestId` already exists (ECS query)
2. If not, creates the entity and sets its components
3. Sets `QuestInfo.IsActive = true`
4. The `StrategyManager` automatically calls `OnAttach` + `OnEnter` when the `Strategy` component is set

This helper can live as a static method or a lightweight service. It does NOT need to be an `ObjectFactory` or manage quest instances.

### Narration Integration

`NarrationBindings.cs` changes from:
```csharp
I.Get<engine.quest.Manager>().TriggerQuest(questName, true);
```
to:
```csharp
QuestFactory.TriggerQuest(questName, true);
// or: I.Get<QuestService>().TriggerQuest(questName, true);
```

The narration JSON format stays identical:
```json
{ "type": "quest.trigger", "quest": "nogame.quests.VisitAgentTwelve" }
```

---

## Implementation Plan

### Phase 1: Add QuestInfo component (non-breaking) — COMPLETE

**Files created:**
- `JoyceCode/engine/quest/components/QuestInfo.cs`

### Phase 2: Migrate VisitAgentTwelve — COMPLETE

**Files created:**
- `nogameCode/nogame/quests/VisitAgentTwelve/VisitAgentTwelveStrategy.cs` — `AOneOfStrategy` with `"navigate"` phase
- `nogameCode/nogame/quests/VisitAgentTwelve/NavigateStrategy.cs` — `AEntityStrategyPart` using `ToLocation`

**Files modified:**
- `nogameCode/nogame/modules/story/NarrationBindings.cs` — added `QuestFactory` registration, hybrid routing (QuestFactory first, Manager fallback)
- `JoyceCode/engine/quest/QuestFactory.cs` — created as lightweight service (non-static, registered via `I`)

**Files deleted:**
- `nogameCode/nogame/quests/VisitAgentTwelve/Quest.cs` — old IQuest/AModule/ICreator implementation

### Phase 3: Migrate HelloFishmonger — COMPLETE

**Files created:**
- `nogameCode/nogame/quests/HelloFishmonger/HelloFishmongerStrategy.cs` — `AOneOfStrategy` with `"trail"` phase
- `nogameCode/nogame/quests/HelloFishmonger/TrailStrategy.cs` — `AEntityStrategyPart` using `TrailVehicle`

**Key implementation detail:** Car creation logic (model loading, street point selection, `CharacterCreator.SetupCharacterMT`) moved into the factory lambda in `NarrationBindings._registerQuestFactories()`. The car entity is created alongside the quest entity but is not a child — it remains alive after quest completion (matching original behavior).

**Files modified:**
- `nogameCode/nogame/modules/story/NarrationBindings.cs` — added HelloFishmonger factory registration
- `nogameCode/nogameCode.projitems` — swapped old Quest.cs for new strategy files
- `models/nogame.quests.json` — emptied to `{}` (all quests now use QuestFactory)

**Files deleted:**
- `nogameCode/nogame/quests/HelloFishmonger/Quest.cs` — old IQuest/AModule/ICreator implementation

### Phase 4: Migrate Taxi quest (multi-phase)

This is the payoff — the quest that motivated this refactor.

**Files to create:**
- `nogameCode/nogame/quests/Taxi/TaxiQuestStrategy.cs` — `AOneOfStrategy` with three phases
- `nogameCode/nogame/quests/Taxi/PickupPhaseStrategy.cs` — `AEntityStrategyPart`
  - `OnEnter`: create guest marker at pickup location, subscribe to arrival event
  - `OnExit`: remove marker
  - On arrival: `Controller.GiveUpStrategy(this)` → parent transitions to "driving"

- `nogameCode/nogame/quests/Taxi/DrivingPhaseStrategy.cs` — `AEntityStrategyPart`
  - `OnEnter`: create destination marker via `ToLocation`, hide passenger
  - `OnExit`: remove marker
  - On arrival: `Controller.GiveUpStrategy(this)` → parent transitions to "complete"

- `nogameCode/nogame/quests/Taxi/CompletePhaseStrategy.cs` — `AEntityStrategyPart`
  - `OnEnter`: play completion effect, spawn passenger at destination, deactivate quest
  - `OnExit`: cleanup

**Quest hierarchy:**
```
Taxi Quest Entity
├── QuestInfo { Title="Taxi Fare" }
├── Strategy → TaxiQuestStrategy
├── Child: Pickup sub-objective entity
│   └── QuestInfo { Title="Pick up passenger" }
└── Child: Destination sub-objective entity
    └── QuestInfo { Title="Drive to destination" }
```

The `TaxiQuestStrategy.GiveUpStrategy()` updates child `QuestInfo.State` as phases transition.

**Validation:** Full three-phase quest works. Sub-objectives visible in quest log query. Save/load across phases.

### Phase 5: Remove old quest infrastructure — COMPLETE

**Files deleted:**
- `JoyceCode/engine/quest/IQuest.cs` — interface, no implementations remained
- `JoyceCode/engine/quest/Manager.cs` — replaced by `QuestFactory`
- `JoyceCode/engine/quest/components/Quest.cs` — replaced by `QuestInfo`
- `JoyceCode/engine/quest/AreaEnteredBehavior.cs` — unused stub from early prototyping

**Files modified:**
- `JoyceCode/JoyceCode.projitems` — removed compile entries for deleted files
- `nogameCode/nogame/Main.cs` — removed `SharedModule<engine.quest.Manager>()` registration
- `nogameCode/nogame/modules/story/NarrationBindings.cs` — removed dead Manager fallback from `quest.trigger` handler; now calls `QuestFactory.TriggerQuest()` directly
- `JoyceCode/builtin/entitySaver/ConverterRegistry.cs` — removed `InterfacePointerConverter<IQuest>` registration

**Files kept (unchanged):**
- `JoyceCode/engine/quest/ToSomewhere.cs` — reusable module for goal markers and navigation
- `JoyceCode/engine/quest/ToLocation.cs` — thin wrapper, still useful
- `JoyceCode/engine/quest/TrailVehicle.cs` — thin wrapper, still useful
- `JoyceCode/engine/quest/GoalMarkerSpinBehavior.cs` — still useful
- `JoyceCode/engine/quest/GoalMarkerVanishBehavior.cs` — still useful
- `models/nogame.quests.json` — kept as `{}` (referenced by Aihao editor `SectionDefinition.Quests`)

### Phase 6: Quest log integration

**Files to create or modify:**
- A `QuestLogSystem` or query helper that collects all entities with `QuestInfo` for UI display
- Support for hierarchy: show parent quests with expandable sub-objectives
- UI bindings (depends on whether this is in-game HUD or Aihao editor)

---

## Quest Factory / Trigger Helper — IMPLEMENTED

`QuestFactory` is a non-static service registered via `I.Get<QuestFactory>()`. It supports async factory lambdas (needed for model loading, target computation). Lives in `JoyceCode/engine/quest/QuestFactory.cs`.

Key API:
- `RegisterQuest(string questId, Func<Engine, Entity, Task> factory)` — registers an async factory
- `HasQuest(string questId)` — checks if a quest is registered
- `TriggerQuest(string questId, bool activate)` — creates entity, runs factory, activates
- `DeactivateQuest(Entity eQuest)` — deactivates, removes Strategy, disposes entity, saves

Quest registration lives in `NarrationBindings._registerQuestFactories()`:

```csharp
questFactory.RegisterQuest("nogame.quests.VisitAgentTwelve.Quest",
    async (engine, eQuest) =>
    {
        var targetPos = await VisitAgentTwelveStrategy.ComputeTargetLocationAsync(engine);
        await engine.TaskMainThread(() =>
        {
            eQuest.Set(new QuestInfo { QuestId = "...", Title = "...", ... });
            eQuest.Set(new Strategy(new VisitAgentTwelveStrategy(targetPos)));
        });
    });
```

---

## Migration Checklist for Each Quest

When converting a quest from the old system to the new one:

- [ ] Identify the quest's states/phases (even if it's just one)
- [ ] Create an `AOneOfStrategy` subclass with named child strategies for each phase
- [ ] Create `AEntityStrategyPart` subclasses for each phase
- [ ] Move `CreateEntities()` logic into `OnAttach()` of the relevant strategy part
- [ ] Move `OnModuleActivate()` logic into `OnEnter()` of the start strategy
- [ ] Move `OnModuleDeactivate()` logic into `OnExit()` of the relevant strategy
- [ ] Move `SetupEntityFrom()` / `SaveEntityTo()` logic — most of it should be unnecessary if components are `[IsPersistable]`
- [ ] Replace `I.Get<quest.Manager>().DeactivateQuest(this)` with setting `QuestInfo.IsActive = false` and removing the `Strategy` component (or transitioning to a "complete" state)
- [ ] Replace narration trigger calls
- [ ] Test: trigger → active → complete cycle
- [ ] Test: save during quest → load → quest resumes correctly
- [ ] Test: quest log query returns correct data

---

## Patterns to Follow

### Event paths for quest strategies

Follow the NPC pattern. Define event paths as static methods on the strategy:

```csharp
public class TaxiQuestStrategy : AOneOfStrategy
{
    public static string PickupReachedPath(in Entity e) =>
        $"@{e}/nogame.quests.Taxi.pickupReached";

    public static string DestinationReachedPath(in Entity e) =>
        $"@{e}/nogame.quests.Taxi.destinationReached";
}
```

### Thread safety

Follow the RecoverStrategy pattern:
- Use `Engine.QueueEventHandler()` for state transitions triggered from background threads
- Use generation IDs for timer callbacks
- Lock around timer state

### ToSomewhere integration

`ToSomewhere` (and its subclasses `ToLocation`, `TrailVehicle`) remain as utility modules. Strategy parts create and manage them:

```csharp
public class NavigateStrategy : AEntityStrategyPart
{
    private ToLocation _toLocation;

    public override void OnEnter()
    {
        _toLocation = new ToLocation
        {
            RelativePosition = _targetPosition,
            SensitivePhysicsName = "...",
            OnReachTarget = () => Controller.GiveUpStrategy(this)
        };
        _toLocation.ModuleActivate();
    }

    public override void OnExit()
    {
        _toLocation?.ModuleDeactivate();
        _toLocation?.Dispose();
        _toLocation = null;
    }
}
```

### Completion flow

When a quest completes:
1. The final strategy part calls `Controller.GiveUpStrategy(this)`
2. The `AOneOfStrategy.GiveUpStrategy()` implementation:
   - Sets `QuestInfo.IsActive = false` on the quest entity
   - Optionally triggers narration
   - Optionally removes the `Strategy` component to fully deactivate
   - Optionally disposes the quest entity if it's not needed anymore

---

## What NOT to Change

- **`ToSomewhere` / `ToLocation` / `TrailVehicle`** — these are reusable utility modules for creating goal markers and navigation routes. They work well as-is. Strategy parts use them as tools.
- **`GoalMarkerSpinBehavior` / `GoalMarkerVanishBehavior`** — per-frame behaviors for marker animation. Keep as-is.
- **`BehaviorSystem`** — the per-frame system that drives behaviors. No changes needed.
- **`StrategyManager`** — already handles `Strategy` component lifecycle. No changes needed.
- **Narration JSON format** — `{ "type": "quest.trigger", "quest": "..." }` stays the same. Only the handler implementation changes.
- **The ECS hierarchy system** — `Parent`/`Children`/`HierarchyApi`. Used as-is for quest hierarchies.

---

## Risk Assessment

| Risk | Mitigation |
|---|---|
| Save file compatibility | Phase 2-4 run in parallel with old system. Old saves still work. Migration happens in Phase 5. |
| `ToSomewhere` is an `AModule` | Strategy parts manage it via `ModuleActivate`/`ModuleDeactivate`. No conflict — modules and strategies coexist. |
| Quest deactivation ordering | `AOneOfStrategy.OnDetach` calls `OnExit` on active child, then `OnDetach` on all children. Same cleanup guarantees as NPCs. |
| Thread safety in quest triggers | `QuestFactory.TriggerQuest` should run on the main thread or use `QueueMainThreadAction`, matching the current `Manager` pattern. |
