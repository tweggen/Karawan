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

### Phase 1: Add QuestInfo component (non-breaking)

**Files to create:**
- `JoyceCode/engine/quest/components/QuestInfo.cs` — the new component as described above

**Files to modify:**
- None yet. The old system continues to work.

**Validation:** Build succeeds. No behavioral change.

### Phase 2: Create the first strategy-based quest (parallel to old system)

Pick the simplest quest: **VisitAgentTwelve**. It has one phase (go to location).

**Files to create:**
- `nogameCode/nogame/quests/VisitAgentTwelve/VisitAgentTwelveStrategy.cs`
  - Extends `AOneOfStrategy` with a single child strategy `"navigate"`
  - `GetStartStrategy()` returns `"navigate"`
  - `GiveUpStrategy()` handles completion (deactivate quest, trigger narration)

- `nogameCode/nogame/quests/VisitAgentTwelve/NavigateStrategy.cs`
  - Extends `AEntityStrategyPart`
  - `OnAttach`: compute target location (the `_computeTargetLocationLT` logic)
  - `OnEnter`: create `ToLocation` with marker, subscribe to reach event
  - `OnExit`: dispose `ToLocation`, unsubscribe
  - When target reached: call `Controller.GiveUpStrategy(this)`

**Files to modify:**
- Create a temporary factory/helper to instantiate the quest entity with both `QuestInfo` and `Strategy` components. This can initially live alongside `quest.Manager` — triggered by a different narration event type or by extending the existing trigger path.

**Validation:**
- The quest works end-to-end: narration triggers it, marker appears, reaching the marker completes it, narration continues.
- Quest entity has `QuestInfo` component queryable for quest log.
- Save/load works via `[IsPersistable]` on `QuestInfo` + `Strategy`.

### Phase 3: Migrate HelloFishmonger

Single-phase quest (trail a vehicle), but involves creating a target entity.

**Files to create:**
- `nogameCode/nogame/quests/HelloFishmonger/HelloFishmongerStrategy.cs` — `AOneOfStrategy`
- `nogameCode/nogame/quests/HelloFishmonger/TrailStrategy.cs` — `AEntityStrategyPart`
  - `OnAttach`: create the car entity (the `CreateEntities` logic)
  - `OnEnter`: create `TrailVehicle` marker
  - `OnExit`: dispose marker
  - On reach: `Controller.GiveUpStrategy(this)` → deactivate + trigger narration

**Key detail:** The car entity should be a child of the quest entity via hierarchy. When the quest entity is deleted, the car is cleaned up automatically.

**Validation:** Same as Phase 2. Also verify that saving/loading reconstructs the car entity correctly.

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

### Phase 5: Replace quest infrastructure

Once all quests are migrated:

**Files to delete:**
- `JoyceCode/engine/quest/IQuest.cs` — interface no longer needed
- `JoyceCode/engine/quest/Manager.cs` — replaced by lightweight trigger helper + ECS queries
- `JoyceCode/engine/quest/components/Quest.cs` — replaced by `QuestInfo.cs`

**Files to modify:**
- `nogameCode/nogame/modules/story/NarrationBindings.cs` — update `quest.trigger` handler to use the new factory/helper instead of `quest.Manager`
- `models/nogame.quests.json` — update to reference new strategy classes (or keep as a simple name→class mapping for the factory)
- Any save file migration if needed (rename `Quest` component to `QuestInfo` in serialized data)

**Files to keep (unchanged):**
- `JoyceCode/engine/quest/ToSomewhere.cs` — still useful as a reusable module for creating goal markers and navigation. Strategy parts call it from their `OnEnter`/`OnExit`.
- `JoyceCode/engine/quest/ToLocation.cs` — thin wrapper, still useful
- `JoyceCode/engine/quest/TrailVehicle.cs` — thin wrapper, still useful
- `JoyceCode/engine/quest/GoalMarkerSpinBehavior.cs` — still useful
- `JoyceCode/engine/quest/GoalMarkerVanishBehavior.cs` — still useful

### Phase 6: Quest log integration

**Files to create or modify:**
- A `QuestLogSystem` or query helper that collects all entities with `QuestInfo` for UI display
- Support for hierarchy: show parent quests with expandable sub-objectives
- UI bindings (depends on whether this is in-game HUD or Aihao editor)

---

## Quest Factory / Trigger Helper

A lightweight replacement for `quest.Manager`. This is NOT a full subsystem — just a convenience layer.

```csharp
namespace engine.quest;

public static class QuestFactory
{
    private static SortedDictionary<string, Func<Engine, Entity>> _factories = new();

    public static void RegisterQuest(string questId, Func<Engine, Entity> factory)
    {
        _factories[questId] = factory;
    }

    public static void TriggerQuest(string questId, bool activate)
    {
        var engine = I.Get<Engine>();

        // Check if already exists
        var existing = engine.GetEcsWorld().GetEntities()
            .With<components.QuestInfo>()
            .AsEnumerable()
            .FirstOrDefault(e => e.Get<components.QuestInfo>().QuestId == questId);

        if (existing.IsAlive)
        {
            if (activate && !existing.Get<components.QuestInfo>().IsActive)
            {
                ref var qi = ref existing.Get<components.QuestInfo>();
                qi.IsActive = true;
            }
            return;
        }

        if (!_factories.TryGetValue(questId, out var factory))
        {
            Logger.Error($"Unknown quest: {questId}");
            return;
        }

        var eQuest = factory(engine);
        if (activate)
        {
            ref var qi = ref eQuest.Get<components.QuestInfo>();
            qi.IsActive = true;
        }
    }
}
```

Quest registration (e.g., in a game startup module or loaded from config):

```csharp
QuestFactory.RegisterQuest("nogame.quests.VisitAgentTwelve", engine =>
{
    var e = engine.CreateEntity("quest VisitAgentTwelve");
    e.Set(new QuestInfo
    {
        QuestId = "nogame.quests.VisitAgentTwelve",
        Title = "Come to the location.",
        ShortDescription = "Find the marker on the map and reach it.",
        IsActive = false
    });
    e.Set(new Strategy(new VisitAgentTwelveStrategy()));
    e.Set(new Creator(I.Get<CreatorRegistry>().FindCreatorId(/* ... */)));
    return e;
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
