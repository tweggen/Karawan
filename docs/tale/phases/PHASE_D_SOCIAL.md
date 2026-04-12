# TALE-SOCIAL Phase D: Pre-Established Social Structures

**Status**: IMPLEMENTATION COMPLETE — D1 ✅, D2 ✅, D3 ✅, D4 ✅, D5 ✅ (all 2026-04-12)

**Prerequisites**: Phases 0-5 (TALE narrative engine, escalation system, GroupDetector, RelationshipTracker)

> **Naming note**: There are two "Phase D" workstreams in this project. This one — TALE-SOCIAL — is about pre-baking social structure for clusters. The other one (`PHASE_D.md` next to this file) is about multi-objective routing. They are independent. CLAUDE.md uses the prefix "TALE-SOCIAL" or "Routing" to disambiguate.

---

## Problem Statement

When the player enters a cluster, NPCs should already have a social fabric: groups, trust relationships, role-tinted personalities. The naive approach is to run a 365-day discrete-event simulation per cluster at runtime, but that takes 2-4 hours and blocks gameplay.

**Key insight**: The relationship graph (who trusts whom, group memberships, role-tuned property snapshots) is independent of the spatial layout (buildings, streets). We can decouple them: pre-compute relationship scenarios *once* at build time against synthetic populations, then re-attach them onto real cluster NPCs at runtime.

**Result**: Fast cluster generation with pre-established depth, full seedability, no runtime simulation cost.

---

## Architecture

```
   ┌───────── BUILD TIME (Chushi) ──────────┐    ┌─── RUNTIME (game) ───┐
   │                                        │    │                       │
   │  models/nogame.scenarios.json          │    │  TaleModule           │
   │  ├── small  : 5 scenarios              │    │  ├── ScenarioLibrary  │
   │  ├── medium : 8 scenarios              │    │  └── ScenarioSelector │
   │  └── large  :12 scenarios              │    │  └── ScenarioApplicator
   │       │                                │    │            │          │
   │       ▼                                │    │  TaleManager.PopulateCluster
   │  ScenarioCompiler                      │    │            │          │
   │  ├── synthetic SpatialModel            │    │            ▼          │
   │  ├── synthetic NpcSchedule pool        │    │  Real cluster NPCs    │
   │  ├── DesSimulation(365 days)           │    │  with baked groups,   │
   │  └── ScenarioExporter                  │    │  trust edges & props  │
   │       │                                │    │                       │
   │       ▼                                │    │                       │
   │  nogame/generated/sc-{hash} × 25       │    │                       │
   │  nogame/generated/scenario-statistics.json  │                       │
   │  AndroidResources.xml + InnoResources.iss   │                       │
   │       │                                     │                       │
   └───────┼─────────────────────────────────────┘                       │
           │                                                              │
           └──────► engine.Assets.Open("sc-{hash}") at runtime ───────────┘
```

---

## Sub-phases

### D1: Build-time scenario baking (Chushi pass)

`Chushi/ConsoleMain.cs` grew a second compiler loop next to the existing `Mazu.AnimationCompiler` loop. Each loop iteration runs `engine.tale.bake.ScenarioCompiler.Compile()`, which:

1. Builds a **synthetic `SpatialModel`** with N homes, N/4 offices, N/8 warehouses, N/10 shops, N/8 social venues, N/4 street segments — laid out on a coarse grid. The scenarios are *cluster-independent* by design, so this synthetic geometry only needs to be plausible enough for the DES to produce a relationship graph.
2. Builds a **synthetic NPC pool** with role distribution drawn from the same default weights as `TalePopulationGenerator` (worker 0.30, merchant 0.13, socialite 0.15, drifter 0.12, authority 0.10, nightworker 0.08, hustler 0.07, reveler 0.05) and per-role property formulas matching `TalePopulationGenerator.GenerateProperties` field-for-field.
3. Loads the storylet library from `models/tale/`.
4. Runs `DesSimulation` for 365 simulated days with `NullEventLogger`.
5. Exports the result as `ScenarioExporter.WriteToFile(scenario, "{outputDir}/sc-{hash}")`.

**The hash function** (`ScenarioFileName.Of(category, index, seed)`) is the contract that lets every layer agree on which artifact corresponds to which identity. It's intentionally duplicated in `Tooling/Cmdline/GameConfig.cs` because Cmdline can't reference JoyceCode (same convention as `ModelAnimationCollectionFileName`).

