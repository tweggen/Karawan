# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Karawan is a C# game engine ("Joyce") and game ("Silicon Desert 2") targeting .NET 9.0. It runs on Windows, Linux, macOS, and Android.

## Quick Start (For New Instances)

**First, read these in order:**
1. `PROCESS.md` — Generic development workflow, mandatory documentation updates
2. `PROCESS_TALE.md` — TALE-specific workflow and test commands
3. `docs/TESTING/` — Testing infrastructure (see `TESTING_STRATEGY.md`)
4. `PROCESS_DOCS.md` — Documentation organization guidelines

**Current Status (as of April 12, 2026):**
- ✅ Phase 0-7 + Phase 7B + Phase 8 TALE systems fully implemented
- ✅ Phase C1 (NPC Conversation Infrastructure): Behavior, bindings, script resolution complete
- ✅ Phase C2 (Storylet-Specific Dialogue): Explicit override + tag-based fallback implemented
- ✅ Phase C3 (Mood/Tone Branches): Role-specific dialogue via npcMood(), npcWealthLabel(), npcRole() functions
- ✅ Phase C4 (Trust, Memory & Quest Hooks): Trust tracking, player memory via fact flags, 30s conversation cooldown
- ✅ TALE-SOCIAL Phase D1 (Scenario Pre-Computation): Chushi bakes 25 `sc-{hash}` social-structure files into `nogame/generated/`, listed in `AndroidResources.xml` / `InnoResources.iss` alongside `ac-{hash}` animations.
- ✅ TALE-SOCIAL Phase D2 (Scenario Library + Selector): `engine.tale.bake.ScenarioLibrary` is registered in `TaleModule` as a lazy singleton; `TryGet(category, index)` probes the baked file via `engine.Assets.Open` and falls through to `ScenarioCompiler.CompileInMemory()` on miss / parse error / `joyce.DisablePrebakedScenarios=true` — exact mirror of `Model.BakeAnimations` at `JoyceCode/engine/joyce/Model.cs:207-237`. `ScenarioSelector.Pick(npcCount, clusterSeed)` picks (category, index) by closest median NPC count + seeded round-robin. Both are lazy: nothing is loaded until the first `I.Get<>` request.
- ✅ TALE-SOCIAL Phase D3 (Scenario Application): `engine.tale.bake.ScenarioApplicator.Apply(scenario, realNpcs)` re-attaches a baked scenario's groups, trust edges and post-365-day property snapshot onto a freshly populated cluster. Matching is two-step: bucket both populations by role, sort each bucket by (wealth desc, morality desc, NpcId asc), then pair positionally — the rank-to-real-NpcId map carries scenario.Groups and scenario.Relationships across to the real NPCs without the scenario needing to know anything about cluster geometry. Per-NPC Trust dicts get populated directly (TaleManager has no global RelationshipTracker at runtime). Wired into `TaleManager.PopulateCluster` AFTER the warmup advance loops, so the warmup desynchronizes schedule positions and the scenario then snaps everyone into their settled social state.
- ✅ TALE-SOCIAL Phase D4 (Seedability validation): wired up `tests/JoyceCode.Tests/JoyceCode.Tests.csproj` (xUnit, references Joyce.csproj). All 46 tests pass in 168 ms — `dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj`. The compiler determinism test runs `CompileInMemory` twice with the same seed and asserts byte-for-byte equality — the core seedability assertion. Pre-existing dead test files under `engine/navigation/` and `engine/streets/` are excluded from compilation pending API updates / DI fixture work; csproj has explanatory comments.
- ✅ TALE-SOCIAL Phase D5 (Statistics + tuning observations): Chushi pass after the bake loop walks each `sc-{hash}` file, computes per-scenario statistics (group counts, relationship density, role distribution, per-property mean/stdev/floor/ceiling fractions), aggregates by category, and writes `nogame/generated/scenario-statistics.json` (~55 KB indented JSON). Five concrete tuning concerns surfaced — most importantly **`GroupDetector.MaxCliques = 500` is binding for the large category** (all 12 large scenarios hit exactly 500 groups, zero stdev) and **`fear` is dead across all 25 scenarios** (`mean=0.000, fractionAtFloor=1.00`). NOT auto-fixed — each is a judgment call. See `docs/tale/phases/PHASE_D_SOCIAL.md` for the full design doc and the actionable tuning list.
- ✅ 192 regression tests passing (29 C-phase tests, 60-day simulations, ~5 min)
- ✅ Recalibration test framework ready (365+ days, ~2-4 hours)
- ✅ Configuration-driven roles, interactions, relationship tiers, group types
- ✅ Building role tagging with geometric attribute intensity zones (Phase 7B)
- ✅ Occupation-based character model assignment (Phase 8): roles define curated model pools in JSON config
- ✅ NavMesh street pathfinding working (Phase 7C deadlock fixed, routing Phase D fixes in place)
- ✅ Critical pathfinding bugs fixed:
  - Fallback storylet safety check (2026-03-28)
  - Same-junction pathfinding fallback using closest lanes (2026-03-28)
