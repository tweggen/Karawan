# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Karawan is a C# game engine ("Joyce") and game ("Silicon Desert 2") targeting .NET 9.0. It runs on Windows, Linux, macOS, and Android.

## Quick Start (For New Instances)

**First, read these in order:**
1. `PROCESS.md` — Generic development workflow, mandatory documentation updates
2. `PROCESS_TALE.md` — TALE-specific workflow and test commands
3. `docs/TESTING/` — Testing infrastructure (see `TESTING_STRATEGY.md`)
4. `PROCESS_DOCS.md` — Documentation organization guidelines

**Current Status (as of August 14, 2026):**
- ✅ **Models are baked; FBX import is build-time only (Platform Phase 4, 2026-08-14)**: Chushi writes one `mo-{hash}` per declared model and the game deserialises that, so **`libassimp.so` is no longer in the APK** and no runtime project references Assimp. The importer moved to a new **`JoyceFbx`** project referenced by `Mazu`/`Chushi` and the tests only — putting it back in `Joyce` would re-add Assimp to every shipped target transitively, which is what used to happen. `ModelCache.FbxLoader` is the seam Chushi installs; in the game it is null and an unbaked fbx raises instead of silently working. Models are declared `"type": "model"` in `models/nogame.resources.json` **with their load properties** (`"modelProperties": { "Scale": "1" }`) because those are part of the bake identity — significance is decided by exclusion, so an unknown property forces a re-bake rather than silently reusing a stale file. The 12 animation-source fbx are `"type": "animationSource"` and ship nothing; their output is the existing `ac-{hash}`. **Bone order is the trap** (`AllBakedMatrices` is indexed `frame * NBones + boneIndex`; a reorder renders a foreign pose without crashing) — asserted per bone in `tests/JoyceCode.Tests/engine/joyce/BakedModelEquivalenceTests.cs`, which loads each model both ways and compares. Hash derivation is duplicated in `Tooling/Cmdline/GameConfig.ModelFileName` — **change both together**. Details in `docs/SYSTEMS/BUILD/PIPELINE.md` § *Models are baked*.
- ✅ **TALE storylets now ship and load on Android / installed builds (fixed 2026-08-03)**: `StoryletLibrary.LoadFromDirectory` enumerated `models/tale/*.json` off the filesystem, so nothing declared those 14 files as resources and neither `AndroidResources.xml` nor `InnoResources.iss` carried a single one (the 11 `tale/conversations/*.json` narration scripts *are* packaged — `nogame.narration.json` `__include__`s them, which is a different mechanism). On Android the model tree only exists inside the APK, so `Directory.Exists("./tale")` was false, `TaleModule.OnModuleActivate` returned before creating the manager, and `TaleModule.TaleManager` stayed **null while the module still reported itself active** — `Scene.cs` handed that null to `TaleSpawnOperator`, which crashed at the first per-fragment spawn poll (`_ensureClusterPopulated`). Fix: the 14 storylets are declared in `models/nogame.resources.json` with `"type": "taleStorylet"`; `TaleModule._collectStoryletTags` filters `/resources/list` on that same type to learn what to open and `StoryletLibrary.LoadFromAssets` reads them via `engine.Assets.Open`, so what ships and what loads are one declaration. Verified end-to-end on desktop (96 storylets from 14 resources) and in the staged APK assets. **`Directory`-based loading is now build-tool/test-harness only** (`ScenarioCompiler`, `Testbed`, `TestRunner`); game-runtime code must go through `engine.Assets`. Note `Wuka.csproj` `<Import>`s the manifest at project-evaluation time, so adding an asset needs **two** builds before it stages. Guards added so this can never present as a null-deref again: `TaleModule` logs an `Error` instead of a `Trace` when it disables itself, `Scene.cs` skips the spawn operator when `TaleManager` is null, `TaleSpawnOperator`'s ctor throws `ArgumentNullException`. Drift tests: `tests/JoyceCode.Tests/engine/tale/TaleStoryletResourceTests.cs`; details in `docs/SYSTEMS/BUILD/PIPELINE.md`.
- ✅ **TALE NPCs no longer stand in the middle of junctions (fixed 2026-08-01)**: `SpatialModel.ExtractFrom` creates one `street_segment` Location per `StreetPoint` — i.e. per **junction node**, positioned at the junction center. 26 storylets use street locations (`random_street`/`street_segment`), and drifter/socialite/hustler roles even get homes/workplaces assigned there, so NPCs walked to and idled at junction centers for whole schedule blocks. The old `_snapToPedestrianLane` mitigation projected the center onto the nearest pedestrian NavLane — at a junction that's almost always a **crossing lane**, whose interior is also in the roadway; with no NavCluster available the raw center was used. Fix: street-point locations now compute their standing points via `SpatialModel._computeStreetEntryCandidates` — ALL distinct pedestrian lane **endpoints** (NavJunctions = sidewalk corners) around the junction (nearest + 15 m tolerance, max 6, deduped on 0.5 m grid), never a lane-interior projection; without nav data, fall back to `StreetPoint.GetSectionArray()` (geometric sidewalk corners, already offset by half street width) each nudged 1 m outward. `Location.EntryCandidates` holds the corners; `Location.EntryPositionFor(npcId)` distributes NPCs bound to the same junction across them (used by `NpcSchedule.PositionAt`, `TaleEntityStrategy` travel destinations, `TaleSpawnOperator` transit sampling) so crowds don't stack on one corner. `Location.Position` stays at the junction center (route graph/travel-time metrics unchanged) — only the physical standing points move. Building/shop entry snapping unchanged, but all lane scans now go through `NavClusterContent.GetLanesNear` (octree-backed after `Recompile()`, full-list fallback before). `ValidateReachability` uses the endpoint-based check for `street_segment` locations so a crossing interior near a geometric corner can't flip unreachable→reachable. `Joyce.csproj` now declares `InternalsVisibleTo("JoyceCode.Tests")`; regression tests in `tests/JoyceCode.Tests/engine/tale/StreetEntryPositionTests.cs`.
- ✅ **Android build: missing `ac-{hash}` assets (fixed 2026-08-02)**: `Tooling/Cmdline/GameConfig.LoadAnimation` still read the pre-packs flat `animationUrls` field, so the generated `AndroidResources.xml` / `InnoResources.iss` listed one `ac-` entry per **model** (hashed from the model file name alone) while Chushi bakes one per **(model, pack)** pair — 12 phantom names vs. 14 real files, disjoint sets. Desktop shrugged it off (re-bakes on demand); the Wuka build failed with `Quelldatei "../nogame/generated/ac-…~" wurde nicht gefunden` for every entry. `LoadAnimation` now expands `packs` (returns `List<Resource>`, legacy `animationUrls` still supported), mirroring `AAssetImplementation._whenLoadedAnimations`. Generated-name derivation is duplicated between `Tooling/Cmdline` and `JoyceCode` on purpose (no project reference possible) — **change both together**; see `docs/SYSTEMS/BUILD/PIPELINE.md` for the verification one-liner.
- ✅ **Skeletal animation frame indexing (fixed 2026-07-20)**: `AnimationBatch.FrameNos` now always carries the **global** baked frame number (`ModelAnimation.GetGlobalFrame` = `FirstFrame + clamped local frame`), never the frame counted within its own clip. This is the contract every renderer strategy depends on: the SSBO shader consumes it directly as the `instanceFrameno` vertex attribute (`models/shaders/LIghtingVS.vert:136` computes `instanceFrameno * nBones + boneId` with no `FirstFrame` term), while the UBO/uniform paths slice `AllBakedMatrices` with `FrameNos[0]`. Previously `MeshBatch.Add` stored the local frame, so under the SSBO strategy — the live one on OpenGL ≥ 4.3 — **every clip read from the start of `AllBakedMatrices`**, which holds whichever animation sorts first by name (`Death_FallForwards` in all current packs). Symptom: walking characters played the fall animation; idle alternated between falling and standing as its frame counter crossed out of `Death`'s range. Note `BakeAnimations` assigns `FirstFrame` while iterating a `SortedDictionary`, so the physical layout is **alphabetical**, not the URL order in `nogame.animations.json`. Related: `AnimBatching` now follows `IThreeD.HasPerInstanceAnimationFrames` rather than the graphics API (macOS runs GL 4.1 → UBO strategy → needs batches split per animation+frame, despite being "OpenGL"); `ModelAnimationCollection.ValidateBakedLayout` asserts offsets tile the matrix array on every model load; `tests/JoyceCode.Tests/engine/joyce/BakedAnimationLayoutTests.cs` asserts the same for the baked `ac-*` files on disk.
- ✅ **Parallel Test Runner (2026-07-14)**: `./run_tests_parallel.sh` runs the TALE suite across all CPU cores (standard tier ~5 min → ~28 s, identical pass sets). Same filters as `run_tests.sh`; knobs: `JOYCE_TEST_JOBS`, `TEST_TIMEOUT`, `JOYCE_TEST_TIMEOUT_SCALE`, `JOYCE_TEST_SLEEP_SCALE` (in-script sleeps default to 0.1×), `JOYCE_TURBO=1` (opt-in engine free-run, fixed dt=1/60). TestRunner now waits on `TestDriverModule.SessionReady`/`Completed` events instead of fixed sleeps. See `docs/TESTING/TIERS.md` § Parallel Runner.
- ✅ **Animation Packs System**: Callers now request animations by pack name ("locomotion", "full", "locomotion_hardday") rather than hardcoding animation URLs. Pack registry (`AnimationPackRegistry`) resolves pack names to URLs at runtime. Enables cache sharing: player and citizen NPCs using the same model now share one baked animation file, one `Model` object, and one GPU SSBO. JSON format changed: `animations.json` now uses `packs` dict per model instead of flat `animationUrls`. Baking pipeline unchanged — one `ac-{hash}` per pack. All 46 tests passing.
- ✅ Phase 0-7 + Phase 7B + Phase 8 TALE systems fully implemented
- ✅ Phase C1 (NPC Conversation Infrastructure): Behavior, bindings, script resolution complete
- ✅ Phase C2 (Storylet-Specific Dialogue): Explicit override + tag-based fallback implemented
- ✅ Phase C3 (Mood/Tone Branches): Role-specific dialogue via npcMood(), npcWealthLabel(), npcRole() functions
- ✅ Phase C4 (Trust, Memory & Quest Hooks): Trust tracking, player memory via fact flags (readable as `props.npc.player_fact.*` in scripts), decay-based persistence (NPCs preserved as Tier-2 only if conversed-with within `TaleManager.ConversationMemorySeconds` = 30 min wall-clock; otherwise schedule dropped via `ForgetSchedule` and slot re-randomized). `IsNoticedByPlayer` retired — only conversation/`tale.npc.remember` triggers persistence. `TaleManager.IsScheduleAlive(npcId)` available for quest-code guards.
- ✅ TALE-SOCIAL Phase D1 (Scenario Pre-Computation): Chushi bakes 25 `sc-{hash}` social-structure files into `nogame/generated/`, listed in `AndroidResources.xml` / `InnoResources.iss` alongside `ac-{hash}` animations.
- ✅ TALE-SOCIAL Phase D2 (Scenario Library + Selector): `engine.tale.bake.ScenarioLibrary` is registered in `TaleModule` as a lazy singleton; `TryGet(category, index)` probes the baked file via `engine.Assets.Open` and falls through to `ScenarioCompiler.CompileInMemory()` on miss / parse error / `joyce.DisablePrebakedScenarios=true` — exact mirror of `Model.BakeAnimations` at `JoyceCode/engine/joyce/Model.cs:207-237`. `ScenarioSelector.Pick(npcCount, clusterSeed)` picks (category, index) by closest median NPC count + seeded round-robin. Both are lazy: nothing is loaded until the first `I.Get<>` request.
- ✅ TALE-SOCIAL Phase D3 (Scenario Application): `engine.tale.bake.ScenarioApplicator.Apply(scenario, realNpcs)` re-attaches a baked scenario's groups, trust edges and post-365-day property snapshot onto a freshly populated cluster. Matching is two-step: bucket both populations by role, sort each bucket by (wealth desc, morality desc, NpcId asc), then pair positionally — the rank-to-real-NpcId map carries scenario.Groups and scenario.Relationships across to the real NPCs without the scenario needing to know anything about cluster geometry. Per-NPC Trust dicts get populated directly (TaleManager has no global RelationshipTracker at runtime). Wired into `TaleManager.PopulateCluster` AFTER the warmup advance loops, so the warmup desynchronizes schedule positions and the scenario then snaps everyone into their settled social state.
- ✅ TALE-SOCIAL Phase D4 (Seedability validation): wired up `tests/JoyceCode.Tests/JoyceCode.Tests.csproj` (xUnit, references Joyce.csproj). All 46 tests pass in 168 ms — `dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj`. The compiler determinism test runs `CompileInMemory` twice with the same seed and asserts byte-for-byte equality — the core seedability assertion. Pre-existing dead test files under `engine/navigation/` and `engine/streets/` are excluded from compilation pending API updates / DI fixture work; csproj has explanatory comments.
- ✅ TALE-SOCIAL Phase D5 (Statistics + tuning observations): Chushi pass after the bake loop walks each `sc-{hash}` file, computes per-scenario statistics (group counts, relationship density, role distribution, per-property mean/stdev/floor/ceiling fractions), aggregates by category, and writes `nogame/generated/scenario-statistics.json` (~55 KB indented JSON). Five concrete tuning concerns surfaced — most importantly **`GroupDetector.MaxCliques = 500` is binding for the large category** (all 12 large scenarios hit exactly 500 groups, zero stdev) and **`fear` is dead across all 25 scenarios** (`mean=0.000, fractionAtFloor=1.00`). NOT auto-fixed — each is a judgment call. See `docs/tale/phases/PHASE_D_SOCIAL.md` for the full design doc and the actionable tuning list.
- ✅ 192 regression tests passing (29 C-phase tests, 60-day simulations, ~5 min)
- ✅ Recalibration test framework ready (365+ days, ~2-4 hours)
- ✅ Configuration-driven roles, interactions, relationship tiers, group types
- ✅ Building role tagging with geometric attribute intensity zones (Phase 7B)
- ✅ Occupation-based character model assignment (Phase 8): roles define curated model pools in JSON config
- ✅ NavMesh street pathfinding working (Phase 7C deadlock fixed, routing Phase D fixes in place)
- ✅ Critical pathfinding bugs fixed:
  - Fallback storylet safety check (2026-03-28)
  - Same-junction pathfinding fallback using closest lanes (2026-03-28)
