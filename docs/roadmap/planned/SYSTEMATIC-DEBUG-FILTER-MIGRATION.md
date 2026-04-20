# Systematic Debug Filter Migration Across Engine

**Status**: Planned  
**Date Created**: 2026-04-20  
**Related**: `docs/roadmap/done/DEBUG-FILTER-ARCHITECTURE.md`  

---

## Overview

The DebugFilter system (category-based, zero-overhead filtering) is now functional with initial implementations in `TaleManager.cs`. This document outlines a **systematic, three-phase approach** to migrate all engine logger calls across the codebase to use categories.

**Goal**: Every `Trace()`, `Warning()`, `Error()`, and `Wonder()` call in the engine uses the DebugFilter category system by end of Q2 2026.

**Success Criteria**:
- All unconditional logger calls wrapped with category guards or category-prefixed overloads
- No hardcoded subsystem prefixes ("TALE MGR:", "STR GEN:") remaining in log output
- Category included in every debug message for log analysis
- CLAUDE.md documents the pattern for all future development
- Zero performance regression in Release builds

---

## Phase 1: Audit & Categorization

### 1.1 Codebase Audit

Determine scope and identify high-impact areas:

```bash
# Count all logger calls by file
grep -r "Trace(\|Warning(\|Error(\|Wonder(" JoyceCode --include="*.cs" | \
  cut -d: -f1 | sort | uniq -c | sort -rn

# Count by subsystem directory
find JoyceCode -name "*.cs" -exec grep -l "Trace(\|Warning(" {} \; | \
  sed 's|JoyceCode/\([^/]*\)/.*|\1|' | sort | uniq -c | sort -rn
```

**Expected output clusters**:
- `engine/tale/` — ~80 logger calls (Trace, Warning, Error)
- `engine/streets/` — ~40 calls
- `builtin/modules/satnav/` — ~30 calls
- `engine/world/` — ~25 calls
- `builtin/loaders/` — ~60 calls (FBX, OBJ, glTF)
- `engine/casette/` — ~15 calls
- Others — ~50 calls scattered

**Deliverable**: `docs/DEBUG-FILTER-AUDIT.md` listing all files with logger call counts.

### 1.2 Category Mapping

Map subsystems to existing or new `Dc` enum entries:

```markdown
## Existing Categories (from DebugCategories.cs)
- Pathfinding → LocalPathfinder, NavMap, routing systems
- TaleManager → Cluster population, NPC advancement ✅ DONE
- SpatialModel → Location extraction, venue resolution
- MetaGen → World generation operators
- SpawnController → NPC spawning
- StreetGen → Street geometry generation
- NavMap → Street-based pathfinding
- Physics → Physics engine (ECS systems)
- AssetLoading → FBX/OBJ/glTF loaders, texture packing, resource compilation
- Casette → Config loading, Mix system
- Animation → Animation baking, playback systems
- ClickModule → Input and click handling

## New Categories Needed
- Modules → DI, module lifecycle, dependency resolution
- Properties → Props system, property changes
- Saver → Entity save/load, persistence
- Inventory → Item system
- Quest → Quest lifecycle (separate from TALE)
```

**Deliverable**: `DebugCategories.cs` expanded with 3-4 new categories; `nogame.properties.json` updated with all category entries.

### 1.3 Migration Priority Matrix

Create a matrix based on:
- **Call frequency** (per-frame vs one-shot)
- **Call count** (total logger calls in subsystem)
- **Impact** (hot path vs cold path)

