# Phase 0C — Output Format & Automated Iteration

**Prerequisites**: Phase 0B (DES runs and produces events).
**Read also**: `REFERENCE.md` for base properties and roles.

---

## Goal

Define and implement the testbed's output: event log, structured metrics, sample traces, and CLI. Make the output machine-readable so Claude Code can operate the write → run → measure → adjust iteration loop autonomously.

## What To Build

### 1. Event Log (`events.jsonl`)

One JSON object per line (JSON Lines). Every DES event produces one line. Common envelope:

```json
{"t":"2026-06-15T14:30:00","day":3,"npc":47,"evt":"<type>", ...}
```

**Event types:**

| Type | When | Key Fields |
|------|------|------------|
| `npc_created` | Sim start, once per NPC | `seed`, `role`, `home`, `workplace`, `social_venues`, `props`, `schedule_template` |
| `node_arrival` | Each story node transition | `storylet`, `verb`, `verb_params`, `props` (full snapshot), `post` (deltas) |
| `encounter` | Two NPCs interact | `other`, `interaction`, `location`, `location_type`, `trigger`, props before/after for both, `relationship_before/after` |
| `request_emitted` | Postcondition emits request | `request_id`, `request_type`, `location`, `urgency`, `timeout_minutes` |
| `request_claimed` | NPC picks up request | `request_id`, `requester`, `request_type`, `npc_storylet_interrupted`, `interrupt_scope` |
| `request_resolved` | Fulfilled/timed out/cancelled | `request_id`, `outcome`, `duration_minutes`, props after |
| `interrupt` | Storylet interrupted | `interrupted_storylet`, `interrupt_source`, `scope`, `new_storylet`, `return_to` |
| `resume` | Return from nested interrupt | `resumed_storylet`, `was_interrupted_by`, `time_lost_minutes` |
| `escalation` | High-threshold storylet fires | `escalation_type`, `target`, `preconditions_met`, `outcome` |
| `relationship_changed` | Trust crosses tier boundary | `other`, `old_tier`, `new_tier`, `trust`, `interaction_count` |
| `day_summary` | End of each sim day, per NPC | `storylets_completed`, `storylets_interrupted`, `encounters`, `props`, `relationships{}` |

**Acceptance criterion**: Filtering `events.jsonl` for a single NPC must contain enough information to write a complete biography from that NPC's perspective — who they are, what they did, where they were, who they know, how they changed.

### 2. Structured Metrics (stdout JSON)

The primary machine-readable output. Emitted to stdout when simulation completes:

```json
{
  "run_id": "20260313_143022_seed42",
  "config": {
    "cluster_index": 0, "npc_count": 500, "days_simulated": 365, "seed": 42,
    "encounter_probabilities": { "venue": 0.07, "street": 0.015, "transport": 0.002, "workplace": 0.04 },
    "interaction_thresholds": { "greet_min_trust": 0.3, "help_min_trust": 0.5, "conflict_max_trust": 0.3 }
  },
  "metrics": {
    "routine_completion_rate": 0.84,
    "interrupts_per_day": { "mean": 2.1, "median": 2, "std": 1.4, "p5": 0, "p95": 5 },
    "interactions_total": 383250,
    "interactions_by_type": { "greet": 201000, "trade": 52000, "chat": 89000, "argue": 12500 },
    "request_fulfillment_rate": 0.92,
    "graph": {
      "nodes": 500, "edges": 18420, "largest_component_fraction": 0.88,
      "clustering_coefficient": 0.42, "degree_distribution_gini": 0.38
    },
    "properties": {
      "hunger": { "mean": 0.48, "std": 0.15, "mean_daily_range": 0.55 },
      "fatigue": { "mean": 0.41, "std": 0.18, "mean_daily_range": 0.65 },
      "wealth": { "mean": 0.44, "std": 0.22 }
    },
    "role_breakdown": {
      "worker":   { "count": 200, "completion_rate": 0.89, "mean_interrupts": 1.6 },
      "merchant": { "count": 100, "completion_rate": 0.76, "mean_interrupts": 3.1 }
    },
    "escalation": {
      "first_conflict_day": 18, "first_gang_formation_day": null,
      "npcs_in_escalation_at_end": 23
    },
    "location_hotspots": [
      { "location_id": "shop_east_42", "type": "shop", "total_encounters": 1240 }
    ]
  },
  "warnings": ["merchant completion_rate 0.76 below target minimum 0.80"],
  "pass": false
}
```

### 3. Target Metrics (`testbed_targets.json`)

