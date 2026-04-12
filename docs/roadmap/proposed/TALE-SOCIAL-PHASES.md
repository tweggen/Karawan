# Pre-Established Social Structures (Dynamic Scenario System)

**Status**: Proposed
**Phase**: D (Discovery & Enhancement) — Pre-game world generation
**Prerequisites**: Phase 0-5 complete (TALE narrative engine, escalation system, GroupDetector)

---

## Vision

When the player enters a game world, social structures should already exist: gangs, protection rackets, authority patrols, trading partnerships. Rather than simulating 365 days per cluster at runtime (2-4 hour wait), we **pre-compute relationship scenarios once** and apply them to real in-game NPCs.

**Key insight**: Emergent social structures are independent of spatial layout. We decouple:
- **Relationship graph** (who trusts whom, group memberships) — computed once, reusable
- **Spatial layout** (buildings, streets) — seeded per cluster, unique per world

**Result**: Fast world generation with pre-established depth, full seedability.

---

## Architecture Overview

### Scenario Templates
Pre-computed at build time or tool-time. Each scenario encodes:
- **Size category**: Small (40-80 NPCs), Medium (150-250 NPCs), Large (400-600 NPCs)
- **Relationship graph**: JSON of NPC IDs → trust edges, group memberships
- **Property distributions**: Histograms of morality, wealth, fear, anger, reputation
- **Group registry**: GroupId → type (criminal, trade, patrol, social), member count

### Runtime Injection
When player spawns in a cluster:
1. Generate spatial layout (buildings, streets) — **seedable, deterministic**
2. Spawn real NPCs with real positions — **seedable, deterministic**
3. Determine cluster size → pick matching scenario template
4. **Apply scenario**: Copy relationships + group assignments to real NPCs
5. Real NPCs now have pre-established trust network and social positions

### Seedability
```
ClusterSeed → {spatial layout, NPC count}
ClusterSize → {scenario template selection}
Scenario + real NPC list → {relationship assignment, GroupId assignment}
All deterministic → same seed = identical social + spatial world
```

---

## Phases

### Phase D1: Scenario Pre-Computation Engine

**Goal**: Build off-game tool to generate relationship scenarios.

**Deliverables**:
1. **ScenarioGenerator.cs** — Configurable DES runner
   - Input: Config with NPC count, role distribution, morality/wealth seeds
   - Runs 365-day simulation
   - Output: DetectedGroup[] + RelationshipTracker snapshot
2. **ScenarioExporter.cs** — Serialize scenario to JSON
   - Format: `{ groups: [...], relationships: [...], properties_histogram: {...} }`
   - Compact schema (store only summary stats + group structure)
3. **Tool integration**: Add console tool or IDE button to generate scenarios
4. **Config file**: `models/tale/scenarios.json`
   ```json
   {
     "size_categories": [
       { "name": "small", "npc_count_range": [40, 80], "scenario_count": 5 },
       { "name": "medium", "npc_count_range": [150, 250], "scenario_count": 8 },
       { "name": "large", "npc_count_range": [400, 600], "scenario_count": 12 }
     ]
   }
   ```
5. **Output directory**: `models/tale/scenarios/` with structure:
   ```
   models/tale/scenarios/
   ├── small/
   │   ├── scenario_1.json
   │   ├── scenario_2.json
   │   └── ... (5 scenarios)
   ├── medium/
   │   └── ... (8 scenarios)
   └── large/
       └── ... (12 scenarios)
   ```

**Files to create**:
- `JoyceCode/engine/tale/ScenarioGenerator.cs`
- `JoyceCode/engine/tale/ScenarioExporter.cs`
- `nogameCode/nogame/tools/ScenarioGeneratorTool.cs` (CLI or IDE hook)
- `models/tale/scenarios.json` (config)

**Tests**:
- Verify 365-day DES produces valid scenario
- Verify JSON round-trips without data loss
- Verify scenario groups are detected correctly
- Run 10 scenarios per size category, validate statistics

**Time estimate**: ~20% effort (mostly reusing DesSimulation, GroupDetector)

---

### Phase D2: Scenario Selection & Caching

**Goal**: Load scenarios at runtime, select based on cluster size.

**Deliverables**:
1. **ScenarioLibrary.cs** — Load all scenarios into memory at startup
   - Maps: size → List<Scenario>
   - Lazy-loads JSON from `models/tale/scenarios/`
2. **ScenarioSelector.cs** — Pick scenario by cluster size
   - Input: NPC count in generated cluster
   - Output: Scenario with matching size category
   - Strategy: Round-robin or random within category (seeded)