**Resource manifest emission**: `Tooling/Cmdline/GameConfig.cs:LoadScenarioList` walks `/scenarios/categories` from the game config and registers one `Resource { Type = "taleScenario", Tag = "sc-{hash}" }` per (category, index) pair. The existing `Res2TargetTask` then automatically writes them into `AndroidResources.xml` and `InnoResources.iss` alongside the `ac-{hash}` animation entries — no new MSBuild target needed.

**Output**: 25 `sc-{hash}` files in `nogame/generated/` (5 small + 8 medium + 12 large). Cumulative bake time on a typical machine: ~7 seconds for 25 scenarios in parallel via `Task.Run`. Re-builds are byte-deterministic for the same seed.

**Key files**:
- `JoyceCode/engine/tale/bake/ScenarioFileName.cs` — hash helper
- `JoyceCode/engine/tale/bake/ScenarioExporter.cs` — DTOs (`Scenario`, `ScenarioNpc`, `ScenarioGroup`, `ScenarioRelationship`, `ScenarioHistogram`) + JSON read/write
- `JoyceCode/engine/tale/bake/ScenarioCompiler.cs` — synthetic spatial model + NPC pool + DES + export
- `Chushi/ConsoleMain.cs` — bake loop (mirrors the animation loop above it)
- `Tooling/Cmdline/GameConfig.cs` — `ScenarioFileName` + `LoadScenario` + `LoadScenarioList`
- `JoyceCode/engine/AAssetImplementation.cs` — `_whenLoadedScenarios` populates `AvailableScenarios`
- `models/nogame.scenarios.json` — 25-scenario config

---

### D2: Runtime ScenarioLibrary + ScenarioSelector

`engine.tale.bake.ScenarioLibrary` is a lazy singleton registered in `TaleModule.OnModuleActivate` next to the existing TALE registries. Its `TryGet(category, index)` flow mirrors `Model.BakeAnimations` at `JoyceCode/engine/joyce/Model.cs:207-237` exactly:

1. Probe the cache; return if present.
2. Look up the matching `ScenarioBakeRequest` in `AAssetImplementation.AvailableScenarios`.
3. If `joyce.DisablePrebakedScenarios != "true"`, attempt `engine.Assets.Open("sc-{hash}")` and `ScenarioExporter.ReadFromStream`.
4. If the disk read failed (missing file, parse error, or override flag), call `ScenarioCompiler.CompileInMemory()` in-process. The fallback is the *same class* Chushi uses at build time — only the output sink differs.
5. Cache the result.

**The fallback exists for graceful degradation, not as a primary path**: in-process baking is cached only in memory, never written back to `generated/`, so the seeded build-artifact contract stays intact.

`engine.tale.bake.ScenarioSelector.Pick(targetNpcCount, clusterSeed)` decides *which* scenario to attach to a given cluster:

1. Group bake requests by category.
2. Pick the category whose median NpcCount is closest to the target.
3. Within the chosen category, pick by `(clusterSeed mod count)` — round-robin under a seeded permutation.

Median (rather than mean or range midpoint) is robust against asymmetric category sizing and handles odd counts cleanly.

**Convenience**: `ScenarioSelector.PickAndLoad(npcCount, clusterSeed, library)` chains both calls.

**Key files**:
- `JoyceCode/engine/tale/bake/ScenarioLibrary.cs`
- `JoyceCode/engine/tale/bake/ScenarioSelector.cs`
- `JoyceCode/engine/Assets.cs` — added `GetAssetImplementation()` accessor for the library
- `nogameCode/nogame/modules/tale/TaleModule.cs` — registration

---

### D3: Scenario application (re-attachment to real NPCs)

`engine.tale.bake.ScenarioApplicator.Apply(scenario, realNpcs)` rewrites a freshly populated cluster's NPCs from a baked scenario. The challenge is that the bake stored everything by stable rank (0..N-1, sorted by NpcId at export time), but the real cluster has different NpcIds in a different order. The two-step solution:

1. **Build a rank → real NpcId map** by bucketing both populations by role and pairing positionally inside each bucket. The within-role sort key is `(wealth desc, morality desc, NpcId/Rank asc)` — that means the richest worker in the scenario lands on the richest worker in the real cluster, regardless of population size mismatch.
2. **Walk** `scenario.Npcs` / `scenario.Groups` / `scenario.Relationships` and rewrite real NPC state via the map: copy the social-meaningful property subset (`morality, wealth, fear, anger, reputation`), set `npc.GroupId` from the scenario's `GroupRank`, and write both directions of every relationship edge into the matched NPCs' `Trust` dicts.

