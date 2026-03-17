### Technical architecture and data schema for the procedural narrative system

---

## 1. High-level architecture

### 1.1 Core components

- **World State Graph**
  - **Responsibility:** Holds locations, factions, NPCs, artifacts, and their relations.
  - **Type:** Mutable graph (nodes + edges + attributes).
- **Narrative State (per-NPC)**
  - **Responsibility:** Tracks active arcs, completed storylets, motif progression, emotional trajectory **per NPC entity**. Each NPC carries its own narrative state. A global aggregation layer provides cross-NPC queries.
  - **Type:** Structured state object per entity, linked to world graph.
- **Storylet Library**
  - **Responsibility:** Persistent store of all storylets (ordinary / uncommon / extraordinary).
  - **Type:** Database or in-memory index with query capabilities.
- **Arc Library**
  - **Responsibility:** Definitions of emotional arcs (3+ node patterns, themes, constraints).
  - **Type:** Static configuration, versioned.
- **Motif Registry**
  - **Responsibility:** Definitions and runtime instances of motifs (who/what/where they bind to).
  - **Type:** Mapping from motif IDs to world entities and progression counters.
- **Selection Engine**
  - **Responsibility:** Given current world + narrative state, selects next storylet(s) to fire.
  - **Inputs:** Active arc, world state, motif state, rarity weights.
  - **Outputs:** Chosen storylet(s) + binding (which NPC, which location, etc.).
- **Graph Rewriting Engine**
  - **Responsibility:** Applies storylet postconditions and rewrite rules to:
    - World State Graph
    - Narrative State (arcs, motifs, emotional trajectory)
- **Arc Rewrite Engine**
  - **Responsibility:** Applies higher-level rewrite rules:
    - Arc substitution
    - Nested expansion
    - Parallel composition
- **Runtime Orchestrator**
  - **Responsibility:** Main loop:
    1. Evaluate world + narrative state
    2. Choose or update active arcs
    3. Select storylets
    4. Apply rewrites
    5. Expose resulting content to gameplay (quests, dialogue hooks, etc.)

---

## 2. Data schema

I’ll use a JSON-like notation for clarity; you can map this to your preferred format (JSON, YAML, DB schema).

### 2.1 World state graph

```json
{
  "WorldState": {
    "nodes": [
      {
        "id": "village_01",
        "type": "location",
        "tags": ["village", "forest"],
        "state": {
          "population": 42,
          "prosperity": "medium",
          "threat_level": "low",
          "status": "intact"
        }
      },
      {
        "id": "faction_bandits",
        "type": "faction",
        "tags": ["bandits"],
        "state": {
          "strength": "high",
          "aggression": 0.8
        }
      }
    ],
    "edges": [
      {
        "id": "rel_village_bandits",
        "from": "village_01",
        "to": "faction_bandits",
        "type": "threatens",
        "weight": 0.6
      }
    ]
  }
}
```

---

### 2.2 Storylet schema

```json
{
  "Storylet": {
    "id": "village_festival",
    "rarity": "ordinary",              // ordinary | uncommon | extraordinary
    "emotional_tone": "hopeful",       // hopeful | tragic | tense | relief | neutral
    "thematic_tags": ["community", "tradition"],
    "motifs": ["village_elder", "festival_banner"],
    "narrative_function": "setup",     // setup | complication | resolution

    "preconditions": {
      "world": [
        { "type": "location", "tag": "village", "state.status": "intact" },
        { "type": "season", "value": "spring" }
      ],
      "factions": [
        { "id": "faction_bandits", "state.strength_max": "medium" }
      ],
      "narrative": [
        { "not_active_arc": "community_vs_chaos" }
      ],
      "entity": [
        { "property": "trust", "min": 0.3 },
        { "property": "anger", "max": 0.5 }
      ]
    },

    "bindings": {
      "location": { "type": "location", "tag": "village" },
      "npc_elder": { "type": "npc", "motif": "village_elder" }
    },

    "content_refs": {
      "description_id": "desc_village_festival",
      "dialogue_template_ids": ["dlg_festival_invite"]
    },

    "postconditions": {
      "world_mutations": [
        {
          "target_binding": "location",
          "changes": { "state.status": "celebrating" }
        }
      ],
      "narrative_mutations": [
        { "type": "unlock_storylet", "id": "bandit_raid" },
        { "type": "motif_progress", "motif": "village_elder", "delta": 1 },
        { "type": "emotion_delta", "value": 1 }   // adjust emotional trajectory
      ],
      "entity_mutations": [
        { "property": "trust", "delta": 0.1 },
        { "property": "anger", "delta": -0.2 }
      ]
    }
  }
}
```

