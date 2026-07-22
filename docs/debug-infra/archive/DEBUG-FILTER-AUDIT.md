# Debug Filter Codebase Audit

**Date**: 2026-04-20  
**Total Files with Logger Calls**: 135  
**Total Logger Calls**: 571  
**Already Migrated**: 24 (4.2% — TaleManager.cs)  
**Remaining**: 547 (95.8%)

---

## Summary by Category

### High Volume (40+ calls)
- Asset Loading (FBX, GlTF, OBJ, ROM Loader) — ~85 calls
- Street Generation (Generator, Operators, StreetPoint, StrokeStore) — ~83 calls
- TALE System (TaleManager, SpatialModel, PopulationGenerator, Scenario baking) — ~47 calls
- World Generation (MetaGen, Clusters, Loader) — ~35 calls

### Medium Volume (15-40 calls)
- Navigation (LocalPathfinder, NavMap, NavCluster) — ~28 calls
- Physics — ~15 calls
- Narration — ~15 calls
- UI & Widgets — ~20 calls
- Model/Animation (Model, ModelCache, AnimationSystem, ModelAnimationCollection) — ~32 calls

### Low Volume (<15 calls)
- Modules, Config, Input, Database, Testing — ~100 calls scattered

---

## Files by Logger Call Count (Top 40)

| Count | File | Category | Priority |
|-------|------|----------|----------|
| 32 | `engine/AAssetImplementation.cs` | AssetLoading | P2 |
| 23 | `engine/tale/TaleManager.cs` | TaleManager | ✅ DONE |
| 18 | `engine/streets/GenerateClusterStreetsOperator.cs` | StreetGen | P1 |
| 18 | `engine/rom/Loader.cs` | AssetLoading | P2 |
| 16 | `engine/Logger.cs` | (framework — NA) | — |
| 16 | `builtin/loader/GlTF.cs` | AssetLoading | P2 |
| 16 | `builtin/loader/fbx/FbxModel.cs` | AssetLoading | P2 |
| 15 | `engine/world/GenerateClustersOperator.cs` | MetaGen | P1 |
| 14 | `engine/streets/StreetPoint.cs` | StreetGen | P1 |
| 12 | `engine/streets/Generator.cs` | StreetGen | P1 |
| 11 | `engine/world/MetaGen.cs` | MetaGen | P1 |
| 11 | `engine/joyce/ModelAnimationCollection.cs` | Animation | P2 |
| 10 | `engine/streets/StrokeStore.cs` | StreetGen | P1 |
| 10 | `engine/physics/API.cs` | Physics | P2 |
| 10 | `builtin/modules/satnav/GenerateNavMapOperator.cs` | NavMap | P1 |
| 9 | `ui/Property.cs` | UI | P3 |
| 9 | `engine/tale/SpatialModel.cs` | SpatialModel | P1 |
| 8 | `engine/world/MetaGenLoader.cs` | MetaGen | P1 |
| 8 | `engine/casette/Mix.cs` | Casette | P2 |
| 8 | `engine/behave/SpawnController.cs` | SpawnController | P1 |
| 7 | `engine/world/Loader.cs` | MetaGen | P1 |
| 7 | `engine/streets/GenerateClusterQuartersOperator.cs` | StreetGen | P1 |
| 7 | `engine/narration/NarrationManager.cs` | Narration | P2 |
| 7 | `engine/joyce/Model.cs` | Animation | P2 |
| 7 | `engine/DBStorage.cs` | Database | P3 |
| 7 | `builtin/tools/ExtrudePoly.cs` | Tools | P3 |
| 7 | `builtin/jt/RootWidget.cs` | UI | P3 |
| 7 | `builtin/controllers/InputController.cs` | Input | P2 |
| 6 | `engine/testing/TestDriverModule.cs` | Testing | P3 |
| 6 | `engine/tale/bake/ScenarioLibrary.cs` | TaleManager | P1 |
| 6 | `engine/tale/bake/ScenarioCompiler.cs` | TaleManager | P1 |
| 6 | `engine/SceneSequencer.cs` | Engine | P2 |
| 6 | `engine/joyce/ModelCache.cs` | Animation | P2 |
| 6 | `engine/elevation/Cache.cs` | Tools | P3 |
| 6 | `builtin/map/DefaultMapProvider.cs` | Map | P3 |
| 5 | `engine/world/ClusterDesc.cs` | World | P3 |
| 5 | `engine/Engine.cs` | Engine | P2 |
| 5 | `builtin/modules/satnav/LocalPathfinder.cs` | Pathfinding | P0 |
| 4 | `engine/Unit.cs` | Framework | P3 |
| 4 | `engine/tale/TalePopulationGenerator.cs` | TaleManager | P1 |

