# Debug Filter Migration Checklist

**Status**: Phase 1 Complete ✅ | Phase 2 IN PROGRESS (Priority 0 + Priority 1 Week 3)  
**Migrated**: 33 calls (5.8%)  
**Remaining**: 538 calls (94.2%)
- Priority 0: ✅ 7 calls (hot paths)
- Priority 1 W3: 26/102 calls (streets & world gen)
- TaleManager: 24 calls (earlier session)

---

## Priority 0: Hot Paths (Week 2 — Weeks 1-2)

**Target**: Per-frame systems, zero-overhead critical.  
**Total Calls**: 12  
**Expected Performance**: ~3-5% frame time improvement when disabled
**Status**: ✅ COMPLETE (2026-04-20)

- [x] `builtin/modules/satnav/LocalPathfinder.cs` — 5 calls → `Dc.Pathfinding`
  - A* inner loops (lines: 97, 101, 129, 145, 214) — ✅ MIGRATED
  - Removed hardcoded "LocalPathfinder: " prefixes

- [x] `engine/physics/systems/MoveKineticsSystem.cs` — 1 active call → `Dc.Physics`
  - Per-frame kinetics sync (line 114) — ✅ MIGRATED
  - Note: Audit counted 4 calls, only 1 active in current codebase

- [x] `engine/joyce/systems/AnimationSystem.cs` — 1 active call → `Dc.Animation`
  - Per-frame animation updates (line 33) — ✅ MIGRATED
  - Note: Audit counted 3 calls, only 1 active in current codebase

**Milestone**: ✅ All P0 migrated (7 calls actual vs 12 audited)

---

## Priority 1: Frequent Operations (Weeks 3-4)

**Target**: Cluster generation, world generation, spawning.  
**Total Calls**: 145  
**Timeline**: 2 weeks
**Status**: 🔄 IN PROGRESS (26/145 calls, 18%)

### Week 3: Street & World Generation

**Completed:**
- [x] `engine/streets/GenerateClusterStreetsOperator.cs` — 18 calls → `Dc.StreetGen` ✅ (2026-04-20)
- [x] `engine/streets/StreetPoint.cs` — 8 actual calls → `Dc.StreetGen` ✅ (2026-04-20)
  - Note: Audit counted 14, only 8 active in codebase

**Pending:**
- [ ] `engine/streets/Generator.cs` — 12 calls → `Dc.StreetGen`
- [ ] `engine/streets/StrokeStore.cs` — 10 calls → `Dc.StreetGen`
- [ ] `engine/streets/GenerateClusterQuartersOperator.cs` — 7 calls → `Dc.StreetGen`
- [ ] `engine/world/GenerateClustersOperator.cs` — 15 calls → `Dc.MetaGen`
- [ ] `engine/world/MetaGen.cs` — 11 calls → `Dc.MetaGen`
- [ ] `engine/world/MetaGenLoader.cs` — 8 calls → `Dc.MetaGen`
- [ ] `engine/world/Loader.cs` — 7 calls → `Dc.MetaGen`

**Week 3 Status**: 26/102 calls migrated (25%)  
**Milestone**: All street & world gen migrated, regression tests pass

### Week 4: TALE & Spatial & Spawn

- [ ] `builtin/modules/satnav/GenerateNavMapOperator.cs` — 10 calls → `Dc.NavMap`
- [ ] `engine/tale/SpatialModel.cs` — 9 calls → `Dc.SpatialModel`
- [ ] `engine/behave/SpawnController.cs` — 8 calls → `Dc.SpawnController`
- [ ] `engine/tale/bake/ScenarioLibrary.cs` — 6 calls → `Dc.TaleManager`
- [ ] `engine/tale/bake/ScenarioCompiler.cs` — 6 calls → `Dc.TaleManager`
- [ ] `engine/tale/TalePopulationGenerator.cs` — 4 calls → `Dc.TaleManager`

**Week 4 Total**: 43 calls  
**Milestone**: All P1 migrated, full regression test suite passes (192 tests)

---

## Priority 2: One-Shot Operations (Weeks 5-6)

