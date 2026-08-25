# Docs Reorganization — Review

Generated 2026-07-21 by the `docs-domain-triage` workflow 
(75 agents; 96 docs classified, 43 verified against the codebase).

**Nothing has been moved.** Read this, then run:

```bash
bash docs/_migration/move.sh
python docs/_migration/apply-status-headers.py
python docs/_migration/fix-links.py
```

## Domain roster

Decide this first — if the roster is wrong, every path below is wrong.

| Domain | Docs | Rationale |
|---|---:|---|
| `tale` | 53 | The TALE narrative system (storylets, DES simulation, population, conversations, social/factions) dominates the tree: 53 docs including ~12 plans. Absorbs the free-text labels tale, tale-social, tale-testing, narration, and simulation — narration docs describe TALE dialogue scripts, and TIMING_RECOMMENDATIONS/ANALYSIS_NPCs are TALE-behavior investigations. tale-social stays inside tale rather than splitting: its phase labels (D_SOCIAL, E_SOCIAL) are already self-disambiguating once routing's Phase D leaves for the navigation domain. |
| `navigation` | 13 | Merges the near-synonyms navigation / routing / pathfinding into one label: transportation types, pipes/flow, temporal constraints, NavMesh routing, multi-objective A*. 13 docs, 8 of them plans. This split is what resolves the 'Phase D' collision — routing Phase D lands here and is renamed to MULTI-OBJECTIVE-ROUTING.md, leaving PHASE_D_SOCIAL.md unambiguous in tale. The three PHASE-x-COMPLETION reports come along so the pipes workstream is documented in one place. |
| `testing` | 7 | Test infrastructure that is not TALE-content-specific: the ExpectEngine harness, the tier model, the testbed architecture, and the runner guides. 7 docs with one plan (TIERS). Pulls EXPECT_ENGINE/EXPECT_IMPLEMENTATION out of SYSTEMS/NARRATION/ where they were misfiled — they describe a test harness, not narration. TALE per-phase test specs deliberately stay in tale/docs/tests/ because they are specs of TALE behavior, not of the harness. |
| `debug-infra` | 4 | Developer tooling: the DebugFilter/Dc category system and its multi-month migration. 4 docs, of which 2 are live plans (checklist + systematic migration) and one is shipped architecture. Meets the >=3 docs + >=1 plan bar and is the only domain with genuinely in-flight work, so it earns its own plans/todo. |
| `world-gen` | 3 | Procedural world generation: L-system engine, its Aihao editor, and fragment operators. 3 docs with 1 plan (bird swarm operator) — exactly at the bar. Kept separate from main because L-systems have their own expression language, JSON schema, and editor surface that nothing else in main touches. |
| `main` | 14 | Everything that did not clear the >=3 docs + >=1 plan bar, folded together: rendering (2 docs, 0 plans), animation (1), engine-core index (1), build (2), persistence (1), platforms (1), quest (1), documentation process (1). Also absorbs character-collision: it technically clears the numeric bar (3 docs, 3 plans) but is a single closed three-part fix with zero living reference material, so a standalone domain would be a folder of nothing but done plans. |

## Needs your decision (4 — excluded from move.sh)

### `docs/roadmap/proposed/BUILD-ASSET-DEPENDENCY-TRACKING.md`

plans/todo or plans/proposed? Nothing from it is built (no UpToDateCheckInput items, no stamp files, no Target Inputs/Outputs anywhere), so status is unambiguously not-started — but it fixes a real correctness bug (JSON/FBX edits do not trigger a re-bake), which reads more like agreed-and-queued work than an idea. It also sits at odds with itself: the WaitForExit timeout removal it argues for is already in both MSBuild tasks, so part of it may have quietly shipped.

- docs/main/plans/todo/BUILD-ASSET-DEPENDENCY-TRACKING.md (treat the stale-asset bug as committed work)
- docs/main/plans/proposed/BUILD-ASSET-DEPENDENCY-TRACKING.md (keep as-filed; nothing signals commitment)

### `docs/roadmap/proposed/TRAFFIC-LIGHTS-SYSTEM.md`

plans/todo or plans/proposed? The substrate shipped as part of navigation Phase B — ITemporalConstraint, CyclicConstraint with a 60 s default cycle, NavLane.Constraint/QueryConstraint, and routing-cost penalties for constrained lanes — but the feature itself did not: no constraint is ever attached to a lane in world generation, no movement executor stops an NPC at a red light, and the elevator/transit extensions have no implementation. Is this queued work whose foundation is deliberately in place, or a design sketch that happened to donate its data model to Phase B?

- docs/navigation/plans/todo/TRAFFIC-LIGHTS-SYSTEM.md, status partially-implemented
- docs/navigation/plans/proposed/TRAFFIC-LIGHTS-SYSTEM.md, status partially-implemented
- docs/navigation/docs/TEMPORAL-CONSTRAINTS.md — reframe as reference for the shipped constraint model and drop the unbuilt traffic-light framing

### `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-D-ROUTING.md`

