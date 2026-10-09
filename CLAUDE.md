# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Karawan is a C# game engine ("Joyce") and game ("Silicon Desert 2") targeting .NET 9.0. It runs on Windows, Linux, macOS, and Android.

## Quick Start (For New Instances)

**First, read these in order:**
1. `PROCESS.md` — Generic development workflow, mandatory documentation updates
2. `docs/tale/docs/PROCESS_TALE.md` — TALE-specific workflow and test commands
3. `docs/testing/docs/` — Testing infrastructure (see `TESTING_STRATEGY.md`)
4. `docs/main/docs/PROCESS_DOCS.md` — Documentation organization guidelines

**History lives in `docs/main/docs/STATUS-LOG.md`** — the full write-up of every fix, work package and phase (measurements, mutation results, what was found and NOT fixed). This file keeps only the current state and the rules that history taught. When something lands: write the entry there, and add a line here only if it changes the current state or teaches a rule.

### Current State (as of September 7, 2026)

- 📍 **Working on the three-dimensional city? Read `docs/roadmap/proposed/CITY-3D-OPEN-POINTS.md` FIRST** — the open ledger of what is broken, deferred, and how this work has gone. `docs/roadmap/proposed/STREETS-3D-TOPOLOGY.md` is the design and history beside it (§7a … §7w). Ledger Parts 1 and 1b are clear; item (o), the last one, closed 2026-09-07 with WP-O1…O3. **Measure before diagnosing: the obvious diagnosis was wrong in about half of these rounds.**
- 🚨 **Grade separation is ON** — `joyce.EnableGradeSeparation` defaults to true (WP-B6, Phase B complete): the shipped world has bridges and tunnels, and the heavy-first queue builds a different city (every block, building, shop, TALE location and nav lane moved). `ClusterStorage.DbVersion` is **1040**; bumping it deletes the whole `worldcache` file for every player.
- ⚠️ **The terrain-following city IS the shipped city** — `joyce.DisableClusterFlattening` defaults to true since `1c54a9e4`. The flat path and its tests still exist and still pin real properties, but you are protecting *a* baseline, not *the* baseline: state what each city does under your change.
- **City blocks** are traced over `BlockGraph`'s 2-core; dead-end spurs are subtracted from estates; each piece of buildable land gets its own building, founded on the block floor as drawn (`BlockFloor`). Buildings have **planar floors and storey-aligned shopfronts** — the owner's explicit choice; a footprint-following base was rejected.
- **Open owner decisions**: what an intercity line IS (graded embankment, viaduct on pylons, or deliberately elevated line) — ledger item (e).
- **Known and NOT fixed, worth knowing before you trust a save**: `DBStorage._readCollection` drops a collection that fails to deserialise with no log line at all; an existing save resolves street junction ids to *different* junctions after regeneration (`FirstOrDefault` in `StreetPointConverter`/`StrokeConverter`). Invalidating saves means bumping `DBStorage.DbVersion`, which also deletes `gamestate.db`.
- ✅ **Silk.NET exit complete** (Platform Phases 0–5): SDL3 + hand-written P/Invoke, models baked at build time, GL binding generated from `gl.xml`. State and open items (GATE-C Linux never run; no CI — KI-17) in `docs/roadmap/proposed/PLATFORM-BACKEND-STATUS.md`.
- ✅ TALE Phases 0–8, C1–C4, TALE-SOCIAL D1–D5 and E (living factions) complete; design in `docs/tale/docs/`, Phase D/E detail in `docs/tale/docs/phases/PHASE_D_SOCIAL.md` and `PHASE_E_SOCIAL.md`.
- 🔄 **In flight**: Debug Filter migration (~54 % of logger calls migrated to `Dc` categories); routing Phase D D2 (multi-objective A*) and D4 (role-based preferences); acting on the five TALE-SOCIAL D5 tuning concerns. ⚠️ "Phase D" is overloaded — routing Phase D and TALE-SOCIAL Phase D are separate workstreams.

### Rules and Gotchas