- ✅ PROCESS.md and documentation audit cycle in place
- 🔄 **Debug Filter Migration (Developer Infrastructure)**: Category-based selective debug output system. 307/571 (~54%) logger calls migrated across 40+ files. Implemented `DebugFilter` static class with 21 categories (Dc enum), `InterpolatedStringHandler` for zero-overhead when disabled. Per-file pattern: `private static readonly engine.Dc _dc = engine.Dc.{Category}` + `Trace(_dc, $"...")` calls. Remaining: ~264 calls across Tools, scattered utilities, testing/world/quest systems. Build verified: 0 errors.
- 🔄 Routing Phase D D2: Multi-objective A* integration pending
- 🔄 Routing Phase D D4: Behavioral variety (role-based preferences) pending
- 🔄 **TALE-SOCIAL Phase E (Living Factions)**: in progress — plan in `docs/roadmap/proposed/IMPLEMENTATION-PLAN-TALE-SOCIAL-PHASE-E.md`. Commit 1 done (2026-07-13): fixed all-groups-classify-as-"social" bake bug (Chushi/TestRunner registered an *empty* `GroupTypeRegistry`; now populated mirroring `models/nogame.group-types.json`). Commit 2 done (2026-07-13): `engine.tale.CommunityDetector` (deterministic label propagation + mutual top-K sparsification, `MaxEdgesPerNode=6`, `TrustThreshold=0.6`) replaces `GroupDetector` cliques in `DesSimulation`; baked scenarios now have 1–14 **disjoint** communities (was 56–500 overlapping cliques), membership ratio 0.74–0.80 (was 1.00); `fear` bootstrapped per role in both property generators (appended draw — existing per-seed values unchanged). Commit 3 done (2026-07-13): runtime group table + social state (E2) — `engine.tale.ClusterSocialState` (`RuntimeGroup` with type/name/roster, per-NPC `NpcSocialSnapshot`); `ScenarioApplicator.ApplyResult.Groups` carries rosters translated to real NpcIds; `TaleManager` seeds the table in `PopulateCluster`, snapshots all social state in `DepopulateCluster` and restores it on repopulate (snapshot wins over bake — social evolution survives leaving the cluster); accessors `GetGroup`/`GetSocialState`/`GetGroupmates`; deterministic faction names via `nogame.modules.tale.GroupNameGenerator` (`TaleManager.GroupNameProvider` hook); new `Dc.TaleSocial` debug category. Commit 4 done (2026-07-13): runtime social evolution tick (E3) — `engine.tale.RuntimeSocialEvolver`: every 10 game-minutes (12.5 real s) one populated cluster (round-robin) gets an encounter tick (co-located NPCs meet with the bake's probability model `P=1-(1-p_loc)^(window/15min)`, trust deltas via `InteractionTypeRegistry`, daily pair dedup, ≤12 pairs/location, homes excluded); every 2 game-hours per cluster the community structure is re-detected from the evolved trust graph and reconciled with the group table (≥50% member overlap keeps group id+name, new communities get fresh ids/names, dead groups dissolve, `Authored` groups always survive). Encounter RNG seeded from (cluster, day, tick) with a fixed integer mix — deterministic, never touches storylet RNG. Wired via `TaleModule._onSocialFrame` on `OnLogicalFrame`. Commit 5 done (2026-07-13): storylet group verbs (E4) — `"postconditions": { "group": "form" | "join" | "leave" }` extracted at parse time into `StoryletDefinition.GroupAction` (never float.Parsed); new `not_in_group` precondition; `engine.tale.GroupActions` executes verbs deterministically (form: founder + up to 4 highest-trust ungrouped neighbors ≥0.35 mutual trust, needs ≥2, group is `Authored`; join: adopt most-trusted member's group ≥0.3; leave: dissolve below 2 members) — shared by `TaleManager.AdvanceNpc` (under `_loSocial`) and `DesSimulation.ProcessNodeArrival` (sim-local table, ids from 100000, finally emits `gang_formed`). `form_gang` circularity fixed in escalation.json (`in_group` → `not_in_group` + `"group": "form"`); new `leave_gang` storylet gives factions churn. Commit 6 done (2026-07-13): player-visible factions (E5) — narration props `npc.has_group/group_type/group_name/group_size` + `npcGroupType()`/`npcGroupName()` functions; faction dialogue branches in all 5 role conversation scripts + tale.generic (drifter criminal / authority patrol_unit / merchant trade route before mood; worker/socialite social after mood); groupmate co-location via `TaleManager.ResolveSocialVenue` hangout bias (`GetGroupHangout`: modal first venue among members, cached on `RuntimeGroup.HangoutLocationId`). Commit 7 done (2026-07-13): save-game persistence (E6) — additive `GameState.TaleSocialState` (old saves compatible); `ClusterSocialState.ToJson/FromJson`; `TaleModule` save hook snapshots populated clusters first (`TaleManager.SnapshotPopulatedClusters`) then serializes all social states; load hook installs them via `ReplaceSocialState` and the PopulateCluster overlay restores onto regenerated schedules. **Phase E complete** — factions now form (bake + storylet verbs), evolve (encounter tick + re-detection), persist (snapshots + save), and are visible (faction dialogue + shared hangouts). NOTE: model JSON under models/tale/ is a runtime input to the TALE test suite AND the scenario bake — never edit while a gate runs, and re-bake (publish Chushi first) after storylet changes.
- 🔄 TALE-SOCIAL Phase D follow-up: act on the five D5 tuning concerns documented in `docs/tale/phases/PHASE_D_SOCIAL.md` (MaxCliques cap, group membership ratio, relationship density, property saturation, fear=0) — being absorbed into Phase E
- ⚠️ Note: "Phase D" is overloaded — routing Phase D and TALE-SOCIAL Phase D are separate workstreams
- ⚠️ Watch for JSON deserialization issues (case-sensitive, see TaleModule.cs)