---

### 2.3 Emotional arc schema

```json
{
  "EmotionalArc": {
    "id": "community_vs_chaos",
    "anchor_theme": "resilience",
    "rarity": "uncommon",   // how often this arc is chosen as a primary arc

    "nodes": [
      {
        "index": 0,
        "required_tone": "hopeful",
        "required_function": "setup",
        "required_thematic_tags": ["community"],
        "optional_motifs": ["village_elder"]
      },
      {
        "index": 1,
        "required_tone": "tragic",
        "required_function": "complication",
        "required_thematic_tags": ["chaos", "threat"],
        "optional_motifs": ["bandits"]
      },
      {
        "index": 2,
        "required_tone": "hopeful",
        "required_function": "resolution",
        "required_thematic_tags": ["recovery", "unity"],
        "optional_motifs": ["village_elder"]
      }
    ],

    "constraints": {
      "max_length": 3,
      "must_end_with_function": "resolution",
      "motifs_required": ["village_elder"],
      "can_run_in_parallel_with": ["trust_vs_betrayal"]
    }
  }
}
```

---

### 2.4 Motif schema

```json
{
  "MotifDefinition": {
    "id": "village_elder",
    "type": "character",
    "binding_rules": {
      "preferred_node_types": ["npc"],
      "preferred_tags": ["elder", "leader"]
    }
  },

  "MotifInstance": {
    "id": "village_elder_instance_01",
    "motif_id": "village_elder",
    "bound_entity_id": "npc_023",
    "progress": 2,              // how many times motif has appeared
    "importance": 0.8           // used for prioritizing recurrence
  }
}
```

---

### 2.5 Narrative state schema

```json
{
  "NarrativeState": {
    "active_arcs": [
      {
        "arc_id": "community_vs_chaos",
        "current_node_index": 1,
        "bound_context": {
          "location": "village_01",
          "motif_instances": ["village_elder_instance_01"]
        }
      }
    ],
    "completed_arcs": [
      { "arc_id": "trust_vs_betrayal", "seed_id": "world_seed_123" }
    ],
    "emotional_trajectory": {
      "current_value": 0.5,
      "history": [0, 0.3, 0.1, 0.5]
    },
    "fired_storylets": ["village_festival", "bandit_raid"],
    "motif_instances": ["village_elder_instance_01", "artifact_relic_01"]
  }
}
```

---

### 2.6 Arc rewrite rule schema

```json
{
  "ArcRewriteRule": {
    "id": "expand_complication_with_parallel_politics",

    "match": {
      "arc_id": "community_vs_chaos",
      "node_index": 1,                      // complication node
      "world_conditions": [
        { "type": "faction", "tag": "political", "state.influence_min": 0.5 }
      ]
    },

    "rewrite": {
      "type": "parallel_composition",
      "add_arc": "trust_vs_betrayal",
      "bindings": {
        "shared_location": "village_01",
        "shared_motif": "village_elder"
      }
    },

    "probability": 0.4
  }
}
```

---

### 2.7 Entity properties schema

