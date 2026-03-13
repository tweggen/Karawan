# Phase 5 — Branching, Interrupts & Escalation

**Prerequisites**: Phase 3 (interaction pool, encounter system).
**Read also**: `REFERENCE.md` for interaction primitives, relationship tiers.

---

## Goal

Implement interrupt mechanics (nest/parallel/replace/cancel). Write escalation storylets with high precondition thresholds that transform daily routine into emergent power structures. Tune via testbed so that structures emerge within 3-6 simulated months.

## What To Build

### 1. Edge Interrupt System

When an external event targets an NPC mid-storylet, the interrupt system determines how to branch:

| Scope | Behavior | Example |
|-------|----------|---------|
| **Nest** | Pause current storylet, run interrupt, resume on completion | Merchant pauses serving to chat with friend |
| **Parallel** | Run interrupt alongside current storylet (both active) | NPC monitors a situation while working |
| **Replace** | Terminate current storylet, start interrupt | NPC abandons work to deal with emergency |
| **Cancel** | Terminate current storylet, no replacement — next node selects fresh | NPC's plan is invalidated by external event |

```csharp
public class ArcStack
{
    // Stack of paused storylets (for nested interrupts)
    public Stack<PausedStorylet> PausedArcs;

    public void PushInterrupt(string newStorylet, InterruptScope scope);
    public string PopAndResume();  // returns resumed storylet id
    public void CancelCurrent();
}

public struct PausedStorylet
{
    public string StoryletId;
    public DateTime PausedAt;
    public float RemainingDuration;
    public Dictionary<string, float> PropertiesAtPause;
}
```

### 2. Interrupt Priority

When multiple events target the same NPC simultaneously:

```csharp
public class InterruptCandidate
{
    public string StoryletId;
    public InterruptScope Scope;
    public int Priority;           // Higher = wins
    public Func<NpcSchedule, bool> Condition;
}
```

Priority ordering: `escalation (10) > conflict (7) > help_request (5) > social (3) > routine (1)`.

Only the highest-priority interrupt fires. Lower-priority candidates are discarded or deferred.

### 3. Property-Driven Branching

Same story seed, different property state → different outcome. The storylet selector already checks property preconditions (Phase 1). For branching, add **conditional postconditions**:

```json
{
  "id": "demand_protection",
  "postconditions_if": [
    {
      "condition": { "target_fear": { "min": 0.6 } },
      "then": { "wealth": "+0.1", "target_wealth": "-0.1", "tag": "protection_established" },
      "storylet_next": "collect_protection"
    },
    {
      "condition": { "target_fear": { "max": 0.3 } },
      "then": { "anger": "+0.3", "tag": "protection_refused" },
      "storylet_next": "threaten_harder"
    }
  ]
}
```

### 4. Escalation Storylet Library (~15-20 entries)

Escalation storylets have **high precondition thresholds** — they only fire after sustained drift:

**Economic escalation:**

| Id | Preconditions | Verb | Postconditions |
|----|---------------|------|---------------|
| `demand_protection` | wealth>0.7 + reputation>0.6 + knows merchant(trust<0.3) | go_to(merchant) → interact_with(threaten) | Branching: merchant complies (wealth transfer) or refuses (conflict) |
| `form_trade_partnership` | mutual trust>0.7 + repeated trades (interaction count>15) | interact_with(partner, negotiate) | Unlock shared storylets: `joint_restock`, `price_coordination` |
| `hire_worker` | merchant + wealth>0.8 + unmet restock requests piling up | emit request(employment, target=high-trust worker) | Worker schedule changes to include merchant's shop |

**Authority escalation:**

| Id | Preconditions | Verb | Postconditions |
|----|---------------|------|---------------|
| `investigate_complaint` | authority role + received report_to_authority request | go_to(reported_location) → interact_with(suspect, question) | suspect fear +0.2; if evidence found → `prepare_arrest` |
| `establish_patrol` | authority + 3+ complaints from same area | replace schedule | Authority's routine now includes regular area patrols |
| `arrest` | authority + evidence threshold met | interact_with(target, arrest) | Target storylet replaced with `detained`(long stay_at, reputation loss) |

**Social escalation:**

Note: Phase 1's `GroupDetector` already identifies emergent groups from the trust graph. Phase 5 upgrades detected groups into active factions with unlocked storylets.