**Common First Tasks:**
- **Adding a test**: Create JSON in `models/tests/tale/phaseN-*/`, update `docs/tale/PHASE_N.md`, run `./run_tests.sh phaseN`
- **Tuning parameters**: Run `./run_recalibration_tests.sh phaseN` with `TALE_SIM_DAYS=365`
- **New phase**: Use `EnterPlanMode`, create plan in `docs/roadmap/proposed/`, follow PROCESS.md
- **Debugging**: Check `docs/tale/PHASE_N.md` for design, read actual test JSON for specs

**Key Rules (from PROCESS.md):**
- Documentation updates are MANDATORY (not optional)
- Always run `./run_tests.sh all` before commit
- Search for all references when changing systems
- Keep JSON config case-insensitive in mind (use `PropertyNameCaseInsensitive = true`)
- **Debug output pattern (mandatory)**: All debug/trace calls must use category-based filtering: add `private static readonly engine.Dc _dc = engine.Dc.{Category};` to class, then use `Trace(_dc, $"...")` instead of plain `Trace($"...")`

## Build & Run

**Prerequisites:** Check out these repos as siblings to the Karawan directory:
- `BepuPhysics2` (github.com/TimosForks/bepuphysics2)
- `DefaultEcs` (github.com/TimosForks/DefaultEcs)
- `ObjLoader` (github.com/TimosForks/ObjLoader)
- `glTF-CSharp-Loader` (github.com/KhronosGroup/glTF-CSharp-Loader)
- `ink` (github.com/TimosForks/ink)

