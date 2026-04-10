# NPC Commuting & Population Density Design Challenge

**Date:** 2026-03-23
**Status:** Design phase - no implementation yet
**Owner:** Next Claude instance

---

## Problem Statement

### What We Observed
Visual inspection of NPCs on the game map showed:
- Strong **diagonal line pattern** of ~10-20 NPCs moving back and forth
- All NPCs clustering at **2-3 home locations** repeatedly
- Unrealistic migration streams despite diverse role-based behavior

### Root Causes (Confirmed)

#### 1. Location Type Mismatch (FIXED)
- **Bug**: Workers/authority roles were looking for `loc.Type == "workplace"`
- **Reality**: SpatialModel creates `"office"` and `"warehouse"` types
- **Impact**: Workers fell back to street_segment workplaces
- **Status**: Fixed in commit f8a4f293, but clustering persists

#### 2. Insufficient Residential Buildings (FUNDAMENTAL ISSUE)
- **Current state**: Only 3 residential buildings per cluster
- **Impact**: Random location assignment causes repeated clustering at same homes
- **Example**: NPCs 256-267 all spawn at 2 positions:
  ```
  NPC 260, 264, 258, 261, 265 → <118.5, Y, -10.3>  (5 NPCs)
  NPC 262, 256, 266, 267, 263, 259 → <121.7, Y, 92.0>  (6 NPCs)
  ```

#### 3. Fundamental Game Design Mismatch (CORE ISSUE)
- **Expected by user**: Realistic city with 500k inhabitants in ~2km²
- **Population density**: 500k/2km² = 250k people/km² (Manhattan-level extreme density)
- **Actual capacity**: 3 residential buildings can realistically house ~100-300 people at max
- **Gap**: ~1667x mismatch between expectation and capability

---

## Current State: SpatialModel Distribution

```
SPATIAL MODEL for cluster:
- 3 homes (residential buildings)
- 3 offices (downtown business)
- 24 warehouses (industrial)
- 1 shops
- 17 social_venues (bars/restaurants)
- 126 streets
- Total: 174 locations
```

**Problem**: With 3 homes and hundreds of NPCs, clustering is inevitable with random selection.

---

## Design Questions (Unanswered)

These must be answered before implementing a solution:

### 1. **Target Population Density**
- **Option A**: 500k NPCs visible (unrealistic, would require 1000s of buildings)
- **Option B**: ~100 visible walking NPCs with ~500 more in buildings (abstracted)
- **Option C**: ~10-50 visible NPCs per cluster with realistic densities (5-10k people/km²)
- **Question**: What feels right for gameplay?

### 2. **Residential Building Distribution**
- **Option A**: Keep 3 homes, reduce NPC count drastically
- **Option B**: Increase residential buildings to 10-20% of all buildings
- **Option C**: Change definition of "home" (e.g., multiple apartments per building)
- **Option D**: Multi-cluster housing model (homes in adjacent clusters too)
- **Question**: How should housing be distributed geographically?

### 3. **Commuting Model (Behavior)**
- **Option A**: Random long-distance commutes (current, unrealistic)
- **Option B**: Local-first model (prefer workplaces <500m from home)
- **Option C**: Time-based migration (morning peak → evening reverse flow)
- **Option D**: Accept unrealistic patterns as aesthetic/gameplay choice
- **Question**: What commuting behavior would create compelling visuals and feel right?

### 4. **NPC Population Per Cluster**
- **Current**: Unlimited, based on cluster parameters
- **Proposal**: Define target NPC count per cluster type
  - Small cluster: 20-30 NPCs
  - Medium cluster: 50-100 NPCs
  - Large cluster: 100-200 NPCs
- **Question**: What feels right for the game experience?

---

## Diagnostic Data Collected

### NPC Assignment Logs (Pre-fix)
```
NPC 0: merchant home=home(id=20) work=shop(id=17) homePos=(121,7,92,1) workPos=(27,2,138,1)
NPC 1: socialite home=street_segment(id=163) work=street_segment(id=149) [by design]
NPC 6: authority home=home(id=10) work=street_segment(id=153) [BUG - FIXED]
```

### Spawn Position Clustering (Post-fix)
```
Multiple NPCs spawn at identical home coordinates:
- Position A: <118.5, Y, -10.3> ← 5 NPCs
- Position B: <121.7, Y, 92.0> ← 6 NPCs
```

### Location Extraction Stats
- Cluster has 3 homes, 3 offices, 24 warehouses, 17 social venues
- 126 street points available
- Total: 174 locations, but heavily skewed toward industrial/streets

---

## Current Implementation Details

### Location Assignment Algorithm
**File**: `JoyceCode/engine/tale/TalePopulationGenerator.cs:AssignLocationByRole()`

```csharp
// Simplified pseudocode:
candidates = filter(all_locations, role, preferred_type)
if (candidates.empty)
    candidates = all_locations  // Fallback: any location
return random(candidates)  // ← Clustering happens here
```

**Problem**: With only 3 homes, random selection has high collision rate.

### Role-to-Location Type Mapping
- `worker` / `authority` / `nightworker` → prefer `office` or `warehouse` ✓
- `merchant` → prefer `shop`
- `socialite` / `reveler` → prefer `social_venue`
- `drifter` / `hustler` → prefer `street_segment`
- Fallback for all → random from all locations

---

## Next Steps (For Future Instance)

### Phase 1: Design Decisions
1. Answer the 4 design questions above
2. Define realistic population assumptions
3. Document target NPC density per cluster
4. Define commuting behavior model

### Phase 2: Implementation (if approved)
1. Adjust building generation (increase residential %)
2. Implement location load-balancing in assignment
3. Add distance-based preferences to commuting
4. Potentially: Multi-cluster home/work assignments

### Phase 3: Validation
1. Run diagnostic logs again
2. Verify clustering is resolved
3. Check visual commuting patterns match expectations

---

## References

### Commits
- `f8a4f293` — Fixed location type mismatch (workers now get office/warehouse, not streets)
- `2f78182e` — Added diagnostic logging for location assignments
- `37d8521d` — Role-based NPC icon coloring

### Key Files
- `JoyceCode/engine/tale/TalePopulationGenerator.cs` — Location assignment logic
- `JoyceCode/engine/tale/SpatialModel.cs` — Building extraction and location types
- `nogameCode/nogame/characters/citizen/TaleSpawnOperator.cs` — NPC materialization
- `JoyceCode/engine/tale/TaleEntityStrategy.cs` — Live game NPC behavior

### Test Data Location
- Game logs show real NPC assignments during cluster population
- Enable traces: look for `TALE GEN NPC` and `SPATIAL MODEL` lines

---

## Open Questions for Discussion

1. Should we aim for realistic density or accept gameplay aesthetics?
2. Is the 500k population target essential, or can we recalibrate expectations?
3. Should commuting be local-first (realistic) or random (current)?
4. How many residential buildings should a typical cluster have?
5. Should NPCs be able to live in adjacent clusters, or is single-cluster homes required?

---

**Status**: Ready for design discussion and decision. Implementation blocked on design choices.
