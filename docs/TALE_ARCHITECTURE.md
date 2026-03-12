### Technical architecture and data schema for the procedural narrative system

---

## 1. High-level architecture

### 1.1 Core components

- **World State Graph**
  - **Responsibility:** Holds locations, factions, NPCs, artifacts, and their relations.
  - **Type:** Mutable graph (nodes + edges + attributes).
- **Narrative State**
  - **Responsibility:** Tracks active arcs, completed storylets, motif progression, emotional trajectory.
  - **Type:** Structured state object, linked to world graph.
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

## 3. Runtime flow (simplified)

1. **Update world state** from simulation/gameplay.
2. **Update narrative state** (emotional trajectory, motif progress, active arcs).
3. **Arc selection/expansion:**
   - If no active arc or arc node completed:
     - Choose next arc from `ArcLibrary` based on:
       - Rarity
       - Thematic fit with recent history
       - World conditions
   - Apply `ArcRewriteRules` to possibly:
     - Expand node into nested arc
     - Add parallel arc
     - Substitute arc
4. **Storylet selection:**
   - For current arc node:
     - Query `StoryletLibrary` with:
       - `required_tone`
       - `required_function`
       - `required_thematic_tags`
       - World preconditions
       - Motif requirements
     - Apply rarity weights and diversity constraints (avoid recent repeats).
5. **Binding:**
   - Bind storylet roles (location, NPC, artifact) to concrete world entities.
6. **Execution:**
   - Present quest/scene to player.
7. **Graph rewriting:**
   - Apply storylet `postconditions` to:
     - WorldState (nodes/edges/attributes)
     - NarrativeState (active arcs, emotional trajectory, motif progress)
8. Loop.

---

If you want, next step could be:  
- Turning this into concrete **type definitions** (e.g., TypeScript/JSON Schema)  
- Or mapping it to your existing **graph rewriting engine’s primitives** (node types, rule syntax, etc.).