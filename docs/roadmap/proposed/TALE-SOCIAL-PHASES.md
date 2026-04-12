# Pre-Established Social Structures (Dynamic Scenario System)

**Status**: D1 ✅ Implemented (2026-04-12); D2 ✅ Implemented (2026-04-12); D3–D5 proposed
**Phase**: D (TALE-SOCIAL — distinct from the routing "Phase D" workstream) — Pre-game world generation
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

### Phase D1: Scenario Pre-Computation Engine ✅ Implemented (2026-04-12)

**Status**: Working end-to-end. `dotnet build nogame/nogame.csproj` produces 25 `sc-{hash}` files (5 small + 8 medium + 12 large) in `nogame/generated/`, listed in both `AndroidResources.xml` and `InnoResources.iss`. Re-builds are deterministic (identical output bytes for the same seed) and add about 0–2 seconds of wall time on top of the existing animation bake.

**Goal**: Build a **build-time bake step** that generates relationship scenarios the same way animation baking generates `ac-{hash}` collections. Scenarios become deterministic build artifacts shipped alongside the rest of `nogame/generated/`.

**Integration model — parallel to animation baking**:

Animation baking today follows this chain (see `nogame/nogame.csproj`):
```
EnsureGeneratedDirectory          // MakeDir ../nogame/generated
  └→ CompileAssetsHost            // invokes Chushi — runs Mazu.AnimationCompiler per (model,anims)
  └→ GatherTexturesHost           // joycecmd pack-textures
  └→ GatherResources              // Res2TargetTask → AndroidResources.xml + InnoResources.iss
  └→ Compile                      // normal C# build; generated files are picked up by MSBuild
```

Scenario baking plugs into this same chain. **No new MSBuild target is added**; instead `CompileAssetsHost` (already Chushi) grows a second compiler pass, and `GatherResources` (already `Res2TargetTask`) grows a second resource category. That way generated scenario files flow through the same `AndroidResources.xml` / `InnoResources.iss` manifests and are listed automatically for inclusion into Wuka / Karawan / Windows installers.

**Deliverables**:

1. **`engine.tale.bake.ScenarioCompiler`** (new, in `JoyceCode/engine/tale/bake/`)
   - Mirrors `Mazu.AnimationCompiler`
   - Inputs: `SizeCategory` (name, NPC count range, seed), `ScenarioIndex` (0..N-1), `OutputDirectory`
   - `Compile()`:
     - Spin up a DES with `TalePopulationGenerator` for the configured NPC count
     - Run 365-day simulation (reusing `DesSimulation` + `GroupDetector` + `RelationshipTracker`)
     - Serialize result via `ScenarioExporter`
     - Write to `{OutputDirectory}/sc-{hash}` (hash = SHA256 of `"{categoryName};{scenarioIndex};{configSeed}"`, base64-encoded like `ac-{hash}`)

2. **`engine.tale.bake.ScenarioExporter`** (new)
   - Serializes a scenario to compact JSON:
     ```json
     {
       "version": 1,
       "category": "medium",
       "index": 3,
       "seed": 12345,
       "groups":       [ { "id": ..., "type": ..., "memberRoles": [...] }, ... ],
       "relationships":[ { "fromRole": ..., "fromRank": ..., "toRole": ..., "toRank": ..., "trust": ... }, ... ],
       "histograms":   { "morality": {...}, "wealth": {...}, "fear": {...}, "anger": {...}, "reputation": {...} }
     }
     ```
   - Roles/ranks are used instead of raw NPC IDs so Phase D3 can remap onto real NPCs deterministically.
   - `ScenarioImporter` (runtime counterpart) is introduced in Phase D2.

3. **Chushi integration** (`Chushi/ConsoleMain.cs`)
   - After the existing `AnimationCompiler` loop (line ~127) add a **Scenario compiler loop** driven by a new `Chushi.AssetImplementation.AvailableScenarios` property (parallel to `AvailableAnimations`).
   - Each entry: `"{categoryName};{scenarioIndex};{configSeed}"`.
   - Runs in parallel via `Task.Run`, same pattern as animations.
   - Output directory resolved identically (`args[3]/args[2]` when invoked by MSBuild, else `./generated`).

4. **Scenario config in `models/`** — declare scenarios just like animations:
   - New satellite file `models/nogame.scenarios.json` (loaded via existing `__include__` mechanism from `nogame.json`):
     ```json
     {
       "scenarios": {
         "categories": [
           { "name": "small",  "npcCountRange": [40, 80],   "count": 5,  "baseSeed": 10000 },
           { "name": "medium", "npcCountRange": [150, 250], "count": 8,  "baseSeed": 20000 },
           { "name": "large",  "npcCountRange": [400, 600], "count": 12, "baseSeed": 30000 }
         ],
         "simulationDays": 365
       }
     }
     ```
   - Total bake: 25 scenarios (5 + 8 + 12).