plans/done or plans/todo? NpcGoal, RoutingPreferences, the A* cost multiplier and role-based goal assignment are all live, and RoutingPreferencesTests passes 10/10. But two of the four goals are inert: ComputeScenicMultiplier and ComputeSafetyMultiplier are placeholders returning 1.0f, and the tests assert that neutrality as expected. Since 'NPCs take visibly different routes by personality' is the plan's entire point, the missing pieces are arguably load-bearing. Three sources also disagree on this plan's status (PHASE_D.md says complete, PHASE-D-PROGRESS.md says in-progress, CLAUDE.md lists D2/D4 as pending while both are in code).

- docs/navigation/plans/done/PHASE-D-ROUTING.md, status partially-implemented
- docs/navigation/plans/todo/PHASE-D-ROUTING.md, status partially-implemented (scenic/safety scoring is the outstanding work)

### `docs/SYSTEMS/README.md`

Where does the systems index go once its subject is split across five domains? It indexes NARRATION (→ tale + testing), WORLD_GEN (→ its own domain), QUEST/PERSISTENCE/PLATFORMS/BUILD (→ main). Any single destination leaves it pointing across domain boundaries, and it may be better deleted and replaced by a top-level docs/README.md listing the new domain roster.

- docs/main/docs/SYSTEMS_OVERVIEW.md, rewritten to cover only main's subsystems
- docs/README.md as the new cross-domain landing page
- docs/main/archive/SYSTEMS_README.md — retire it; the domain folders are self-describing

## Suspicious — candidates for rewrite (15)

Body contradicts the evidence. **Not edited.** Filing is still handled by move.sh.

### HIGH

- **`docs/roadmap/PHASE-C-COMPLETION.md`** — Reports 'Test Results: 171/171 PASSED (100% success)' as validation of the pipe dynamics work. That table is verbatim identical (same date, same per-phase breakdown) to the one in PHASE-B-COMPLETION.md and refers to the TALE narrative JSON suite, which exercises none of this code. The tests that would actually validate Phase C — PipeSubdivisionTests, SpeedFunctionsTests, PipeControllerTests — are excluded from compilation at JoyceCode.Tests.csproj:41, whose own comment says they 'have never actually been compiled'. Worse, the csproj that could have run them did not exist until 2026-04-12, nearly three weeks after this report was written, so no version of the claim was ever true.

- **`docs/SYSTEMS/WORLD_GEN/LSYSTEM_EDITOR.md`** — Declares 'All Phases Complete!' with Phase 5 being a visual node-graph editor, and includes an ASCII diagram of draggable Seed/Rule/Macro nodes with connection lines. What shipped is a three-pane ListBox-plus-form editor: none of the named files exist (LSystemNodeViewModel.cs, LSystemConnectionViewModel.cs, LSystemNodeView.axaml), and grepping the Aihao ViewModels/Views for 'Canvas', 'NodeView' and 'Connection' returns nothing. The document's own progress log for Phase 5 describes the tree+form UI, so its heading and its body disagree with each other as well as with the code.

- **`docs/roadmap/PHASE-D-PROGRESS.md`** — Tracks D2 (multi-objective A* integration) and D4 (role-based behavioral variety) as outstanding, but both are in the tree: LocalPathfinder._childNode multiplies RoutingPreferences.ComputeCostMultiplier into A* edge cost (LocalPathfinder.cs:66,81-88) and TalePopulationGenerator.GenerateRoutingPreferences assigns goals per role (TalePopulationGenerator.cs:395-439). The contradiction has propagated: CLAUDE.md still lists both as pending, while docs/tale/phases/PHASE_D.md states 'IMPLEMENTATION COMPLETE' for the same work.

- **`docs/tale/TALE_ARCHITECTURE.md`** — Presents six subsystems as the TALE architecture; two have no counterpart in code under any name or shape. There is no World State Graph — per-NPC state is a flat Dictionary<string,float> property bag plus trust dictionaries. There is no Graph Rewriting Engine — 'rewriting' is StoryletSelector.ApplyPostconditions doing scalar +/-/= mutations on that dictionary. Two more exist only under other names (Selection Engine = StoryletSelector, Runtime Orchestrator = TaleManager), and 'Arc Rewrite Engine' is really ArcStack, an interrupt/resume stack that rewrites nothing. As the primary architecture reference for the largest system in the repo, this misleads about the fundamental data model.

### MEDIUM

- **`docs/tale/phases/PHASE_C.md`** — Carries claimedStatus not-started while all five named artifacts exist and 28 JSON tests across phaseC1-C4 exercise them. The same work is separately documented as complete in docs/roadmap/proposed/TALE-NPC-CONVERSATION-SYSTEM.md and in CLAUDE.md's status block. A reader taking this phase doc at face value would rebuild a shipped system.

- **`docs/tale/phases/PHASE_7B.md`** — Claims not-started while building role tagging is live (ClusterDesc.GetAttributeIntensity, Building.Tags, TalePopulationGenerator.AssignLocationByRole). It also specifies a GenerateBuildingRolesOperator cluster operator that was never written — the logic went inline into QuarterGenerator._determineBuildingRoles instead — so anyone searching for the operator will conclude nothing was built.