```bash
# Build everything
dotnet build Karawan.sln

# Run desktop app
dotnet run --project Karawan/Karawan.csproj

# Run the minimal grid example
dotnet run --project examples/Launcher/Karawan.GenericLauncher.csproj
```

No test suite exists in this repository.

**Build notes:**
- The `nogame/generated/` directory is auto-created by an `EnsureGeneratedDirectory` MSBuild target before asset compilation. If you see build errors about missing generated files, verify this target runs first.
- Build pipeline order in `nogame.csproj`: `EnsureGeneratedDirectory` → `GatherTexturesHost` (texture packer) → `CompileAssetsHost` (Chushi) → `GatherResources` (resource compiler) → `Compile`. Chushi reads the packed atlas JSON files (`atlas-*.json`) during its texture-loading pass, so the texture packer must run first; this is enforced by `CompileAssetsHost`'s `DependsOnTargets="GatherTexturesHost"`. Without it, a fresh clone fails because Chushi has no atlas to open.

## Architecture

### ECS Foundation
The engine uses **DefaultEcs** (Entity-Component-System). Entities are composed of components; systems process entities matching component queries. Hierarchy (parent-child) is handled via Hierarchy and Transform components on entities.

### Project Structure (key projects)

| Project | Role |
|---------|------|
| **Joyce** | Core engine library: ECS, scene management, transforms, modules, physics, assets, serialization |
| **JoyceCode** (.shproj) | Engine builtins: components, systems, controllers, UI, map system, inventory, loaders (FBX/OBJ/glTF), behaviours |
| **Splash** | Abstract renderer (platform-agnostic mesh/material/texture interfaces) |
| **Splash.Silk** | OpenGL renderer via Silk.NET |
| **Boom** / **Boom.OpenAL** | Audio framework and OpenAL implementation |
| **BoomCode** (.shproj) | Shared audio code |
| **nogame** + **nogameCode** (.shproj) | Game-specific logic for Silicon Desert 2 |
| **Karawan** | Desktop launcher (`DesktopMain.cs`) |
| **Wuka** | Android MAUI app (packages nogame + native libs) |
| **Aihao** | Avalonia-based game editor IDE |
| **Chushi** | Asset compiler (console tool, also used as MSBuild task) |
| **Mazu** | Animation compiler |
| **Tooling/Cmdline** | CLI utilities (texture packing, resource compilation) |

