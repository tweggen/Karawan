# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Karawan is a C# game engine ("Joyce") and game ("Silicon Desert 2") targeting .NET 9.0. It runs on Windows, Linux, macOS, and Android.

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
