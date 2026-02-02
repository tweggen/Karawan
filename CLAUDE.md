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