---

## Categorization Plan

### New Categories Needed

Beyond the existing 12 in `Dc` enum, add:
- `Narration` → NarrationManager, NarrationRunner, NarrationScript
- `Database` → DBStorage, ROM loaders
- `UI` → Widget system, Property editor, JT framework
- `Input` → InputController, ClickModule, InputEventPipeline
- `Tools` → ExtrudePoly, Lindenmayer, tool utilities
- `Map` → MapProvider, MapViewer, MapIconManager
- `World` → Fragment, ClusterDesc, general world operations
- `Engine` → Engine, SceneSequencer, Module system
- `Framework` → Logger, ModuleFactory, ImplementationLoader

This brings total to **21 categories** (12 existing + 9 new).

### Files by New Category

**Narration** (7 files, 15 calls):
- `NarrationManager.cs` — 7 calls
- `NarrationRunner.cs` — 2 calls
- `NarrationScript.cs` — 2 calls
- `NarrationConditionEvaluator.cs` — 1 call
- `NarrationInterpolator.cs` — 2 calls
- `LuaScriptManager.cs` — 1 call

**Database** (3 files, 12 calls):
- `DBStorage.cs` — 7 calls
- `rom/Loader.cs` — 5 calls (note: already counted in AssetLoading ROM)

**UI & Widgets** (7 files, 20 calls):
- `ui/Property.cs` — 9 calls
- `builtin/jt/RootWidget.cs` — 7 calls
- `builtin/jt/Widget.cs` — 4 calls

**Input & Click** (4 files, 10 calls):
- `builtin/controllers/InputController.cs` — 7 calls
- `ClickModule.cs` — 2 calls
- `InputEventPipeline.cs` — 1 call

**Tools** (5 files, 12 calls):
- `builtin/tools/ExtrudePoly.cs` — 7 calls
- `elevation/Cache.cs` — 6 calls (could be "Elevation" category)
- `builtin/tools/ModelBuilder.cs` — 1 call
- `builtin/tools/Lindenmayer/LGenerator.cs` — 2 calls

**Map** (3 files, 8 calls):
- `builtin/map/DefaultMapProvider.cs` — 6 calls
- `MapViewer.cs` — 2 calls

---

## Migration Priority Matrix

### Priority 0: Hot Paths (Per-Frame Systems)

**Target**: Zero-overhead critical for performance.

| File | Calls | Reason | Week |
|------|-------|--------|------|
| `LocalPathfinder.cs` | 5 | A* inner loops | W2 |
| `MoveKineticsSystem.cs` | 4 | Per-frame physics | W2 |
| `AnimationSystem.cs` | 3 | Per-frame animation | W2 |

**Total P0**: 12 calls  
**Expected Win**: ~3-5% frame time improvement when disabled

### Priority 1: Frequent Operations (Cluster Gen, Spawning, World Gen)

**Target**: Called repeatedly during world gen/cluster load, not per-frame.

