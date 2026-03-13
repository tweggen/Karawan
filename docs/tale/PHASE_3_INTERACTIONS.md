# Phase 3 — NPC-NPC Interaction

**Prerequisites**: Phase 1 (storylets), Phase 0B (DES with encounter resolver).
**Read also**: `REFERENCE.md` for interaction primitives, relationship tiers.

---

## Goal

Implement the interaction pool. Write interaction storylets that make NPCs create work for each other. Tune encounter probabilities via testbed. Achieve emergent economic and social activity.

## What To Build

### 1. Interaction Pool

A cluster-scoped store for requests and signals:

```csharp
public class InteractionPool
{
    // Active requests awaiting claim
    public void EmitRequest(InteractionRequest request);
    public InteractionRequest? FindMatchingRequest(NpcSchedule claimer, string requestType);
    public void ClaimRequest(int requestId, int claimerId);

    // Signals for wait-edges
    public void EmitSignal(InteractionSignal signal);
    public bool CheckSignal(int requestId);

    // Cleanup: remove expired requests
    public void PurgeExpired(DateTime now);
}

public class InteractionRequest
{
    public int Id;
    public int RequesterId;
    public string Type;          // "food_delivery", "trade", "help", etc.
    public int LocationId;
    public float Urgency;
    public DateTime Timeout;
    public string StoryletContext; // which storylet emitted this
}
```

### 2. DSL Extensions

Add to the storylet JSON format:

**Request postcondition:**
```json
{
  "postconditions": {
    "hunger": "-0.55",
    "request": {
      "type": "food_delivery",
      "location": "current",
      "urgency": 0.7,
      "timeout_minutes": 60
    }
  }
}
```

**Claim interrupt trigger:**
```json
{
  "id": "deliver_food",
  "trigger": {
    "type": "claim",
    "request_type": "food_delivery",
    "role_match": ["merchant", "drifter"]
  },
  "interrupt_scope": "nest",
  ...
}
```

**Wait edge:**
```json
{
  "verb": "wait_for",
  "verb_params": {
    "signal_type": "request_fulfilled",
    "request_id": "$current_request",
    "timeout_minutes": 60,
    "fallback_storylet": "eat_stale_food"
  }
}
```

### 3. Tier 3 Abstract Resolution

When a request is emitted and no specific Tier 2 NPC claims it:
- Check if any Tier 3 NPC (by role/schedule) could plausibly fulfill it
- If yes: resolve abstractly — "some matching NPC fulfilled the order"
- Emit signal immediately (with delay for travel time)
- No specific NPC is materialized or interrupted

This keeps Tier 3 simulation cheap while preventing requests from always timing out.

### 4. Interaction Storylet Library (~15-20 entries)

**Service interactions (economic):**

| Id | Trigger | Verb | Postconditions |
|----|---------|------|---------------|
| `order_food` | hunger>0.6 + no food shop nearby | wait_for(food_delivery, 60min) | emit request(food_delivery) |
| `deliver_food` | claim(food_delivery) + role=merchant/drifter | go_to(requester) → interact_with(requester, deliver) | signal(fulfilled), wealth +0.04 |
| `buy_from_shop` | at shop + hunger>0.5 | interact_with(shopkeeper, buy) | hunger -0.5, wealth -0.03; shopkeeper wealth +0.03 |
| `restock_urgent` | merchant + inventory_stress>0.7 | emit request(supply_delivery) + wait | wealth -0.08 |

**Social interactions (relationship-building):**

| Id | Trigger | Verb | Postconditions |
|----|---------|------|---------------|
| `greet_acquaintance` | co-located + trust>0.3 | interact_with(other, greet) | trust +0.04 both sides |
| `chat_at_venue` | at social_venue + co-located with any NPC | interact_with(other, chat) | trust +0.08, happiness +0.1 |
| `ask_for_help` | unmet need + knows NPC with capability + trust>0.5 | emit request(help, target=specific_npc) | — |
| `provide_help` | claim(help) + trust with requester>0.4 | go_to(requester) → interact_with | trust +0.15 both sides |

**Conflict interactions (tension):**