```markdown
| Priority | Subsystem | File(s) | Calls | Reason |
|----------|-----------|---------|-------|--------|
| **P0** | Pathfinding | LocalPathfinder.cs | 28 | A* inner loops (hot path) |
| **P0** | Physics | MoveKineticsSystem.cs | 12 | Per-frame system |
| **P0** | Animation | AnimationSystem.cs | 8 | Per-frame system |
| **P1** | Streets | Generator.cs, StrokeStore.cs | 32 | Frequent (cluster gen) |
| **P1** | TALE | TaleManager.cs | 23 | Frequent (cluster pop) ✅ DONE |
| **P1** | SpatialModel | SpatialModel.cs | 15 | Location queries |
| **P2** | Loaders | Fbx.cs, ObjLoader.cs, glTF.cs | 60 | One-shot (startup) |
| **P2** | Casette/Mix | Loader.cs, Mix.cs | 20 | One-shot (config load) |
| **P3** | Other | Remaining | ~50 | Scattered, low impact |
```

**Deliverable**: `DEBUG-FILTER-MIGRATION-PRIORITIES.md` with prioritized checklist.

---

## Phase 2: Priority-Based Migration

### 2.1 Implementation Pattern

All classes follow this standard pattern:

```csharp
namespace engine.subsystem;

public class ClassName
{
    // ← ALWAYS declare at class top
    private static readonly engine.Dc _dc = engine.Dc.YourCategory;

    public void SomeMethod()
    {
        // Filtered trace (string built only if enabled)
        Trace(_dc, $"Detailed debug info: {someValue}");
        
        // Category-prefixed warning (always emits, tagged)
        Warning(_dc, $"Unexpected condition: {explanation}");
        
        // Category-prefixed error (always emits, tagged)
        Error(_dc, $"Critical failure: {details}");
        
        // Rarely needed: explicit guard for extreme hot paths (inner loop per-iteration)
        if (Is(engine.Dc.YourCategory)) 
            Trace($"Per-iteration: {iteration}");
    }

    private void InnerLoop()
    {
        for (int i = 0; i < 10000; i++)
        {
            // Only use if this trace is truly per-iteration AND in compiled Release
            // Otherwise use the pattern above outside the loop
            if (Is(_dc)) Trace($"Iteration {i}");
        }
    }
}
```

### 2.2 Priority 0 (Weeks 1-2): Hot Path Systems

**Target**: Per-frame systems where logger overhead is measurable.

Files:
- `JoyceCode/builtin/modules/satnav/LocalPathfinder.cs` — A* inner loop (28 calls)
- `JoyceCode/engine/physics/systems/MoveKineticsSystem.cs` — Per-frame kinetics
- `JoyceCode/engine/joyce/systems/AnimationSystem.cs` — Per-frame animation updates

Approach:
1. Add `private static readonly Dc _dc = Dc.Pathfinding/Physics/Animation;`
2. Migrate all unconditional calls to `Trace(_dc, $"...")`
3. Verify zero overhead with profiler snapshot before/after
4. Run full regression test suite (192 tests)

Expected result: ~4-8% frame time improvement when categories disabled.

### 2.3 Priority 1 (Weeks 3-4): Frequent Operations

**Target**: Subsystems called repeatedly but not per-frame (cluster gen, spawning, world gen).

Files:
- `JoyceCode/engine/streets/GenerateClusterStreetsOperator.cs` — Street generation
- `JoyceCode/engine/streets/StrokeStore.cs` — Stroke caching
- `JoyceCode/engine/tale/TalePopulationGenerator.cs` — NPC generation
- `JoyceCode/engine/tale/SpatialModel.cs` — Location extraction and queries
- `JoyceCode/builtin/modules/spawn/SpawnController.cs` — NPC spawning

Approach:
1. Each file gets `private static readonly Dc _dc` for its category
2. Migrate all `Trace()` calls to `Trace(_dc, $"...")`
3. Migrate all `Warning()` calls to `Warning(_dc, $"...")`
4. Remove hardcoded subsystem prefixes from strings (e.g., "STR GEN: " → just the message)
5. Run regression tests

### 2.4 Priority 2 (Weeks 5-6): One-Shot Operations

**Target**: Loaders, initialization, one-time operations (startup only).

