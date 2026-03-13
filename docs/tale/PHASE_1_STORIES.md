# Phase 1 — Story Generation + Content

**Prerequisites**: Phase 0A (spatial model) or Phase 0B (DES engine for testing).
**Read also**: `REFERENCE.md` for base properties, spatial verbs, roles.

---

## Goal

Define the storylet authoring format. Write the first storylet library (~30 entries). Implement `NpcNarrativeState` and seed-based storylet selection. Balance property effects for plausible daily cycles. **Seed the mechanisms for emergent criminality, law enforcement, and group formation** — these structures emerge from property pressure, interaction types, and trust-graph dynamics rather than being scripted. Validate via testbed DES.

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
- `desperation`: shorthand for compound check (see Section 7)

**Postcondition types:**
- Property mutations: `"+0.28"` (relative) or `"=0.1"` (absolute)
- Multiple properties per storylet

**Location resolution in `verb_params`:**
- `"workplace"` → NPC's assigned workplace
- `"home"` → NPC's assigned home
- `"nearest_shop_Eat"` → closest shop of type Eat from current location
- `"social_venue"` → one of NPC's assigned social venues
- `"current"` → stay where you are

### 2. New Property: Morality

Add `morality` to the base property set (range 0.0–1.0, default ~0.6–0.8 from seed). Morality governs which interaction types an NPC considers during encounters:

- **High morality (>0.5)**: NPC sticks to legitimate interactions (greet, chat, trade, help)
- **Low morality (<0.3)**: NPC considers criminal interactions (rob, blackmail, intimidate)
- **Morality drift**: sustained desperation erodes morality; positive social contact restores it

```
Per day:
  if (hunger > 0.7 && wealth < 0.2): morality -= 0.005
  if (happiness > 0.5 && trust_with_any_friend > 0.5): morality += 0.003
  morality = clamp(morality, 0.0, 1.0)
```

### 3. Expanded Interaction Types

The existing `RelationshipTracker.DetermineInteractionType` currently supports: greet, chat, trade, help, argue. Expand to include:

| Type | Trust Required | Property Gate | Trust Delta | Other Effects |
|------|---------------|---------------|-------------|---------------|
| `greet` | any | — | +0.015 | — |
| `chat` | >0.2 | — | +0.03 | happiness +0.02 both |
| `trade` | >0.5 | — | +0.025 | wealth ±small |
| `help` | >0.5 | — | +0.05 | — |
| `argue` | <0.3 | anger>0.5 either | -0.04 | anger +0.1 both |
| `rob` | <0.2 | attacker: desperate + morality<0.3 | -0.15 | wealth transfer, victim fear+0.3 |
| `blackmail` | 0.2–0.5 | attacker: morality<0.3, has witnessed_crime | -0.2 | recurring wealth drain |
| `intimidate` | <0.3 | attacker: anger>0.5 + morality<0.4 | -0.1 | victim fear+0.2, victim reputation info |
| `recruit` | >0.6 | both desperate + morality<0.4 | +0.08 | marks both as affiliated |
| `report_crime` | >0.4 | reporter: witnessed crime, other: authority role | +0.02 | creates crime_report signal |
| `patrol_check` | <0.2 | checker: authority role | 0 | may lead to investigate |
| `arrest` | <0.1 | authority + evidence | -0.3 | target schedule → detained |

**Selection logic in DetermineInteractionType:**

```
1. If attacker is desperate AND morality < 0.3 AND target wealth > 0.5 AND trust < 0.2:
   → "rob" with probability based on desperation severity
2. If authority role AND target reputation < 0.2:
   → "patrol_check"
3. Else: existing trust-based selection (greet/chat/trade/help/argue)
```

Criminal interactions are rare — they only fire when property pressure has built up over days/weeks of simulated time.

### 4. The Authority Role

Add a fifth base role. Authorities (~5% of NPCs) have:

**Schedule template:**
```
wake → patrol(assigned_area) → patrol → eat → patrol → report(authority_venue) → sleep
```

**Key storylets:**

| Id | Verb | Duration | Preconditions | Postconditions |
|----|------|----------|---------------|----------------|
| `patrol` | go_to(street_segment in area) | 1-2h | authority role | fatigue +0.08 |
| `investigate` | go_to(crime_location) | 30min | received crime_report | — |
| `arrest` | interact_with(suspect) | 15min | evidence + co-located with suspect | suspect → detained storylet |
| `file_report` | stay_at(authority_venue) | 30min | has evidence/reports | fatigue +0.05 |