Shared projects (`.shproj`) are compiled into each referencing assembly — they are not standalone DLLs.

### Configuration System (Mix)
Game configuration is JSON-based and composable. The root is `models/nogame.json` which references satellite files (`nogame.modules.json`, `nogame.implementations.json`, `nogame.resources.json`, etc.). The Mix system merges these at runtime. Key config paths:
- `/implementations` — factory/DI bindings (className + properties)
- `/modules/root/className` — main game module class
- `/mapProviders` — world map generation providers
- `/metaGen` — procedural generation operators (fragment, building, populating, cluster)
- `/scenes/catalogue` and `/scenes/startup` — scene definitions
- `/properties` — runtime-configurable values with change subscriptions
- `/quests` — quest definitions

### World Generation Pipeline
The world is built by a hierarchy of **operators**:
1. **WorldOperator** — applied to the entire world in sequence
2. **ClusterOperator** — applied to each cluster on creation
3. **FragmentOperator** — applied to each fragment on (re-)load

Everything is designed to be re-creatable on demand.

### Entity Lifecycle
Entities track a **Creator** (can serialize/deserialize) and an **Owner** (controls lifetime). Components use `[Persistable]` attribute for serialization. Save/load hooks via the Saver module (`OnBeforeSaveGame` / `OnAfterLoadGame`).

