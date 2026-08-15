# NPC-Centric Narrative Design
*Player as witness, catalyst, and social nexus*

## Core Concept

Every NPC has their own story arc running independently. The player is **not** the protagonist of a central questline — instead, the player's story emerges from **increasing entanglement** with NPC storylines. The player may:

- Be irrelevant to an NPC's story (witness it passively)
- Intersect briefly (a small favor, a taxi ride, a conversation)
- Become deeply entangled (mediating conflicts, owing and being owed favors)

Player "progression" is the accumulation of **social capital** — not XP or stats.

---

## Social Capital as Progression

### Tier 1 — Anonymous
- NPCs treat the player as a stranger or service provider (taxi driver).
- The player overhears things but has no leverage.
- Only lever: choosing to show up or not.

### Tier 2 — Recognized
- A few NPCs know the player by name.
- NPCs share more openly, ask small favors.
- The player can introduce NPCs to each other.
- Meaningful choices begin: who to help, who to ignore.

### Tier 3 — Connected
- Factions are aware of the player.
- NPCs come to the player with problems unsolicited.
- The player can call in favors (get someone a job, bail someone out).
- Conflicts arrive uninvited.

### Tier 4 — Entangled
- Past decisions have visible consequences.
- NPCs the player helped (or refused) are now in positions of influence.
- NPC storylines route *through* the player — the social graph has made them a hub.
- Contradictory obligations create genuine dilemmas.

There is no XP bar. Progression is felt through the changing texture of interactions.

---

## Concrete Player Verbs

"Witnessing" alone isn't gameplay. The player needs actionable verbs:

- **Overhear** — gain knowledge NPCs don't have about each other.
- **Inform** — choose to share (or withhold) information between NPCs.
- **Assist** — perform small tasks that nudge an NPC's story at a branch point.
- **Mediate** — resolve conflicts between NPCs (verbally or violently).
- **Introduce** — connect NPCs from different social circles.
- **Refuse** — declining involvement is also a choice with consequences.

---

## NPC Story Simulation

NPC stories don't need to be individually authored. They are:

- **Generated from storylet templates** parameterized by NPC traits, faction, location, and emotional tone (see TALE_CONCEPT.md).
- **Run as lightweight state machines** — most tick silently in the background.
- **Elaborated on demand** — narrative detail only renders when the player is nearby or involved.
- **Branch-sensitive** — at key moments, if the player is present and has relevant knowledge, they can nudge the outcome. Otherwise the story resolves on its own.

This means the computational cost is: "tick N simple state machines, elaborate the 5–20 the player is currently near" — not "simulate N full narratives."

---

## Geography as Pacing

Silicon Desert's physical scale naturally supports this design:

- **~90×90 km world** with ~50+ clusters (cities) of varying size.
- **Real travel time** between clusters at ~60 km/h creates natural decompression.
- **Per-cluster reputation**: arriving in a new cluster resets the player to Tier 1 locally. Reputation in previous clusters persists but decays over time.
- **The world map becomes a map of relationships**, not just locations.

### The Pressure Valve
Entanglement at Tier 3–4 can become stressful (too many obligations, colliding interests). The player can **disentangle by leaving** — drive to another cluster, start fresh. Stories in the old cluster keep ticking and may resolve without the player. This is also thematic: the player is not the center of the universe, even when it feels like it.

### Long Drives
Travel between clusters isn't dead time — it's decompression and transition:
- Hitchhikers with their own micro-stories
- Radio chatter hinting at events in distant clusters
- Silence and desert after a tense sequence

---

## Cross-Cluster Emergence

The most powerful moments come from stories spanning clusters:

- A passenger from City A appears in City B weeks later, changed by events after the player left.
- An NPC in City B mentions someone the player knows from City A — two social graphs connect unexpectedly.
- Faction conflicts in one cluster create refugees or opportunities in another.

These moments feel genuinely emergent because the player **earned** them through time and travel.