**Target**: Loaders, startup config, module initialization.  
**Total Calls**: 150  
**Timeline**: 2 weeks

### Week 5: Asset Loaders & Animation

- [ ] `engine/AAssetImplementation.cs` — 32 calls → `Dc.AssetLoading`
- [ ] `builtin/loader/GlTF.cs` — 16 calls → `Dc.AssetLoading`
- [ ] `builtin/loader/fbx/FbxModel.cs` — 16 calls → `Dc.AssetLoading`
- [ ] `engine/rom/Loader.cs` — 18 calls → `Dc.AssetLoading`
- [ ] `engine/joyce/ModelAnimationCollection.cs` — 11 calls → `Dc.Animation`
- [ ] `engine/joyce/Model.cs` — 7 calls → `Dc.Animation`
- [ ] `engine/joyce/ModelCache.cs` — 6 calls → `Dc.Animation`

**Week 5 Total**: 106 calls  
**Milestone**: All asset loaders migrated, startup verified

### Week 6: Physics, Config, Engine, Input

- [ ] `engine/physics/API.cs` — 10 calls → `Dc.Physics`
- [ ] `engine/casette/Mix.cs` — 8 calls → `Dc.Casette`
- [ ] `engine/narration/NarrationManager.cs` — 7 calls → `Dc.Narration`
- [ ] `engine/SceneSequencer.cs` — 6 calls → `Dc.Engine`
- [ ] `engine/Engine.cs` — 5 calls → `Dc.Engine`
- [ ] `builtin/controllers/InputController.cs` — 7 calls → `Dc.Input`

**Week 6 Total**: 43 calls  
**Milestone**: All P2 migrated, startup & load times verified

---

## Priority 3: Remaining (Week 7)

**Target**: UI, tools, testing, scattered subsystems.  
**Total Calls**: 111

### Categories

- [ ] **UI** (20 calls) → `Dc.UI`
  - `ui/Property.cs` — 9 calls
  - `builtin/jt/RootWidget.cs` — 7 calls
  - `builtin/jt/Widget.cs` — 4 calls

- [ ] **Tools** (12 calls) → `Dc.Tools`
  - `builtin/tools/ExtrudePoly.cs` — 7 calls
  - `elevation/Cache.cs` — 6 calls (or create `Dc.Elevation`?)
  - `ModelBuilder.cs`, `LGenerator.cs` — 2 calls

- [ ] **Database** (12 calls) → `Dc.Database`
  - `engine/DBStorage.cs` — 7 calls
  - `rom/Loader.cs` subset — 5 calls (note: overlaps with AssetLoading)

- [ ] **Narration** (8 calls, remaining) → `Dc.Narration`
  - `NarrationRunner.cs`, `NarrationScript.cs`, etc.

- [ ] **Map** (8 calls) → `Dc.Map`
  - `builtin/map/DefaultMapProvider.cs` — 6 calls
  - `MapViewer.cs`, `MapIconManager.cs` — 2 calls

- [ ] **Other Scattered** (51 calls)
  - `engine/testing/TestDriverModule.cs` — 6 calls → `Dc.Framework`
  - Module system, utilities, edge cases

**Week 7 Total**: 111 calls  
**Milestone**: All remaining calls migrated, comprehensive audit complete

---

## Verification Checkpoints

### After Priority 0 ✅

- [x] Build succeeds: `dotnet build Joyce/Joyce.csproj -c Release` — ✅ (591 warnings, 0 errors)
- [x] No new warnings introduced (591 baseline) — ✅ CONFIRMED
- [x] All migrated files follow pattern: `private static readonly Dc _dc = Dc.Category;` — ✅ 3/3 files
- [x] No hardcoded prefixes remain in P0 files — ✅ CONFIRMED
- [x] Category in all messages: `Trace(_dc, $"...")`, `Error(_dc, $"...")` — ✅ CONFIRMED

### After Each Priority Level (Ongoing)