5. **Resource registration for the build manifest** (`Tooling/Cmdline/GameConfig.cs`)
   - Add `LoadScenario(string categoryName, int index, int baseSeed)` mirroring `LoadAnimation()` (lines 95-140)
   - Add `LoadScenarioList(JsonNode root)` mirroring `LoadAnimationList()` (lines 171-196); reads `/scenarios/categories` and iterates `count` entries per category
   - Filename scheme: `sc-{Base64(SHA256(key))}` where `key = "{category};{index};{seed}"`
   - Each entry becomes a `Resource { Tag = "sc-{hash}", Uri = "{DestinationPath}/sc-{hash}", Type = "taleScenario" }` in `MapResources`
   - Hook into `LoadGameConfig()` next to the existing animation call
   - **Effect**: `Res2TargetTask` automatically emits `<AndroidAsset Include="...sc-{hash}" LogicalName="sc-{hash}" />` into `AndroidResources.xml`, and the matching `InnoResources.iss` line for Windows installers — no new manifest file needed.

6. **Runtime loader plumbing** (`JoyceCode/engine/AAssetImplementation.cs`)
   - Add `_whenLoadedScenarios()` callback mirroring `_whenLoadedAnimations()` (lines 73-148).
   - Register it in `WithLoader()` for JSON path `/scenarios/categories`.
   - Compute the same `sc-{hash}` filename (shared helper in `engine.tale.bake.ScenarioFileName` so Chushi + runtime + GameConfig agree on one algorithm).
   - Call `AddAssociation(scenarioTag, uriBaked)` so loaded `.json` content can be retrieved by tag at runtime (`scenario:small:0`, etc.).
   - Note: Runtime **consumption** of these files (ScenarioLibrary, selection, application) is covered in D2/D3. D1 only guarantees the files exist on disk and are visible to the asset system.

7. **Output layout**:
   ```
   nogame/generated/
   ├── ac-{hash}              (existing baked animations)
   ├── atlas-albedo.json      (existing)
   ├── sc-{hash}              ← new: 25 files, one per scenario
   ├── AndroidResources.xml   (existing, now also lists sc-* entries)
   └── InnoResources.iss      (existing, now also lists sc-* entries)
   ```
   No new subdirectory; scenarios live alongside animations as flat hashed artifacts, matching the established convention.

**Files to create**:
- `JoyceCode/engine/tale/bake/ScenarioCompiler.cs`
- `JoyceCode/engine/tale/bake/ScenarioExporter.cs`
- `JoyceCode/engine/tale/bake/ScenarioFileName.cs` (shared hash helper)
- `models/nogame.scenarios.json`

**Files to modify**:
- `Chushi/ConsoleMain.cs` — add scenario compiler loop after the animation loop
- `Chushi/AssetImplementation.cs` — expose `AvailableScenarios` (reads from `/scenarios/categories`)
- `Tooling/Cmdline/GameConfig.cs` — `LoadScenario` / `LoadScenarioList` + call site in `LoadGameConfig()`
- `Tooling/Cmdline/Resource.cs` — accept `"taleScenario"` type (if a switch exists)
- `JoyceCode/engine/AAssetImplementation.cs` — `_whenLoadedScenarios` + `WithLoader` registration
- `models/nogame.json` — add `__include__` reference to `nogame.scenarios.json`

**Files NOT touched** (intentionally):
- `nogame/nogame.csproj` — no new MSBuild target; existing `CompileAssetsHost` + `GatherResources` already do the work once Chushi and `Res2TargetTask` know about scenarios.
- `Wuka.csproj` / `Karawan.csproj` — continue importing `AndroidResources.xml` / Content items; no changes needed.

**Tests**:
- `dotnet build` produces 25 `sc-*` files in `nogame/generated/`
- `AndroidResources.xml` contains 25 new `<AndroidAsset>` entries with `sc-` tags
- `InnoResources.iss` contains matching entries
- Re-running build with unchanged config produces byte-identical files (seeded determinism)
- `ScenarioExporter` round-trip: export → import → compare group count, relationship count, histogram bins
- `GroupDetector` run against a deserialized scenario reproduces the same groups it originally detected
- Running Chushi directly (`dotnet run --project Chushi`) from a clean `generated/` regenerates scenarios without rebuilding the whole solution

**Layering & on-demand fallback (mirrors animation baking)**:

Animation baking uses a two-layer pattern that D1 must preserve for scenarios:

| Layer | Animation | Scenario (D1) |
|-------|-----------|---------------|
| **Shared bake core** (engine-side, reusable) | `AnimationCollection.BakeAnimations()` — computes per-frame bone matrices from loaded model data | `ScenarioCompiler.Compile()` — runs 365-day DES and produces a scenario graph |
| **Build-time entry** (Chushi) | `Mazu.AnimationCompiler.Compile()` → writes MessagePack to `generated/ac-{hash}` | `Chushi.ConsoleMain` scenario loop → writes JSON to `generated/sc-{hash}` |
| **Asset registration** (warn-only) | `AAssetImplementation._whenLoadedAnimations` — probes, warns if missing | `AAssetImplementation._whenLoadedScenarios` — same behaviour, same warning |
| **Runtime try-load-then-bake** | `Model.TryLoadModelAnimationCollection()` → falls through to in-process `AnimationCollection.BakeAnimations()` (see `Model.cs:207-237`) | `ScenarioLibrary.TryGetScenario()` → falls through to in-process `ScenarioCompiler.Compile()` (introduced in D2) |

**Implication for D1 file placement**: `ScenarioCompiler` and `ScenarioExporter` must live engine-side in `JoyceCode/engine/tale/bake/` (not in Chushi), because both the build-time Chushi pass **and** the runtime `ScenarioLibrary` fallback call into the exact same class. Chushi is just one of two entry points; `ScenarioLibrary` is the other.

This means D1 does **not** need to decide the "missing file" policy — `_whenLoadedScenarios` simply warns, identical to animations, and the fallback is transparent because D2's `ScenarioLibrary.TryGetScenario` will in-process regenerate on demand. If `sc-{hash}` is missing at runtime the user just gets a short stall (seconds per scenario) instead of a hard failure, with trace `"Manually generated scenario for {category}/{index}"` mirroring `"Manually baked animations for {ModelUrl} {AnimationUrls}"` at `Model.cs:231`.

Optional debug override, also mirroring animations (`joyce.DisablePrebakedAnimations` at `Model.cs:164`): add `joyce.DisablePrebakedScenarios` that forces `ScenarioLibrary` to skip the file probe and always bake in-process — useful for iterating on `ScenarioCompiler` without rebuilding Chushi.

**Time estimate**: ~20% effort. Heavy reuse of `DesSimulation` + `GroupDetector` + existing Chushi/`Res2TargetTask` plumbing. Main novelty is the Chushi loop and the `GameConfig` resource entries.

---

### Phase D2: Scenario Selection & Caching ✅ Implemented (2026-04-12)

**Status**: Working. `engine.tale.bake.ScenarioLibrary` and `ScenarioSelector` are registered as lazy singletons in `nogameCode/nogame/modules/tale/TaleModule.cs:OnModuleActivate` next to the existing `RoleRegistry`/`InteractionTypeRegistry`/etc registrations. The library reads `AAssetImplementation.AvailableScenarios` (populated by `_whenLoadedScenarios` during config interpretation) via the new `engine.Assets.GetAssetImplementation()` accessor, and the `TryGet` / `GetOrLoad` flow does try-disk-then-fall-back-to-`ScenarioCompiler.CompileInMemory()`. The on-disk reader is exercised against the already-baked Phase D1 artifacts; round-trip correctness was verified by inspecting the JSON shape against the DTO field names. The runtime fallback is exercised when the user toggles `joyce.DisablePrebakedScenarios=true` (mirrors `joyce.DisablePrebakedAnimations` at `Model.cs:164`).

**Goal**: Load scenarios at runtime, select based on cluster size.

**Deliverables**:
1. **ScenarioLibrary.cs** — Load all scenarios into memory lazily
   - Maps: size → List<Scenario>
   - `TryGetScenario(category, index)` mirrors `Model.BakeAnimations` / `TryLoadModelAnimationCollection` (`JoyceCode/engine/joyce/Model.cs:207-237`): first probes `sc-{hash}` via `engine.Assets.Open(...)`, and on any failure (missing file, parse error, or `joyce.DisablePrebakedScenarios == "true"`) falls through to `ScenarioCompiler.Compile()` in-process
   - Any in-process regeneration is cached in memory for the process lifetime; it is NOT written back to `generated/` (that would diverge from the seeded build artifact)
   - Logs `"Manually generated scenario for {category}/{index}"` on fallback (mirrors `"Manually baked animations for ..."`)
2. **ScenarioSelector.cs** — Pick scenario by cluster size
   - Input: NPC count in generated cluster
   - Output: Scenario with matching size category
   - Strategy: Round-robin or random within category (seeded)
3. **ScenarioCache.cs** — Cache loaded scenarios (LRU if needed)
4. **Integration point**: TaleManager initialization
   - `TaleManager.InitializeCluster(clusterIndex, spatialModel, npcPool)`
   - Calls `ScenarioSelector.PickScenario(npcPool.Count)` → `ScenarioLibrary.TryGetScenario(...)`

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