### Rendering (Splash)
Geometry is broken into **InstanceDesc** objects (mesh + materials). The renderer batches identical InstanceDescs for instanced draw calls. Platform primitives (`AMeshEntry`, `AMaterialEntry`, `ATextureEntry`) follow create → fill → upload → unload → dispose lifecycle. OpenGL version: 4.1 on macOS, 4.3 on Windows/Linux.

### Input Pipeline
Platform events → logical translation → event queue → `InputEventPipeline` (distributes by priority) → `InputController` (maps to game controller state). Higher-priority listeners consume events before the standard controller.

### Game Assembly Loading
The launcher loads game DLLs dynamically based on `game.launch.json` (`/defaults/loader/assembly`). This allows different games to run on the same engine.

### Quest System
Quests are pure ECS entities with `QuestInfo` and `Strategy` components. The old `IQuest`/`quest.Manager` system has been fully removed (Phase 5 complete). `QuestFactory` creates/activates/deactivates quest entities. Strategy-based quests use `AOneOfStrategy` for multi-phase state machines (e.g., taxi quest has pickup → driving phases). `QuestDeactivatedEvent` carries `Title` and `IsSuccess` for completion feedback. The Quest Log UI is accessible from the pause menu and supports Follow/Unfollow per quest (Phase 6+7 complete). See `QUEST_REFACTOR.md` for full migration history.