3. **ScenarioCache.cs** — Cache loaded scenarios (LRU if needed)
4. **Integration point**: TaleManager initialization
   - `TaleManager.InitializeCluster(clusterIndex, spatialModel, npcPool)`
   - Calls `ScenarioSelector.PickScenario(npcPool.Count)` → loads JSON

**Files to modify/create**:
- `JoyceCode/engine/tale/ScenarioLibrary.cs` (new)
- `JoyceCode/engine/tale/ScenarioSelector.cs` (new)
- `JoyceCode/engine/tale/TaleManager.cs` (add scenario loading)
- `nogameCode/nogame/tale/TaleModule.cs` (register ScenarioLibrary at startup)

**Tests**:
- Load all scenarios successfully
- Size category matching (small cluster → small scenario)
- Scenario selection is deterministic (seeded)

**Time estimate**: ~10% effort (mostly glue code)

---

### Phase D3: Scenario Application (Runtime Injection)

**Goal**: Apply pre-computed relationships to real in-game NPCs.

**Deliverables**:
1. **ScenarioApplicator.cs** — Core injection logic
   ```csharp
   public void ApplyScenarioToNpcs(
       Scenario scenario,
       IReadOnlyDictionary<int, NpcSchedule> realNpcs,
       RelationshipTracker relationships,
       out GroupDetectionResult appliedGroups)
   ```

   Algorithm:
   - For each group in scenario:
     - Pick N real NPCs matching role/size (deterministic via seed)
     - Assign them the scenario's GroupId
     - Copy scenario's property distributions (sample for each NPC)
   - For each relationship edge in scenario:
     - Map scenario NPC IDs → real NPC IDs (via role-based matching)
     - Copy trust values into RelationshipTracker
   - Run GroupDetector to validate applied groups
   - Output: Modified NPCs + RelationshipTracker

2. **NPC Matching Strategy** (critical for coherence)
   - Match by role first (drifter ↔ drifter, merchant ↔ merchant)
   - Within role, sort by properties and match by position in sorted list
   - Deterministic: same seed + same NPC pool = same matching

3. **Property Sampling**
   - From scenario histogram: {morality: {bins: [...], counts: [...]} }
   - For each real NPC: sample from histogram matching role
   - Clamp to [0,1]

4. **Integration point**: TaleManager.PopulateCluster()
   - After spatial model extraction + NPC spawning
   - Before first storylet selection
   - Calls `ScenarioApplicator.ApplyScenarioToNpcs(...)`

**Files to modify/create**:
- `JoyceCode/engine/tale/ScenarioApplicator.cs` (new)
- `JoyceCode/engine/tale/TaleManager.cs` (call applicator in PopulateCluster)
- `JoyceCode/engine/tale/NpcSchedule.cs` (ensure GroupId serialization)

**Tests**:
- Apply scenario to different NPC pools of same size → same structure
- Apply scenario to different sizes → correct group scaling
- GroupDetector validates applied groups
- Relationships are coherent (cliques stay cliques)
- Property distributions match scenario input

**Time estimate**: ~25% effort (complex matching logic)

---

### Phase D4: Seedability Validation

**Goal**: Guarantee deterministic world generation.

**Deliverables**:
1. **SeedabilityTest.cs** — Automated validation
   ```csharp
   void TestWorldGeneration(int seed)
   {
       // Generate world 1: cluster layout + NPCs + scenario application
       var world1 = GenerateCluster(seed);

       // Generate world 2: same seed
       var world2 = GenerateCluster(seed);

       // Assert identical:
       Assert(world1.SpatialModel == world2.SpatialModel);
       Assert(world1.NpcGroups == world2.NpcGroups);
       Assert(world1.RelationshipGraph == world2.RelationshipGraph);
   }
   ```

2. **Diff comparison tool** — Debug seed divergence
   - If world1 ≠ world2, output delta
   - Helps identify non-deterministic code paths

3. **Test matrix**:
   - 10 clusters × 10 random seeds = 100 generations
   - Verify all reproduce identically on second run
   - Measure generation time (should be <100ms per cluster)

**Files to create/modify**:
- `TestRunner/SeedabilityTests.cs` (new test suite)
- Run as part of phase D validation

**Time estimate**: ~10% effort (mostly test harness)

---

### Phase D5: Tuning & Variation

**Goal**: Validate scenarios feel natural, offer enough variety.

**Deliverables**:
1. **Scenario statistics** — Measure each pre-computed scenario:
   - Group count, types, size distribution
   - Relationship density (% of possible trust edges)
   - Property means/stdevs (morality, wealth, etc.)
   - Record in `scenarios/statistics.json`

