# NPC Commuting & Population Density Design Challenge

## Re-measured on the shipped city (2026-10-19, KAR-435)

Measured at `9d202ac2` with `dotnet run --project Testbed -- --measure-population 8`.
- The measurement runs the game's own `SpatialModel.ExtractFrom` and `TalePopulationGenerator.Generate`, the two calls `TaleManager.PopulateCluster` makes, on the first eight of 70 clusters of the shipped city: terrain-following, grade-separated, one building per piece of land.
- The Testbed has no `NavCluster`, so every location counts as pedestrian-reachable. The numbers are an upper bound on what the game's reachability filter leaves.

| # | cluster | size m | buildings | home | office | warehouse | shop | social_venue | street | NPCs | NPCs in a home | per home building | per home storey | most in one home |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | Yelukhdidru | 1000 | 28 | 5 | 4 | 18 | 0 | 1 | 91 | 279 | 269 | 53.8 | 1.6 | 70 |
| 1 | Ufyo | 3782 | 554 | 53 | 52 | 410 | 0 | 127 | 1578 | 4095 | 3667 | 69.2 | 1.8 | 84 |
| 2 | Jeeslufa | 3759 | 589 | 29 | 50 | 458 | 0 | 176 | 1565 | 4095 | 3282 | 113.2 | 2.8 | 159 |
| 3 | Seeeegi | 3789 | 592 | 73 | 96 | 372 | 0 | 164 | 1643 | 4095 | 3198 | 43.8 | 1.3 | 69 |
| 4 | Uaskeeu | 3764 | 416 | 28 | 49 | 312 | 0 | 103 | 1561 | 4095 | 3311 | 118.3 | 3.0 | 157 |
| 5 | Ekhgi | 3797 | 579 | 57 | 79 | 414 | 0 | 94 | 1688 | 4095 | 3316 | 58.2 | 1.5 | 83 |
| 6 | Fofep | 3713 | 550 | 78 | 105 | 323 | 0 | 151 | 1526 | 4095 | 3189 | 40.9 | 0.9 | 66 |
| 7 | Gokseeosatee | 1041 | 16 | 4 | 4 | 7 | 0 | 9 | 103 | 309 | 259 | 64.8 | 1.6 | 89 |

Column notes:
- "per home storey" uses `Location.Capacity`, which is the building's storey count (height / 3 m).
- The remaining NPCs (10–22 %) live at a `street_segment`; drifters and socialites may.
- Workplaces spread out: the median workplace has 2–4 NPCs and the busiest 17–45. Warehouses take the most workers, then streets.

### Conclusion

1. **The "3 homes" premise is gone for large clusters.** They have 28–78 home buildings, against 3 before the rework.
   - The two small clusters (≈ 1 km) still have only 4–5 homes for about 280–310 NPCs.
   - Homes are still a minority: 5–13 % of buildings. Warehouses are 55–78 %, because most buildings are tagged and only untagged ones default to `home`.
2. **Every large cluster sits at the population cap.** At 3 NPCs per street point, about 1,560 street points ask for about 4,700 NPCs, and `ComputeNpcCount` caps them at 4,095 (the 12-bit NPC index).
3. **Housing capacity is no longer the problem; distribution is.** Per storey, the load is 0.9–3.0 NPCs, which is plausible for apartment towers. Per building it is 41–118 on average and up to 159 in one home.
   - The cause is that `AssignLocationByRole` picks a home uniformly at random, ignoring `Capacity`. A low house therefore gets as many residents as a tower, and dozens of NPCs share a door.
   - Load balancing, i.e. weighting the home choice by `Capacity`, is still worth doing. It is deterministic and cheap, but it changes every NPC's home, so it changes the bake inputs and test baselines. That is the owner's call, which is why this re-measurement does not make the change.
4. **New: no `shop` locations at all.** Only the fishmonger pass in shopping zones (`GenerateShopsOperator`, `Game2`) produces type `shop`, and none of the eight clusters got one. Drink/Eat shops become `social_venue`. A merchant's workplace (`shop`) therefore falls back to a random reachable location, mostly a street segment.

The historical write-up below (2026-03-23) is kept as history. Its numbers describe the old flat city.

---

**Date:** 2026-03-23
**Status (2026-03-23):** Design phase - no implementation yet. *Superseded by the 2026-10-19 measurement above.*
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
- **Current**: 3 NPCs per street point, capped at 4095 (`TalePopulationGenerator.ComputeNpcCount`, 12-bit NPC index)
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
candidates = filter(pedestrian_reachable_locations, role, preferred_type)
if (candidates.empty)
    candidates = pedestrian_reachable_locations  // Fallback: any reachable location
return random(candidates)  // ← Clustering happens here
```

**Problem**: With only 3 homes, random selection has high collision rate.

### Role-to-Location Type Mapping
- `worker` / `authority` / `nightworker` → prefer `office` or `warehouse` ✓
- `merchant` → prefer `shop` (home may also be a shop)
- `reveler` → prefer `social_venue`; `socialite` → `street_segment` (workplace) / `social_venue` (venue); socialite home may be a `street_segment`
- `drifter` / `hustler` → prefer `street_segment` (also as venue; drifter home may be a `street_segment`)
- Fallback for all → random from all pedestrian-reachable locations

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
- `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` — Live game NPC behavior

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