#### Followed Quest (Phase 7)
At most one active quest is the "followed" quest — only it renders its goal marker and satnav route. `SatnavService` is the central singleton managing this:
- Auto-follows the first triggered quest; auto-advances to the next when a followed quest completes
- `FollowedQuestId` persisted in `GameState` and restored on load
- Fires `QuestFollowedEvent` / `QuestUnfollowedEvent` (Code = questId)
- `ToSomewhere.OwnerQuestEntity` — set on all quest navigation targets; when set, marker and route are only created/shown while the owning quest is followed. Unset = legacy behavior (always shown).
- Quest Log UI shows Follow/Unfollow buttons per active quest (uses the newly-implemented `<if test='...'>` JT XML element)

Key classes:
- `QuestFactory` (`JoyceCode/engine/quest/QuestFactory.cs`) — quest lifecycle management (register, trigger, deactivate)
- `ISatnavService` / `SatnavService` (`JoyceCode/engine/quest/ISatnavService.cs`, `nogameCode/nogame/quest/SatnavService.cs`) — followed quest tracking, auto-follow, persistence; registered as `engine.quest.ISatnavService` in `nogame.implementations.json`
- `ToSomewhere` (`JoyceCode/engine/quest/ToSomewhere.cs`) — base module for navigation-based quest targets; set `OwnerQuestEntity` to opt into followed-quest visibility control
- `NarrationBindings` (`nogameCode/nogame/modules/story/NarrationBindings.cs`) — quest factory registrations, narration event wiring, and early `ISatnavService` initialization
- `QuestLuaBindings` (`nogameCode/nogame/quests/QuestLuaBindings.cs`) — Lua bindings: `getQuestList()` (includes `followed` field), `followQuest(id)`, `unfollowQuest()`, `isFollowed(id)`
- `ICreator` implementations — save/load quest state via `TaxiQuestData` etc.

### Placement System
`Placer` (`JoyceCode/engine/Placer.cs`) places entities in the world using `PlacementDescription` constraints:
- `MinDistance`/`MaxDistance` — horizontal distance filtering from `PlacementContext.CurrentPosition`
- `MaxAttempts` — retry loop for distance-constrained placement

### ForceSpawn API
`SpawnController.ForceSpawn(Type behaviorType, Vector3 position)` spawns a full-lifecycle character at a specific position:
- Looks up `ISpawnOperator` by behavior type
- Calls `ISpawnOperator.SpawnCharacterAt(Vector3)` (default interface method)
- Citizen implementation finds cluster/quarter/streetpoint, builds `PositionDescription`, creates entity with full Walk→Flee→Recover strategy