**Edge cases handled cleanly**: scenario role overflow (extra ranks dropped), real role overflow (unmatched real NPCs keep generator-assigned properties), `GroupRank == -1` (NPC stays ungrouped), edges crossing unmatched ranks (skipped + counted in `ApplyResult`).

**Two divergences from the original D3 plan worth knowing about**:

1. **No `RelationshipTracker` parameter.** The plan signature was `Apply(scenario, realNpcs, relationships, out appliedGroups)`, but `TaleManager` has no global `RelationshipTracker` at runtime — trust lives per-NPC inside `NpcSchedule.Trust`. The applicator writes there directly. The `out GroupDetectionResult` was dropped in favor of an `ApplyResult` stats object (matched count, per-role breakdown, edges applied/skipped, groups touched).

2. **Insertion point is AFTER the warmup loops, not before them.** `TaleManager.PopulateCluster` runs two `AdvanceNpc` warmup loops to desynchronize NPCs into staggered schedule positions and align transit phases with spawn time. Both loops mutate properties via storylet postconditions. Applying scenario state *after* the warmup lets the desynchronization work and then snaps everyone into the scenario's settled state. The property overwrite is limited to the social-meaningful subset, so the warmup's `hunger / fatigue / health / happiness / trust` adjustments survive.

**Key files**:
- `JoyceCode/engine/tale/bake/ScenarioApplicator.cs`
- `JoyceCode/engine/tale/TaleManager.cs:PopulateCluster` — D3 hook block

---

### D4: Seedability validation tests

`tests/JoyceCode.Tests/JoyceCode.Tests.csproj` (xUnit, references `Joyce.csproj`) ships 39 unit tests:

| Suite | Tests | Coverage |
|---|---|---|
| `ScenarioFileNameTests` | 8 | Hash determinism, `sc-` prefix, URL-safe base64, key format stability, `Of`/`OfKey` consistency |
| `ScenarioExporterTests` | 2 | Round-trip preserves all fields, write produces non-empty artifact |
| `ScenarioApplicatorTests` | 11 | Null/empty inputs, single-pair copy, all-five-property overwrite, wealth-descending pairing, `GroupRank == -1`, both-direction trust edges, scenario role overflow, real role overflow, edges crossing unmatched ranks, determinism across calls |
| `ScenarioSelectorTests` | 6 | Empty requests, determinism, closest-median small/medium, seeded round-robin, negative cluster seed |
| `ScenarioCompilerTests` | 2 | Same-seed byte-equality (the core seedability assertion), different-seed divergence |
| `ScenarioStatisticsTests` (D5) | 7 | Empty scenario, group counts, relationship density formula, role distribution, property summary, determinism, category aggregation |
| `RoutingPreferencesTests` (pre-existing) | 10 | Revived as a side effect of wiring up the .csproj |

**Total: 46 tests, 168 ms via `dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj`.**

The compiler determinism test is the heart of D4: it runs `ScenarioCompiler.CompileInMemory` twice with the same `(category, index, seed, npcCount, simulationDays)` and asserts NPC count, role assignments, properties, group ranks, and trust edge values all match field-for-field. If any code path inside the synthetic spatial model builder, the synthetic NPC builder, or `DesSimulation` ever reaches an unseeded RNG, this test catches it.

**Two notes on what D4 deliberately doesn't include**:

1. **No 100-cluster generation matrix** (10 clusters × 10 seeds, per the original plan). Full cluster generation requires booting the world generator, which is heavyweight, slow, and doesn't fit a unit test profile. The deterministic-bake-and-apply chain is fully covered above.
2. **Pre-existing dead test files needed triage**. Wiring up the .csproj surfaced two groups of drift in the dead test files: the navigation tests had wrong namespaces (fixed) plus float-vs-int API drift (excluded), and `StreetGenerationDiagnosticsTests` needs `ClusterStorage` from a full DI container (excluded). Both groups are preserved on disk for whoever revives them; the csproj has explanatory comments.

**Key files**:
- `tests/JoyceCode.Tests/JoyceCode.Tests.csproj`
- `tests/JoyceCode.Tests/engine/tale/bake/*.cs` (six test files)

---

### D5: Statistics + tuning observations

`engine.tale.bake.ScenarioStatisticsBuilder` is a pure-data builder that turns each baked `Scenario` into a `PerScenarioStats` (group counts/types/sizes, relationship density, role distribution, per-property mean/stdev/min/max plus floor and ceiling fractions). `BuildReport` aggregates per-category and produces a `StatisticsReport` ready for JSON serialization.