```json
{
  "EntityProperties": {
    "entity_id": "npc_023",
    "seed": 948172,
    "properties": {
      "anger": 0.2,
      "trust": 0.6,
      "fatigue": 0.4,
      "loyalty": 0.8,
      "curiosity": 0.5
    },
    "deviation_log": [
      {
        "graph_position": { "arc_id": "community_vs_chaos", "edge_index": 1, "progress": 0.4 },
        "event": "player_mediated_conflict",
        "timestamp": 48200,
        "property_snapshot": { "anger": 0.7, "trust": 0.3 }
      }
    ]
  }
}
```

Properties are mutable numeric values (0.0–1.0) on each NPC entity. They are read by storylet preconditions and edge interrupt conditions, and written by postconditions, world events, strategy execution, and player interactions. Two NPCs with identical seeds but different accumulated properties will take different story branches — this is the primary source of emergent individuality.

The `deviation_log` records player-caused branch deviations from the seed-deterministic path, enabling story reconstruction.

---

### 2.8 Edge interrupt schema

```json
{
  "EdgeInterrupt": {
    "id": "road_rage_interrupt",

    "trigger": {
      "event_types": ["npc_collision", "player_interaction"],
      "entity_conditions": [
        { "property": "anger", "min": 0.6 }
      ],
      "world_conditions": [
        { "type": "location", "tag": "road" }
      ]
    },

    "priority": 70,

    "scope": "nest",

    "branch_arc": "confrontation_micro_arc",

    "return_condition": {
      "type": "on_resolution",
      "invalidate_parent_if": [
        { "property": "anger", "min": 0.9 }
      ]
    },

    "cancel_effects": {
      "entity_mutations": [
        { "property": "trust", "delta": -0.3 }
      ],
      "narrative_mutations": [
        { "type": "unlock_storylet", "id": "grudge_formation" }
      ]
    }
  }
}
```

Edge interrupts declare:
- **trigger**: event types + entity property conditions + world conditions that activate this interrupt
- **priority**: conflict resolution when multiple interrupts target the same edge (higher wins)
- **scope**: `nest` (push parent, run branch, return) | `parallel` (run alongside parent) | `replace` (abandon parent) | `cancel` (invalidate parent with cancel-specific effects)
- **branch_arc**: the arc to activate on interrupt
- **return_condition**: for `nest` scope — when the branch resolves, determines whether parent resumes or is invalidated based on post-branch entity state
- **cancel_effects**: postconditions that fire only when the parent arc is cancelled (distinct from normal resolution)

---

### 2.9 Per-NPC narrative state schema

```json
{
  "NpcNarrativeState": {
    "entity_id": "npc_023",
    "seed": 948172,
    "graph_position": {
      "current_edge": { "arc_id": "community_vs_chaos", "from_node": 0, "to_node": 1 },
      "edge_progress": 0.6,
      "depth": 3
    },
    "arc_stack": [
      {
        "arc_id": "community_vs_chaos",
        "paused_edge": { "from_node": 0, "to_node": 1 },
        "edge_progress_snapshot": 0.4,
        "reason": "nest:confrontation_micro_arc"
      }
    ],
    "parallel_arcs": [],
    "active_arcs": [
      {
        "arc_id": "confrontation_micro_arc",
        "current_node_index": 1,
        "bound_context": {
          "location": "street_segment_47",
          "other_npc": "npc_089"
        }
      }
    ],
    "completed_arcs": [
      { "arc_id": "trust_vs_betrayal", "outcome": "resolved", "timestamp": 41000 }
    ],
    "emotional_trajectory": {
      "current_value": 0.7,
      "history": [0, 0.3, 0.5, 0.7]
    },
    "fired_storylets": ["village_festival", "market_argument"],
    "motif_instances": ["village_elder_instance_01"]
  }
}
```

Key additions vs. the original global NarrativeState:
- **Per-entity**: each NPC carries this independently
- **arc_stack**: supports nested interrupts with mid-edge resumption (stack semantics)
- **parallel_arcs**: arcs running alongside the main arc with join conditions
- **graph_position**: current edge + progress + subdivision depth for lazy evaluation
- **seed**: enables deterministic regeneration of unperturbed branches