### Citizen Collision Routing
NPC `OnCollision` is routed through `nogame.characters.citizen.CitizenCollisionRouter` — a single static dispatcher used by `WalkBehavior`, `IdleBehavior`, `RecoverBehavior`, `TaleWalkBehavior`, `TaleConversationBehavior`. The router classifies the contact by `SolidLayerMask` and publishes one of three events on the NPC entity's event path:
- `EntityStrategy.HitEventPath` — `AnyWeapon` contact (player melee or NPC weapon) → `FleeStrategy`.
- `EntityStrategy.CrashEventPath` — `AnyVehicle` contact → `RecoverStrategy` (death animation).
- `EntityStrategy.BumpEventPath` — pure `PlayerCharacter` body contact (no weapon, no vehicle) → walking behaviors apply a transient lateral offset on `SegmentNavigator.ApplyLateralBump` (0.4 m, 400 ms linear decay, capped at 0.6 m accumulated) so the NPC steps out of the player's way without flee or collapse. Only the two walking behaviors subscribe to bump; idle/conversation/recover NPCs simply block the player.

TALE sites (`TaleWalkBehavior`, `TaleConversationBehavior`) keep their custom conversation-cancel side effect inline before calling `Dispatch`, using the router's `IsWeapon` / `IsVehicle` helpers for the classification.

### Aihao Editor IDE

Aihao is an Avalonia 11-based game editor built with **CommunityToolkit.Mvvm** and **Dock.Avalonia** for a dockable panel layout.

#### Tech Stack
- **UI**: Avalonia 11.3.8 (cross-platform desktop)
- **Layout**: Dock.Avalonia (tool windows + document tabs)
- **MVVM**: CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`)
- **JSON**: System.Text.Json throughout

#### JSON Loading & Storage
The editor uses **Mix** (from JoyceCode) as its single source of truth. `EditorFileProvider` (implements `IMixFileProvider`) gives Mix direct filesystem access instead of the engine's asset system. Loading flow:

1. `ProjectService.LoadProjectAsync()` creates a Mix instance with `EditorFileProvider`
2. Root JSON is loaded at `/` with priority 0; `__include__` files are discovered and tracked
3. `AihaoProject` wraps the Mix instance and exposes `GetSection(sectionId)` → `JsonNode`
4. Overlays can be added at higher priority via `AddOverlayAsync()` for debug/override configs

Saving reverses the flow: `ViewModel.ToJsonObject()` → serialize → write to disk via `ProjectService.SaveFileAsync()`.

#### Editor Architecture

Each config section (globalSettings, properties, resources, implementations, metaGen) has:
- A **DocumentViewModel** (dockable tab) that owns a section-specific editor VM
- A **section editor ViewModel** that typically wraps `JsonPropertyEditorViewModel`
- An **AXAML View** mapped via `DataTemplate` in MainWindow

The generic `JsonPropertyEditorViewModel` + `PropertyNodeViewModel` provide recursive JSON tree editing. `PropertyNodeViewModel` represents any JSON node with:
- `Name`, `Value`, `ValueKind` (String/Number/Boolean/Null/Object/Array)
- `Children` (ObservableCollection for objects/arrays)
- `IsModified` dirty tracking with callback propagation to parent
- `ToJsonNode()` / `FromJsonNode()` for round-trip serialization
- Auto-detected special editors (resolution, vector2/3, color, slider) based on key patterns and value format

#### Change Flow
```
UI TextBox → Binding → PropertyNodeViewModel.Value setter
  → Validate() → MarkModified() → _onModified callback
  → JsonPropertyEditorViewModel.IsDirty = true
  → Document tab shows dirty indicator
  → Save: ToJsonObject() → ProjectService.SaveFileAsync()
```

#### Docking Layout
- **Left pane**: Project tree (tool window)
- **Center**: Document tabs (section editors, render output)
- **Right pane**: Inspector (tool window)
- **Bottom**: Console with level filtering and search
- `AihaoDockFactory` builds the layout; `DockingService` manages registration

#### Key Services
- `ProjectService` — load/save/reload projects, overlay management
- `ProcessService` — build/run/debug game, IDE detection (Rider/VS/VS Code)
- `ActionService` — command registry with keybinding overrides
- `UserSettingsService` — persists preferences to `~/.aihao/settings.json`

#### Patterns to Follow When Adding Editors
1. Create a `FooEditorViewModel : ObservableObject` with load/save methods operating on `JsonNode`
2. Create a `FooDocumentViewModel : DocumentViewModel` wrapping the editor VM
3. Create a `FooEditor.axaml` view with bindings
4. Create a `FooDocumentView.axaml` hosting the editor view
5. Register the DataTemplate mapping in `MainWindow.axaml`
6. Register the document type in `AihaoDockFactory`
7. Add an open action in `MainWindowViewModel` + `BuiltInActions`