- **`docs/ANIMATION_ASSIMP_VERSION_COMPENSATION.md`** — Reads as an unstarted proposal, but AssimpVersion, AssimpVersionDetector and the version-gated chain correction in FbxModel are all present and wired. Two artifacts it names cannot be found under any spelling (NeedsComplexTransformationChain, BakedAnimationData), suggesting the doc was never reconciled with what was actually built. Separately, AssimpVersionDetector's own class comment claims reflection-based assembly-version detection while the implementation calls native GetVersionMajor/Minor/Patch.

- **`docs/tale/phases/PHASE_E_SOCIAL.md`** — States in-progress, but all six named artifacts exist, 44 xUnit tests pass across the six Phase E test files, and the commit log shows all seven planned commits landed including save-game persistence (E6). The corresponding plan is already filed in roadmap/done. The phase is complete; only this doc says otherwise.

- **`docs/roadmap/done/TALE-SOCIAL-PHASES.md`** — States 25 baked sc-{hash} scenarios (5 small + 8 medium + 12 large). models/nogame.scenarios.json configures 13, with the large category explicitly set to count=0 and commented as disabled because 600-NPC/365-day simulations are too expensive; the 13 files present locally match that config. The same 25 figure is repeated in CLAUDE.md and in docs/tale/phases/PHASE_D_SOCIAL.md, so the error is replicated three times. Note also that nogame/generated/ is git-ignored, so no scenario file count is a property of the repo at all.

- **`docs/tale/phases/PHASE_5.md`** — Publishes an ArcStack API that does not exist: PushInterrupt and PopAndResume return zero matches repo-wide, while the real methods are SetInterrupt/ClearInterrupt/Push/TryPop. InterruptCandidate does not exist at all — priority lives as an int field on StoryletDefinition with comparison logic inline in DesSimulation. It also states escalation.json has 15 storylets; it has 17. The sibling test doc docs/tale/tests/PHASE_5.md documents the correct method names, so the two disagree.

- **`docs/TESTING/TIERS.md`** — Names phase6-population/02-tier1-persistence.json as a smoke test; no file of that name exists anywhere, and the real smoke manifest picks 01-deterministic-generation.json for phase6. It also proposes a .github/workflows/test.yml CI integration — there is no .github directory in the repository and no CI of any kind, yet the doc reads as though the tier structure it describes is fully realized.

- **`docs/tale/TALE_IMPLEMENTATION_PLAN.md`** — Lists Phase 4 (Player) as 'Not started' while CLAUDE.md declares Phase 0-8 fully implemented. The evidence sides with this doc rather than CLAUDE.md — no PHASE_4_PLAYER design doc, no social_capital or overhear code, and the 20 phase4-player tests assert only generic DES events — so the contradiction is worth resolving in the other direction: CLAUDE.md's blanket 'Phase 0-8 complete' claim is the inaccurate one.

### LOW

- **`docs/ARCHITECTURE/README.md`** — Indexes RENDERING.md, PHYSICS.md, AUDIO.md, INPUT.md and ENGINE.md as the architecture documentation set. Only RENDERING.md exists — four of five entries are links to documents that were never written, making the index read as a claim of coverage the tree does not have.

- **`docs/tale/tests/QUICK_START.md`** — Headlines '171 test scripts total' and gives a per-phase breakdown. 171 was accurate before phaseC1-C4 (29) and phase7-spatial (102) were added; disk now holds 302 JSON files, the standard tier runs 200, and docs/TESTING/README.md says 192. Four numbers, none matching, across docs that are meant to be read together.

- **`docs/roadmap/done/DEBUG-FILTER-ARCHITECTURE.md`** — Describes 12 predefined Dc categories; DebugCategories.cs defines 27 plus a sentinel. It also locates the debug.category.* settings in nogame.globalSettings.json — that file exists but contains none of them; all 27 keys live in nogame.properties.json. DebugFilter.cs's own doc comment repeats the wrong filename, so the error is mirrored in the source.

## Cross-cutting issues (9)

### 'Phase D' names two unrelated workstreams and both currently live in docs/tale/phases/. PHASE_D.md is multi-objective routing; PHASE_D_SOCIAL.md is pre-computed social scenarios. They share a directory, a prefix, and a completion date range, and CLAUDE.md carries an explicit warning about the ambiguity. The domain split alone does not fix this — the routing doc must both move to navigation AND lose the phase label, which is why it is retargeted to docs/navigation/docs/MULTI-OBJECTIVE-ROUTING.md and the social plan to TALE-SOCIAL-PHASE-D-SCENARIOS.md.

- `docs/tale/phases/PHASE_D.md`
- `docs/tale/phases/PHASE_D_SOCIAL.md`
- `docs/roadmap/PHASE-D-PROGRESS.md`
- `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-D-ROUTING.md`
- `docs/roadmap/done/TALE-SOCIAL-PHASES.md`

**Recommendation:** Retire the 'Phase D' label on the routing side entirely: routing docs move to docs/navigation/ and are renamed by subject; social scenario docs keep a D_SOCIAL/PHASE-D-SCENARIOS name inside docs/tale/. After the move, no directory contains two 'Phase D' documents.

