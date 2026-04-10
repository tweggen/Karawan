# Phase 2 — Story-to-Strategy Translation

**Prerequisites**: Phase 1 (storylet library exists, NpcNarrativeState works).
**Read also**: `REFERENCE.md` for spatial verb alphabet.

---

## Goal

Implement strategy executors that translate spatial verbs into visible NPC behavior in the game world. NPCs walk between locations, stay for appropriate durations, and follow believable daily routines. Adjust story content based on how it looks spatially.

## What To Build

### 1. Strategy Executors

One executor per spatial verb. Each takes verb parameters and drives the NPC's physical behavior until completion, then signals back to the story graph:

| Verb | Executor Behavior |
|------|-------------------|
| `go_to` | Pathfind to destination using street graph (`StreetPoint`/`Stroke` network). Walk animation. Complete when NPC reaches destination. |
| `stay_at` | Hold position at location. Play `animation_hint` loop (idle, working, eating, sleeping). Complete after `duration` elapses in game time. |
| `follow` | Maintain `distance` to `target_entity`. Re-pathfind each frame. Complete when target stops or timeout. |
| `interact_with` | Approach `target_entity`. When within range, play interaction animation (`argue`, `trade`, `greet`). Complete after interaction duration. |
| `use_transport` | If tube: enter tube system at origin, wait deterministic delay, exit at destination. If walk: same as `go_to`. If taxi: emit taxi request (Phase 3). |
| `wait_for` | Idle animation at current position. Listen for `signal_type` event. Complete on signal or `timeout`. |

### 2. Story-to-Strategy Bridge

On each story node arrival in `NpcNarrativeState`:
1. Read the selected storylet's verb + verb_params
2. Resolve location references (`"workplace"` → actual world position from NPC assignment)
3. If verb sequence (e.g., `go_to` then `stay_at`): queue verbs, execute sequentially
4. Create the appropriate strategy executor
5. On executor completion: signal story graph → advance to next node

### 3. Verb Sequencing

A single storylet may produce multiple verbs:
- `eat_out`: `go_to(nearest_shop_Eat)` → `stay_at(shop, 30min, "eating")`
- `work_manual`: `go_to(workplace)` → `stay_at(workplace, 4h30, "working")`
- `commute`: `use_transport(origin, destination, walk)`

The strategy executor must handle the sequence: complete verb 1, then start verb 2. Only signal story graph completion after the last verb finishes.

### 4. Existing Strategy Integration

The engine already has strategy infrastructure:
- `EntityStrategy` (`nogameCode/nogame/characters/citizen/EntityStrategy.cs`) — NPC behavior state machine
- `SegmentNavigator` (`JoyceCode/builtin/tools/SegmentNavigator.cs`) — movement along streets
- `QuarterLoopRouteGenerator` — patrol route creation from quarter perimeters

The new verb executors should integrate with or replace parts of this system. The story graph becomes the top-level controller; the existing walking/navigation code handles the physical movement.

### 5. Time-of-Day Awareness

The storylet selector (from Phase 1) already uses `time_of_day` preconditions. In Phase 2, verify that the game-time clock (`daynite.Controller.GameNow`) correctly drives selection: NPCs should wake in the morning, work during the day, socialize in the evening, sleep at night.

## Content Adjustment (after seeing NPCs move)

Once NPCs physically move through the generated world, new content issues become visible:

### Travel Time Realism
- If commute takes 10 real minutes but storylet allocates 30 → NPC stands idle for 20 minutes
- Solution: `commute` storylet duration should be computed from actual route distance, or `go_to` verb consumes exactly as long as pathfinding requires (implicit duration)

### Location Variety
- If all merchants go to the same shop → robotic appearance
- Solution: location resolution picks from pool per NPC seed, not a single global location

### Pacing
- Transitions too abrupt? → Add transitional storylets: `arrive_at_work` (brief settling-in), `prepare_to_leave`
- NPCs standing around? → Check for schedule gaps, add fill storylets (`wander`, `window_shop`)

### Animation Hints
- Review `animation_hint` values. Split generic hints into specific variants:
  - `"working"` → `"working_standing"`, `"working_seated"`, `"working_heavy"` (per workplace type)
  - `"eating"` → `"eating_seated"`, `"eating_standing"`

## Testing

### In-Game Visual Validation
- Follow an NPC for a full game day. Do they complete a plausible routine?
- Do NPCs arrive at the right locations (their assigned home/workplace/shops)?
- Are transitions smooth? No teleporting, no stuck-at-wrong-location?

### Testbed Cross-Validation (Phase D)
- Run continuous headless mode with NPCs physically moving
- Measure actual per-frame co-location rates
- Compare against DES probabilistic predictions from Phase 0B
- If they diverge: calibrate DES encounter probability table

## Deliverable

1. Strategy executors for all 6 spatial verbs
2. Story-to-strategy bridge: storylet → verb → executor → completion signal → next storylet
3. Verb sequencing for multi-verb storylets
4. NPCs with visible, believable daily routines driven by their story graph
5. Content adjustments for spatial realism documented and applied
