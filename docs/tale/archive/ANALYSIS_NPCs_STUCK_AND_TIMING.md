# Analysis: NPCs Stuck at Street Points & Game Time Scaling

**Date:** 2026-03-30
**Context:** ~20% of NPCs at near-exact street points not moving; walking speeds and game time acceleration need review

---

## Issue 1: NPCs Stuck at Exact Street Points (No Movement)

### Root Cause

In `TaleEntityStrategy.cs` lines 283-289 and 400-406, a fallback behavior prevents NPCs from walking through buildings:

```csharp
// Fallback: if route is null and destination is far away but unreachable,
// location is likely isolated (dead-end street or disconnected venue).
// Stay at current location instead of using straight-line fallback through buildings.
if (route == null && distToDestination > 10f)
{
    Trace($"TALE ENTITY: NPC {_npcId} destination '{locationName}' unreachable (distance={distToDestination:F0}m but no path). Staying at current location instead.");
    _setupActivity();
    TriggerStrategy("activity");
    return;
}
```

**Trigger conditions:**
1. Pathfinding returns `null` (destination unreachable via NavMap)
2. Distance to destination > 10m

**Consequence:** NPC stays at current location and enters "activity" state, appearing visibly idle on the street.

### Why Pathfinding Returns Null

These are the likely causes:

| Cause | Evidence | Fix Location |
|-------|----------|--------------|
| **Isolated venue** | Entry point placed outside NavMap boundaries | World generation validation |
| **Orphaned street points** | Location's entry point = street junction with no connected lanes | Street network validation (Phase 7C) |
| **Dead-end with no lanes** | Street point exists but GenerateNavMapOperator created no crossing/sidewalk lanes | NavMap generation |
| **Unreachable cluster** | Location in different cluster with no inter-cluster navigation | Inter-cluster routing (Phase D D2) |
| **Wrong transportation type** | NPC requires pedestrian but destination only has car lanes | Routing preference filtering |

### Investigation Steps

1. **Start game with autologin to "new"**
2. **Check game logs for:**
   ```
   "destination unreachable (distance="
   "Staying at current location instead."
   ```
3. **Identify stuck NPCs by locationName in logs**
4. **Cross-reference with spatial model:**
   - Is entry point != Vector3.Zero?
   - Is entry point within cluster AABB?
   - Are any NavJunctions within 5m of entry point?

### Recommended Fixes (Priority Order)

#### Fix A: Validate Location Entry Points During World Gen (HIGH PRIORITY)
- Add post-generation check: for each location, verify entry point has adjacent NavJunction
- If entry point is orphaned, snap to nearest NavJunction or flag location as unreachable
- **Impact:** Prevent unreachable destinations from being assigned to NPCs

#### Fix B: Extend Fallback Distance to Allow Straight-Line Travel (MEDIUM)
Replace `distToDestination > 10f` with:
```csharp
if (route == null && distToDestination > 30f)  // Allow up to 30m straight-line
{
    // Fallback to straight-line if closer than 30m
    // Stay in place only if destination is very far and isolated
}
```
**Rationale:** A 30m detour through open space is acceptable; 100m+ warrants staying put

#### Fix C: Log and Alert on Unreachable Assignments (MEDIUM)
Add diagnostic before calling TriggerStrategy:
```csharp
Warn($"UNREACHABLE_LOCATION: NPC {_npcId} assigned to '{locationName}' but no path exists. " +
     $"EntryPoint={destLoc.EntryPosition}, Distance={distToDestination:F1}m. Location type={destLocType}");
```
Collect these in test runs to identify systemic unreachable venue patterns.

#### Fix D: Implement Inter-Cluster Routing (PHASE D D2)
Enable NPCs to pathfind across cluster boundaries for locations in nearby clusters.

---

## Issue 2: Walking Speed & Game Time Analysis

### Current Settings

| Parameter | Value |
|-----------|-------|
| **Pedestrian speed** | 1.5 m/s (realistic ✓) |
| **Game day duration** | 1 real day = 1 game day; 1 real minute = 1440 game minutes |
| **Time acceleration** | 1800 real seconds per game day = **480x** |
| **Run threshold** | 7/3.6 = 1.944 m/s |

### Time Conversion Formula

```
Real seconds → Game minutes: multiply by 480
Game minutes → Real seconds: divide by 480
Game minutes → Real seconds: gameMin / 480

Examples:
- 480 game min (1 game day shift) = 1 real second... NO WAIT
```

Wait, let me recalculate. In TaleEntityStrategy.cs line 315:
```csharp
float realSeconds = gameMinutes / (24f * 60f) * RealSecondsPerGameDay;
// = gameMinutes / 1440 * 1800
// = gameMinutes * 1.25
```

So: **1 game minute = 1.25 real seconds** (not 480x as I initially thought)

**Corrected time factor: 1 real second = 0.8 game minutes = 48 game seconds**

### Realistic Activity Durations (Converted to Game Time)