| Id | Preconditions | Verb | Postconditions |
|----|---------------|------|---------------|
| `form_gang` | GroupDetector found criminal clique (3+ NPCs, mutual trust>0.7, avg morality<0.4, avg wealth<0.3) | interact_with(group, recruit) | Upgrade group: unlock `intimidate_together`, `territory_claim` |
| `become_informant` | knows authority (trust>0.5) + knows gang member (trust>0.3) | interact_with(authority, inform) | Dual loyalty: contradictory obligations |
| `community_meeting` | 5+ NPCs in quarter + shared high anger about common target | stay_at(venue) → interact_with(group, discuss) | Collective storylet: `protest`, `petition_authority`, `vigilante_action` |
| `flee_cluster` | fear>0.8 + anger<0.3 | use_transport(current, other_cluster) | NPC leaves cluster entirely. Appears in another cluster's Tier 3 pool. |
| `seek_revenge` | anger>0.8 + trust with target<0.1 + past betrayal | go_to(target) → interact_with(target, confront) | Branching based on relative strength/allies |

### 5. Escalation Chain Example

A concrete sequence showing how routine → structure:

```
Day 1-14:  NPC_203 (drifter) works odd jobs, accumulates low wealth, low reputation.
Day 15:    NPC_203 meets NPC_205 (drifter) at bar, repeated chat → trust rises to 0.6.
Day 20-30: Both meet NPC_210 (drifter) at same bar. Three-way trust > 0.7.
Day 35:    form_gang triggers (3 low-rep, low-wealth, high-trust NPCs).
           Gang storylets unlocked: intimidate_together, territory_claim.
Day 40:    territory_claim fires near merchant NPC_112's shop.
Day 42:    demand_protection: NPC_203 → NPC_112. Merchant's fear is only 0.4 → refuses.
Day 43:    threaten_harder: NPC_203 returns with NPC_205. Merchant fear rises to 0.7 → complies.
Day 44:    collect_protection: recurring wealth transfer established.
Day 50:    NPC_112 (merchant) hits anger threshold, triggers report_to_authority.
Day 52:    NPC_300 (authority) receives complaint, triggers investigate_complaint.
Day 55:    establish_patrol: NPC_300's schedule now includes market area patrols.
Day 60:    NPC_300 encounters NPC_203 on patrol → arrest attempt.
           Outcome branches on NPC_203's allies present: fight or flee.
```

This entire chain is emergent — no scripted quest, just storylet preconditions triggering in sequence.

## Probability Tuning for Emergence

### Target Timeline

| Period | What Should Happen |
|--------|-------------------|
| Week 1-2 | Pure routine. Acquaintances form. |
| Week 3-4 | First economic interactions. Trade patterns. |
| Month 2-3 | First conflicts. Authority investigates. |
| Month 3-6 | Power structures crystallize. Protection rackets, gangs, patrol routes. |
| Month 6+ | Dynamic equilibrium. Structures exist but shift. |

### Testbed Metrics

Run DES for 365 days. Check:
- [ ] `escalation.first_conflict_day` < 30? If not: lower conflict thresholds
- [ ] `escalation.first_gang_formation_day` between 30-120? If null: lower `form_gang` thresholds
- [ ] `escalation_fraction` at day 180 between 3-15%? Too high → raise thresholds. Too low → lower them.
- [ ] Do power structures form? (degree distribution gini > 0.25)
- [ ] Are there "boring" NPCs who never escalate? (desired — most people just go to work)
- [ ] Does every cluster produce identical structures? → add seed-based faction tendencies
- [ ] Do escalation storylets emit requests/signals the player can intercept? (Phase 4 hook)

### Content Iteration Checklist

- [ ] Gangs not forming? → lower trust/reputation thresholds, increase anger accumulation
- [ ] Authorities not responding? → increase complaint rate, lower investigation threshold
- [ ] Every cluster identical? → add faction-specific storylet variants, seed-based tendencies
- [ ] Escalation too deterministic? → add branching: `demand_protection` sometimes succeeds, sometimes fails
- [ ] Player has nothing to do? → ensure escalation storylets emit claimable requests
- [ ] No de-escalation? → add `make_peace`, `leave_gang`, `reform` storylets

## Deliverable

1. Edge interrupt system with nest/parallel/replace/cancel scopes
2. Arc stack for nested interrupts with return conditions
3. Interrupt priority resolution
4. Conditional postconditions (property-driven branching)
5. ~15-20 escalation storylets (economic + authority + social)
6. Testbed validation: emergent power structures within 3-6 simulated months
7. Tuned escalation thresholds (validated across multiple seeds)