The testbed compares output against this file and populates `pass` and `warnings`:

```json
{
  "routine_completion_rate": { "min": 0.80, "max": 0.90 },
  "interrupts_per_day_mean": { "min": 1.0, "max": 3.0 },
  "request_fulfillment_rate": { "min": 0.90 },
  "largest_component_fraction": { "min": 0.80 },
  "clustering_coefficient": { "min": 0.30, "max": 0.60 },
  "degree_distribution_gini": { "min": 0.25 },
  "property_mean_daily_range": {
    "hunger": { "min": 0.40 },
    "fatigue": { "min": 0.50 }
  },
  "role_completion_rate_min": { "all_roles": 0.75 },
  "escalation_first_conflict_day": { "max": 30 },
  "escalation_fraction_at_day_180": { "min": 0.03, "max": 0.15 }
}
```

Exit code: 0 = pass, 1 = fail.

### 4. Sample Traces (`traces.log`)

5 NPCs (one per role + one random) with full daily text traces for qualitative review:

```
=== npc_023 (worker, seed=8847) — Day 1 ===
[06:15] node_arrival → "wake_up" (fatigue=0.08, hunger=0.52)
  verb: stay_at(home_qtr4_est12, 45min, "morning_routine")
  post: fatigue -0.05, hunger +0.08
[07:00] node_arrival → "commute" (fatigue=0.03, hunger=0.60)
  verb: use_transport(home_qtr4_est12, garage_qtr2_est5, walk)
  route: 3 segments, 14min travel
  ** encounter: npc_112 (merchant) at shop_eat_qtr2_17 — greet (trust 0.35→0.39)
```

### 5. Relationship Graph Snapshot (`graph.json`)

Final social graph state for visualization tools:

```json
{
  "snapshot_day": 365,
  "nodes": [
    { "id": 23, "seed": 8847, "role": "worker", "quarter": "qtr4",
      "props": { "hunger": 0.45, "wealth": 0.56 },
      "total_encounters": 892, "degree": 47 }
  ],
  "relationships": [
    { "a": 23, "b": 112, "trust_ab": 0.72, "trust_ba": 0.68,
      "tier": "friend", "total_interactions": 89,
      "interactions_by_type": { "greet": 52, "trade": 18, "chat": 14, "help": 5 },
      "first_interaction_day": 1, "last_interaction_day": 362 }
  ]
}
```

### 6. CLI

```
dotnet run --project Testbed -- [options]

  --days N              Simulated days (default: 365)
  --seed N              World + NPC seed (default: 42)
  --cluster N           Cluster index (default: 0, largest)
  --npcs N              Override NPC count
  --params FILE         Parameter file (default: testbed_params.json)
  --targets FILE        Target metrics file (default: testbed_targets.json)
  --traces N            Sample NPCs to trace (default: 5)
  --trace-file FILE     Trace output (default: traces.log)
  --graph-file FILE     Graph output (default: graph.json)
  --events-file FILE    Event log output (default: events.jsonl)
  --quiet               Only emit metrics JSON to stdout
  --batch FILE          Parameter sweep: read configs from FILE, output CSV
```

### 7. Automated Iteration Protocol

Claude Code operates the loop:

1. Read `testbed_targets.json` (knows what "good" looks like)
2. Read current storylet files + `testbed_params.json`
3. Run `dotnet run --project Testbed -- --days 365 --seed 42 --quiet`
4. Parse metrics JSON from stdout, check `pass` and `warnings`
5. If fail: diagnose from warnings + sample traces, edit storylet/parameter files
6. Re-run. Validate across multiple seeds for stability.

**What Claude adjusts:**

| Type | Files | Example |
|------|-------|---------|
| Encounter probabilities | `testbed_params.json` | Lower venue prob 0.07 → 0.05 |
| Storylet preconditions | `storylets/*.json` | Raise hunger threshold for eat |
| Storylet postconditions | `storylets/*.json` | Reduce anger from argue |
| Property mutation rates | `storylets/*.json` | Slow hunger growth |
| Role schedules | `roles/*.json` | Longer merchant lunch break |
| New storylets | `storylets/*.json` | Add evening_stroll |
| Interaction thresholds | `testbed_params.json` | Lower greet_min_trust |

## Deliverable

Running `dotnet run --project Testbed -- --days 365 --quiet` should:
1. Print exactly one JSON object to stdout (the metrics)
2. Write `events.jsonl`, `traces.log`, `graph.json` to disk
3. Exit with code 0 (pass) or 1 (fail)
4. Complete in under 10 seconds for 500 NPCs × 365 days