| Activity | Real-life | Game Minutes | Real Seconds @ 1800s/day |
|----------|-----------|--------------|-------------------------|
| **Sleep** | 8 hours | 480 | 600 |
| **Work shift** | 8 hours | 480 | 600 |
| **Lunch break** | 1 hour | 60 | 75 |
| **Shop/errands** | 30 min | 30 | 37.5 |
| **Coffee break** | 15 min | 15 | 18.75 |
| **Meal at home** | 1 hour | 60 | 75 |
| **Commute 500m** | 5.6 min | 5.6 | 7 |
| **Commute 1km** | 11.1 min | 11.1 | 14 |

### Current Durations in JSON

From `factory_worker.json`:
```json
"duration_minutes_min": 230,  // Work shift
"duration_minutes_max": 240,  // ≈ 288-300 real seconds (4.8-5 min)
"duration_minutes": 30        // Short activity ≈ 37.5 real seconds
```

These are **good approximations** for realistic shift lengths (~8 hours = 480 min, but randomized 230-240 min gives variation).

### Proposed Modifications

The current system is reasonable, but could be made more realistic:

#### Current State (Reasonable)
| Duration | Game Min | Real Sec | Use Case |
|----------|----------|----------|----------|
| **4-5 min** | 230-240 | 287-300 | Work shifts (factory, office) |
| **2.5-3 min** | 150-180 | 187-225 | Extended errands |
| **1.5-2 min** | 90-120 | 112-150 | Short shopping/meals |
| **45 sec** | 30-40 | 37-50 | Quick activity (chat, ATM) |

#### Recommended: Align with Real-World Schedules

| Activity | Current | Proposed | Game Min | Real Sec | Reason |
|----------|---------|----------|----------|----------|--------|
| **Sleep** | ~24h real | 8h game (varies) | 480 | 600 | Full night sleep |
| **Work shift** | 230-240m | 8h game | 480 | 600 | Standard shift duration |
| **Commute time** | Auto-calculated | Keep as-is | N/A | Based on distance |
| **Lunch break** | 30m | 1h game | 60 | 75 | Dedicated lunch period |
| **Shopping** | 30m | 30-45m game | 30-45 | 37-56 | Short errand |
| **Dining** | 30m | 30m game | 30 | 37 | Quick meal |
| **Casual activity** | 30m | 15-30m game | 15-30 | 18-37 | Chat, relax |

#### Travel Time Calculations @ 1.5 m/s

```
Distance → Game Time → Real Time
100m     → 67 sec    → 83.75 real sec
200m     → 133 sec   → 166.25 real sec
500m     → 333 sec   → 416.25 real sec
1000m    → 667 sec   → 833.75 real sec (14 min)
```

**Key insight:** At current scaling, a 1km commute takes ~14 real minutes, which is quite long if activities only last 37-50 seconds. **Consider:**
- Increasing `RealSecondsPerGameDay` from 1800 to 3600 (60 min/game day) to give more time for realistic activities
- OR keeping 1800s but adjusting activity durations upward proportionally

### Table: Proposed Timing Adjustments

| Setting | Current | Recommended | Impact |
|---------|---------|-------------|--------|
| **RealSecondsPerGameDay** | 1800s | 3600s | Doubles real-time per activity; halves visual speed |
| **Work shift game time** | 230-240m | 480m | Matches 8-hour shift realism |
| **Lunch break** | 30m | 60m | Full hour for lunch |
| **Shopping/errand** | 30m | 45m | More realistic errand duration |
| **Sleep duration** | Not explicit | 480m | Full night (matches work) |
| **Short activity** | 30m | 15-30m | Keep as-is for variety |

### My Recommendation

**Option 1 (Minimal changes):**
- Keep `RealSecondsPerGameDay = 1800s`
- Update work shift durations: `480 game minutes` (8 hrs)
- Update meal durations: `60 game minutes` (1 hr)
- Keep commute times as distance-based (auto-calculated)

**Option 2 (Better balance - RECOMMENDED):**
- Increase `RealSecondsPerGameDay = 3600s` (60 min per game day)
- Keep durations in the 30-240 game minute range
- Result: More time to observe NPC activities before next transition

---

## Action Items

### Priority 1: Debug Stuck NPCs
- [ ] Start game with autologin
- [ ] Extract logs matching "unreachable" or "Staying at current location"
- [ ] Identify which NPCs are stuck and at which locations
- [ ] Check if they're on street junctions vs. inside buildings

### Priority 2: Validate Entry Points (Prevents Future Issues)
- [ ] Add post-generation check in TalePopulationGenerator or SpatialModel.ExtractFrom()
- [ ] Verify each location's entry point has adjacent NavJunction (within 5m)
- [ ] Warn if isolated locations found; optionally disable them

### Priority 3: Review Timing if Needed
- [ ] Run 60-day test with proposed timing changes
- [ ] Check if activities feel rushed or natural
- [ ] Adjust `RealSecondsPerGameDay` if activities feel too quick

---

## Files to Check

| File | Purpose |
|------|---------|
| `TaleEntityStrategy.cs` | Fallback logic (lines 283-289, 400-406) |
| `TalePopulationGenerator.cs` | Location assignment; add entry point validation |
| `SpatialModel.cs` | ExtractFrom(); add entry point checks |
| `Pipe.cs` | Default walking speed (1.5 m/s, line 170) |
| `models/tale/*.json` | Activity durations (review and adjust if needed) |