---

### 2.10 Fork/join schema

```json
{
  "ForkJoin": {
    "parent_arc": "community_vs_chaos",
    "parallel_arc": "trust_vs_betrayal",
    "join_condition": "sync",
    "shared_bindings": {
      "location": "village_01",
      "motif": "village_elder"
    }
  }
}
```

Join conditions:
- `sync` — both arcs must complete before the NPC advances
- `race` — first arc to complete wins; the other is cancelled
- `independent` — no synchronization; arcs run and resolve on their own timelines

---

## 3. Runtime flow (revised)

The runtime processes each actively simulated NPC (Tier 2 + story-pinned) independently. See `NPC_STORIES_DESIGN.md` for simulation tier definitions.

### 3.1 Per-NPC tick (on node arrival or interrupt)

1. **Check for edge interrupts:**
   - Evaluate all registered `EdgeInterrupt` triggers against current entity properties, world state, and pending events.
   - If multiple interrupts match, highest priority wins.
   - Apply interrupt scope:
     - `nest` → push current arc onto `arc_stack`, activate `branch_arc`
     - `parallel` → add to `parallel_arcs` with join condition
     - `replace` → discard current arc, activate `branch_arc`
     - `cancel` → discard current arc, fire `cancel_effects`

2. **Node arrival (no interrupt):**
   - If the outgoing edge exists (previously generated), follow it.
   - If not, **subdivide**: use seed + depth to generate next story node and edge.
   - Evaluate entity properties against edge/node preconditions to select branch direction.

3. **Arc selection (if no active arc):**
   - Choose next arc from `ArcLibrary` based on:
     - Rarity
     - Thematic fit with NPC’s emotional trajectory
     - World conditions
     - Entity property state (e.g., high anger biases toward conflict arcs)
   - Apply `ArcRewriteRules` to possibly expand, nest, or compose.

4. **Storylet selection (for current arc node):**
   - Query `StoryletLibrary` with:
     - `required_tone`, `required_function`, `required_thematic_tags`
     - World preconditions
     - **Entity property preconditions** (e.g., `anger > 0.7`)
     - Motif requirements
   - Apply rarity weights and diversity constraints.

5. **Binding:**
   - Bind storylet roles (location, NPC, artifact) to concrete world entities.

6. **Execution:**
   - If player is nearby (Tier 1): present as visible scene / dialogue / quest hook.
   - If Tier 2 only: resolve silently, advance state.

7. **Postconditions:**
   - Apply `world_mutations` to WorldState.
   - Apply `narrative_mutations` to NPC’s NarrativeState.
   - Apply `entity_mutations` to NPC’s properties (e.g., `anger += 0.3`, `trust -= 0.1`).
   - Log deviation if player interaction caused a non-seed-deterministic branch.

8. **Stack/join resolution:**
   - If nested arc resolved: check `return_condition`. Resume parent from `arc_stack` or invalidate.
   - If parallel arc resolved: evaluate join condition (`sync`, `race`, `independent`).

### 3.2 Edge traversal (between nodes)

No story computation. The NPC acts according to the current edge’s parameters (spatial movement, animation, strategy execution). Position is derived from edge progress, not simulated per-frame. This is the common case — the vast majority of time is spent here.

### 3.3 Property drift

Entity properties change continuously from multiple sources:
- **Storylet postconditions** (discrete jumps at story nodes)
- **World events** (e.g., faction conflict raises anger for all faction members)
- **Strategy execution** (e.g., socializing reduces fatigue, increases trust toward present NPCs)
- **Player interaction** (e.g., helping an NPC increases their trust)
- **Passive decay/growth** (configurable per property — anger decays over time, loyalty is sticky)

This drift means that even without player involvement, NPCs gradually diverge from their seed-deterministic paths as their accumulated experiences shape their property state. Two NPCs with identical seeds placed in different clusters will live different lives.