- ✅ PROCESS.md and documentation audit cycle in place
- 🔄 Routing Phase D D2: Multi-objective A* integration pending
- 🔄 Routing Phase D D4: Behavioral variety (role-based preferences) pending
- 🔄 TALE-SOCIAL Phase D follow-up: act on the five D5 tuning concerns documented in `docs/tale/phases/PHASE_D_SOCIAL.md` (MaxCliques cap, group membership ratio, relationship density, property saturation, fear=0)
- ⚠️ Note: "Phase D" is overloaded — routing Phase D and TALE-SOCIAL Phase D are separate workstreams
- ⚠️ Watch for JSON deserialization issues (case-sensitive, see TaleModule.cs)

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

## Build & Run

**Prerequisites:** Check out these repos as siblings to the Karawan directory:
- `BepuPhysics2` (github.com/TimosForks/bepuphysics2)
- `DefaultEcs` (github.com/TimosForks/DefaultEcs)
- `ObjLoader` (github.com/TimosForks/ObjLoader)
- `FbxSharp` (github.com/TimosForks/FbxSharp)
- `glTF-CSharp-Loader` (github.com/KhronosGroup/glTF-CSharp-Loader)
- `ink` (github.com/TimosForks/ink)

```bash
# Build everything
dotnet build Karawan.sln

# Run desktop app
dotnet run --project Karawan/Karawan.csproj

# Run the minimal grid example
dotnet run --project examples/Launcher/Karawan.GenericLauncher.csproj
```

No test suite exists in this repository.

**Build notes:**
- The `nogame/generated/` directory is auto-created by an `EnsureGeneratedDirectory` MSBuild target before asset compilation. If you see build errors about missing generated files, verify this target runs first.
- Build pipeline order in `nogame.csproj`: `EnsureGeneratedDirectory` → `CompileAssetsHost` (Chushi) → `GatherTexturesHost` (texture packer) → `GatherResources` (resource compiler) → `Compile`

## Architecture

### ECS Foundation
The engine uses **DefaultEcs** (Entity-Component-System). Entities are composed of components; systems process entities matching component queries. Hierarchy (parent-child) is handled via Hierarchy and Transform components on entities.

### Project Structure (key projects)

| Project | Role |
|---------|------|
| **Joyce** | Core engine library: ECS, scene management, transforms, modules, physics, assets, serialization |
| **JoyceCode** (.shproj) | Engine builtins: components, systems, controllers, UI, map system, inventory, loaders (FBX/OBJ/glTF), behaviours |
| **Splash** | Abstract renderer (platform-agnostic mesh/material/texture interfaces) |
| **Splash.Silk** | OpenGL renderer via Silk.NET |
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
