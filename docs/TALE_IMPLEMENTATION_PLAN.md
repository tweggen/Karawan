# TALE Implementation Plan
*Agile phases — story generation through emergent NPC interaction*

---

## NPC-NPC Interaction Model

NPC stories are not isolated — they communicate via a shared **interaction pool** (scoped to cluster or fragment set). The pattern:

1. NPC A's storylet postcondition emits a **request** into the pool: `{ type: "service_request", service: "food_delivery", location: ..., requester: npc_A }`
2. NPC B's story graph has an **edge interrupt** that triggers on matching requests (e.g., `service_request` where `service = "food_delivery"` and NPC B's role matches).
3. NPC B's interrupt fires (scope: `nest`), branching into a "deliver pizza" arc.
4. NPC B's arc resolves → postcondition emits a **signal**: `{ type: "service_fulfilled", request_id: ..., provider: npc_B }`
5. NPC A was on a **wait edge** (advances on signal or timeout). The signal arrives, NPC A advances.

If NPC B is in Tier 3 (background), the interaction resolves abstractly — "some matching supplier fulfilled the order" without materializing a specific NPC. Only in Tier 2+ does a concrete NPC perform the action.

### Interaction Primitives

| Primitive | DSL Location | Meaning |
|-----------|-------------|---------|
| **Request** | Postcondition | Emit a typed request into the interaction pool |
| **Wait** | Edge type | Block/slow edge advancement until signal or timeout |
| **Signal** | Postcondition | Notify a specific request that it has been fulfilled/failed |
| **Claim** | Interrupt trigger | Pick up a matching request from the pool |

This pattern covers all NPC-NPC interaction: hiring, trading, arguing, asking for help, socializing. The player is just another entity that can emit requests, claim them, or be claimed.

---

## Base Entity Properties

Every NPC gets a common property set. Properties are a flat `Dictionary<string, float>` (range 0.0–1.0) on the entity. No class hierarchy — just a conventional base set. Formalize only if patterns emerge later.

| Category | Properties |
|----------|-----------|
| **Emotional** | `anger`, `fear`, `trust`, `happiness` |
| **Physical** | `health`, `fatigue`, `hunger` |
| **Social** | `wealth`, `reputation` |

Specific NPC types may add properties on top (e.g., a merchant adds `inventory_stress`). These are simply additional dictionary entries — no schema change required.

---

## Spatial Verb Alphabet

Every storylet must declare a **spatial verb** — the physical manifestation of the narrative node. The story graph is the *brain* (rich narrative state), the strategy is the *body* (small set of physical actions). The mapping is not storylet-type → strategy. It is: each storylet carries a required output field specifying one of these verbs plus parameters.

| Verb | Parameters | Example |
|------|-----------|---------|
| `go_to` | location, speed | Walk to the garage |
| `stay_at` | location, duration, animation_hint | Work at garage for 4h |
| `follow` | target_entity, distance | Follow a friend to the bar |
| `interact_with` | target_entity, interaction_type | Argue with neighbor |
| `use_transport` | origin, destination, transport_type | Take tube from home to work |
| `wait_for` | signal_type, timeout | Wait for pizza delivery |

A storylet like "NPC works at the garage" produces: `go_to(garage)` → `stay_at(garage, 4h, "working")`.
A storylet like "NPC argues with neighbor" produces: `go_to(neighbor.location)` → `interact_with(neighbor, "argue")`.

The narrative richness lives in preconditions, postconditions, property mutations, and emotional arcs. The physical manifestation is always one of the few verbs. This keeps the strategy system simple while the story graph can be arbitrarily complex.

---

## Phase 1 — Single NPC Story Generation

**Goal:** One NPC gets a generated story graph and walks through it.

- Define a minimal storylet library (5–10 storylets: wake, commute, work, eat, socialize, sleep)
- Implement seed-based graph subdivision (1–2 levels deep)
- Implement `NpcNarrativeState` on a single ECS entity
- Implement base entity properties (emotional, physical, social — see table above)
- Node arrival triggers storylet selection based on preconditions (including entity property conditions)
- Postconditions mutate entity properties
- Each storylet declares its spatial verb + parameters (see verb alphabet above)
- No player interaction, no interrupts, no NPC-NPC interaction yet