Authorities respond to crime through the normal encounter system — they don't need special AI. A patrol puts them on the street; if they encounter a low-reputation NPC, `patrol_check` fires. Crime reports (emitted by victims/witnesses via the interaction pool in Phase 3) guide their patrol routes.

### 5. Initial Storylet Library

Write ~30 storylets covering all five roles:

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

**Authority:**

| Id | Verb | Duration | Key Postconditions |
|----|------|----------|-------------------|
| `patrol` | go_to(street_segment) | 1-2h | fatigue +0.08 |
| `patrol_venue` | go_to+stay_at(social_venue) | 30min | fatigue +0.05 |
| `investigate` | go_to(reported_location) | 30min | fatigue +0.05 |
| `file_report` | stay_at(authority_venue) | 30min | fatigue +0.05 |
| `detained` | stay_at(authority_venue) | 12-24h | (applied to arrested NPC, not authority) fatigue +0.3, anger +0.2, reputation -0.1 |

**Desperation-gated (any role when desperate):**

| Id | Verb | Duration | Preconditions | Key Postconditions |
|----|------|----------|---------------|-------------------|
| `steal_food` | stay_at(nearest_shop_Eat) | 5min | hunger>0.8, wealth<0.1, morality<0.4 | hunger -0.4, morality -0.02, reputation -0.05 |
| `rob_npc` | (encounter interaction) | — | desperate, morality<0.3, target wealthy | wealth transfer, reputation -0.1, victim fear +0.3 |
| `sleep_rough` | stay_at(current) | 8h | no home access or wealth<0.05 | fatigue =0.2, health -0.02 |

