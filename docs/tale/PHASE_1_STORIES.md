# Phase 1 — Story Generation + Content

**Prerequisites**: Phase 0A (spatial model) or Phase 0B (DES engine for testing).
**Read also**: `REFERENCE.md` for base properties, spatial verbs, roles.

---

## Goal

Define the storylet authoring format. Write the first storylet library (~15-20 entries). Implement `NpcNarrativeState` and seed-based storylet selection. Balance property effects for plausible daily cycles. Validate via testbed DES.

## What To Build

### 1. Storylet Authoring Format

Each storylet is a JSON object. The storylet library is a collection of these in one or more JSON files:

```json
{
  "id": "work_manual",
  "name": "Manual Labor",
  "roles": ["worker"],
  "preconditions": {
    "time_of_day": { "min": "07:00", "max": "17:00" },
    "fatigue": { "max": 0.85 },
    "location_type": "workplace"
  },
  "postconditions": {
    "fatigue": "+0.28",
    "wealth": "+0.08",
    "hunger": "+0.06"
  },
  "verb": "stay_at",
  "verb_params": {
    "location": "workplace",
    "duration_minutes": 270,
    "animation_hint": "working"
  },
  "weight": 1.0,
  "tags": ["routine", "economic"]
}
```

**Precondition types:**
- `time_of_day`: `{ "min": "HH:MM", "max": "HH:MM" }` — storylet only available in this window
- Property ranges: `{ "min": 0.0, "max": 1.0 }` — check NPC property value
- `location_type`: must be at this type of location
- `role`: NPC must have this role (also expressible via top-level `roles` array)
- `has_relationship`: `{ "other_role": "merchant", "min_trust": 0.5 }` — knows someone matching

**Postcondition types:**
- Property mutations: `"+0.28"` (relative) or `"=0.1"` (absolute)
- Multiple properties per storylet

**Location resolution in `verb_params`:**
- `"workplace"` → NPC's assigned workplace
- `"home"` → NPC's assigned home
- `"nearest_shop_Eat"` → closest shop of type Eat from current location
- `"social_venue"` → one of NPC's assigned social venues
- `"current"` → stay where you are

### 2. Initial Storylet Library

Write ~15-20 storylets covering all four roles:

**Universal (all roles):**

| Id | Verb | Duration | Key Postconditions | Time Window |
|----|------|----------|-------------------|-------------|
| `wake_up` | stay_at(home) | 30-45min | fatigue -0.05, hunger +0.08 | 05:00-08:00 |
| `sleep` | stay_at(home) | 8-10h | fatigue =0.1 | 20:00-06:00 |
| `eat_at_home` | stay_at(home) | 20min | hunger -0.45, wealth -0.02 | any, hunger>0.6 |
| `eat_out` | go_to+stay_at(nearest_shop_Eat) | 30min | hunger -0.55, wealth -0.03 | any, hunger>0.6 |
| `commute` | use_transport(origin, dest) | variable | fatigue +0.02 | any |
| `wander` | go_to(random_street) | 30-60min | fatigue +0.05 | 08:00-22:00 |
| `rest` | stay_at(current) | 30min | fatigue -0.1 | any, fatigue>0.6 |

**Worker:**

| Id | Verb | Duration | Key Postconditions |
|----|------|----------|-------------------|
| `work_manual` | stay_at(workplace) | 4-5h | fatigue +0.28, wealth +0.08 |
| `work_office` | stay_at(workplace) | 4-5h | fatigue +0.15, wealth +0.10 |
| `lunch_break` | go_to+stay_at(nearest_shop_Eat) | 25min | hunger -0.55, wealth -0.03 |

**Merchant:**

| Id | Verb | Duration | Key Postconditions |
|----|------|----------|-------------------|
| `open_shop` | stay_at(workplace) | 30min | fatigue +0.05 |
| `serve_customers` | stay_at(workplace) | 3-4h | fatigue +0.2, wealth +0.12 |
| `close_shop` | stay_at(workplace) | 20min | fatigue +0.03 |
| `restock` | go_to+stay_at(workplace) | 1h | fatigue +0.1, wealth -0.05 |