| File | Calls | Category | Week |
|------|-------|----------|------|
| `GenerateClusterStreetsOperator.cs` | 18 | StreetGen | W3 |
| `GenerateClustersOperator.cs` | 15 | MetaGen | W3 |
| `StreetPoint.cs` | 14 | StreetGen | W3 |
| `Generator.cs` | 12 | StreetGen | W3 |
| `MetaGen.cs` | 11 | MetaGen | W3 |
| `GenerateNavMapOperator.cs` | 10 | NavMap | W4 |
| `SpatialModel.cs` | 9 | SpatialModel | W4 |
| `MetaGenLoader.cs` | 8 | MetaGen | W3 |
| `SpawnController.cs` | 8 | SpawnController | W4 |
| `StrokeStore.cs` | 10 | StreetGen | W3 |
| `ScenarioLibrary.cs` | 6 | TaleManager | W4 |
| `ScenarioCompiler.cs` | 6 | TaleManager | W4 |
| `GenerateClusterQuartersOperator.cs` | 7 | StreetGen | W3 |
| `Loader.cs` (world) | 7 | MetaGen | W3 |
| `TalePopulationGenerator.cs` | 4 | TaleManager | W4 |

**Total P1**: 145 calls  
**Timeline**: Weeks 3-4

### Priority 2: One-Shot Operations (Loaders, Config, Modules)

**Target**: Startup-time, no per-frame impact, but high call volume.

| File | Calls | Category | Week |
|------|-------|----------|------|
| `AAssetImplementation.cs` | 32 | AssetLoading | W5 |
| `GlTF.cs` | 16 | AssetLoading | W5 |
| `FbxModel.cs` | 16 | AssetLoading | W5 |
| `ROM Loader` | 18 | AssetLoading | W5 |
| `ModelAnimationCollection.cs` | 11 | Animation | W5 |
| `Physics/API.cs` | 10 | Physics | W6 |
| `Model.cs` | 7 | Animation | W5 |
| `ModelCache.cs` | 6 | Animation | W5 |
| `Mix.cs` | 8 | Casette | W6 |
| `NarrationManager.cs` | 7 | Narration | W6 |
| `SceneSequencer.cs` | 6 | Engine | W6 |
| `Engine.cs` | 5 | Engine | W6 |
| `InputController.cs` | 7 | Input | W6 |

**Total P2**: 150 calls  
**Timeline**: Weeks 5-6

### Priority 3: Remaining (Scattered, Low Impact)

**Target**: Everything else — tools, testing, UI, edge cases.

| Category | Calls | Week |
|----------|-------|------|
| UI & Widgets | 20 | W7 |
| Tools | 12 | W7 |
| Database | 12 | W7 |
| Map | 8 | W7 |
| Narration (remaining) | 8 | W7 |
| Testing | 6 | W7 |
| Other scattered | ~45 | W7 |

**Total P3**: 111 calls  
**Timeline**: Week 7

---

## Existing Category Assignments

From `JoyceCode/engine/DebugCategories.cs`:

```
Pathfinding = 0      → LocalPathfinder, NavMap
TaleManager = 1      → TaleManager, SpatialModel, scenario baking ✅
SpatialModel = 2     → Location queries (overlaps with TaleManager)
MetaGen = 3          → World generation, MetaGen operators
SpawnController = 4  → NPC spawning
StreetGen = 5        → Streets, Strokes, Street points, Nav lanes
NavMap = 6           → NavMap operations
Physics = 7          → Physics engine
AssetLoading = 8     → All loaders (FBX, OBJ, glTF, ROM, assets)
Casette = 9          → Config/Mix system
Animation = 10       → Animation baking, playback
ClickModule = 11     → Click handling
_Count = 12
```

---

## Implementation Notes

1. **Uncategorized calls**: Many in scattered files (UI, tools, testing) with no existing category
2. **Hardcoded prefixes**: Look for patterns like "STR GEN:", "TALE MGR:", "MGR:", etc. in strings
3. **Hot path verification**: LocalPathfinder, physics, animation need profiler validation after migration
4. **Framework files**: Logger.cs itself, ModuleFactory, etc. are infrastructure (low priority)
5. **Logging intensity**: Some files like PropertyEditor log on every keystroke/change — verify category defaults

---

## Next Steps

1. ✅ **Completed**: This audit (Phase 1.1)
2. **Pending**: Phase 1.2 — Expand Dc enum with 9 new categories
3. **Pending**: Phase 1.3 — Update nogame.properties.json with all categories
4. **Pending**: Phase 2 — Execute migration by priority (P0→P1→P2→P3)