### Testing Phase 1

No visual rendering or player interaction exists yet. Validation via **text trace** — a console/log output printing story state transitions:

```
[T=08:00] npc_023: node_arrival → "wake_up" (fatigue=0.3, hunger=0.6)
  → verb: stay_at(home, 30min, "morning_routine")
  → postconditions: fatigue -0.1, hunger +0.1
[T=08:30] npc_023: node_arrival → "commute" (fatigue=0.2, hunger=0.7)
  → verb: use_transport(home, garage, tube)
[T=08:45] npc_023: node_arrival → "work" (fatigue=0.2, hunger=0.7)
  → verb: stay_at(garage, 4h, "working")
  → postconditions: fatigue +0.3, wealth +0.1
```

This validates:
- Story graph generation and subdivision from seed
- Storylet selection based on preconditions and entity properties
- Property mutation via postconditions
- Seed determinism (same seed → same trace)
- Spatial verb output (verbs are logged, not executed — Phase 2 executes them)

Optional: a debug marker (colored cube) placed at the storylet's target location, teleporting on node transitions. Ugly, but proves spatial sequencing.

**Deliverable:** An NPC that lives a generated daily routine, producing a verifiable text trace of story nodes, property changes, and spatial verbs.

---

## Phase 2 — Story-to-Strategy Translation

**Goal:** Story nodes produce concrete spatial behavior (Skyrim-like daily routines).

- Implement a **strategy executor** for each spatial verb:
  - `go_to` → pathfinding + walk/drive animation
  - `stay_at` → hold position + play animation_hint loop
  - `follow` → maintain distance to target entity
  - `interact_with` → approach target + play interaction animation
  - `use_transport` → enter transport tube (or taxi, bus — transport method selection)
  - `wait_for` → idle at current position until signal or timeout
- Strategy execution loop: story node arrival → read verb + parameters → execute strategy → on completion, signal back to story graph → next node
- NPC visibly walks between locations, stays for appropriate durations
- Time-of-day awareness: story graph seeds schedule-appropriate storylets (wake/work/socialize/sleep follow plausible daily rhythm)
- Handle verb sequencing: a single storylet may produce multiple verbs in sequence (e.g., `go_to(garage)` then `stay_at(garage, 4h, "working")`)

**Deliverable:** NPCs with visible, believable daily routines driven by their story graph.

---

## Phase 3 — NPC-NPC Interaction

**Goal:** NPC storylines can communicate via the interaction pool.

- Implement the interaction pool (cluster-scoped request/signal store)
- Add `request` and `signal` postcondition types to the DSL
- Add `claim` interrupt trigger type (NPC picks up a matching request)
- Add `wait` edge type (advances on signal or timeout)
- Implement abstract resolution for Tier 3 NPCs (request fulfilled by "some matching NPC" without materializing anyone)
- Test case: NPC A orders food, NPC B delivers

**Deliverable:** NPCs that create work for each other. Emergent economic/social activity.

---

## Phase 4 — Player Intersection

**Goal:** The player can participate in the interaction pool.

- Player can `claim` requests (e.g., taxi ride, delivery)
- Player presence triggers edge interrupts on nearby NPCs
- Player actions emit signals that NPC wait-edges can receive
- Social capital tracking begins (which NPCs know the player, at what tier)

**Deliverable:** The player can organically enter NPC storylines through the same mechanism NPCs use with each other.

---

## Phase 5 — Branching & Interrupts

**Goal:** External events branch storylines.

- Implement edge interrupts with priority and scope (nest/parallel/replace/cancel)
- Implement arc stack for nested interrupts with return conditions
- Implement cancel propagation with cancel-specific effects
- Property-driven branching: same story seed, different property state → different outcome

**Deliverable:** NPC stories react to disruption and diverge based on accumulated experience.
