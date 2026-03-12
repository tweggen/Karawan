Here’s a clean, self‑contained **Markdown concept summary** you can hand to any future AI instance as a foundation for generating a full design document. It captures the architecture, terminology, and logic we developed — without assuming prior context.

---

# Procedural Narrative System: Storylets, Emotional Arcs & Graph Rewriting  
*A high‑level concept summary for future expansion*

## 1. Core Idea  
This system generates **coherent, replayable story arcs** in a fully procedural RPG world by combining:

- **Storylets** (modular narrative units)  
- **Emotional arcs** (tension–release patterns)  
- **Motifs & themes** (recurring narrative anchors)  
- **Graph rewriting** (dynamic expansion, nesting, and interweaving of arcs)

The goal is to produce **meaningful, emergent questlines** driven by world state, player actions, and faction relationships — not predetermined scripts.

---

## 2. Storylets  
Storylets are small, self‑contained narrative fragments with:

- **Preconditions** (environment, factions, NPC states)  
- **Emotional tone** (hopeful, tragic, tense, relief)  
- **Thematic tags** (community, betrayal, discovery, corruption)  
- **Motifs** (recurring characters, artifacts, locations)  
- **Narrative function** (setup, complication, resolution)  
- **Postconditions** (world graph changes, new storylets unlocked)

Storylets are the atomic building blocks of all arcs.

---

## 3. Emotional Arcs  
Emotional arcs provide **pacing and structure**.  
A typical arc is a **3‑node pattern**:

1. **Setup** (low tension, hopeful or neutral)  
2. **Complication** (rising tension, conflict or mystery)  
3. **Resolution** (tension release, transformation)

Arcs are tagged with:

- **Anchor theme** (e.g., “community vs. chaos”, “trust vs. betrayal”)  
- **Required emotional tones** per node  
- **Thematic constraints**  
- **Motif requirements**  

Arcs ensure the story feels coherent and emotionally satisfying.

---

## 4. Motifs & Themes  
Motifs are recurring narrative anchors:

- Characters (village elder, faction leader)  
- Artifacts (ancient relic)  
- Locations (ruined temple, frontier village)

Themes define the **meaning** of an arc (e.g., resilience, corruption, loyalty).  
Motifs and themes ensure continuity across storylets and arcs.

---

## 5. Graph Rewriting  
The narrative is represented as a **graph of arcs and storylets**.  
Rewrite rules allow:

### 5.1 Arc Substitution  
Replace a 3‑node arc with another arc or a variant.

### 5.2 Nested Expansion  
Expand a single node into a smaller arc (e.g., “Bandit Raid” → 3‑node sub‑arc).

### 5.3 Parallel Composition  
Split an arc into two parallel arcs that interweave through shared motifs or world states.

### 5.4 Environment‑Driven Rewrites  
World changes (ruined village, faction war) trigger new arcs or modify existing ones.

This prevents predictable patterns and enables complex, interwoven questlines.

---

## 6. Storylet Rarity Tiers  
To maintain replayability:

- **Ordinary** (frequent, repeatable)  
- **Uncommon** (conditional, mid‑level tension)  
- **Extraordinary** (rare, high‑impact twists with multiple variants)

Rarity controls pacing and prevents major twists from repeating every playthrough.

---

## 7. Emergent Questlines  
Even a small pool of storylets and arcs can generate many distinct questlines by recombining:

- Different storylets filling the same arc slots  
- Different arcs triggered by different seeds  
- Nested or parallel expansions  
- Motif‑driven interweaving  
- Environment‑dependent rewrite rules

The “main questline” is not authored — it **emerges** from the world graph.

---

## 8. Example Minimal Pool  
With just 9 storylets (3 ordinary, 3 uncommon, 3 extraordinary) and 3 emotional arcs, the system can generate:

- Community vs. Chaos arcs  
- Discovery vs. Corruption arcs  
- Trust vs. Betrayal arcs  
- Hybrid arcs combining motifs and themes  
- Nested arcs (e.g., raid → rescue → rebuilding)  
- Parallel arcs (political intrigue + village crisis)

This demonstrates scalability from a small base.

---

## 9. NPC-Centric Application

The TALE system is designed to drive **per-NPC story arcs**, not a single global narrative. See `NPC_STORIES_DESIGN.md` for the full design. Key implications for the TALE system:

- Each NPC carries its own narrative state (active arcs, emotional trajectory, motif instances).
- Storylets are evaluated per-NPC against that NPC's entity properties and world context.
- The player is not the protagonist — they intersect with NPC stories as witness, catalyst, or participant.
- Story graphs are lazily evaluated: only the current edge and next node are materialized. Subdivision fires on node arrival or external disruption.

---

## 10. Entity Properties

NPC entities carry **mutable numeric properties** (e.g., `anger`, `trust`, `fatigue`, `loyalty`) that:

- Are modified by storylet postconditions, world events, player interactions, and strategy execution.
- Serve as **branch conditions** in storylet preconditions and edge interrupt conditions.
- Create **emergent individuality**: two NPCs with identical story seeds but different accumulated properties take different branches at the same subdivision point.

Properties are shared state — readable and writable by the story graph, the strategy system, the transport layer, and player interactions. The TALE DSL references them but does not own them.

---

## 11. Control Flow Constructs

When external events branch new storylines from existing ones, the TALE system must express control flow:

### 11.1 Edge Interrupts
Edges (transitions between story nodes) declare what can interrupt them:
- **Interrupt conditions**: event types that can branch this edge (player interaction, NPC collision, world state change)
- **Interrupt priority**: numeric weight for conflict resolution when multiple events target the same edge
- **Interrupt scope**: `nest` (push current arc, run branch, return) | `parallel` (run alongside) | `replace` (abandon current arc) | `cancel` (invalidate current arc with cancel-specific effects)

### 11.2 Stack Semantics (Nest/Return)
When an interrupt nests a new arc:
- The nested arc has a **return condition** — when it resolves, it determines whether the parent resumes or is invalidated.
- Parent arc edge progress is snapshotted for mid-edge resumption.

### 11.3 Fork/Join
Parallel composition (§5.3) gains a **join condition**:
- `sync` — wait for both arcs to complete
- `race` — first to complete wins, other is cancelled
- `independent` — no synchronization needed

### 11.4 Cancel Propagation
When a branch outcome invalidates a parent arc, **cancel effects** fire — postconditions specific to cancellation, distinct from normal resolution postconditions.

---

## 12. Seed-Based Deterministic Generation

NPC story graphs are generated deterministically from a seed:
- `seed` + `depth` fields in arc/storylet evaluation context ensure reproducible subdivision.
- Player-caused deviations are logged as `deviation_entries` that override seed-deterministic paths.
- An NPC's entire life story compresses to: **seed + current graph position + deviation log**.

---

## 13. Design Goals
- **Replayability:** Different seeds produce different arcs and twists.
- **Coherence:** Emotional arcs and motifs unify the narrative.
- **Meaning:** Themes ensure arcs feel purposeful, not random.
- **Integration:** Story and environment evolve together.
- **Scalability:** Add more storylets, arcs, motifs, and rewrite rules to expand narrative richness.
- **Per-NPC autonomy:** Every NPC runs their own story; the player's narrative emerges from entanglement.
- **Lazy evaluation:** Story complexity is unbounded but computational cost is bounded by on-demand generation.