### The Phase B and Phase C navigation work is each documented twice: once as an implementation plan in roadmap/proposed/ and once as a completion report in roadmap/. A third doc, PIPES-FLOW-BASED-MOVEMENT.md, is the original design for the same pipes system. Three documents, one body of work, with the plans filed as 'proposed' despite the completion reports next door.

- `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-B-FLOW.md`
- `docs/roadmap/PHASE-B-COMPLETION.md`
- `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-C-DYNAMICS.md`
- `docs/roadmap/PHASE-C-COMPLETION.md`
- `docs/roadmap/proposed/PIPES-FLOW-BASED-MOVEMENT.md`

**Recommendation:** Keep the plans as the durable record in navigation/plans/done/, put the completion reports in navigation/archive/, and archive PIPES-FLOW-BASED-MOVEMENT as the superseded predecessor. Consider folding each completion report's verified-artifact list into its plan and deleting the report.

### The TALE conversation system (C1-C4) is documented in five places with conflicting status: an umbrella plan in proposed/ claiming complete, a C1 plan in proposed/ that is actually shipped, a C2 plan in done/ whose body says not-started, a phase doc claiming not-started, and a test spec. All describe the same TaleConversationBehavior/TaleNarrationBindings implementation.

- `docs/roadmap/proposed/TALE-NPC-CONVERSATION-SYSTEM.md`
- `docs/roadmap/proposed/PHASE-C1-INFRASTRUCTURE.md`
- `docs/roadmap/done/PHASE-C2-STORYLET-DIALOGUE.md`
- `docs/tale/phases/PHASE_C.md`
- `docs/tale/tests/PHASE_C.md`

**Recommendation:** All four plan-shaped docs move to docs/tale/plans/done/ so the status question disappears, and the test spec stays in docs/tale/docs/tests/. Then collapse the umbrella plan and the two sub-plans into one C1-C4 record, keeping PHASE_C as the design reference.

### Four different current test counts circulate across the docs: 171 (QUICK_START, TIERS, both navigation completion reports), 192 (TESTING/README, CLAUDE.md), 200 (what run_tests.sh's standard tier actually runs), 302 (JSON files on disk), plus 62 in RUN_TESTS_GUIDE and TALE_TEST_INFRASTRUCTURE and 273/212 in the two tale index docs. 171 is a genuine historical count (the seven pre-C phases summed exactly), so these are accreted snapshots rather than errors — but no reader can tell which is current.

- `docs/tale/tests/QUICK_START.md`
- `docs/TESTING/TIERS.md`
- `docs/TESTING/README.md`
- `docs/RUN_TESTS_GUIDE.md`
- `docs/TALE_TEST_INFRASTRUCTURE.md`
- `docs/tale/OVERVIEW.md`
- `docs/tale/README.md`
- `docs/roadmap/PHASE-B-COMPLETION.md`
- `docs/roadmap/PHASE-C-COMPLETION.md`

**Recommendation:** Stop quoting totals in prose. Have run_tests.sh print the count it discovers, cite that one number in TESTING_STRATEGY only, and strip counts from every other doc. Note that phase7-spatial (102 tests) is excluded from the standard tier, which is the main reason the numbers diverge.

### The debug-filter effort has two overlapping live trackers plus a stale survey: DEBUG-FILTER-MIGRATION-CHECKLIST (per-priority progress), SYSTEMATIC-DEBUG-FILTER-MIGRATION (a 7-week plan filed under roadmap/planned), and DEBUG-FILTER-AUDIT (the original 571-call survey). All three carry different progress figures (101/571, 307/571, ~486/571 measured now) and none is authoritative.

- `docs/DEBUG-FILTER-MIGRATION-CHECKLIST.md`
- `docs/roadmap/planned/SYSTEMATIC-DEBUG-FILTER-MIGRATION.md`
- `docs/DEBUG-FILTER-AUDIT.md`
- `docs/roadmap/done/DEBUG-FILTER-ARCHITECTURE.md`