Each of these cost a debugging round; the story behind each is in `STATUS-LOG.md`.

**Engine & threading**
- **Never touch an ECS entity in a continuation after an `await`.** There is no synchronization context, so the continuation runs on a thread-pool thread and the entity may be detached. Use `AOneOfStrategy.TriggerStrategyOnLogicalThread` (re-checks `IsAlive` after the hop). "Entity was not created from a World" means a *default* Entity, not a thread error.
- **`async void` turns any escaping fault into a process kill** — fire-and-forget entry points must catch and log.
- **Dooming is not destroying**: the drain skips frames, so an entity can be doomed again before it dies. `DoomedEntitySet` makes a repeat doom a no-op; self-dooming behaviours still carry a latch field (`SelfDoomingBehaviorTests`).
- **Game-runtime code loads files through `engine.Assets`**, never `Directory`/`File` — on Android the model tree exists only inside the APK. Directory loading is for build tools and test harnesses.
- **A new `.cs` under `JoyceCode/` must be added to `JoyceCode.projitems` by hand** — the shared project does not glob.
- **Namespaces follow the layer, not the app**: `Karawan` and `Wuka` are startup applications; shared code belongs to its subsystem's namespace (`Splash`, `Boom`, `engine`, `builtin`).

**Build & assets**
- ⚠️ **Build tools run from their PUBLISHED output and `dotnet build` does not refresh them.** After changing or pulling `Chushi/`, `Mazu/` or `Tooling/Cmdline/`, run `bash Chushi/build.sh` and/or `bash Tooling/Cmdline/build.sh` before building `nogame`. A stale Chushi bakes nothing, silently. Detail in `docs/main/docs/build/PIPELINE.md`.
- **Models are baked; FBX import is build-time only** (`JoyceFbx`, referenced by Mazu/Chushi/tests only — putting it back into `Joyce` re-adds Assimp to every shipped target). Models are declared `"type": "model"` with their `modelProperties`, which are part of the bake identity.
- **Generated asset names (`mo-`/`ac-` hashes) are derived in two places** — `Tooling/Cmdline/GameConfig` and `JoyceCode` — on purpose; change both together.
- **`AnimationBatch.FrameNos` carries the GLOBAL baked frame number** (`FirstFrame` + local frame); every renderer strategy depends on it. Baked clips are laid out alphabetically, and bone order is contract.
- **`Wuka.csproj` imports the resource manifest at evaluation time**, so a newly declared asset needs two builds before it stages into the APK.
- **Input bindings live in `models/nogame.bindings.json`, stored by `ScanCode`** (a position, not a character), with no fallback copy in code. Gamepad stick/trigger indices are contract with `Sdl3WindowBackend._onGamepadAxis`. **Anything that drives an action without being a control (touch buttons) pushes `INPUT_ACTION_PRESSED`/`_RELEASED` with the action id, never a fake key event** — key events resolve only by `ScanCode`, and a synthetic one has none.

**Streets & navigation**
- **Say `Ramp`/`Bridge`/`Tunnel`, never "non-`Street`"** — `ConnectorBridge` strokes are ordinary ground roads; `StrokeKinds.IsStructure` is the one expression.
- **Generator flags are injected, not read from `GlobalSettings`** — the one global read is in `ClusterDesc._generateStrokes`, because a process global cannot be driven both ways in one test run.
- **Routing names its transport type** — `LocalPathfinder`, `TryCreateCursor`, satnav `Route` and `ToSomewhere.TransportType` have no default; cursor and A* must use the same type.
- **Every `NavJunction` is built by `NavJunction.At` / `Between` / `AtNavigationHeight`** (source-scanned). It carries `GroundHeight`; each consumer adds its own offset (`NavigationHeightOf`, `WalkingHeightOf`).
- **A character's animation driver is a CALL to `SetAnimation`, not a mention** — every creation site must reach one; a character with no `Body` cannot use `IdleBehavior` (`CharacterAnimationDriverTests`).