2. **Variation report**:
   - Do 25 scenarios (small+medium+large) show diversity?
   - Are there outlier scenarios (too many gangs, too few)?
   - Adjust scenario generation config if needed

3. **Gameplay tuning**:
   - Playtest: Do pre-established groups feel natural?
   - Do players understand the social landscape?
   - Adjust scenario count per size if variety is lacking

4. **Documentation**:
   - Update CLAUDE.md with Phase D status
   - Update `docs/tale/PHASE_D.md` with scenario architecture

**Files to create/modify**:
- `models/tale/scenarios/statistics.json` (generated)
- `docs/tale/PHASE_D.md` (new design doc)
- `CLAUDE.md` (Phase D completion status)

**Time estimate**: ~10% effort (analysis + iteration)

---

## Implementation Order

1. **Phase D1** (20%) — Build scenario generator
   - Produces 25 pre-computed scenarios (5 small, 8 medium, 12 large)
   - Cached in `models/tale/scenarios/`

2. **Phase D2** (10%) — Load scenarios at startup
   - ScenarioLibrary + ScenarioSelector working

3. **Phase D3** (25%) — Apply scenarios to real NPCs
   - Matching algorithm working correctly
   - GroupDetector validates applied groups

4. **Phase D4** (10%) — Validate seedability
   - 100 tests passing

5. **Phase D5** (10%) — Tune and document
   - Scenarios feel natural
   - Documentation updated

---

## Integration Points

### TaleManager.PopulateCluster()
**Current flow:**
```csharp
PopulateCluster(clusterIndex, spatialModel)
  → TalePopulationGenerator.GenerateNpcs(...)
  → Insert NPCs into _schedules dict
```

**New flow:**
```csharp
PopulateCluster(clusterIndex, spatialModel)
  → TalePopulationGenerator.GenerateNpcs(...)
  → ScenarioSelector.PickScenario(npcCount)
  → ScenarioApplicator.ApplyScenarioToNpcs(scenario, schedules, relationships)
  → Insert NPCs into _schedules dict with GroupIds + relationships
```

### Serialization (NpcSchedule, RelationshipTracker)
- NpcSchedule.GroupId must serialize/deserialize (currently does)
- RelationshipTracker must serialize/deserialize (check current capability)
- No new persistent fields needed

### Performance
- Scenario loading: <100ms (JSON parse)
- Scenario selection: O(1)
- Scenario application: O(n log n) where n = NPC count (sorting for matching)
- **Total per cluster**: <500ms (acceptable)

---

## Success Criteria

✅ **Seedability**: Same seed → identical world (spatial + social)
✅ **Perf**: Cluster generation <1 second (mostly spatial layout)
✅ **Variety**: 25 scenarios across 3 size categories show distinct structures
✅ **Natural**: Social structures feel coherent (groups make sense)
✅ **Scalable**: Config allows adding more scenarios without code changes

---

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| NPC matching produces incoherent groups | Test matching algorithm thoroughly; seed-based comparison |
| Scenarios don't transfer across spatial layouts | Validate in Phase D4 with multiple cluster seeds |
| Generation too slow | Profile Phase D3; optimize matching if needed |
| Not enough variety with 25 scenarios | Increase per-size count; add scenario config parameters |
| Serialization breaks during deserialization | Validate in Phase D4; add version guard |

---

## Timeline & Effort

| Phase | Effort | Duration | Notes |
|-------|--------|----------|-------|
| D1 | 20% | 1-2 days | Reuse DesSimulation + GroupDetector |
| D2 | 10% | 0.5 day | Straightforward loading |
| D3 | 25% | 2-3 days | Most complex (matching) |
| D4 | 10% | 1 day | Test harness |
| D5 | 10% | 1 day | Analysis + tuning |
| **Total** | **75%** | **5-7 days** | ~1 week |

---

## Future Enhancements (Post-Phase-D)

1. **Dynamic scenario generation** — Generate scenarios on-demand if variety insufficient
2. **Scenario parameters** — Config-driven precondition thresholds per scenario
3. **Player influence preview** — Show social graph UI before/after player interactions
4. **Cross-cluster relationships** — Link scenarios across adjacent clusters
5. **Difficulty scaling** — Scenarios tuned by game difficulty setting

---

## Documentation Updates Needed

1. **CLAUDE.md** — Update Phase D status, add scenario architecture summary
2. **docs/tale/PHASE_D.md** — Full design doc (similar to PHASE_5.md)
3. **docs/tale/REFERENCE.md** — Scenario format specification
4. **PROCESS.md** — Update for new scenario build artifact