Files:
- `JoyceCode/builtin/loader/Fbx.cs` → `Dc.AssetLoading`
- `JoyceCode/builtin/loader/ObjLoader.cs` → `Dc.AssetLoading`
- `JoyceCode/builtin/loader/GltfSharp.cs` → `Dc.AssetLoading`
- `JoyceCode/engine/casette/Loader.cs` → `Dc.Casette`
- `JoyceCode/engine/casette/Mix.cs` → `Dc.Casette`
- `JoyceCode/engine/Props.cs` → `Dc.Properties` (new category)

Approach:
1. Create new `Dc.Properties` category if needed
2. Migrate all calls to category-prefixed versions
3. One-shot performance impact negligible, focus on consistency
4. Run startup sequence to verify config loading still works

### 2.5 Priority 3 (Week 7): Remaining Subsystems

**Target**: Everything else (module system, inventory, quests, test code).

Approach:
1. Audit remaining files
2. Group by category
3. Batch migrate similar files
4. Add new categories as needed

---

## Phase 3: Implementation with Enforcement

### 3.1 Update CLAUDE.md

Add a new section:

```markdown
## Debug Output and Categories

### Pattern for All Debug Output

Every class that uses `Trace()`, `Warning()`, `Error()`, or `Wonder()` must declare its category at the top:

\`\`\`csharp
public class MyClass
{
    private static readonly engine.Dc _dc = engine.Dc.YourCategory;

    public void Method()
    {
        Trace(_dc, $"message");           // Filtered, zero overhead when disabled
        Warning(_dc, $"warning");          // Always emits, category-tagged
        Error(_dc, $"error");              // Always emits, category-tagged
    }
}
\`\`\`

### Categories

See `JoyceCode/engine/DebugCategories.cs` for the complete list. If your subsystem doesn't have a category, add one:

1. Add entry to `Dc` enum in `DebugCategories.cs` (before `_Count`)
2. Increment `_Count`
3. Add `"debug.category.newname": false` to `models/nogame.properties.json`
4. Use `private static readonly Dc _dc = Dc.NewName;` in your class

### No Hardcoded Prefixes

**Bad:**
```csharp
Trace($"MY_SUBSYSTEM: Something happened");
Warning($"MY_SUBSYSTEM: Problem occurred");
```

**Good:**
```csharp
Trace(_dc, $"Something happened");
Warning(_dc, $"Problem occurred");
// Output automatically becomes: [MySubsystem] Something happened
```

### Hot Path Optimization (Rare)

For true inner-loop scenarios where even category overhead matters:

```csharp
for (int i = 0; i < largeCount; i++)
{
    // Only if this is truly per-iteration AND in hot path
    if (Is(_dc)) Trace($"Iteration {i}");
}
```

But prefer moving the logging outside the loop when possible.

### Configuration

Enable categories at runtime:
- Edit `nogame.properties.json` before startup: `"debug.category.pathfinding": true`
- Or at runtime: `DebugFilter.SetCategory(Dc.Pathfinding, true)`

See `docs/roadmap/done/DEBUG-FILTER-ARCHITECTURE.md` for full API.
```

### 3.2 Code Review Guidelines

Add to PR review checklist:

- [ ] New logger calls use category-prefixed versions (`Trace(_dc, ...)`)
- [ ] No hardcoded subsystem prefixes in log strings
- [ ] If new subsystem: category added to `Dc` enum
- [ ] If new category: entry added to `nogame.properties.json`
- [ ] Regression tests pass

### 3.3 Future Prevention

**For new code**:
- Any new file with logger calls must declare `_dc` at top
- Any PR adding logger calls without category gets feedback during review
- Use IDE templates/snippets to auto-insert the pattern

**Optional (future enhancement)**: 
- Custom Roslyn analyzer to warn about uncategorized calls
- Git pre-commit hook to check for hardcoded prefixes

---

## Execution Timeline