### 6. NpcNarrativeState Component

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
    public int GroupId;           // -1 = no group, else group identifier
    // For Phase 5: arc stack for nested interrupts
}
```

### 7. Desperation — The Engine of Emergent Crime

Desperation is not a stored property — it's a **computed score** from existing properties:

```
desperation = clamp(hunger * 0.4 + (1 - wealth) * 0.3 + anger * 0.2 + (1 - health) * 0.1, 0, 1)
```

When `desperation > 0.6` AND `morality < 0.4`, the NPC enters a state where criminal storylets become available. This creates the core feedback loop:

```
poverty → hunger + low wealth → desperation rises
→ morality erodes over days of desperation
→ criminal storylets become available (steal_food, rob)
→ crime committed → reputation drops → witnesses may report
→ low reputation → fewer legitimate interactions (NPCs avoid low-rep strangers)
→ exclusion from legitimate work storylets (precondition: reputation > 0.3)
→ more poverty → more desperation → more crime
```

The **opposite loop** also operates:
```
stable job → wealth + fed + low anger → low desperation
→ morality stays high → only legitimate interactions
→ positive social contact → trust builds → friend group forms
→ friends help when in need → resilience against desperation spirals
```

Both loops are emergent from property dynamics — no scripting required.

### 8. Group Detection — Emergent Factions

Groups are **detected**, not declared. Add a `GroupDetector` that runs periodically (e.g., every 7 sim-days) and scans the trust graph:

**Detection algorithm:**
1. Find all maximal cliques of size >= 3 where mutual trust > 0.6
2. For each clique, compute the average property profile
3. Classify the group by shared properties:
   - Low wealth + low morality + high anger → **criminal group** (gang)
   - High wealth + high reputation → **elite group** (trade network)
   - Authority role members with high mutual trust → **patrol unit**
   - Mixed roles with shared venue → **social circle**
4. Assign `GroupId` to members in `NpcNarrativeState`

**Group membership effects:**
- Members prefer to help each other (trust bonus on interactions)
- Criminal groups: unlock `intimidate_together` (Phase 5), share territory
- Groups with 3+ members and shared anger toward a target: precondition for `community_meeting` (Phase 5)

For Phase 1, group detection is **observational only** — the testbed reports groups in metrics but they don't yet unlock new storylets. Phase 3 and 5 build on this.

**Testbed metric additions:**
```json
{
  "groups": {
    "total_groups": 12,
    "groups_by_type": { "criminal": 2, "social": 7, "trade": 3 },
    "largest_group_size": 5,
    "npcs_in_groups": 42,
    "first_group_day": 18,
    "first_criminal_group_day": 45
  }
}
```

### 9. Storylet Selector

Given an NPC's current state (properties, time-of-day, location, role), select the next storylet:

1. Filter storylet library by role match
2. Filter by preconditions (time window, property ranges, location type)
3. If desperate and morality is low, include desperation-gated storylets
4. From remaining candidates, select by weight (weighted random, seed-deterministic)
5. If no candidate matches, fall back to `wander` or `rest`

### 10. Property Effect Balancing

Target daily cycles:
- **hunger**: 0.1 at breakfast → ~0.6 by lunch → 0.1 after lunch → ~0.6 by dinner → 0.1 after dinner. Rate: ~+0.04/hour awake (reduced from Phase 0's +0.06 to prevent saturation).
- **fatigue**: 0.1 after sleep → ~0.8 by evening. Rate: varies by activity (+0.28 for work, +0.05 for walking).
- **wealth**: Net slightly positive for workers/merchants per day. Net near-zero for socialites. Net slightly negative for drifters (creates desperation pressure over weeks).
- **morality**: Stable for employed NPCs. Slow erosion for NPCs stuck in poverty. Should take 2-4 weeks of desperation before criminal threshold is reached.
- **Emotional properties**: Stable during routine. Shift during interactions (Phase 3). Should not drift to extremes from routine alone.

**Known balance issues from Phase 0:**
- Hunger mean was 0.947 after 7 days — the +0.06/hr rate needs to drop to ~+0.04/hr, and NPCs need to eat twice per day (lunch + dinner) not just once
- All interactions were "greet" in 7 days — trust builds too slowly for acquaintance tier. Consider either raising greet trust delta to +0.02 or lowering the acquaintance threshold to 0.15

### 11. Seed Determinism

Same seed must produce same storylet sequence. The storylet selector uses a seeded PRNG — `builtin.tools.RandomSource(npcSeed + graphDepth + graphPosition)` — for weighted selection. No `DateTime.Now` or thread-dependent randomness.

## Testing via Testbed

Run `dotnet run --project Testbed -- --days 30 --traces 5`:

1. Read traces for each role. Do they look like plausible daily routines?
2. Check property trajectories in `day_summary` events: does hunger cycle? Does fatigue peak by evening?
3. Verify seed determinism: same seed → same trace (run twice, diff output)
4. Check role differentiation: workers and drifters should produce obviously different traces
5. **Check desperation emergence**: after 14+ days, do some drifters start showing criminal interactions?
6. **Check authority activity**: do authority NPCs patrol and encounter low-reputation NPCs?
7. **Check group formation**: after 21+ days, do trust cliques appear in the graph output?

Run `dotnet run -c Release --project Testbed -- --days 180 --quiet`:

8. Do criminal groups form by day 45-90?
9. Does the degree distribution Gini increase over time (power concentration)?
10. Is there a natural ratio of ~80% routine NPCs to ~20% involved in non-routine activity?

### Content Iteration Checklist

After each testbed run, check for:
- [ ] Any role with idle gaps > 2 hours? → add storylets to fill
- [ ] Properties drifting to extremes (stuck at 0 or 1)? → adjust mutation rates
- [ ] Same storylet repeating excessively? → adjust weights, add alternatives
- [ ] Hunger not triggering eat storylets? → lower eat precondition threshold
- [ ] Fatigue not triggering sleep? → adjust sleep precondition
- [ ] All roles completing full day cycles? → check schedule templates
- [ ] Morality collapsing too fast? → reduce desperation erosion rate
- [ ] Morality never dropping? → increase desperation erosion or lower initial morality for drifters
- [ ] No criminal interactions after 30 days? → lower morality/desperation thresholds
- [ ] Everyone becoming criminal? → raise thresholds, increase morality recovery from social contact
- [ ] Authority NPCs never encountering criminals? → check patrol routes overlap with crime locations
- [ ] No groups forming? → lower trust threshold for group detection, increase encounter rates

## Deliverable

1. Storylet JSON format specification
2. ~30 storylets in JSON files (in `models/` directory) covering 5 roles + desperation-gated
3. `NpcNarrativeState` ECS component with GroupId
4. Storylet selector with seed-deterministic weighted selection and desperation awareness
5. Expanded interaction types in RelationshipTracker (rob, intimidate, recruit, patrol_check, arrest)
6. `morality` property with desperation-driven drift
7. `GroupDetector` — periodic trust-graph clique detection (observational for Phase 1)
8. Testbed traces showing differentiated daily routines for all 5 roles
9. Property effects balanced for plausible daily cycles (hunger, morality especially)
10. 30-day and 180-day testbed runs showing early signs of emergent structure
