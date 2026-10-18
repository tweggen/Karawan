# TALE Docs Against the Code — Reconciliation Plan

> **Status:** done 2026-10-18 (KAR-431) · **Written:** 2026-10-18

The TALE docs under `docs/tale/docs/` were written for the March 2026 concept.
A doc-by-doc check against the code on 2026-10-18 (master `8d5e98b5`) found
which parts were never built, which were built differently, and which still
hold. This plan proposes what to do with each doc. It also lists four findings
about the **code and tests**, not the docs, which need a decision before the
docs can describe them truthfully.

Per the doc-verification convention, trivial mismatches (paths, renames,
counts) are fixed directly; everything else waits for the owner.

## 1. What shipped, in one paragraph

Each NPC is an `NpcSchedule` with flat float `Properties`, a `Trust` map, an
`ArcStack` of paused **storylets** and a `HasPlayerDeviation` flag.
`DesSimulation` advances schedules; `StoryletSelector` picks the next
`StoryletDefinition` by role, time of day, location type and property ranges,
then applies string postconditions (`+`/`-`/`=`, optionally conditional).
`InteractionPool` carries request/claim/signal pairs. Interrupts are one
`InterruptPriority` int: ≥ 8 means Replace, otherwise Nest. Groups come from
`CommunityDetector` and change at runtime (`RuntimeSocialEvolver`, Phase E);
social scenarios are baked (Phase D). The player meets NPCs through Phase C
conversations. In the game, `TaleEntityStrategy` runs travel → activity with
`GoToStrategyPart` / `StayAtStrategyPart`, routing through `IRouteGenerator`
(`NavMeshRouteGenerator`).

**Never built** (zero hits in code and models): the world state graph, arc
library and emotional arcs, motif registry, graph and arc rewriting, rarity
tiers, fork/join and the `Parallel` interrupt scope, the spatial verb
alphabet (`verb`, `follow`, `interact_with`, `use_transport`, `wait_for`),
`NpcNarrativeState`, `PlayerSocialCapital` / `SocialTier` / `overhear_text`,
transport tubes, lazy story-graph subdivision, condition classes for group
types, `InterruptCandidate`, transit (shared-segment) encounters,
`TestbedRootModule`.

## 2. Proposed disposition per doc

KEEP = trivial fixes only. BANNER = keep as history, add a "built
differently" banner at the top that names what shipped and links to it.
ARCHIVE = move to `docs/tale/archive/` with a pointer. REWRITE = the doc is
used as a reference, so it must describe the shipped system.