---

## The Taxi Loop as Anchor

The cozy taxi gameplay is the reliable, low-stakes foundation:

- Always available regardless of entanglement level.
- Natural encounter generator (passengers talk, reveal stories, make requests).
- The interface between "anonymous service provider" and "getting pulled into someone's life."
- A safe return point when the social web becomes too dense.

---

## Transport Tube Optimization

### Problem
Simulating every traffic participant (cars, pedestrians) individually per-frame is expensive and limits population density.

### Tube Model
Road segments between junctions become **FIFO queues** (tubes) with a flow rate determined by lane speed:

- Cars enter at one end, exit after a deterministic delay.
- No per-frame movement update — position is a pure function of flow rate and entry time.
- Logic only fires at **junctions**: routing decisions, merging, traffic lights.
- 1000 cars on a straight road = 1 tube with a count and flow rate. O(1) instead of O(1000).

Each car in the tube is still a **persistent entity** with individual properties (owner NPC, origin, destination, traits, story state). The tube governs how position is ticked, not what the entity is.

### Dynamic Splits
When the player (or an uncoordinated NPC) obstructs a road segment:

1. The tube splits into two shorter tubes with a new junction at the obstruction.
2. Upstream tube outflow drops to zero (or reduced if a lane remains passable).
3. Cars queue up — just a counter and backlog.
4. Obstruction clears → tubes merge back, backlog flushes at normal flow rate.

Pedestrians use the same model: sidewalk tubes, with road crossings as temporary junctions.

### Strategy-Driven Transport
The tube is one **transport implementation** among several. NPC daily schedules are controlled by the strategy system as a higher-level state machine:

- **Rest** at location A (duration)
- **Commute** A → B (transport method: tube / taxi / walk / bus)
- **Work** at location B (duration)
- **Commute** B → C (transport method)
- **Socialize** at location C (duration)

The strategy requests "move NPC from A to B" — the transport layer picks the implementation. The tube is cheapest, but if the NPC hails a taxi, they enter the player's world as a passenger with dialogue. Same NPC, same schedule, different simulation cost.

---

## Simulation Tiers

Three-tier model based on distance from the player, plus a story-pinned exception.

### Tier 1 — Visible (~150m radius)
Full visual representation. Cars rendered, pedestrians animated, NPCs have faces. Entities materialized from tubes into discrete scene objects.

### Tier 2 — Simulated (active fragment set, ~1.2×1.2 km)
The current fragment and its 8 neighbours (3×3 grid, each fragment 400×400m). Tubes tick, strategies execute, junctions resolve. NPCs exist as entities with computed position but no visuals. Story branch points are evaluated here. When an NPC crosses into visible range, they materialize.

### Tier 3 — Background (rest of cluster / world)
NPCs exist only as strategy state + current location pointer. No tube ticking — position is computed on demand from schedule. "NPC #4837 left home at 8:00, commute takes 12 minutes, so at 8:07 they're 58% along tube X."

### Story-Pinned (Tier 3+ → Tier 2)
Selected NPCs outside the active fragment set remain in Tier 2 simulation because they are **entangled** with the player. The player's social graph extends the simulation boundary for a handful of entities.

### Budgets
- **Tier 1**: Dozens of entities. Capped by visible range.
- **Tier 2**: Hundreds to low thousands. Capped by fragment set. Mostly tubes, so cheap.
- **Tier 3**: Everything else. Near-zero cost per entity.
- **Story-pinned**: Bounded by entanglement — realistically 20–50 NPCs at any time. The player physically cannot form deep relationships with thousands of NPCs.

### Tier Transitions
- **Tier 3 → 2**: Player approaches a fragment, or story-pinned NPC needs active simulation. Compute current position from schedule, inject into tube or location.
- **Tier 2 → 1**: NPC enters visible range. Spawn visual entity from tube/strategy state.
- **Tier 1 → 2**: NPC leaves visible range. Despawn visuals, keep ticking.
- **Tier 2 → 3**: Player moves away, NPC isn't story-pinned. Snapshot schedule state, stop active simulation.