**Socialite:**

| Id | Verb | Duration | Key Postconditions |
|----|------|----------|-------------------|
| `visit_bar` | go_to+stay_at(social_venue) | 2h | happiness +0.15, wealth -0.05, fatigue +0.1 |
| `visit_friend` | go_to(known_npc.location) | 1h | happiness +0.1, trust +0.05 |
| `loiter` | stay_at(current) | 1h | happiness +0.02 |

**Drifter:**

| Id | Verb | Duration | Key Postconditions |
|----|------|----------|-------------------|
| `scavenge` | go_to(random_location) | 1-2h | wealth +0.03, fatigue +0.15 |
| `beg` | stay_at(street_segment) | 1h | wealth +0.01-0.04, reputation -0.02 |
| `sleep_rough` | stay_at(current) | 8h | fatigue =0.2, health -0.02 |

### 3. NpcNarrativeState Component

An ECS component tracking the NPC's position in the story graph:

```csharp
[Persistable]
public struct NpcNarrativeState
{
    public int Seed;
    public string Role;
    public string CurrentStoryletId;
    public DateTime StoryletStartTime;
    public DateTime StoryletEndTime;
    public int GraphDepth;        // Current subdivision level
    public int GraphPosition;     // Node index at current level
    // For Phase 5: arc stack for nested interrupts
}
```

### 4. Storylet Selector

Given an NPC's current state (properties, time-of-day, location, role), select the next storylet:

1. Filter storylet library by role match
2. Filter by preconditions (time window, property ranges, location type)
3. From remaining candidates, select by weight (weighted random, seed-deterministic)
4. If no candidate matches, fall back to `wander` or `rest`

### 5. Property Effect Balancing

Target daily cycles:
- **hunger**: 0.1 at breakfast → ~0.6 by lunch → 0.1 after lunch → ~0.6 by dinner → 0.1 after dinner. Rate: ~+0.06/hour awake.
- **fatigue**: 0.1 after sleep → ~0.8 by evening. Rate: varies by activity (+0.28 for work, +0.05 for walking).
- **wealth**: Net slightly positive for workers/merchants per day. Net near-zero for socialites. Net slightly negative for drifters.
- **Emotional properties**: Stable during routine. Shift during interactions (Phase 3). Should not drift to extremes from routine alone.

### 6. Seed Determinism

Same seed must produce same storylet sequence. The storylet selector uses a seeded PRNG — `builtin.tools.RandomSource(npcSeed + graphDepth + graphPosition)` — for weighted selection. No `DateTime.Now` or thread-dependent randomness.

## Testing via Testbed

Run `dotnet run --project Testbed -- --days 7 --traces 5`:

1. Read traces for each role. Do they look like plausible daily routines?
2. Check property trajectories in `day_summary` events: does hunger cycle? Does fatigue peak by evening?
3. Verify seed determinism: same seed → same trace (run twice, diff output)
4. Check role differentiation: workers and drifters should produce obviously different traces

### Content Iteration Checklist

After each testbed run, check for:
- [ ] Any role with idle gaps > 2 hours? → add storylets to fill
- [ ] Properties drifting to extremes (stuck at 0 or 1)? → adjust mutation rates
- [ ] Same storylet repeating excessively? → adjust weights, add alternatives
- [ ] Hunger not triggering eat storylets? → lower eat precondition threshold
- [ ] Fatigue not triggering sleep? → adjust sleep precondition
- [ ] All roles completing full day cycles? → check schedule templates

## Deliverable

1. Storylet JSON format specification
2. ~15-20 storylets in JSON files (in `models/` directory)
3. `NpcNarrativeState` ECS component
4. Storylet selector with seed-deterministic weighted selection
5. Testbed traces showing differentiated daily routines for all 4 roles
6. Property effects balanced for plausible daily cycles