### Week 1 (Apr 21-27)
- [ ] Run codebase audit, generate `DEBUG-FILTER-AUDIT.md`
- [ ] Create migration priorities checklist
- [ ] Expand `Dc` enum with new categories
- [ ] Update `nogame.properties.json`

### Week 2 (Apr 28 - May 4)
- [ ] Migrate Priority 0: LocalPathfinder, Physics, Animation (P0)
- [ ] Run profiler: measure zero-overhead improvement
- [ ] Run regression tests (192 tests)

### Week 3 (May 5-11)
- [ ] Migrate Priority 1: Streets, TaleManager (mostly done), SpatialModel, Spawn (P1)
- [ ] Run regression tests

### Week 4 (May 12-18)
- [ ] Migrate Priority 1 remainder
- [ ] Update CLAUDE.md with debug output guidelines
- [ ] Run full test suite

### Week 5 (May 19-25)
- [ ] Migrate Priority 2: Loaders, Casette (P2)
- [ ] Verify startup sequence still works

### Week 6 (May 26 - Jun 1)
- [ ] Migrate Priority 2 remainder
- [ ] Final review of all changes

### Week 7 (Jun 2-8)
- [ ] Migrate Priority 3 remainder
- [ ] Audit for any remaining uncategorized calls
- [ ] Final cleanup

### Week 8+ (Jun 9+)
- [ ] Enforcement: code review feedback on new code
- [ ] Monitor adoption in new features

---

## Success Metrics

By completion:
- **100% of logger calls** use category system
- **0% hardcoded prefixes** in log strings (removed all "TALE MGR:", "STR GEN:", etc.)
- **0 performance regression** in Release builds (net positive due to filtering)
- **All 192 regression tests passing**
- **CLAUDE.md documents pattern** for all future development
- **Code reviews enforce** category requirement in new PRs

---

## Risks & Mitigation

| Risk | Mitigation |
|------|-----------|
| Large refactoring causes regressions | Run regression tests after each priority batch |
| Developers forget to add category to new code | Update CLAUDE.md + code review checklist + IDE templates |
| Performance regression in Release builds | Measure with profiler before/after; category overhead is zero in Release |
| Some subsystems hard to categorize | Create catch-all categories (e.g., `Engine`, `Infrastructure`) if needed |
| Inconsistent prefix format in log output | Enforce `[CategoryName]` format in Logger overloads (already done) |

---

## Deliverables

By project completion:
1. `DEBUG-FILTER-AUDIT.md` — Complete audit of all logger calls
2. `DEBUG-FILTER-MIGRATION-PRIORITIES.md` — Prioritized checklist with tracking
3. Updated `JoyceCode/engine/DebugCategories.cs` — Full category enum
4. Updated `models/nogame.properties.json` — All category entries
5. Updated `CLAUDE.md` — Debug output guidelines for all future work
6. ~500 lines of migrated code across ~30 files
7. Zero regressions: 192/192 tests passing

---

## Questions for Stakeholders

Before execution:

1. **Timeline**: Are 8 weeks (through early June) acceptable, or should this be faster/slower?
2. **Category granularity**: Should we have broad categories (e.g., "Engine") or fine-grained per-subsystem?
3. **New categories**: Should new subsystems auto-generate new categories, or reuse existing ones?
4. **Enforcement**: How strict should code review be about requiring categories on day 1 after rollout?
5. **Documentation**: Is CLAUDE.md the right place, or should there be a separate "Debug Output Style Guide"?

---

## Related Documents

- `docs/roadmap/done/DEBUG-FILTER-ARCHITECTURE.md` — System design and API
- `docs/roadmap/done/SYSTEMATIC-DEBUG-FILTER-MIGRATION.md` — This document
- `JoyceCode/engine/DebugCategories.cs` — Category enum definition
- `JoyceCode/engine/DebugFilter.cs` — Filtering implementation