All transitions are **lossless** — the NPC's persistent data (identity, strategy state, schedule, story state) lives on the entity regardless of tier. Only simulation fidelity scales.

---

## Lazy Story Graph Evaluation

### Recursive Subdivision Model
Story complexity is modeled as recursive graph subdivision — each edge can be split by inserting a node, yielding two edges. Starting from a single edge (birth → death):

- Level 0: 1 edge
- Level 1: 2 edges, 1 midpoint
- Level n: 2^n edges, 2^n − 1 nodes

At 10 levels of recursion: ~1024 story beats. At ~15 minutes per beat, that's ~256 hours of NPC life — far more than needed for any play session.

### Lazy Evaluation
The full tree is **never materialized**. At any moment, an NPC only needs:

- The current edge being traversed
- The node being approached
- Enough parent context (seed + depth) to subdivide the next level on arrival

An NPC's entire life story compresses to: **seed + current graph position + log of player-caused deviations**. Regeneration from seed is deterministic for unperturbed branches.

### When Subdivision Fires
- **Node arrival**: NPC reaches the next story node. If the outgoing edge hasn't been generated, subdivide one level. Otherwise, follow the existing edge.
- **External disruption**: Player interaction or NPC-NPC collision mid-edge. The current edge is subdivided at the disruption point — analogous to tube splitting for traffic.
- **Edge traversal**: No story computation. Pure spatial/animation tick. The NPC acts according to the current edge's parameters.

### Computational Cost
The expensive dimension is: **(density of story nodes in time) × (number of simultaneously simulated stories)**

- **Sparse phases** (commuting, working, resting): One evaluation every 10–20 minutes of game time. Near-zero cost.
- **Dense phases** (arguments, social events, crises): Evaluations every few seconds. Expensive but rare.
- The player's presence typically triggers dense phases, and the player can only be in one place.
- A **budget cap** on simultaneous dense-phase NPCs in Tier 2 can enforce this. Deferred evaluations appear as natural hesitation.

### Story Branching and DSL Control Flow

When an external event branches off a new storyline from an existing one, the TALE DSL must express control flow semantics:

- **Branch conditions**: What external events can interrupt an edge? (player interaction, NPC-NPC story collision, world state change)
- **Branch priority**: When multiple events target the same edge, which wins?
- **Branch scope**: Does the branch replace the current arc, run parallel, or nest inside it and return? (Maps to TALE_CONCEPT.md's graph rewriting: substitution, parallel composition, nested expansion)
- **Merge/return**: When a branched storyline resolves, does the NPC return to the original arc, or is it invalidated?

These are fundamentally control flow constructs:

| Construct | Meaning |
|-----------|---------|
| **Interrupt** | External event claims NPC attention, pausing current edge |
| **Fork/Join** | Parallel storylines with a synchronization point |
| **Stack (nest/return)** | Branched arc completes, NPC resumes parent arc |
| **Cancel** | Original arc invalidated by branch outcome |

These must be expressible in the TALE DSL — either as explicit first-class constructs or implicitly through storylet preconditions/postconditions. Given hundreds of NPCs with intersecting stories, **branch scope and merge/return semantics should be explicit** in the DSL for predictability. Conditions can remain declarative.

---

## Relationship to Existing Systems

- **Storylets & Arcs**: TALE_CONCEPT.md provides the generation framework (storylet templates, emotional arcs, graph rewriting). NPC stories are assembled from this pool.
- **Quest System**: Existing quest infrastructure (QuestFactory, strategies, SatnavService) handles the player-facing side when an NPC story becomes a player quest.
- **World Generation**: Cluster/fragment operators already generate the physical world. NPC story generation would be an additional layer seeded by cluster properties.
