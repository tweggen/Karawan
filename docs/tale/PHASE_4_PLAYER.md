# Phase 4 — Player Intersection

**Prerequisites**: Phase 2 (strategy executors, visible NPCs), Phase 3 (interaction pool).
**Read also**: `REFERENCE.md` for interaction primitives, relationship tiers.

---

## Goal

The player participates in the interaction pool using the same mechanism as NPCs. Write player-facing narrative content: overhear dialogue, direct interaction text, social capital feedback. Tested in-game (not testbed — testbed has no player).

## What To Build

### 1. Player as Interaction Pool Participant

The player is another entity that can:
- **Claim requests**: see nearby requests in the pool, accept them (taxi ride, delivery, help)
- **Emit signals**: completing a delivery emits `request_fulfilled` signal to the requesting NPC
- **Trigger interrupts**: player presence within range of an NPC can interrupt their storylet (NPC notices player, initiates conversation)
- **Emit requests**: player can ask NPCs for things (information, services)

The player does NOT have a story graph or `NpcNarrativeState`. The player's "storylet" is free-form gameplay. Integration is through the interaction pool only.

### 2. Social Capital Tracking

Per-NPC relationship with the player, using the same trust system as NPC-NPC:

```csharp
public class PlayerSocialCapital
{
    // Trust per NPC (same Dictionary<int, float> as NPC-NPC trust)
    public Dictionary<int, float> TrustPerNpc;

    // Derived social tier per cluster (aggregate of individual relationships)
    public SocialTier GetTierInCluster(int clusterId);
}
```

| Tier | Condition | Effect |
|------|-----------|--------|
| Anonymous | No NPC trust > 0.2 in cluster | NPCs ignore player unless service interaction |
| Recognized | 3+ NPCs with trust > 0.3 | NPCs greet player, share casual information |
| Connected | 10+ NPCs with trust > 0.5 | NPCs approach unsolicited, ask favors |
| Entangled | 5+ NPCs with trust > 0.7 | Past decisions visible in NPC behavior, conflicting obligations |

### 3. Player-Facing Narrative Content

#### Overhear Content

When the player is within earshot of an NPC-NPC interaction, display a text fragment. Attached to interaction storylets as optional `overhear_text`:

```json
{
  "id": "chat_at_venue",
  "overhear_text": [
    "Did you hear about the new shop on {quarter}?",
    "I've been so tired lately, work has been brutal.",
    "{other_name} helped me out last week, good person."
  ]
}
```

```json
{
  "id": "argue",
  "overhear_text": [
    "You still owe me for last week!",
    "Stay away from my shop!",
    "I don't trust {other_name} anymore."
  ]
}
```

Template variables: `{other_name}`, `{quarter}`, `{shop_name}`, `{npc_name}`.

#### Direct Interaction Text

When the player interacts with an NPC (claims their request, or NPC approaches player):

**Taxi requests** (connect to existing taxi quest system):
- *"Take me to {destination}. I need to be there by {time}."*
- *"Can you drive me to {npc_name}'s place? I owe them a visit."*

**Delivery requests:**
- *"Can you bring this to {target_name} at {location}?"*
- *"I ordered food but nobody's delivering — could you?"*

**Help requests (Connected+ tier):**
- *"That guy {threat_npc} has been following me for two days."*
- *"I need someone to talk to {authority_npc} for me."*

**Information sharing (Recognized+ tier):**
- *"Be careful around {quarter} — there's been trouble."*
- *"Did you know {npc_name} lost their job at the {shop}?"*

#### Social Capital Feedback

How the player perceives tier transitions:

- **→ Recognized**: NPCs start using the player's name. *"Hey, you're the taxi driver, right? I'm {name}."*
- **→ Connected**: NPCs approach without prompting. *"I've been meaning to ask you..."*
- **→ Entangled**: NPCs reference past events. *"Remember when you helped me with {past_event}? I need another favor."*

### 4. Request Visibility

The player needs to discover available requests. Options:
- **Proximity**: requests from nearby NPCs appear as UI indicators
- **Reputation**: at Connected+ tier, NPCs come to the player with requests
- **Overhear**: player hears an NPC mention a need ("I wish someone would deliver this...")
- **Direct ask**: player initiates conversation, NPC mentions pending needs

## Content Iteration

Play-test in the real game:
- [ ] Can the player perceive NPC stories through overheard dialogue?
- [ ] Do NPCs give enough context when requesting help?
- [ ] Does social capital progression feel earned? (Not too fast, not too slow)
- [ ] Are there enough player verbs? (mediate, inform, introduce — not just deliver)
- [ ] Do NPCs at different tiers behave noticeably differently toward the player?
- [ ] Does refusing a request have visible consequences? (trust drops, NPC remembers)

## Deliverable

1. Player as interaction pool participant (claim, emit, trigger)
2. Social capital tracking with 4 tiers
3. Overhear text for core interaction storylets
4. Direct interaction dialogue for requests/help/information
5. Social tier transition feedback (NPC dialogue changes)
6. Request visibility system (proximity + reputation + overhear)