- [ ] Build succeeds: `dotnet build Joyce/Joyce.csproj -c Release`
- [ ] No new warnings introduced (591 baseline)
- [ ] All migrated files follow pattern: `private static readonly Dc _dc = Dc.Category;`
- [ ] No hardcoded prefixes remain ("TALE MGR:", "STR GEN:", etc.)
- [ ] Category in all messages: `Trace(_dc, $"...")`, `Warning(_dc, $"...")`

### After All Priorities Complete

- [ ] Regression tests: `dotnet test tests/JoyceCode.Tests/` — all 192 passing
- [ ] Startup sequence verified: Game launches, loads config, populates world
- [ ] Profiler snapshot: Zero performance regression in Release builds
- [ ] CLAUDE.md updated with debug output guidelines
- [ ] Code review checklist added to PR template
- [ ] All 571 calls migrated (547 remaining + 24 already done)

---

## Execution Notes

### General Pattern

Every file follows this structure:

```csharp
namespace engine.subsystem;

public class ClassName
{
    // ← ALWAYS declare at class top
    private static readonly Dc _dc = Dc.YourCategory;

    public void Method()
    {
        // Filtered (zero overhead when disabled)
        Trace(_dc, $"message");
        
        // Always emits with category tag
        Warning(_dc, $"warning");
        Error(_dc, $"error");
    }
}
```

### Removing Hardcoded Prefixes

**Before:**
```csharp
Trace($"STR GEN: Generating {count} strokes");
Warning($"STR GEN: Sanity check failed");
```

**After:**
```csharp
Trace(_dc, $"Generating {count} strokes");
Warning(_dc, $"Sanity check failed");
// Output becomes: [StreetGen] Generating ...
```

### Hot Path Optimization (Rare)

Only for true per-iteration inner loops:

```csharp
for (int i = 0; i < 10000; i++)
{
    if (Is(_dc)) Trace($"Iteration {i}");
}
```

But prefer moving logging outside loops when possible.

---

## Success Metrics Summary

| Metric | Target | Status |
|--------|--------|--------|
| Files migrated | 135 | Starting |
| Logger calls migrated | 571 | 24/571 (4.2%) |
| Performance regression | 0% | TBD (P0 will measure) |
| Tests passing | 192/192 | TBD (full run after P1) |
| Build warnings | 591 (no new) | TBD |
| CLAUDE.md updated | ✅ | Pending (W8) |

---

## Timeline Summary

| Week | Priority | Files | Calls | Status | Deliverable |
|------|----------|-------|-------|--------|-------------|
| W1 | Audit | 135 | 571 | ✅ | Complete (this document) |
| W2 | P0 | 3 | 7/12 | ✅ | Hot paths migrated (2026-04-20), profiler pending |
| W3 | P1 | 9 | 26/102 | 🔄 | 2/9 files done (streets initial), continuing |
| W4 | P1 | 6 | 43 | ⏳ | TALE, Spatial, Spawn migrated, 192 tests pass |
| W5 | P2 | 7 | 106 | ⏳ | Asset loaders migrated, startup verified |
| W6 | P2 | 6 | 43 | ⏳ | Config, physics, narration migrated |
| W7 | P3 | ~15 | 111 | ⏳ | Remaining subsystems migrated |
| W8+ | — | — | — | ⏳ | Code review enforcement, CLAUDE.md updates |

---

## Next Steps

1. ✅ Phase 1.1: Codebase audit completed → `DEBUG-FILTER-AUDIT.md`
2. ✅ Phase 1.2: Dc enum expanded (12 → 21 categories)
3. ✅ Phase 1.3: nogame.properties.json updated with all categories
4. ✅ Phase 1.4: This checklist created → `DEBUG-FILTER-MIGRATION-CHECKLIST.md`
5. ✅ Phase 2.1: Priority 0 migration complete (LocalPathfinder, Physics, Animation) — commit b238d772
6. ⏭️ **Next**: Priority 1 migration (Week 3) — Streets & World Generation (102 calls)
   - Begin with `engine/streets/GenerateClusterStreetsOperator.cs` (18 calls)
   - Continue through Week 3 street generation batch
   - Week 4: TALE, Spatial, Spawn systems

**Status**: On track. Priority 0 complete, moving to Priority 1.