**Recommendation:** Keep one tracker in debug-infra/plans/todo/ (merge the systematic plan's sequencing into the checklist), archive the audit, and keep ARCHITECTURE as the docs/ reference. Recompute the migrated/remaining split once rather than maintaining three counts.

### Navigation unit tests exist on disk but are excluded from compilation (JoyceCode.Tests.csproj:41 removes engine/navigation/**), with a comment stating they have never been compiled and have drifted against the current API. Four separate documents cite these tests as passing evidence for shipped work.

- `docs/roadmap/PHASE-B-COMPLETION.md`
- `docs/roadmap/PHASE-C-COMPLETION.md`
- `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-C-DYNAMICS.md`
- `docs/roadmap/proposed/NAVMAP-TRANSPORTATION-GRAPHS.md`
- `docs/roadmap/proposed/TRAFFIC-LIGHTS-SYSTEM.md`

**Recommendation:** Not a filing problem — track it as engineering debt: either revive engine/navigation tests against the current API or delete them, because right now the pipes/constraints/graph subsystem has documentation asserting test coverage that cannot run.

### PROCESS_DOCS.md is the standard that prescribes the current docs layout (docs/ARCHITECTURE/, docs/SYSTEMS/, docs/tale/phases|tests|design/). This reorganization invalidates every path it specifies, and CLAUDE.md's Quick Start points new instances at it as required reading. docs/tale/tests/STRATEGY.md has the same problem at smaller scale.

- `docs/PROCESS_DOCS.md`
- `docs/tale/tests/STRATEGY.md`
- `docs/tale/README.md`
- `docs/tale/OVERVIEW.md`
- `docs/SYSTEMS/README.md`

**Recommendation:** Rewrite PROCESS_DOCS.md to describe the domain/bucket layout as part of the same change that runs the moves, and refresh the four index/nav docs plus CLAUDE.md's Quick Start paths. Moving files without this leaves the repo's own documentation standard describing a tree that no longer exists.

### Two documents describe ExpectEngine — an architecture/design doc and an implementation doc — and both are filed under docs/SYSTEMS/NARRATION/ despite having nothing to do with narration. They overlap substantially in content.

- `docs/SYSTEMS/NARRATION/EXPECT_ENGINE.md`
- `docs/SYSTEMS/NARRATION/EXPECT_IMPLEMENTATION.md`

**Recommendation:** Both move to docs/testing/docs/. Evaluate merging them; if kept separate, correct the drift in EXPECT_ENGINE (TestEvent is a sealed class not a record, no Complete() method, no TaskCompletionSource, in-solution project not a NuGet package).

### Two plan documents were filed by outcome rather than by state: PHASE-C1-INFRASTRUCTURE and TALE-NPC-CONVERSATION-SYSTEM sit in roadmap/proposed/ with every artifact shipped and tested, while SYSTEMATIC-DEBUG-FILTER-MIGRATION sits in roadmap/planned/ with substantial work already done. Meanwhile PHASE-C2 sits correctly in done/ but its body reads not-started. The proposed/planned/done folders have stopped tracking reality.

- `docs/roadmap/proposed/PHASE-C1-INFRASTRUCTURE.md`
- `docs/roadmap/proposed/TALE-NPC-CONVERSATION-SYSTEM.md`
- `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-A-FOUNDATION.md`
- `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-B-FLOW.md`
- `docs/roadmap/proposed/NAVMESH-PATHFINDING-TALE-NPCS.md`
- `docs/roadmap/planned/SYSTEMATIC-DEBUG-FILTER-MIGRATION.md`

**Recommendation:** The per-plan status header this pass prepends is the fix: state is asserted in the document with a verification date and file:line evidence, so folder location becomes a filing convenience rather than the source of truth. Re-verify headers whenever a plan is touched.

## Pre-existing link rot (25)

Links pointing at files that do not exist — unrelated to this move, not auto-fixed.

- `docs/ARCHITECTURE/README.md` -> `../../CLAUDE.md`
- `docs/ARCHITECTURE/README.md` -> `../PROCESS.md`
- `docs/ARCHITECTURE/README.md` -> `AUDIO.md`
- `docs/ARCHITECTURE/README.md` -> `ENGINE.md`
- `docs/ARCHITECTURE/README.md` -> `INPUT.md`
- `docs/ARCHITECTURE/README.md` -> `PHYSICS.md`
- `docs/PROCESS_DOCS.md` -> `../PROCESS_TALE.md`
- `docs/PROCESS_DOCS.md` -> `ARCHITECTURE.md`
- `docs/PROCESS_DOCS.md` -> `OVERVIEW.md`
- `docs/PROCESS_DOCS.md` -> `phases/PHASE_0.md`
- `docs/TESTING/README.md` -> `../PROCESS.md`
- `docs/tale/README.md` -> `ARCHITECTURE.md`
- `docs/tale/README.md` -> `phases/PHASE_0.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `NPC_STORIES_DESIGN.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `TESTBED_PLAN.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/OVERVIEW.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/PHASE_0A_TESTBED_INFRA.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/PHASE_0B_DES_ENGINE.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/PHASE_0C_OUTPUT.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/PHASE_1_STORIES.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/PHASE_2_STRATEGIES.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/PHASE_3_INTERACTIONS.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/PHASE_4_PLAYER.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/PHASE_5_ESCALATION.md`
- `docs/tale/TALE_IMPLEMENTATION_PLAN.md` -> `tale/REFERENCE.md`

## Full assignment table (92)

### debug-infra (4)

| From | To | Status | Conf |
|---|---|---|---:|
| `docs/DEBUG-FILTER-AUDIT.md` | `docs/debug-infra/archive/DEBUG-FILTER-AUDIT.md` | superseded | 0.70 |
| `docs/roadmap/done/DEBUG-FILTER-ARCHITECTURE.md` | `docs/debug-infra/docs/ARCHITECTURE.md` | implemented | 0.75 |
| `docs/DEBUG-FILTER-MIGRATION-CHECKLIST.md` | `docs/debug-infra/plans/todo/MIGRATION-CHECKLIST.md` | partially-implemented | 0.80 |
| `docs/roadmap/planned/SYSTEMATIC-DEBUG-FILTER-MIGRATION.md` | `docs/debug-infra/plans/todo/SYSTEMATIC-MIGRATION.md` | partially-implemented | 0.78 |

### main (12)

| From | To | Status | Conf |
|---|---|---|---:|
| `docs/FRAME_NUMBER_INVESTIGATION.md` | `docs/main/archive/FRAME_NUMBER_INVESTIGATION.md` | superseded | 0.90 |
| `docs/PROCESS_DOCS.md` | `docs/main/docs/PROCESS_DOCS.md` | n/a | 0.78 |
| `docs/ARCHITECTURE/README.md` | `docs/main/docs/architecture/README.md` | n/a | 0.82 |
| `docs/ARCHITECTURE/RENDERING.md` | `docs/main/docs/architecture/RENDERING.md` | n/a | 0.85 |
| `docs/SYSTEMS/BUILD/PIPELINE.md` | `docs/main/docs/build/PIPELINE.md` | n/a | 0.82 |
| `docs/SYSTEMS/PERSISTENCE/LITEDB.md` | `docs/main/docs/persistence/LITEDB.md` | n/a | 0.82 |
| `docs/SYSTEMS/PLATFORMS/ANDROID.md` | `docs/main/docs/platforms/ANDROID.md` | n/a | 0.82 |
| `docs/SYSTEMS/QUEST/QUEST_SYSTEM.md` | `docs/main/docs/quest/QUEST_SYSTEM.md` | n/a | 0.82 |
| `docs/ANIMATION_ASSIMP_VERSION_COMPENSATION.md` | `docs/main/plans/done/ANIMATION-ASSIMP-VERSION-COMPENSATION.md` | implemented | 0.75 |
| `docs/roadmap/done/PLAYER-NPC-COLLISION-A-LAYER-FIX.md` | `docs/main/plans/done/PLAYER-NPC-COLLISION-A-LAYER-FIX.md` | implemented | 0.88 |
| `docs/roadmap/done/PLAYER-NPC-COLLISION-B-HAND-ARM-TOGGLE.md` | `docs/main/plans/done/PLAYER-NPC-COLLISION-B-HAND-ARM-TOGGLE.md` | implemented | 0.88 |
| `docs/roadmap/done/PLAYER-NPC-COLLISION-C-PUSH-ASIDE.md` | `docs/main/plans/done/PLAYER-NPC-COLLISION-C-PUSH-ASIDE.md` | implemented | 0.88 |

### navigation (11)

| From | To | Status | Conf |
|---|---|---|---:|
| `docs/roadmap/PHASE-B-COMPLETION.md` | `docs/navigation/archive/PHASE-B-COMPLETION.md` | implemented | 0.88 |
| `docs/roadmap/PHASE-C-COMPLETION.md` | `docs/navigation/archive/PHASE-C-COMPLETION.md` | implemented | 0.85 |
| `docs/roadmap/PHASE-D-PROGRESS.md` | `docs/navigation/archive/PHASE-D-PROGRESS.md` | implemented | 0.75 |
| `docs/roadmap/proposed/PIPES-FLOW-BASED-MOVEMENT.md` | `docs/navigation/archive/PIPES-FLOW-BASED-MOVEMENT.md` | superseded | 0.72 |
| `docs/tale/phases/PHASE_D.md` | `docs/navigation/docs/MULTI-OBJECTIVE-ROUTING.md` | partially-implemented | 0.80 |
| `docs/roadmap/done/IMPLEMENTATION-PLAN-NAVIGATION-SYSTEM.md` | `docs/navigation/plans/done/IMPLEMENTATION-PLAN-NAVIGATION-SYSTEM.md` | implemented | 0.85 |
| `docs/roadmap/proposed/NAVMAP-TRANSPORTATION-GRAPHS.md` | `docs/navigation/plans/done/NAVMAP-TRANSPORTATION-GRAPHS.md` | partially-implemented | 0.75 |
| `docs/roadmap/proposed/NAVMESH-PATHFINDING-TALE-NPCS.md` | `docs/navigation/plans/done/NAVMESH-PATHFINDING-TALE-NPCS.md` | implemented | 0.90 |
| `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-A-FOUNDATION.md` | `docs/navigation/plans/done/PHASE-A-FOUNDATION.md` | implemented | 0.78 |
| `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-B-FLOW.md` | `docs/navigation/plans/done/PHASE-B-FLOW.md` | implemented | 0.90 |
| `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PHASE-C-DYNAMICS.md` | `docs/navigation/plans/done/PHASE-C-DYNAMICS.md` | implemented | 0.82 |

### tale (55)

| From | To | Status | Conf |
|---|---|---|---:|
| `docs/ANALYSIS_NPCs_STUCK_AND_TIMING.md` | `docs/tale/archive/ANALYSIS_NPCs_STUCK_AND_TIMING.md` | n/a | 0.82 |
| `docs/tale/TALE_ABSTRACTION_INVENTORY.md` | `docs/tale/archive/TALE_ABSTRACTION_INVENTORY.md` | partially-implemented | 0.75 |
| `docs/TALE_TEST_INFRASTRUCTURE.md` | `docs/tale/archive/TALE_TEST_INFRASTRUCTURE.md` | implemented | 0.75 |
| `docs/tale/TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md` | `docs/tale/archive/TALE_TEST_SCRIPTS_PHASES_1_2_4_5_SKELETON.md` | superseded | 0.80 |
| `docs/tale/TALE_TEST_SCRIPTS_PHASE_0_SKELETON.md` | `docs/tale/archive/TALE_TEST_SCRIPTS_PHASE_0_SKELETON.md` | superseded | 0.80 |
| `docs/TIMING_RECOMMENDATIONS.md` | `docs/tale/archive/TIMING_RECOMMENDATIONS.md` | n/a | 0.72 |
| `docs/tale/TALE_ARCHITECTURE.md` | `docs/tale/docs/ARCHITECTURE.md` | n/a | 0.80 |
| `docs/tale/TALE_CONCEPT.md` | `docs/tale/docs/CONCEPT.md` | n/a | 0.88 |
| `docs/tale/OVERVIEW.md` | `docs/tale/docs/OVERVIEW.md` | n/a | 0.75 |
| `docs/PROCESS_TALE.md` | `docs/tale/docs/PROCESS_TALE.md` | n/a | 0.85 |
| `docs/tale/README.md` | `docs/tale/docs/README.md` | n/a | 0.85 |
| `docs/tale/REFERENCE.md` | `docs/tale/docs/REFERENCE.md` | n/a | 0.90 |
| `docs/tale/concepts/NARRATION.md` | `docs/tale/docs/concepts/NARRATION.md` | n/a | 0.85 |
| `docs/tale/design/NPC_COMMUTING.md` | `docs/tale/docs/design/NPC_COMMUTING.md` | n/a | 0.75 |
| `docs/tale/design/NPC_STORIES.md` | `docs/tale/docs/design/NPC_STORIES.md` | n/a | 0.75 |
| `docs/tale/design/STRATEGY_ARCHITECTURE.md` | `docs/tale/docs/design/STRATEGY_ARCHITECTURE.md` | n/a | 0.75 |
| `docs/tale/phases/PHASE_0A.md` | `docs/tale/docs/phases/PHASE_0A.md` | implemented | 0.78 |
| `docs/tale/phases/PHASE_0B.md` | `docs/tale/docs/phases/PHASE_0B.md` | implemented | 0.72 |
| `docs/tale/phases/PHASE_0C.md` | `docs/tale/docs/phases/PHASE_0C.md` | implemented | 0.72 |
| `docs/tale/phases/PHASE_1.md` | `docs/tale/docs/phases/PHASE_1.md` | implemented | 0.75 |
| `docs/tale/phases/PHASE_2.md` | `docs/tale/docs/phases/PHASE_2.md` | implemented | 0.75 |
| `docs/tale/phases/PHASE_2_STRATEGIES.md` | `docs/tale/docs/phases/PHASE_2_STRATEGIES.md` | implemented | 0.70 |
| `docs/tale/phases/PHASE_3.md` | `docs/tale/docs/phases/PHASE_3.md` | implemented | 0.75 |
| `docs/tale/phases/PHASE_4.md` | `docs/tale/docs/phases/PHASE_4.md` | partially-implemented | 0.70 |
| `docs/tale/phases/PHASE_5.md` | `docs/tale/docs/phases/PHASE_5.md` | implemented | 0.80 |
| `docs/tale/phases/PHASE_6.md` | `docs/tale/docs/phases/PHASE_6.md` | implemented | 0.85 |
| `docs/tale/phases/PHASE_7.md` | `docs/tale/docs/phases/PHASE_7.md` | implemented | 0.85 |
| `docs/tale/phases/PHASE_8.md` | `docs/tale/docs/phases/PHASE_8.md` | implemented | 0.85 |
| `docs/tale/phases/PHASE_D_SOCIAL.md` | `docs/tale/docs/phases/PHASE_D_SOCIAL.md` | implemented | 0.88 |
| `docs/tale/phases/PHASE_E_SOCIAL.md` | `docs/tale/docs/phases/PHASE_E_SOCIAL.md` | implemented | 0.85 |
| `docs/tale/TALE_TESTING_FRAMEWORK.md` | `docs/tale/docs/tests/FRAMEWORK.md` | n/a | 0.75 |
| `docs/tale/tests/PHASE_0.md` | `docs/tale/docs/tests/PHASE_0.md` | n/a | 0.88 |
| `docs/tale/tests/PHASE_1.md` | `docs/tale/docs/tests/PHASE_1.md` | n/a | 0.88 |
| `docs/tale/tests/PHASE_2.md` | `docs/tale/docs/tests/PHASE_2.md` | n/a | 0.88 |
| `docs/tale/tests/PHASE_3.md` | `docs/tale/docs/tests/PHASE_3.md` | n/a | 0.88 |
| `docs/tale/tests/PHASE_4.md` | `docs/tale/docs/tests/PHASE_4.md` | n/a | 0.80 |
| `docs/tale/tests/PHASE_5.md` | `docs/tale/docs/tests/PHASE_5.md` | n/a | 0.88 |
| `docs/tale/tests/PHASE_6.md` | `docs/tale/docs/tests/PHASE_6.md` | n/a | 0.88 |
| `docs/tale/tests/PHASE_7.md` | `docs/tale/docs/tests/PHASE_7.md` | n/a | 0.88 |
| `docs/tale/tests/PHASE_C.md` | `docs/tale/docs/tests/PHASE_C.md` | n/a | 0.85 |
| `docs/tale/tests/QUICK_START.md` | `docs/tale/docs/tests/QUICK_START.md` | n/a | 0.78 |
| `docs/tale/tests/STRATEGY.md` | `docs/tale/docs/tests/STRATEGY.md` | n/a | 0.75 |
| `docs/tale/TALE_TEST_PLAN.md` | `docs/tale/docs/tests/TEST_PLAN.md` | n/a | 0.82 |
| `docs/tale/concepts/INTERACTIONS.md` | `docs/tale/plans/done/INTERACTION-TYPE-REGISTRY.md` | implemented | 0.85 |
| `docs/roadmap/proposed/PHASE-C1-INFRASTRUCTURE.md` | `docs/tale/plans/done/PHASE-C1-INFRASTRUCTURE.md` | implemented | 0.90 |
| `docs/roadmap/done/PHASE-C2-STORYLET-DIALOGUE.md` | `docs/tale/plans/done/PHASE-C2-STORYLET-DIALOGUE.md` | implemented | 0.90 |
| `docs/tale/phases/PHASE_7B.md` | `docs/tale/plans/done/PHASE_7B-BUILDING-ROLE-TAGGING.md` | implemented | 0.80 |
| `docs/tale/phases/PHASE_C.md` | `docs/tale/plans/done/PHASE_C-NPC-CONVERSATIONS.md` | implemented | 0.85 |
| `docs/tale/concepts/ROLES.md` | `docs/tale/plans/done/ROLE-REGISTRY.md` | implemented | 0.82 |
| `docs/roadmap/proposed/TALE-NPC-CONVERSATION-SYSTEM.md` | `docs/tale/plans/done/TALE-NPC-CONVERSATION-SYSTEM-C1-C4.md` | implemented | 0.88 |
| `docs/roadmap/done/TALE-SOCIAL-PHASES.md` | `docs/tale/plans/done/TALE-SOCIAL-PHASE-D-SCENARIOS.md` | implemented | 0.82 |
| `docs/roadmap/done/IMPLEMENTATION-PLAN-TALE-SOCIAL-PHASE-E.md` | `docs/tale/plans/done/TALE-SOCIAL-PHASE-E.md` | implemented | 0.95 |
| `docs/tale/TALE_IMPLEMENTATION_PLAN.md` | `docs/tale/plans/done/TALE_IMPLEMENTATION_PLAN.md` | partially-implemented | 0.72 |
| `docs/roadmap/proposed/PHASE-C5-AMBIENT-NPC-DIALOGUE.md` | `docs/tale/plans/proposed/PHASE-C5-AMBIENT-NPC-DIALOGUE.md` | not-started | 0.90 |
| `docs/roadmap/proposed/PHASE-C6-STORYLINE-CONVERSATION-TREES.md` | `docs/tale/plans/proposed/PHASE-C6-STORYLINE-CONVERSATION-TREES.md` | not-started | 0.82 |

### testing (7)

| From | To | Status | Conf |
|---|---|---|---:|
| `docs/SYSTEMS/NARRATION/EXPECT_ENGINE.md` | `docs/testing/docs/EXPECT_ENGINE.md` | n/a | 0.80 |
| `docs/SYSTEMS/NARRATION/EXPECT_IMPLEMENTATION.md` | `docs/testing/docs/EXPECT_IMPLEMENTATION.md` | n/a | 0.78 |
| `docs/TESTING/README.md` | `docs/testing/docs/README.md` | n/a | 0.90 |
| `docs/RUN_TESTS_GUIDE.md` | `docs/testing/docs/RUN_TESTS_GUIDE.md` | n/a | 0.72 |
| `docs/TESTING/TESTBED.md` | `docs/testing/docs/TESTBED.md` | n/a | 0.85 |
| `docs/TESTING/TESTING_STRATEGY.md` | `docs/testing/docs/TESTING_STRATEGY.md` | n/a | 0.90 |
| `docs/TESTING/TIERS.md` | `docs/testing/plans/done/TIERS.md` | partially-implemented | 0.78 |

### world-gen (3)

| From | To | Status | Conf |
|---|---|---|---:|
| `docs/SYSTEMS/WORLD_GEN/LSYSTEM_EDITOR.md` | `docs/world-gen/archive/LSYSTEM_EDITOR.md` | partially-implemented | 0.78 |
| `docs/SYSTEMS/WORLD_GEN/LSYSTEM.md` | `docs/world-gen/docs/LSYSTEM.md` | n/a | 0.88 |
| `docs/SYSTEMS/WORLD_GEN/FRAGMENT_OPERATORS.md` | `docs/world-gen/plans/proposed/BIRD-SWARM-OPERATOR.md` | not-started | 0.80 |
