---
name: TALE Tier 3 population architecture
description: Design for seed-based NPC population, cluster lifecycle integration, deviation-only persistence, and TaleSpawnOperator rework
type: project
---

## Architecture Decision (2026-03-15)

### Problem
`TaleManager.GetSchedule()` always returns null because `TaleSpawnOperator` creates anonymous NPCs with `_seed++` but never creates/registers `NpcSchedule` objects. The deeper issue: no Tier 3 background population exists.

### Agreed Design

**Seed-based deterministic generation:**
- Each NPC's seed = `Hash(clusterSeed, npcIndex)` — position-independent, skip-safe
- From seed: role, home, workplace, venues, properties are all deterministic
- An NPC that hasn't interacted with the player can be fully regenerated from seed

**Cluster lifecycle drives population:**
- On cluster activation (`ClusterCompletedEvent`): generate NpcSchedules deterministically, register with TaleManager
- On cluster deactivation: drop non-deviated schedules (regenerable), keep deviated ones
- No world-wide PopulationOperator — population is per-cluster, on-demand

**Deviation-only persistence:**
- Only save NPCs the player has interacted with **and** that are in non-primary state
- "Primary state" = algorithmically generated, replaceable without player noticing
- Save format: seed reference + deviation delta, not full NPC state
- On cluster reactivation: regenerate from seed, **skip deviated indices**, overlay deviated NPCs from save

**TaleSpawnOperator becomes a materializer:**
- Queries TaleManager for NPCs in the fragment (not _seed++)
- Materializes existing Tier 3 schedules into Tier 2/1 ECS entities
- On despawn: strip visuals/strategy, schedule persists in TaleManager

**Why:** 1MB save files uploading over mobile every minute is unacceptable. Seed determinism keeps saves tiny (proportional to player impact, not world size).

**How to apply:** See `docs/tale/PHASE_6.md` for the implementation plan.