A new pass at the end of `Chushi/ConsoleMain.cs` (after `Task.WaitAll` on the bake tasks) walks each `sc-{hash}` file, runs the builder, and writes `nogame/generated/scenario-statistics.json` (~55 KB indented JSON, 25 scenarios + 3 category aggregates). Failures here log a warning instead of breaking the build — statistics are observability, not part of the runtime contract.

**Concrete tuning observations from the current bake** (every line below is a real number from the latest `scenario-statistics.json`):

| Metric | small (5) | medium (8) | large (12) |
|---|---|---|---|
| `meanGroupCount` | **190.20** | **434.50** | **500.00** |
| `stdevGroupCount` | 66.54 | 91.12 | **0.00** |
| `meanRelationshipDensity` | **0.91** | 0.57 | 0.34 |
| `meanGroupMembershipRatio` | **1.00** | 0.94 | 0.70 |

Five concrete tuning concerns this surfaces — recorded here as findings, **not auto-fixed** because each is a judgment call that needs user input:

1. **`GroupDetector.MaxCliques = 500` is binding for the large category.** All 12 large scenarios hit *exactly* 500 groups (zero stdev). The detector finds many more cliques than the cap allows and truncates at 500. Large-scenario group counts are not meaningful as currently configured — they're all at the artificial cap. Tuning options: lower `MaxCliques` and accept the truncation as legit, or tighten `MinCliqueSize` / `TrustThreshold` so fewer cliques form in the first place.

2. **Group membership ratio is unrealistically high.** Every NPC in a small scenario is in at least one clique (1.00). Real social structures have isolated individuals.

3. **Relationship density is extreme for small clusters.** 91% of all possible NPC pairs in a 40-NPC scenario have a recorded relationship. The synthetic spatial model probably packs everyone into too few shared venues over 365 days. Tuning option: increase venue diversity in `ScenarioCompiler.BuildSyntheticSpatialModel`, OR shorten the bake duration (currently 365 days) to a more reasonable settling time.

4. **Properties saturate to the extremes.** `morality` ends with **87% of NPCs at floor (≤0.05)** and 8% at ceiling — almost no one in the middle. `wealth` is 55% at floor / 20% at ceiling. `reputation` is 67% / 16%. The 365-day sim is long enough to drive everyone to one extreme via storylet postconditions and morality drift.

5. **Fear is dead across all 25 scenarios**: `mean=0.000, stdev=0.000, fractionAtFloor=1.00`. No NPC has any fear. This is a system-level observation, not a bake bug — `TalePopulationGenerator` initializes fear at 0 and the storylet postconditions don't appear to raise it from there. Worth investigating whether this is intentional.

**Key files**:
- `JoyceCode/engine/tale/bake/ScenarioStatistics.cs`
- `Chushi/ConsoleMain.cs` — statistics pass after the bake loop
- `tests/JoyceCode.Tests/engine/tale/bake/ScenarioStatisticsTests.cs`
- `nogame/generated/scenario-statistics.json` — generated artifact

---

## How to run things

| Action | Command |
|---|---|
| Bake all 25 scenarios + emit statistics | `dotnet build nogame/nogame.csproj` |
| Run unit tests | `dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj` |
| Force in-process bake fallback (skip the on-disk artifacts) | Set `joyce.DisablePrebakedScenarios=true` in `models/nogame.globalSettings.json` |
| Inspect a baked scenario | `nogame/generated/sc-{hash}` is plain JSON — `python3 -m json.tool < ...` |
| Inspect aggregated statistics | `nogame/generated/scenario-statistics.json` |

---

## What's NOT in TALE-SOCIAL Phase D

- **No automatic tuning of bake parameters** in response to D5 observations. The five concerns surfaced above are documented but not fixed; each requires judgment about what kind of social structure feels right in the game. Adjustments are a follow-up that should commit `MaxCliques` / `SimulationDays` / synthetic spatial model changes deliberately.
- **No 100-cluster integration matrix** for seedability validation. The deterministic-bake-and-apply chain is covered by unit tests; a full matrix would naturally live beside the existing TestRunner harness.
- **No playtest tuning** ("do pre-established groups feel natural?"). Out of scope for a code-only phase.
- **Pre-existing dead navigation/streets test files** are excluded from compilation, not fixed. Their API drift / DI requirements are real follow-ups, not blockers for D4.
- **Cross-cluster relationships** (NPCs in cluster A trusting NPCs in cluster B). The applicator writes only intra-scenario edges. Listed as a "post-Phase-D enhancement" in the plan.

---

## Document History

- **2026-04-12**: Initial — covers D1-D5 as a single arc, all implemented same day.
