# Quest System Refactor: From Subsystem to ECS Components + Strategy

> **Status:** complete — Phases 1–7 shipped (Phase 7, the followed quest, extends the original plan). The sub-objective hierarchy sketched below was **not implemented**; no quest has child sub-objective entities.

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
- **`[IsPersistable]` + `[JsonInclude]`** on all fields. Serialization works identically to every other component. A quest whose `Strategy` must be rebuilt on load still needs an `ICreator` (the Taxi quest has `TaxiQuestCreator`, see Phase 4).
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

> **Not implemented.** The sub-objective hierarchy below was the plan; no quest creates child entities with their own `QuestInfo`, and the quest log lists only top-level quests. The Taxi quest shipped as one entity (Phase 4).

A multi-phase quest with sub-objectives would use hierarchy:

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

Each quest type implements an `AOneOfStrategy` subclass. The Taxi quest as shipped:

```
TaxiQuestStrategy : AOneOfStrategy
├── "pickup"   → PickupStrategy : AEntityStrategyPart
└── "driving"  → DrivingStrategy : AEntityStrategyPart
```

There is no "complete" phase: when `driving` gives up, the controller calls `QuestFactory.DeactivateQuest` (see Completion flow).

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

Sub-objectives: not implemented (see Entity Structure).

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

### Phase 4: Migrate Taxi quest (multi-phase) — COMPLETE

This was the payoff — the quest that motivated this refactor. It shipped with a simpler design than planned: two phases, no "complete" phase and no sub-objective entities.

**Files** (`nogameCode/nogame/quests/Taxi/`):
- `TaxiQuestStrategy.cs` — `AOneOfStrategy` with `"pickup"` → `"driving"`; takes a start phase so a load can resume mid-quest. When `pickup` gives up it sets `TaxiQuestData.Phase = 1` and triggers `driving`; when `driving` gives up it calls `QuestFactory.DeactivateQuest`.
- `PickupStrategy.cs` — `ToLocation` marker at the guest position (car-routed, `OwnerQuestEntity` set); on arrival gives up.
- `DrivingStrategy.cs` — `ToLocation` marker at the destination plus a waiting person there; on arrival force-spawns a walking citizen at the destination and gives up.
- `TaxiQuestData.cs` — `[IsPersistable]` component: `GuestPosition`, `DestinationPosition`, `Phase` (0 = pickup, 1 = driving).
- `TaxiQuestCreator.cs` — `ICreator`: on save writes the active phase into `TaxiQuestData.Phase`; on load rebuilds `TaxiQuestStrategy` from `TaxiQuestData` and pushes `QuestTriggeredEvent`.
- `TaxiNpcSpawnerModule.cs` — places taxi NPC markers (about one quarter in ten per cluster); touching one hides the other markers and triggers `nogame.quests.Taxi.Quest`; the markers return on `QuestDeactivatedEvent`. It owns and registers the `TaxiQuestCreator`.

The factory in `NarrationBindings._registerQuestFactories()` places a destination 100–300 m from the player (street point, current cluster) and starts the strategy directly in `"driving"` (`Phase = 1`), since the player has already met the passenger at the marker. The `"pickup"` phase is reached only through a strategy started in it.

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

### Phase 6: Quest Log UI — COMPLETE

**Files created:**
- `nogameCode/nogame/quests/QuestLuaBindings.cs` — Lua bindings exposing `getQuestList()` to jt XML (follows `InvLuaBindings` pattern)

**Files modified:**
- `nogameCode/nogameCode.projitems` — added compile entry for `QuestLuaBindings.cs`
- `nogameCode/nogame/Main.cs` — registered `questlog` Lua binding namespace via `AddDefaultBinding`
- `models/menu/menu.xml` — added "Quest Log..." option in pause menu and `menuQuestLog` view with `<for>` loop over `questlog:getQuestList()`

### Phase 7: Followed quest — COMPLETE

At most one active quest is *followed*; only it shows its goal marker and satnav route.

- `ISatnavService` (`JoyceCode/engine/quest/ISatnavService.cs`) / `SatnavService` (`nogameCode/nogame/quest/SatnavService.cs`): `FollowedQuestId`, `IsFollowed(entity)`, `FollowQuest(id)`, `UnfollowQuest()`; fires `QuestFollowedEvent` / `QuestUnfollowedEvent`. It auto-follows a triggered quest when none is followed and, when the followed quest is deactivated, follows the next active one. `FollowedQuestId` is persisted in `GameState` and restored after load.
- `ToSomewhere.OwnerQuestEntity`: when set, the marker and route exist only while the owning quest is followed; unset keeps the legacy always-shown behaviour. The Taxi phases set it.
- Quest Log: `QuestLuaBindings` adds `followed` to `getQuestList()` and `followQuest(id)`, `unfollowQuest()`, `isFollowed(id)`; `models/menu/menu.xml` shows Follow / Unfollow per quest.

---

## Quest Factory / Trigger Helper — IMPLEMENTED

`QuestFactory` is a non-static service registered via `I.Get<QuestFactory>()`. It supports async factory lambdas (needed for model loading, target computation). Lives in `JoyceCode/engine/quest/QuestFactory.cs`.

Key API:
- `RegisterQuest(string questId, Func<Engine, Entity, Task> factory)` — registers an async factory
- `HasQuest(string questId)` — checks if a quest is registered
- `TriggerQuest(string questId, bool activate)` — creates entity, runs factory, activates
- `DeactivateQuest(Entity eQuest, bool isSuccess = true)` — deactivates, removes Strategy, disposes entity, saves, publishes `QuestDeactivatedEvent`

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
- [ ] Replace `I.Get<quest.Manager>().DeactivateQuest(this)` with `I.Get<QuestFactory>().DeactivateQuest(_entity)` from the controller's `GiveUpStrategy`
- [ ] Replace narration trigger calls
- [ ] Test: trigger → active → complete cycle
- [ ] Test: save during quest → load → quest resumes correctly
- [ ] Test: quest log query returns correct data

---

## Patterns to Follow

### Event paths for quest strategies

Follow the NPC pattern. Define event paths as static methods on the strategy (illustrative — the shipped quests react to `ToLocation.OnReachTarget` instead and define no such paths):

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
2. The quest's `AOneOfStrategy.GiveUpStrategy()` calls `I.Get<QuestFactory>().DeactivateQuest(_entity)` (Taxi first calls `TriggerStrategy(null)`)
3. `DeactivateQuest(eQuest, isSuccess = true)` queues onto the main thread and there:
   - sets `QuestInfo.IsActive = false`
   - removes the `Strategy` component (`StrategyManager` runs `OnExit` + `OnDetach`)
   - disposes the quest entity and saves the game
   - pushes `QuestDeactivatedEvent` (quest id, title, success), which `SatnavService` and e.g. `TaxiNpcSpawnerModule` react to

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