**TALE**
- **Model JSON under `models/tale/` is a runtime input to the TALE suite and the scenario bake** — never edit it while a gate runs, and re-bake (publish Chushi first) after storylet changes.
- **Storylets ship as `"type": "taleStorylet"` resources** in `models/nogame.resources.json`; that one declaration decides both what ships and what loads.

**Common First Tasks:**
- **Adding a test**: Create JSON in `models/tests/tale/phaseN-*/`, update `docs/tale/PHASE_N.md`, run `./run_tests.sh phaseN`
- **Tuning parameters**: Run `./run_recalibration_tests.sh phaseN` with `TALE_SIM_DAYS=365`
- **New phase**: Use `EnterPlanMode`, create plan in `docs/roadmap/proposed/`, follow PROCESS.md
- **Debugging**: Check `docs/tale/PHASE_N.md` for design, read actual test JSON for specs

**Key Rules (from PROCESS.md):**
- Documentation updates are MANDATORY (not optional)
- Always run `./run_tests.sh all` before commit
- Search for all references when changing systems
- Keep JSON config case-insensitive in mind (use `PropertyNameCaseInsensitive = true`)
- **Debug output pattern (mandatory)**: All debug/**trace** calls must use category-based filtering: add `private static readonly engine.Dc _dc = engine.Dc.{Category};` to class, then use `Trace(_dc, $"...")` instead of plain `Trace($"...")`. **The filtering applies to `Trace` ONLY.** `Warning(_dc, …)` and `Error(_dc, …)` prefix the category and always emit — a category decides how much *detail* to keep, never whether a problem is reported. `tests/JoyceCode.Tests/engine/LoggerFilteringTests.cs` fails if a filtered `Warning`/`Error` overload reappears (one once silently suppressed all 57 `Warning(_dc, …)` call sites).

## Build & Run

**Prerequisites:** Check out these repos as siblings to the Karawan directory:
- `BepuPhysics2` (github.com/TimosForks/bepuphysics2)
- `DefaultEcs` (github.com/TimosForks/DefaultEcs)
- `ObjLoader` (github.com/TimosForks/ObjLoader)
- `glTF-CSharp-Loader` (github.com/KhronosGroup/glTF-CSharp-Loader) — **pin to a commit before `d8be51b`** (e.g. `git checkout d8be51b^`). That commit switched the loader from Newtonsoft.Json to System.Text.Json, and `JoyceCode/engine/{PriorityMap,rom/Loader,world/TerrainKnitter}.cs` still depend on Newtonsoft arriving transitively from it. A fresh clone of upstream `main` fails with three `CS0246: Newtonsoft` errors that name none of this.
- `ink` (github.com/TimosForks/ink)

```bash
# Build everything
dotnet build Karawan.sln

# Run desktop app
dotnet run --project Karawan/Karawan.csproj

# Run the minimal grid example
dotnet run --project examples/Launcher/Karawan.GenericLauncher.csproj
```

**Test suites** (this section used to say none existed; that has not been true for some time):

```bash
# xUnit unit/regression suite — fast, no engine boot required
dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj

# TALE scenario suite — needs the build-tool chain published first, see below
bash Tooling/Cmdline/build.sh && bash Chushi/build.sh
dotnet build TestRunner/TestRunner.csproj -c Release
./run_tests_parallel.sh all      # or ./run_tests.sh all
```

Three xUnit tests (`BakedAnimationLayoutTests` ×2, `BakedModelEquivalenceTests`) read
baked assets from `nogame/generated/` and fail until the asset pipeline has run at
least once — which the `TestRunner` build above does. A green run is currently
1804 xUnit + 200 TALE.

**Build notes:**
- The `nogame/generated/` directory is auto-created by an `EnsureGeneratedDirectory` MSBuild target before asset compilation. If you see build errors about missing generated files, verify this target runs first.
- Build pipeline order in `nogame.csproj`: `EnsureGeneratedDirectory` → `GatherTexturesHost` (texture packer) → `CompileAssetsHost` (Chushi) → `GatherResources` (resource compiler) → `Compile`. Chushi reads the packed atlas JSON files (`atlas-*.json`) during its texture-loading pass, so the texture packer must run first; this is enforced by `CompileAssetsHost`'s `DependsOnTargets="GatherTexturesHost"`. Without it, a fresh clone fails because Chushi has no atlas to open.

## Architecture

### ECS Foundation
The engine uses **DefaultEcs** (Entity-Component-System). Entities are composed of components; systems process entities matching component queries. Hierarchy (parent-child) is handled via Hierarchy and Transform components on entities.

### Project Structure (key projects)

| Project | Role |
|---------|------|
| **Joyce** | Core engine library: ECS, scene management, transforms, modules, physics, assets, serialization |
| **JoyceCode** (.shproj) | Engine builtins: components, systems, controllers, UI, map system, inventory, loaders (FBX/OBJ/glTF), behaviours |
| **Splash** | Abstract renderer (platform-agnostic mesh/material/texture interfaces) |
| **Splash.OpenGL** | OpenGL renderer (was `Splash.Silk`; renamed WP-5.4 — no Silk.NET left in it) |
| **Splash.API.OpenGL** | Generated OpenGL bindings from the Khronos `gl.xml` registry (was `Splash.GL`, namespace was `Karawan.Graphics.OpenGL`). Regenerate with `docs/roadmap/proposed/wp-5.1/gen.py`; never hand-edit — see KI-17 |
| **Boom** / **Boom.OpenAL** | Audio framework and OpenAL implementation |
| **BoomCode** (.shproj) | Shared audio code |
| **nogame** + **nogameCode** (.shproj) | Game-specific logic for Silicon Desert 2 |
| **Karawan** | Desktop launcher (`DesktopMain.cs`) |
| **Wuka** | Android MAUI app (packages nogame + native libs) |
| **Aihao** | Avalonia-based game editor IDE |
| **Chushi** | Asset compiler (console tool, also used as MSBuild task) |
| **Mazu** | Animation compiler |
| **Tooling/Cmdline** | CLI utilities (texture packing, resource compilation) |

Shared projects (`.shproj`) are compiled into each referencing assembly — they are not standalone DLLs.

### Configuration System (Mix)
Game configuration is JSON-based and composable. The root is `models/nogame.json` which references satellite files (`nogame.modules.json`, `nogame.implementations.json`, `nogame.resources.json`, etc.). The Mix system merges these at runtime. Key config paths:
- `/implementations` — factory/DI bindings (className + properties)
- `/modules/root/className` — main game module class
- `/mapProviders` — world map generation providers
- `/metaGen` — procedural generation operators (fragment, building, populating, cluster)
- `/scenes/catalogue` and `/scenes/startup` — scene definitions
- `/properties` — runtime-configurable values with change subscriptions
- `/quests` — quest definitions

### World Generation Pipeline
The world is built by a hierarchy of **operators**:
1. **WorldOperator** — applied to the entire world in sequence
2. **ClusterOperator** — applied to each cluster on creation
3. **FragmentOperator** — applied to each fragment on (re-)load

Everything is designed to be re-creatable on demand.

### Entity Lifecycle
Entities track a **Creator** (can serialize/deserialize) and an **Owner** (controls lifetime). Components use `[Persistable]` attribute for serialization. Save/load hooks via the Saver module (`OnBeforeSaveGame` / `OnAfterLoadGame`).

### Rendering (Splash)
Geometry is broken into **InstanceDesc** objects (mesh + materials). The renderer batches identical InstanceDescs for instanced draw calls. Platform primitives (`AMeshEntry`, `AMaterialEntry`, `ATextureEntry`) follow create → fill → upload → unload → dispose lifecycle. OpenGL version: 4.1 on macOS, 4.3 on Windows/Linux.

### Input Pipeline
Platform events → logical translation → event queue → `InputEventPipeline` (distributes by priority) → `InputController` (maps to game controller state). Higher-priority listeners consume events before the standard controller.

### Game Assembly Loading
The launcher loads game DLLs dynamically based on `game.launch.json` (`/defaults/loader/assembly`). This allows different games to run on the same engine.

### Quest System
Quests are pure ECS entities with `QuestInfo` and `Strategy` components. The old `IQuest`/`quest.Manager` system has been fully removed (Phase 5 complete). `QuestFactory` creates/activates/deactivates quest entities. Strategy-based quests use `AOneOfStrategy` for multi-phase state machines (e.g., taxi quest has pickup → driving phases). `QuestDeactivatedEvent` carries `Title` and `IsSuccess` for completion feedback. The Quest Log UI is accessible from the pause menu and supports Follow/Unfollow per quest (Phase 6+7 complete). See `QUEST_REFACTOR.md` for full migration history.

#### Followed Quest (Phase 7)
At most one active quest is the "followed" quest — only it renders its goal marker and satnav route. `SatnavService` is the central singleton managing this:
- Auto-follows the first triggered quest; auto-advances to the next when a followed quest completes
- `FollowedQuestId` persisted in `GameState` and restored on load
- Fires `QuestFollowedEvent` / `QuestUnfollowedEvent` (Code = questId)
- `ToSomewhere.OwnerQuestEntity` — set on all quest navigation targets; when set, marker and route are only created/shown while the owning quest is followed. Unset = legacy behavior (always shown).
- Quest Log UI shows Follow/Unfollow buttons per active quest (uses the newly-implemented `<if test='...'>` JT XML element)

Key classes:
- `QuestFactory` (`JoyceCode/engine/quest/QuestFactory.cs`) — quest lifecycle management (register, trigger, deactivate)
- `ISatnavService` / `SatnavService` (`JoyceCode/engine/quest/ISatnavService.cs`, `nogameCode/nogame/quest/SatnavService.cs`) — followed quest tracking, auto-follow, persistence; registered as `engine.quest.ISatnavService` in `nogame.implementations.json`
- `ToSomewhere` (`JoyceCode/engine/quest/ToSomewhere.cs`) — base module for navigation-based quest targets; set `OwnerQuestEntity` to opt into followed-quest visibility control
- `NarrationBindings` (`nogameCode/nogame/modules/story/NarrationBindings.cs`) — quest factory registrations, narration event wiring, and early `ISatnavService` initialization
- `QuestLuaBindings` (`nogameCode/nogame/quests/QuestLuaBindings.cs`) — Lua bindings: `getQuestList()` (includes `followed` field), `followQuest(id)`, `unfollowQuest()`, `isFollowed(id)`
- `ICreator` implementations — save/load quest state via `TaxiQuestData` etc.

### Placement System
`Placer` (`JoyceCode/engine/Placer.cs`) places entities in the world using `PlacementDescription` constraints:
- `MinDistance`/`MaxDistance` — horizontal distance filtering from `PlacementContext.CurrentPosition`
- `MaxAttempts` — retry loop for distance-constrained placement

### ForceSpawn API
`SpawnController.ForceSpawn(Type behaviorType, Vector3 position)` spawns a full-lifecycle character at a specific position:
- Looks up `ISpawnOperator` by behavior type
- Calls `ISpawnOperator.SpawnCharacterAt(Vector3)` (default interface method)
- Citizen implementation finds cluster/quarter/streetpoint, builds `PositionDescription`, creates entity with full Walk→Flee→Recover strategy

### Citizen Collision Routing
NPC `OnCollision` is routed through `nogame.characters.citizen.CitizenCollisionRouter` — a single static dispatcher used by `WalkBehavior`, `IdleBehavior`, `RecoverBehavior`, `TaleWalkBehavior`, `TaleConversationBehavior`. The router classifies the contact by `SolidLayerMask` and publishes one of three events on the NPC entity's event path:
- `EntityStrategy.HitEventPath` — `AnyWeapon` contact (player melee or NPC weapon) → `FleeStrategy`.
- `EntityStrategy.CrashEventPath` — `AnyVehicle` contact → `RecoverStrategy` (death animation).
- `EntityStrategy.BumpEventPath` — pure `PlayerCharacter` body contact (no weapon, no vehicle) → walking behaviors apply a transient lateral offset on `SegmentNavigator.ApplyLateralBump` (0.4 m, 400 ms linear decay, capped at 0.6 m accumulated) so the NPC steps out of the player's way without flee or collapse. Only the two walking behaviors subscribe to bump; idle/conversation/recover NPCs simply block the player.

TALE sites (`TaleWalkBehavior`, `TaleConversationBehavior`) keep their custom conversation-cancel side effect inline before calling `Dispatch`, using the router's `IsWeapon` / `IsVehicle` helpers for the classification.

### Aihao Editor IDE

Aihao is an Avalonia 11-based game editor built with **CommunityToolkit.Mvvm** and **Dock.Avalonia** for a dockable panel layout.

#### Tech Stack
- **UI**: Avalonia 11.3.8 (cross-platform desktop)
- **Layout**: Dock.Avalonia (tool windows + document tabs)
- **MVVM**: CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`)
- **JSON**: System.Text.Json throughout

#### JSON Loading & Storage
The editor uses **Mix** (from JoyceCode) as its single source of truth. `EditorFileProvider` (implements `IMixFileProvider`) gives Mix direct filesystem access instead of the engine's asset system. Loading flow:

1. `ProjectService.LoadProjectAsync()` creates a Mix instance with `EditorFileProvider`
2. Root JSON is loaded at `/` with priority 0; `__include__` files are discovered and tracked
3. `AihaoProject` wraps the Mix instance and exposes `GetSection(sectionId)` → `JsonNode`
4. Overlays can be added at higher priority via `AddOverlayAsync()` for debug/override configs

Saving reverses the flow: `ViewModel.ToJsonObject()` → serialize → write to disk via `ProjectService.SaveFileAsync()`.

#### Editor Architecture

Each config section (globalSettings, properties, resources, implementations, metaGen) has:
- A **DocumentViewModel** (dockable tab) that owns a section-specific editor VM
- A **section editor ViewModel** that typically wraps `JsonPropertyEditorViewModel`
- An **AXAML View** mapped via `DataTemplate` in MainWindow

The generic `JsonPropertyEditorViewModel` + `PropertyNodeViewModel` provide recursive JSON tree editing. `PropertyNodeViewModel` represents any JSON node with:
- `Name`, `Value`, `ValueKind` (String/Number/Boolean/Null/Object/Array)
- `Children` (ObservableCollection for objects/arrays)
- `IsModified` dirty tracking with callback propagation to parent
- `ToJsonNode()` / `FromJsonNode()` for round-trip serialization
- Auto-detected special editors (resolution, vector2/3, color, slider) based on key patterns and value format

#### Change Flow
```
UI TextBox → Binding → PropertyNodeViewModel.Value setter
  → Validate() → MarkModified() → _onModified callback
  → JsonPropertyEditorViewModel.IsDirty = true
  → Document tab shows dirty indicator
  → Save: ToJsonObject() → ProjectService.SaveFileAsync()
```

#### Docking Layout
- **Left pane**: Project tree (tool window)
- **Center**: Document tabs (section editors, render output)
- **Right pane**: Inspector (tool window)
- **Bottom**: Console with level filtering and search
- `AihaoDockFactory` builds the layout; `DockingService` manages registration

#### Key Services
- `ProjectService` — load/save/reload projects, overlay management
- `ProcessService` — build/run/debug game, IDE detection (Rider/VS/VS Code)
- `ActionService` — command registry with keybinding overrides
- `UserSettingsService` — persists preferences to `~/.aihao/settings.json`

#### Patterns to Follow When Adding Editors
1. Create a `FooEditorViewModel : ObservableObject` with load/save methods operating on `JsonNode`
2. Create a `FooDocumentViewModel : DocumentViewModel` wrapping the editor VM
3. Create a `FooEditor.axaml` view with bindings
4. Create a `FooDocumentView.axaml` hosting the editor view
5. Register the DataTemplate mapping in `MainWindow.axaml`
6. Register the document type in `AihaoDockFactory`
7. Add an open action in `MainWindowViewModel` + `BuiltInActions`
