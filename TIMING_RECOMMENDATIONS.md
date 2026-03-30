# NPC Timing Recommendations – Detailed Table

## Current State Analysis

### Game Time Acceleration
- **RealSecondsPerGameDay:** 1800 seconds (30 minutes real-time per game day)
- **Pedestrian walking speed:** 1.5 m/s (matches real-life)
- **Time formula:** `realSeconds = gameMins * (1800 / 1440)` = `gameMins × 1.25`
- **Effective ratio:** 1 real second ≈ 48 game seconds (or 0.8 game minutes)

### Current Activity Durations
From `models/tale/*.json`:
- Factory/office worker shift: 230–240 game minutes
- General activity: 30 game minutes
- Other: varies 3–40 game minutes

---

## Commute Time Matrix (at 1.5 m/s)

| Distance | Game Seconds | Game Minutes | Real Seconds |
|----------|--------------|--------------|--------------|
| 50m      | 33           | 0.55         | 41.25        |
| 100m     | 67           | 1.12         | 83.75        |
| 200m     | 133          | 2.22         | 166.25       |
| 300m     | 200          | 3.33         | 250          |
| 500m     | 333          | 5.56         | 416.25       |
| 750m     | 500          | 8.33         | 625          |
| 1000m    | 667          | 11.11        | 833.75       |

---

## Proposed Activity Duration Table

### Option A: Keep RealSecondsPerGameDay = 1800s (Current)

Adjust game-minute durations to match real-world activity lengths:

| Activity Type | Real-life Duration | Game Minutes (Proposed) | Real Seconds | Comment |
|---------------|--------------------|------------------------|--------------|---------|
| **Sleep** | 8 hours | 480 | 600 | Full night rest |
| **Work shift** | 8 hours | 480 | 600 | Standard job shift |
| **School/Training** | 8 hours | 480 | 600 | Full day activity |
| **Lunch break** | 1 hour | 60 | 75 | Mid-day meal |
| **Dinner at home** | 1 hour | 60 | 75 | Evening meal |
| **Casual activity** | 30 min | 30 | 37.5 | Chat, relax, ATM |
| **Shopping/errand** | 45 min | 45 | 56.25 | Quick shop visit |
| **Extended errand** | 1.5 hours | 90 | 112.5 | Extended shopping |
| **Workout/exercise** | 1 hour | 60 | 75 | Gym, running |
| **Social visit** | 1 hour | 60 | 75 | Visit friend/bar |

**Real-world to game min formula:** `gameMin = realMin`
(e.g., 30 real-life minutes → 30 game minutes)

**Real seconds result:** `realSec = gameMin × 1.25`

---

### Option B: Increase RealSecondsPerGameDay = 3600s (RECOMMENDED)

Double real-time per game day for better observation:

| Activity Type | Real-life Duration | Game Minutes | Real Seconds | Comment |
|---------------|--------------------|--------------|--------------|---------|
| **Sleep** | 8 hours | 480 | 240 | Full night rest |
| **Work shift** | 8 hours | 480 | 240 | Standard job shift |
| **School/Training** | 8 hours | 480 | 240 | Full day activity |
| **Lunch break** | 1 hour | 60 | 30 | Mid-day meal |
| **Dinner at home** | 1 hour | 60 | 30 | Evening meal |
| **Casual activity** | 30 min | 30 | 15 | Chat, relax, ATM |
| **Shopping/errand** | 45 min | 45 | 22.5 | Quick shop visit |
| **Extended errand** | 1.5 hours | 90 | 45 | Extended shopping |
| **Workout/exercise** | 1 hour | 60 | 30 | Gym, running |
| **Social visit** | 1 hour | 60 | 30 | Visit friend/bar |

**Real seconds formula:** `realSec = gameMin × 0.625` (with 3600s/day)

**Advantage:** Activities feel less rushed; more time to observe NPC behavior.
**Disadvantage:** Game day takes 60 real minutes instead of 30.

---

## Current vs. Proposed Comparison

### Factory Worker Example

**Current state** (`factory_worker.json`):
```
Work shift: 230–240 game min → 287–300 real seconds (4.75–5 min)
Activity:   30 game min       → 37.5 real seconds
```

**With Option A (align game min to real min):**
```
Work shift: 480 game min      → 600 real seconds (10 min)
Activity:   30 game min       → 37.5 real seconds (no change)
```

**With Option B (RealSecondsPerGameDay = 3600):**
```
Work shift: 480 game min      → 300 real seconds (5 min)
Activity:   30 game min       → 18.75 real seconds
```

---

## Commute vs. Activity Duration Mismatch

### Current Issue

At 1 real second = 48 game seconds:
- A 100m commute ≈ 67 game sec ≈ 84 real sec (1.4 min)
- A typical activity ≈ 30 game min ≈ 37.5 real sec (0.6 min)
- **Ratio:** Commute takes 2.2× longer than the activity itself

**Problem:** NPCs appear to spend more time walking than doing their activity.

### With Option A (Game min = Real min)

- A 100m commute ≈ 67 game sec ≈ 84 real sec (1.4 min)
- Work shift ≈ 480 game min ≈ 600 real sec (10 min)
- **Ratio:** Shift duration >> commute time ✓

### With Option B (RealSecondsPerGameDay = 3600)

- A 100m commute ≈ 67 game sec ≈ 42 real sec (0.7 min)
- Work shift ≈ 480 game min ≈ 300 real sec (5 min)
- **Ratio:** Shift duration >> commute time ✓
- **Bonus:** Overall day progresses at 60 real min/game day (easier to observe long sequences)

---

## Implementation Guide

### To Implement Option A

1. Update `models/tale/*.json` files:
   - Work shift: Change `duration_minutes_min/max` from 230–240 to 480
   - Meals: Change 30 to 60
   - Casual activity: Keep 30

2. Update `models/nogame.json` if any global defaults exist

3. No code changes required

### To Implement Option B

1. Update `TaleEntityStrategy.cs` line 45:
   ```csharp
   public float RealSecondsPerGameDay { get; set; } = 60f * 60f;  // 3600s instead of 1800s
   ```

2. Update `models/tale/*.json` activity durations (same as Option A)

3. Update test expectations in `TestRunner/TestRunnerMain.cs`:
   - Adjust expected simulation times (will be 2× longer per game day)

4. Update documentation in `docs/tale/PHASE_N.md`:
   - Note new time scaling factor

---

## Recommendation Summary

| Aspect | Option A | Option B |
|--------|----------|----------|
| **Code change** | None | 1 line |
| **Config change** | Minimal | Minimal |
| **Real time per game day** | 30 min | 60 min |
| **Activity duration feel** | Good | Good |
| **Observation ease** | Fast | Comfortable |
| **Test runtime** | 5 min (171 tests) | 10 min (171 tests) |
| **Recommendation** | Quick fix | Better UX |

### **My Pick: Option B**
The extra 30 minutes per game day is worth it for:
- More natural activity observation
- Better feel for game time progression
- Easier manual testing with autologin
- Matches real-life walking speed realism

---

## Validation Checklist

After implementation, verify:
- [ ] 100m commute takes ~40–50 real seconds
- [ ] Work shift takes ~5–10 real minutes
- [ ] A 60-day test completes in ~60 minutes (Option B) or ~30 minutes (Option A)
- [ ] Activities don't feel rushed when manually observing
- [ ] Travel times feel proportional to activity durations
- [ ] No NPCs stuck on street points due to unreachable destinations (separate fix)