| Id | Trigger | Verb | Postconditions |
|----|---------|------|---------------|
| `argue` | co-located + mutual trust<0.3 or competing interest | interact_with(other, argue) | anger +0.2 both, trust -0.1 |
| `threaten` | anger>0.7 + target trust<0.2 | interact_with(target, threaten) | target fear +0.3 |
| `report_to_authority` | witnessed threat/crime + knows authority NPC + trust>0.4 | emit request(investigate, target=authority) | — |
| `flee` | fear>0.7 | go_to(home) | fear -0.2, anger +0.1 |

**Criminal interactions (builds on Phase 1 desperation mechanics):**

Phase 1 introduces the `morality` property and `desperation` score that gate criminal interaction types (rob, blackmail, intimidate, recruit). Phase 3 extends these into full interaction-pool flows:

| Id | Trigger | Verb | Postconditions |
|----|---------|------|---------------|
| `rob_with_backup` | criminal group + target wealthy + co-located with group member | interact_with(target, rob) | Higher success rate than solo rob; group trust +0.05 |
| `blackmail` | knows witnessed_crime fact + target is perpetrator + morality<0.3 | emit request(payment, target=perpetrator) | Recurring wealth drain via interaction pool |
| `fence_goods` | criminal group member + stolen goods + knows merchant(trust>0.3, morality<0.5) | interact_with(merchant, trade_stolen) | wealth +0.06, merchant morality -0.01 |
| `tip_off_authority` | witness + knows authority(trust>0.4) + morality>0.5 | emit request(investigate) | Creates crime_report in pool for authority to claim |

Note: Phase 1's `GroupDetector` identifies criminal groups by trust-clique + shared low-morality/low-wealth profile. Phase 3 uses these group IDs to unlock group-coordinated interactions.

### 5. Encounter → Interaction Mapping

When the `EncounterResolver` determines two NPCs encounter each other:

1. Check relationship tier (stranger / acquaintance / friend)
2. Check both NPCs' story states (any open interrupt slots?)
3. Select interaction storylet based on:
   - Relationship tier + properties (e.g., low trust + high anger → `argue`)
   - Location type (venue → `chat_at_venue`, street → `greet_acquaintance`)
   - Active requests (if one NPC has an open request the other can claim)
4. Apply the interaction storylet to both NPCs (dual postconditions)

### 6. DES Integration

In `EncounterResolver.ResolveEncounters()`:
- When an encounter fires, create `InterruptResolution` events for both NPCs
- The interrupt applies the selected interaction storylet
- If scope is `nest`: current storylet is paused, interaction runs, then current resumes
- Emit `encounter` + `interrupt` + `resume` events to the event log

## Probability Tuning (the core testbed use case)

### Parameters to Sweep

| Parameter | Range | Effect |
|-----------|-------|--------|
| `P_venue` | 0.03–0.12 | Encounter rate at social venues |
| `P_street` | 0.005–0.03 | Encounter rate on streets |
| `P_workplace` | 0.02–0.08 | Encounter rate at workplaces |
| `greet_min_trust` | 0.15–0.4 | When NPCs start greeting |
| `conflict_max_trust` | 0.2–0.4 | When conflicts can trigger |
| `claim_eagerness` | 0.3–0.8 | How readily NPCs claim requests |
| `request_rate_multiplier` | 0.5–2.0 | Global scaling of request generation |

### Success Criteria (from `testbed_targets.json`)

- Routine completion rate: 80-90%
- Mean interrupts per NPC per day: 1-3
- Request fulfillment rate: >90%
- After 30 days: visible cliques in interaction graph
- After 90 days: some NPCs have disproportionately high degree
- Graph largest component: >80% of NPCs

### Content Iteration Checklist

- [ ] Certain roles over-generating requests? → reduce frequency or add cooldowns
- [ ] Some roles never claiming? → add interaction storylets to their pool
- [ ] Conflicts never arising? → lower anger threshold, add conflict triggers
- [ ] Acquaintances not forming? → check `chat_at_venue` frequency, lower trust threshold
- [ ] Interaction graph fragmented? → add cross-quarter storylets (`visit_other_quarter`)
- [ ] Request pool saturated? → increase claim eagerness or add more claimer roles

## Deliverable

1. Interaction pool implementation (cluster-scoped request/signal store)
2. DSL extensions: request/signal postconditions, claim triggers, wait edges
3. Tier 3 abstract resolution for background NPCs
4. ~15-20 interaction storylets (service + social + conflict)
5. Tuned encounter probabilities (validated across multiple seeds)
6. Testbed metrics showing emergent social/economic activity