| Doc | Still accurate | Proposal | Why / what changes |
|---|---|---|---|
| `ARCHITECTURE.md` | ~10 % | **ARCHIVE + REWRITE** | Entry point from README ("how TALE works"), but describes the unbuilt concept. Archive as `archive/ARCHITECTURE_CONCEPT_2026-03.md`; write a new ARCHITECTURE.md from §1 above, with the class map. |
| `CONCEPT.md` | ~25 % | **ARCHIVE** | Pure concept (starts with a chat preamble). Re-point links in `design/NPC_STORIES.md`, `plans/done/TALE_IMPLEMENTATION_PLAN.md`. |
| `REFERENCE.md` | ~65 % | **REWRITE sections** | Remove the verb alphabet (→ storylet fields `location`, `duration_minutes(_min/_max)`, `time_of_day`); roles table → 8 population roles + Phase 8 occupations (13 in `nogame.roles.json`); deviation and survival → `HasPlayerDeviation` set in `TaleModule`, survival needs a conversation within `ConversationMemorySeconds` (1800 s); drop "recursively deviated". |
| `OVERVIEW.md` | ~90 % | KEEP | Story-file list (6 of 14), code map misses registries and D/E code, script-path inconsistency. |
| `README.md` | ~85 % | KEEP | "powers … quest systems" is wrong; two link names; re-point to the new ARCHITECTURE. |
| `PROCESS_TALE.md` | ~95 % | KEEP | Example `01-init.json` doesn't exist; `bugfix` filter undocumented. |
| `concepts/NARRATION.md` | ~95 % | KEEP (move?) | Accurate, but documents `engine/narration`, not TALE. Optional move to `docs/main/docs/`. |
| `phases/PHASE_0A.md` | ~80 % | BANNER | No `TestbedRootModule` (inline boot); SpatialModel moved to JoyceCode. |
| `phases/PHASE_0B.md` | ~80 % | BANNER | `CheckEncounters` not `ResolveEncounters`; transit encounters never built. |
| `phases/PHASE_0C.md` | ~60 % | BANNER → `docs/testing/docs/TESTBED.md` | Event names (12 in `JsonlEventLogger`), CLI flags, metrics differ. |
| `phases/PHASE_1.md` | ~40 % | **BANNER + move the storylet format into REFERENCE** | No verbs or `NpcNarrativeState`; morality drift, trust deltas (all ×2 but `patrol_check`), clique threshold (0.75) and selector seed differ; interaction types are data (`nogame.interactions.json`). |
| `phases/PHASE_2.md` | ~35 % | BANNER | Registries shipped rule-based (`Rules` with a switch), not condition classes; config is `nogame.relationship-tiers.json` / `nogame.group-types.json` at `/relationships/tiers`, `/groups/types` (fix the paths directly). |
| `phases/PHASE_2_STRATEGIES.md` | ~25 % | BANNER → `TaleEntityStrategy` | Verb executors never built. |
| `phases/PHASE_3.md` | ~50 % | BANNER | Pool built; `claim_trigger` shape; real library = 10 files / 20 storylets in `models/tale/interactions/`; sweep parameters never built. |
| `phases/PHASE_4.md` | ~10 % | **ARCHIVE** (or BANNER) | Superseded by Phase C conversations. |
| `phases/PHASE_5.md` | ~55 % | BANNER | No `Parallel`, no `InterruptCandidate`; ArcStack API names; escalation.json has 17 storylets, 4 named ones absent; Cancel never produced (§3). |
| `phases/PHASE_6.md` | ~65 % | BANNER | Persistence is now conversation-memory based; depopulation unwired (§3). |
| `phases/PHASE_7.md` | ~45 % | **REWRITE 7B, 7C, 7E** | Serves as the spatial/routing reference: sidewalk-corner `EntryCandidates` + `EntryPositionFor`; real `IRouteGenerator`; `JsonObject` serialisation. |
| `phases/PHASE_8.md` | ~95 % | KEEP | "all 9 roles" → 13 roles, 8 with models. |
| `phases/PHASE_D_SOCIAL.md` | ~95 % | KEEP | Test counts (13, total 48). |
| `phases/PHASE_E_SOCIAL.md` | ~98 % | KEEP | — |
| `design/STRATEGY_ARCHITECTURE.md` | ~85 % | KEEP + section | Add `TaleEntityStrategy`, complete the file list. |
| `design/NPC_STORIES.md` | ~30 % | BANNER (vision) | List what was never built (tubes, player reputation, lazy graph, fork/join). |
| `design/NPC_COMMUTING.md` | ~50 % | **RE-MEASURE** | All numbers predate the city rework; untagged buildings now default to home, so the "3 homes" problem may be gone. Re-run the `SPATIAL MODEL for cluster` trace. |
| `tests/PHASE_0.md`, `PHASE_1.md` | ~40 % | BANNER | Specs describe per-test setups; every test runs the same fixed 10 NPC / 3 location world and checks event streams only. |
| `tests/PHASE_2.md`, `PHASE_4.md` | low | BANNER | Smoke tests with DES-event proxies; no strategy or player engine is exercised. |
| `tests/PHASE_3.md`, `PHASE_6.md` | high | KEEP | (PHASE_6: depopulate tests don't exercise depopulation, §3.) |
| `tests/PHASE_5.md`, `PHASE_C.md` | — | depends on §3 (1) | These tests assert nothing today. |
| `tests/PHASE_7.md` | 0 of 103 names | **REWRITE** from the directory | Not one cited file name exists. |
| `tests/STRATEGY.md` | ~70 % | KEEP or ARCHIVE | The process it prescribes (`verify_test_docs.sh`, coverage sections) was never adopted. |

## 3. Findings about the code and tests (owner decision each)

1. **49 of the 200 TALE tests check nothing.** All of `phase5-escalation`
   (20) and `phaseC1`–`C4` (29) declare an `"expected"` block, but
   `ExpectEngine/ScriptRunner.cs:58` reads only `"steps"`; with zero steps a
   script passes at once (`:117`). Several would fail if they ran: they expect
   11 NPCs (the runner creates 10) and `dialogue_started`, which no code emits.
   The "200/200 green" in CLAUDE.md therefore covers 151 real tests.
2. **`TaleManager.DepopulateCluster` has no caller.** Schedules of unloaded
   clusters are never removed; phase-6 tests 11–17 can't notice because they
   wait for an event the TestRunner emits itself.
3. **Smaller ones:**
   - `GroupTypeRegistry.EvaluatePropertyThreshold` reads a string from a float
     dictionary (marked "Hack" in the code). No shipped group type uses
     `property_threshold`, so it is latent.
   - `InterruptScope.Cancel` is handled but never produced.
   - `TaleManager.AdvanceNpc` sets `CurrentWorldPosition` to the junction
     centre for street locations (`TaleManager.cs:832`); only fragment
     bucketing uses it.
   - Testbed metrics `routine_completion_rate` and `request_fulfillment_rate`
     are hard-coded to 1.0 (`SimMetrics.cs:244,248`).

## 4. Owner decisions (2026-10-18)

1. Dispositions as proposed in §2.
2. In addition: TALE keeps a **concept that leads into a plan** from the shipped
   state to the original goal — NPCs evolve through their interactions, form
   factions and eventually draw the player into storylines, *without*
   generating storylines explicitly. → new `docs/tale/docs/CONCEPT.md` and
   `docs/tale/plans/proposed/TALE-EMERGENT-STORIES.md`.
3. Follow-up tickets: KAR-433 (49 empty tests), KAR-434 (`DepopulateCluster`
   unwired), KAR-435 (re-measure NPC_COMMUTING), KAR-436 (minor code issues).

## 5. Steps

0. New CONCEPT.md and the emergent-stories roadmap (§4.2).
1. Trivial fixes (paths, counts, link names) across all docs.
2. Archive moves with pointers; banners on the BANNER docs.
3. New ARCHITECTURE.md; REFERENCE and PHASE_7 sections rewritten; storylet
   format moved into REFERENCE.
4. `tests/PHASE_7.md` regenerated from the directory.
5. README, OVERVIEW, PROCESS_TALE links updated; STATUS-LOG entry; CLAUDE.md
   test count corrected if §3.1 stays open.
6. Re-run the thinktank import for each changed doc.

## 6. Outcome

Executed as planned on branch `docs/kar-431-tale-docs`. Writing the new docs
turned up four more code findings, recorded on the tickets:

- The 102 `phase7-spatial` scripts are prose placeholders that error at step
  0 ("Unknown action") — added to KAR-433. Real TALE coverage: 151 of 302
  scripts.
- Requests/claims, `postconditions_if` and interrupts run only in
  `DesSimulation`; the live `TaleManager` applies plain postconditions and
  group verbs only. Treated as a design gap: roadmap stage S0.
- The five Phase 8 occupations are never assigned; `models/tale/interactions/`
  is loaded by nothing; `GroupDetector` has no production caller — added to
  KAR-436.
