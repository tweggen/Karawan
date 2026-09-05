# Phase B — the crossing policy

**Status:** implementation plan, twice reviewed. **WP-B1 is DONE (2026-09-05, §7 below).**
**§7.3's corridor mid point is FIXED (2026-09-05, §8) — the one change in this phase that
deliberately moves a baseline.** **WP-B2 is DONE (2026-09-05, §9).**
**WP-B0 may proceed.** **WP-B3a is DONE (2026-09-05, §10); WP-B3b is UNBLOCKED.**
**Follows:** Phase A (`STREETS-3D-TOPOLOGY.md` §7a … §7s).

---

## 0. What exists, and what two drafts of this plan got wrong

**Built and correct:** `Stroke.Level` / `StreetPoint.Level`; `StrokeKind`;
`StreetLevels.DeckHeight = 8f`; `OverpassBuilder.Build`; `NetworkBuilder.CommitChain` and
`_checkLevels`; V1 omits `Level`, V2 includes it. `OverpassBuilder` is referenced in
production by **two comments and no code**.

**Wrong, each verified against the tree:**

1. ⚠️ **`ClearanceConstraint` and `SpanLengthConstraint` are built but NOT in the pipeline**
   (`RampClearance` is assigned only in `MultilayerTests.cs:400`). A ground candidate
   crossing a **ramp** — stored at `Level = groundLevel` — gets a Split verdict, and
   `SplitStrokeAt` has no `Kind` guard, **bypassing `_checkLevels`**.
2. ⚠️ **`IntersectionConstraint` is not where crossings are decided.** Split-created
   junctions **1 / 1 / 0 / 11 / 1** against **2 / 11 / 64 / 256 / 66** four-arm crossings.
   Crossings form by **snapping**, over two candidates.
3. **Level filtering covers four `StrokeStore` queries** (`:104/182/269/337`), not every one.
4. ⚠️ **`IsPrimary` IS NOT HIERARCHY.** `Stroke.cs:136` calls it a *"primary or secondary
   direction"*; `SuccessorEmitter` **flips** it per branch; nothing in `JoyceCode/` or
   `nogameCode/` reads it except the fingerprint and two hard-codes. 46–66 % of strokes
   carry it. **`Weight` is the only hierarchy signal**, and `STREETS-3D-TOPOLOGY.md` §4's
   table is wrong where it names `IsPrimary`.
5. **`ConnectComponentsPass` is level-blind and bypasses `NetworkBuilder`**; runs after the
   queue drains.
6. **`ClusterStorage.DbVersion = 1039`** must be bumped — and the bump **deletes the whole
   `worldcache` file**, cluster list included, not just the street collections.
7. **`ConnectorBridge` strokes exist in shipped flat cities** (1–3 per city). Any rule
   phrased as "non-`Street`" changes the default city. Say `Ramp`/`Bridge`/`Tunnel`.

---

## 1. The distinction that must not be blurred

`Level` is a **topological deck index**; terrain height is **continuous**. Height informs
the policy; the policy sets `Level`; `Level` drives the filtering.

---

## 2. Buildability — the ramp must fit the END SPAN, and that changes the answer

A ramp cannot have a crossing under it (a road passing beneath at 2–3 m is a collision, and
a junction *on* a ramp is forbidden). So the ramp must fit in the corridor's **end span** —
an ordinary stroke, p50 **75 m**, p90 79.8, max 125.6 — **not** in the corridor's total
length. Corridor length decides only how many crossings one deck covers.

Crossings whose best straight-through pair has both arms ≥ ramp + 10 m:

| ramp grade → length | seed000@1500 (77) | seed017@2400 (177) | Yelukhdidru@3000 (343) |
|---|---|---|---|
| **5 % → 160 m** | **0** | **0** | **0** |
| 10 % → 80 m | 15 | 4 | 15 |
| 14 % → 57 m | 72 | 149 | 294 |

> ⚠️ **At 5 % — the grade `GradePolicy` holds a heavy road to — nothing is buildable in any
> city.** Phase B is only possible with ramps at **10–14 %**, i.e. steeper than any road the
> deck carries. **That is a decision, not a tuning constant** (§2a).

And the shipped network has almost no hierarchy to separate: Yelukhdidru@3000 has **1308 of
1875 strokes at exactly weight 0.200**, 94 % below 0.5, and only **35 at ≥ 1.0**. Heavy
straight-through pairs at 4-arm crossings: **12** and **5**. Intersected with the 10 % row,
the shipped ruleset yields **a handful of candidates per large city at most**.

### 2a. ✅ D1 and D2 — SETTLED 2026-09-05

**D1 — `MaxRampGrade` = 10 %.** 5 % builds nothing anywhere; 14 % would put **294**
structures in `Yelukhdidru@3000`, making grade separation the norm rather than a feature and
using the grade `GradePolicy` reserves for the lightest alley. 10 % gives **15 / 4 / 15** —
occasional landmark structures — and is already steeper than any road the deck carries,
which is how real interchanges are built. It is one named constant; retuning it once there
is something to look at is a one-line change and a re-measure.

**D2 — narrow scope. No new arterial ruleset in Phase B.** Structures are placed where the
existing network already permits them. Rationale: a new ruleset **changes every city
wholesale before a single bridge exists**, for a feature nobody has seen; it would dominate
B6.4 entirely ("everything changed, because the ruleset changed"); there *are* candidates
today (~12 heavy straight-through pairs and 15 ramp-fit crossings per large city); and it
defers the `DbVersion` bump's blast radius until the feature has proved itself. If the
result is "three overpasses in the world and they look great", *that* is the informed
argument for an arterial phase.

#### ⚠️ D2 changes how decision §3a is realised

C was chosen over a post-pass because *"no side street ever attaches where a deck will
be"* — but that argument assumed arterials were generated first. **Under D2 there is no
arterial stage**, so a naive reading of C collapses back into the post-pass, orphaning and
all.

**The priority-queue variant is what makes C survive D2.** Ordering the queue by `Weight`
on the flag-on path drains heavy candidates before any branch is popped, so a structure is
placed on a heavy corridor **before** side streets attach to it — C's actual benefit,
without a second loop, a re-seeding walk or a third rule table. That is now WP-B2's
mechanism, not a "worth costing first" alternative.

---

## 3. The three settled decisions, and what review changed

### 3a. Seam → two-stage generator (option C) — **mechanism needs rework**

**Gating is sufficient for the network**, confirmed: provisional ids are process-global but
`StrokeStore._assignLocalId`/`_assignLocalSid` overwrite them per network, nothing during
generation keys on a provisional id (`StreetPointIdTests` pins this), the RNG is one
`RandomSource` per `Generator`, and counters are per instance. **A flag-off run that never
enters stage 1 and leaves `RampClearance == 0` is bit-for-bit today's loop.**

**But "run the loop twice" does not work as drafted:**

- **Stage 2 has no seed.** `SuccessorEmitter.Emit` fires only on a just-accepted stroke;
  after stage 1 drains, the queue is empty. Stage 2 needs a re-seeding walk over stage-1
  strokes in `Sid` order with a **branch-only** table — i.e. three rule tables, not two.
- **`Generate()` cannot be called twice**: it rebuilds the pipeline and runs
  `_connectPass.Run()` on both exits. The body must become a `_drain()` with the connect
  pass hoisted to the end — which is also where §0.5's level-blindness must be fixed.
- ⚠️ **There is no `RemovePoint`.** `PolishStreetPoints` drops a strokeless junction from
  `_listPoints` **but not from `_octreeSP`**, so a lift leaves **ghost junctions** that
  stage-2 candidates will snap onto — exactly what option C was chosen to prevent.
- **Interior T-branches still orphan.** A corridor's interior junction that is a T-branch of
  another arterial loses its arm exactly as in the post-pass. With ~30 arterial strokes per
  city, such corridors may be most of them.
- **`OverpassBuilder.Build` sizes ramps by `rampFraction` of the run**; it needs a ramp
  *length* from `MaxRampGrade` bounded by the end spans. Signature changes.

**Cheaper realisation worth costing first:** replace the stack with a **priority queue by
weight** on the flag-on path. Heavy candidates drain before any branch is popped — two-stage
*behaviour* without two loops or a third rule table. Still needs the lift step and the ghost
fix; gated identically.

### 3b. Deck elevation → keep `level · DeckHeight`, **but refusal cannot live in WP-B3**

Terrain-difference separation stays out of scope.

⚠️ **B3.4 as drafted is unsatisfiable.** Relaxation is Jacobi over the **whole final
graph**, so there is no relaxed height at generation time; `RelaxedStreetHeight` called
during generation returns the **partial** store and caches it permanently; and
`_findStrokes` persists to LiteDB **immediately**, so a post-relaxation refusal must
un-build something already on disk. Checking against unrelaxed terrain needs a margin
larger than `DeckHeight` (§7e residuals: p99 ±7–10.5 m, worst −18.3/+16.9 m), which
swallows the check.

#### ✅ SETTLED 2026-09-05 — option **B**: pin the structure, let the ground conform

Two ways were put to the owner, and the review's own formulation was **wrong** on the way:

> ⚠️ *"permitted ground rise is `MaxRampGrade·L − DeckHeight`"* is a **shrunken symmetric**
> bound. The real constraint is an **offset interval**: `|groundRise + Δlevel·DeckHeight| ≤
> MaxRampGrade·L`. With `M`=10 m and `d`=+8 m that is `g ∈ [−18, +2]`, not `[−2, +2]` — the
> review's form **forbids a ramp descending a hill that falls away from it**, the easiest
> case there is. It also ignores sign: a `Tunnel` ramp has `Δlevel = −1`.

**Option A (not taken)** — teach the policy signed rise bounds (`MaxGradeFor → RiseBoundsFor`
returning an interval) and let the relaxer clamp into it. Simple, one proven pass, but the
relaxer then *decides* the structure's profile and can fight the placement. And the budget is
brutal: at 10 % over 8 m the deck climb eats nearly all of it — **0 m of permitted ground
rise at the 80 m minimum**, 2 m at 100 m, 4.5 m at the 125 m longest stroke — against
shipped terrain running **14.9 % per 20 m cell at the median**. The relaxer would pull hard
and, because `resistance` splits each correction between both ends, propagate into the
neighbours.

**Option B (TAKEN)** — the structure's junctions are **boundary conditions**: immovable under
relaxation, so the relaxer moves their *neighbours* instead (`resistance` is already a
per-junction map, so immovability may be cheap). The structure's profile is then **designed,
exactly**, and §2c's `ClusterConformElevationOperator` grades the terrain to it — machinery
that already exists and is proven. This matches how the rest of this workstream has gone:
**the road is designed and the ground conforms**, not the ground bending the road.

Known risk: a pinned structure on a steep hillside demands a terrain cut the **20 m
elevation grid cannot cut** — the standing §2c limit (ledger 2.1). Measure it; do not assume.

⚠️ **The guard, either option:** `MaxGradeFor` for a **`Street`** must return exactly what it
returns today, bit for bit, or every terrain city moves — and the terrain city is now the
shipped one. **Provably identity for non-structure strokes**, asserted over whole generated
cities rather than argued from the code.

B3.3 becomes a *relaxer property*, and B3.4 a post-relaxation **report** (B3.5), not a refusal.

`ElevationOf` is **already** the one expression with four consumers, so "prepared for B" is
largely done — but under B, `LevelElevation` stops being `[BsonIgnore]`, costing its own
`DbVersion` bump.

### 3c. Blocks → decks and ramps absent from the block graph — **confirmed, plus two gaps**

Measured on a built fixture: control (ground crossroads) **5 quarters, all clean**; with an
overpass **2 quarters**, one a 16-corner face carrying **6 delimiters on `Ramp`/`Bridge`
strokes, 4 at `Level = 1`**, traversing the ground road under the deck **on both sides in
the same face**. `QuarterGenerator.cs:339` skips only *start* points at `Level != 0`;
`GetNextAngle` happily follows a ramp. **Skipping `Ramp`/`Bridge`/`Tunnel` in the arm
choice restores planarity** and blocks merge 4 → 2 as predicted.

Two gaps §3c did not state:

- ⚠️ **The structure is then INSIDE the merged block.** A ramp is a ground-level road in the
  block's interior in plan; the estate is the outline inset by `SidewalkWidth`, and
  `_createBuildings` will build on it — a 24 m median building under a deck at +8 m. Option
  A needs a **structure-footprint exclusion** on the estate, which moves buildings.
- **Two decks over the same area** are both at level 1, so `IntersectionConstraint` splits
  one *at level 1* — a junction on a deck. Either refuse such corridors or send one to level
  2 (which `_checkLevels` makes a two-ramp climb).
- `ConnectorBridge` must **stay** in the block graph.

---

## 4. Work packages

### WP-B0 — the gate (measurement and a design note; no production code)

| AC | criterion |
|---|---|
| B0.1 | **End-span** distribution is the headline unit (not corridor, not span). Corridor and minSide reported alongside. |
| B0.2 | Buildable fractions at 5 / 10 / 14 % ramp grade, per city and world-wide. |
| B0.3 | Hierarchy measured by **`Weight`** — `IsPrimary` is an orientation bit (§0.4). Report the weight distribution and the count of heavy straight-through pairs. |
| B0.4 | **Block census** on lifted corridors: quarters before/after, delimiters on Ramp/Bridge/Tunnel, blocks containing a junction in their interior, self-intersecting blocks. |
| B0.5 | **What a merge moves** — estates, footprints, buildings, shops, TALE locations — **plus what the structure-footprint exclusion moves** (§3c). |
| B0.6 | **D1 and D2 (§2a) put to the owner with numbers.** This AC is a decision, not an artifact. |

### WP-B1 — make the built machinery real (flag off) — ✅ **DONE 2026-09-05**

These are live defects in shipped code and stand alone whatever happens to the rest of the phase.

| AC | criterion | how it is met |
|---|---|---|
| B1.1 | `ClearanceConstraint` and `SpanLengthConstraint` are **in the pipeline**, and their **placement in the order is stated**. Mutation: dropping either must fail a test **driven through `Generate()` over a store already holding an overpass**. | ✅ Placed **after `StrokeNearPointConstraint`** — the *last* constraint that can return `Restart`, which is a stronger statement than "after Snap" — and **before `IntersectionConstraint`**, span length first of the two. `TheConstraintPipelineRunsInThisOrder` asserts the whole ten-entry order by name; until now the order was pinned only by fingerprints, which say nothing about a constraint that is a no-op in a flag-off city. Dropping either fails three tests driven through `Generate()`. |
| B1.2 | **No ground stroke is ever split on a `Ramp`/`Bridge`/`Tunnel`**; no junction on one except the builder's. Positive control: the same candidate crossing an ordinary street **does** split. | ✅ `ACandidateCrossingARampDoesNotSplitIt`, run with clearance switched **off** so that only the `Kind` invisibility is under test, plus `TheSameCandidateCrossingAnOrdinaryStreetDoesSplitIt` at identical plan geometry. |
| B1.3 | `ConnectComponentsPass` filters orphan candidates **by level** and goes through `NetworkBuilder`. Fixture: nearest main junction is a deck junction; control: a ground one is nearer. | ✅ …and the filter is needed on **both** loops, which the plan did not say — §7.4. Seven tests in `ConnectComponentsLevelTests`. The `NetworkBuilder` half is a backstop that is **provably equivalent** given the filter — §7.7. |
| B1.4 | The refusal lives in the **verdict** — better, the ramp is invisible to the intersection query by `Kind` — with the `_checkLevels` throw as backstop only. | ✅ `StrokeStore.IntersectsMayTouchClosest` skips `StrokeKinds.IsStructure`; `SplitStrokeAt` refuses a structure, and a split point on another deck, **before** it removes anything from the store. |
| B1.5 | `RampClearance` is supplied **only with the flag on**. | ✅ Confirmed by mutation: supplying it unconditionally fails four tests, `StreetCostTests` among them, exactly as predicted. |
| B1.6 | Flag off: V1, V2, `street-geometry.json` byte-identical; cost within the existing gate; TALE 200/200. | ✅ No baseline file differs from `origin/master` by a byte. V2 has no baseline of its own, so `V2AddsNothingToAFlagOffCity` shows V2 is **determined** by V1 on all eight seeds rather than recording a second file to maintain. TALE 200/200. |
| B1.7 | A drift scan keeps `ElevationOf` the single deck-elevation expression (already true — this pins it). | ✅ Two-sided: `DeckHeight` may be named only in `StreetLevels.cs`, and each of the four consumers must contain `.LevelElevation` and must **not** contain `DeckHeight`. Plus a behavioural pin over the whole `sbyte` range, because a scan can only see names. |

**The flag** is `joyce.EnableGradeSeparation`, read **once** in `ClusterDesc._generateStrokes`
through `engine.streets.GradeSeparation.IsEnabled` and injected into `Generator` as a value,
exactly like `RuleTable`. A scan keeps that the only read. `ClusterStorage.DbVersion` is
**not** bumped; that is WP-B6's.

Tests: `tests/JoyceCode.Tests/engine/streets/{GradeSeparationPipelineTests,ConnectComponentsLevelTests,DeckElevationDriftTests}.cs`
— 47 new, 1211 xUnit against 1164 before, TALE 200/200.

### WP-B2 — heavy-first ordering and the removal primitive — ✅ **DONE 2026-09-05 (§9)**

D2 removes the arterial stage, so the re-seeding walk and the third rule table are **not
built**. What remains is the mechanism a lift needs.

| AC | criterion | how it is met |
|---|---|---|
| B2.1 | **Flag off runs today's stack, unmodified** — V1, V2, `street-geometry.json` byte-identical and `StreetCostTests` within its gate, asserted *after* the ordering exists. Confirmed sufficient: ids are masked by `_assignLocalId`/`_assignLocalSid`, the RNG is one `RandomSource` per `Generator`, counters are per instance. | ✅ No baseline file differs from commit 1 by a byte. `CandidateQueue.HeavyFirst` false makes `Pop()` `RemoveAt(Count - 1)` **on the same list**, not a lookalike of it. Mutation: setting `HeavyFirst` unconditionally fails 44 tests including all eight V1 baselines, all five `street-geometry.json` entries and `StreetCostTests`. |
| B2.2 | Flag on, the queue orders by `Weight`, so **every heavy candidate is accepted before any branch is popped** — asserted on emission order over generated cities, not on the comparer. | ✅ `Generator.OnCandidatePopped` reports each candidate as it leaves the queue together with everything still waiting; over six generated cities, **no candidate is ever popped while something heavier waits**, and separately no *branch* is. Its control is the same measurement flag off, where that happens on 5–20 % of pops. |
| B2.3 | Determinism: `GenerationIsRepeatableWithinAProcess` on **V2 with the flag on**, plus a recorded V2 baseline per seed. Not "draws RNG in a stated order", which is documentation. | ✅ `HeavyFirstGenerationIsRepeatableWithinAProcess` and `HeavyFirstGenerationMatchesRecordedBaseline` over all eight seeds; `street-fingerprints-gradesep.json` is new. |
| B2.4 | `StrokeStore.RemovePoint` exists and **clears `_octreeSP` as well as `_listPoints`**. Positive control: a removed junction is no longer returned by `FindClosestBelowButNot`/`GetClosestPoint` — the ghost that would otherwise defeat the whole ordering. Mutation: leaving the octree entry must fail. | ✅ Both queries are asserted **through the store's own API**, before and after, and there is an end-to-end control in which a growing street is offered the chance to snap onto the removed junction. Leaving the octree entry fails 3. `PolishStreetPoints` goes through the primitive. |
| B2.5 | `_connectPass.Run()` is hoisted out of `Generate()`'s two exits to a single end-of-generation call, so nothing can run between an ordering pass and the connect pass. | ✅ `Generate()` is `_drain()` plus one call. ⚠️ And the budget exit's copy **was reached by nothing** — §9.2. |
| B2.6 | `maxGenerations` is budgeted **once**, not per ordering tier. | ✅ Computed once inside `_drain()`, which has one call site, and driven: on a generator whose budget genuinely binds, a second `Generate()` adds nothing, and the control resets the counter and shows the same call does keep building. |

Deliberately **not** in B2: any structure placement, `OverpassBuilder` changes, or the
interior-T-branch rule — those are WP-B3, where a lift actually happens.

### WP-B3a — the height model — ✅ **DONE 2026-09-05 (§10)**

The relaxer stops being able to move a structure. Nothing is placed here; fixtures stand in
for structures, exactly as WP-B1 did — **no generated city contains one, so real data cannot
catch anything.**

| AC | criterion | how it is met |
|---|---|---|
| B3a.1 | A `Ramp`/`Bridge`/`Tunnel` junction is **immovable** under relaxation; its neighbours absorb the correction. Asserted on the relaxer's output over a sloping fixture, not on the resistance map. | ✅ `StructureProfile.PinnedJunctionsOf` names every junction a structure touches — feet included — and `GradeRelaxer.RelaxAround` skips a stroke with two pinned ends and gives a stroke with one pinned end its **whole** excess rather than that end's share. Asserted as exact equality against the boundary value rebuilt from its two pieces. ⚠️ **"its neighbours absorb the correction" turns out to be vacuous in a real city, and why is §10.2.** |
| B3a.2 | ⚠️ **Provably identity for non-structure strokes**: `MaxGradeFor(Street)` and every relaxed height in all eight seeds are **bit for bit** what they are today. The terrain city is the shipped city. | ✅ `street-relaxed-heights.json` — every junction's relaxed height and every stroke's permitted grade as exact float **bits**, recorded at `a135898e` before a line of WP-B3a existed, and unmoved. Plus the property in the clear: every stroke of eight cities is graded by its weight alone. |
| B3a.3 | A pinned structure's ramp carries **exactly** the grade its profile specifies — `MaxRampGrade` = 10 % — measured on the relaxed heights, not on the intent. | ✅ On fixtures over bridges and tunnels on ground levels −1, 0, 1 and 2, and on six generated cities with a real `OverpassBuilder` chain lifted onto their longest straight-through corridor. The design itself is asserted as an **identity** in the terms it is made of, so a dropped `LevelElevation` fails on the term. |
| B3a.4 | §2c's conform pass grades the terrain to the pinned structure. **Report the residual**: how far the graded ground ends up from the structure's own profile, and how much of that the 20 m grid cannot cut (ledger 2.1). | ✅ §10.4. ⚠️ **The 20 m grid is the smaller half** — the plan expected it to be the limit and it is not. |
| B3a.5 | Positive control: **without** pinning, the same fixture's structure junctions **do** move — otherwise B3a.1 passes when pinning is inert. | ✅ Same geometry, same weights, same starting heights — the designed profile itself — with the three structure strokes declared `Street`. The junctions move, and left to settle the ramp comes to rest at the 5 % its corridor's weight entitles a street to. |
| B3a.6 | Flag off: every baseline byte-identical; `StreetCostTests` in its gate; TALE 200/200. | ✅ No baseline file differs from `a135898e` by a byte. 1361 xUnit against 1275, TALE 200/200. |

### WP-B3b — placement — **blocked on B3a**

Find corridors on the existing network, decide, build via `OverpassBuilder` + `CommitChain`.
Carries the old B3.5–B3.8: the clearance **report** whose distribution says whether the deck
model needs decoupling after all, visible refusal (`GenerationReport` counts, a `Warning`),
the interior-T-branch rule, `Build`'s ramp-length signature from `MaxRampGrade`, and
`OverpassBuilder` no longer hard-coding `IsPrimary`.

### WP-B4 / B5 / B6 — hierarchy & angle / blocks / turn it on

Unchanged in intent. B4 measures hierarchy by `Weight`. B5 adds the structure-footprint
exclusion and the two-decks rule. B6 records that the `DbVersion` bump **deletes the whole
`worldcache`** (cluster list included — verify it regenerates identically) and that
**save games resolve strokes by id with `FirstOrDefault`**, so a regenerated network
silently resolves the same id to a different stroke.

---

## 5. Consumers to check rather than assume

- **TALE** — `SpatialModel.ExtractFrom` makes a location per `StreetPoint`, **deck junctions
  included**.
- **`Placer` / `citizen.SpawnOperator`** nearest-junction lookups are **unfiltered** by level.
- **Pedestrian crossings** — drawn per junction; assert rather than assume.
- **Nav lanes** — `GenerateNavMapOperator` emits a lane per stroke regardless of `Kind`.
- **`GenerateClusterStreetsOperator` reads no `Kind` at all**, so a deck renders as a road in
  the air with nothing under it until Phase C — the intended "floating slab".
- **`StreetHeightField.Build`** takes `GroundHeightAt` only, so the conform pass grades to the
  ground under a deck junction, not to the deck. Correct — the opposite would fill the
  underpass with a berm.
- **Flat cities** — `EnableGradeSeparation` is independent of `DisableClusterFlattening`.

---

## 6. How this work has gone

Two reviews of this plan each disproved central claims by measurement. That is the method,
not a mishap: **measure before diagnosing**; **mutation-test every gate** (`if (false)`
passes a source scan; a scan matching a bare identifier is satisfied by a field declaration
or a comment; a scan sees a call's name but not how many of its results are used; **a
containment test cannot tell a guess from a refusal**; **a rule can be invisible to
unlimited real data**); **a `Trace` in a `catch` is a silent failure**.

**Specific to Phase B:** this is the first change that moves the street **network** rather
than a surface on it.

---

## 7. WP-B1 as built (2026-09-05) — and the seven things the plan got wrong

The default city does not move: **no baseline file differs from `origin/master` by a byte**,
1211 xUnit (1164 before) and TALE 200/200. Twenty mutations were driven; eighteen were
killed, and the two survivors are proved equivalent below rather than excused.

### 7.1 ⚠️ The biggest finding: `GetRampsNear` could not detect a crossing, so B1.1 and B1.4 are only safe TOGETHER

`StrokeStore.GetRampsNear` tested four terms — each segment's two endpoints against the
other segment. **Those four are the distance between two segments only when the segments do
not cross.** Two segments crossing at their midpoints have all four endpoints far away and a
true distance of zero, so the query returned nothing for the one case a ramp clearance rule
exists for: **a street laid straight through a ramp.**

That was harmless while `ClearanceConstraint` was out of the pipeline, because such a
candidate got a `Split` verdict on the ramp instead — visibly wrong, but recorded. B1.4
removes the split. **So wiring B1.4 without repairing this query would have produced a road
passing clean through a ramp with nothing anywhere recording that it did** — a strictly
worse failure than the one B1.4 fixes, and completely silent. Neither the plan nor the brief
saw it; it surfaced because a test fixture measured the clearance independently of the
implementation instead of mirroring it. `|| null != cand.Intersects(stroke)` is the repair;
`AStreetLaidStraightThroughARampIsRefused` asserts all four endpoint terms clear 20 m before
asserting the refusal, so it cannot pass for the wrong reason.

### 7.2 ⚠️ `IntersectionConstraint` put every crossing junction on the ground

`new StreetPoint() { ClusterId = ctx.ClusterId }` leaves `Level` at its default 0 whatever
deck the crossing was found on. Latent today and exactly zero-cost to fix (level 0 *is* the
default), but it would have made B1.4's own `SplitStrokeAt` level backstop fire spuriously
on the first level-1 crossing, and behind that it is a junction filed on the wrong deck.

### 7.3 ⚠️ `_createBridgeCorridor` never assigns its mid point's position — ✅ **FIXED 2026-09-05, see §8**

`mid` is computed, the `RandomSource` draw for its offset is made, and the value is **never
assigned to `midPoint`**, so the corridor's middle junction sits at the **cluster origin**.
Measured on `seed017@2400`, the only one of 180 clusters that reaches this branch: a
**318 m** gap between two components is bridged by **1341.7 m + 1050.3 m** through the middle
of the city. Pre-existing. Deliberately not fixed in WP-B1: `SetPos`-ing the point moves that
seed's recorded fingerprint, and WP-B1 may not move the default city. Fixed on its own,
ahead of WP-B2, on the owner's authorisation — §8.

### 7.4 ⚠️ B1.3's level filter is needed on BOTH loops, not just the partner choice

The plan and the brief both said "filter the candidates by level". `_bridgeOrphanToMain` has
**two** loops: the first decides which junction of the ORPHAN the bridge leaves from, by how
near the main component comes to it; the second picks the partner. Filtering only the second
still yields a level-correct bridge — leaving from the **wrong end of the orphan**, because a
deck junction stacked over one end made that end look nearest. The mutation that drops the
first filter survived every test until a second fixture was built for it.

### 7.5 The placement answer is stronger than "after Snap"

The brief's trap was "a `Reject` before `SnapToNearbyPointConstraint` throws away a candidate
that would have snapped clear of the ramp". The correct boundary is not `Snap` but the **last
constraint that can return `Restart`**, which is `StrokeNearPointConstraint` two entries
later — it also rewrites the candidate's far end. So both new constraints go after it, and
before `IntersectionConstraint` so a doomed candidate does not pay for the most expensive
check. The whole order is now asserted by name; it had been pinned only by the eight
fingerprints, which say nothing at all about a constraint that is a no-op in a flat city.

### 7.6 The tunables had no values, so they are derived

The plan named `RampClearance`, `MinSpanLength` and `MaxSpanLength` and gave none of them a
number. Both length-like ones default to `Stroke.WidthForWeight(weightMax)` — the widest
carriageway the ruleset can build, i.e. the separation at which two carriageways at maximum
width just touch — which required hoisting `StreetWidth()`'s expression into a static so
there is still only one copy of it. `MaxSpanLength` defaults to unbounded: how long a deck
may stand up is a structural question WP-B1 deliberately does not answer.

### 7.7 The two surviving mutations, both provably equivalent

Routing `_createBridgeStroke` and `_createBridgeCorridor` through `NetworkBuilder` instead of
`StrokeStore.AddStroke` **cannot be caught by any test**, and always could not: given §7.4's
filter, both ends of a connector are on the same level by construction, so `_checkLevels`
can never refuse a `ConnectorBridge`. It is a backstop against the filter being removed, and
removing the filter is itself killed. Stated rather than papered over — B1.3 asks for the
routing, and the routing is genuinely unobservable while the choice is correct.

### 7.8 Found and NOT fixed

- **`ConnectComponentsPass` does not run the constraint pipeline at all**, so a
  `ConnectorBridge` may be laid straight past or through a ramp with no clearance check. Not
  in WP-B1's ACs; the clearance tests exclude `ConnectorBridge` explicitly and say why.
- **The corridor mid point** of §7.3. ✅ fixed 2026-09-05, §8.
- **`Generator.Generate()` still ends with `_connectPass.Run()` on both exits**, which §3a
  already records as blocking WP-B2.

---

## 8. The corridor mid point (2026-09-05) — §7.3 fixed on its own

Done alone, ahead of WP-B2, because it is the one change in this phase that is **meant** to
move a baseline, and doing it separately is what lets WP-B2's own "nothing moves" gate mean
anything.

**The `RandomSource` draw was already being consumed.** `offset = 40f + _rnd.GetFloat() * 40f`
is on the line above; only the assignment of the resulting position was missing. So the
random sequence does **not** shift, the network's shape and size are untouched, and the fix
is a pure change of one junction's coordinates. That is visible in the fingerprint: the
counts are identical either side of it.

**What moved, per seed.** `seed017@2400` and nothing else, exactly as predicted:

| seed | before | after |
|---|---|---|
| `seed017@2400` | `n=785,s=1034,h=27B690F7094A1DE5` | `n=785,s=1034,h=A3ACCA494D9A7A3B` |

**A genuine geometry change, not a re-hash**, and measured rather than asserted: the
canonical stroke list differs in **2 lines of 1034**, both of them the corridor's own halves,
with the other 1032 byte-identical, `n` and `s` unchanged, and the point count unchanged. The
mid junction moves from `(0.0, 0.0)` to `(-841.9, 822.4)` — **1178.3 m** — and the corridor
that bridges a **318.0 m** gap goes from **1341.7 m + 1050.3 m** to **165.4 m + 165.3 m**.
The remaining seven seeds are byte-identical, `street-geometry.json` does not record
`seed017@2400` at all so no geometry baseline moved, and `StreetCostTests` stayed inside its
existing 2 % gate.

**⚠️ What the test has to assert, which is not what the symptom looks like.** "The mid is not
at the cluster origin" is the *shape of the defect*, not the property, and it is wrong in both
directions: a corridor whose two ends straddle the origin has its mid there legitimately, and
`seed017@2400`'s corridor happens to be a kilometre away from the origin, so an origin check
would have passed there for a reason unconnected to the defect. The property is that the mid
stands **between** its two ends — its projection onto the chord is the chord's own midpoint —
**and off** that chord by the offset that was drawn for it. Both halves are load bearing:
`TheOffsetIsWhatSeparatesADrawnMidFromADefaultOne` puts the chord's midpoint exactly on the
origin, and there the defect satisfies the *between* half on its own.

**Mutations: three driven, two killed by the new tests, one killed by the fingerprint.**
Dropping the assignment fails 3; assigning the plain chord midpoint without the perpendicular
offset fails 3 (the offset assertion, in all three fixtures); **flipping the sign of the
offset survives all nine corridor tests and always will** — which side of the chord a corridor
bows to is not a property of anything, and both answers are equally correct — and is caught by
the recorded fingerprint for `seed017@2400`, which is the only thing that can pin it.

Tests: `tests/JoyceCode.Tests/engine/streets/ConnectComponentsCorridorTests.cs` (9). One of
them records that **no other pinned seed reaches the corridor branch**, so that a ruleset
change which quietly stops exercising it is visible rather than silent.

---

## 9. WP-B2 as built (2026-09-05) — and the three things the plan got wrong

**No baseline moved**: `street-fingerprints.json`, `street-geometry.json` and
`street-cost-baseline.json` are byte-identical to commit 1's state, TALE is 200/200, and
1275 xUnit against 1220 before. **Thirteen mutations were driven and all thirteen were
killed** — the first round in this work stream with no survivor, which is itself worth
distrusting, so §9.4 says which gate killed each.

### 9.1 The mechanism, and why it is one class

`engine.streets.generation.CandidateQueue` replaces `Generator`'s `List<Stroke>` work
queue. `HeavyFirst` false — every run of the shipped game — makes `Pop()`
`RemoveAt(Count - 1)` **on the same list**, so B2.1 is not "a lookalike that agrees"; it is
the same two lines the generator has always run. `HeavyFirst` true scans the pending list
**backwards** for the greatest weight, so among equal weights the most recently pushed
still wins and the queue is still a stack *within* one weight — which is what keeps a
split's head ahead of its tail without that call site having to change.

Linear per pop, and paid only with the flag on. A heap would buy it back and would need
its own tie break; a tie break is where determinism is lost, and the cost is bounded by a
pending list that peaks at 470 entries on the largest city.

### 9.2 ⚠️ The finding: **no pinned seed reaches the generation budget**, so one of the two calls being hoisted was reached by nothing

`StreetDeterminismTests` has said since it was written that `Yelukhdidru@3000` *"exercises
the maxGenerations = Size^2/1000 budget cut-off"*. Measured while gating B2.6:

| seed | `_generationCounter` at the end | budget |
|---|---|---|
| `seed000@1500` | 365 | 2250 |
| `seed017@2400` | 1034 | 5760 |
| `Yelukhdidru@3000` | **1886** | **9000** |

All eight leave the drain by **the queue running dry**. So the budget exit — and its own
copy of `_connectPass.Run()`, which is half of what B2.5 hoists — was reached by no test
and no recorded city, and **deleting that copy would have passed every gate in this
repository**. That is §7's *"a rule can be invisible to unlimited real data"* again, one
turn on: here the shape the data does not have is not a ramp but an *exit*.

The fixture that fixes it is available because **the budget and the growable area are
independent**: `maxGenerations` is `ClusterDesc.Size²/1000` while where streets may grow
comes from `SetBounds`. A 200 m cluster — budget 40 — growing inside a 2 km square hits
the budget in a few dozen strokes. With two strokes already in the store 2.5 km apart, a
run cut off by its budget still has to come back as one component, and only bridging can
do that. The mutation that puts the connect pass back on the queue-empty exit alone fails
exactly that fixture (and the scan), and nothing else.

The comment in `StreetDeterminismTests` is corrected rather than deleted, and records the
numbers.

### 9.3 B2.2 is asserted where the ordering happens

`Generator.OnCandidatePopped` reports each candidate as it leaves the queue, together with
everything still waiting behind it, and the gate is that **nothing heavier is ever left
waiting** — over six generated cities, 34 to 2592 pops each. A test that asked the queue
how it compares two candidates would pass with the queue unwired from the accept loop
entirely, which is precisely what happened to `ClearanceConstraint` and
`SpanLengthConstraint` (§7).

Three things stop that gate being vacuous, and all three were needed:

- **the control**: the same measurement with the flag off, where a lighter candidate is
  popped ahead of a heavier one on 5–20 % of pops. Without it the invariant could be
  satisfied by candidates that happen to arrive in descending weight;
- **the branch floor**: `ABranchIsNeverPoppedWhileAHeavierCandidateWaits` asserts the city
  contains more than five branch candidates before asserting that they wait;
- **`TheOrderingChangesTheCityThatComesOut`**: the observer could be wired to a queue the
  accept loop ignores. It cannot survive the two orderings producing different networks —
  and they do, on every seed that generates anything at all.

### 9.4 The thirteen mutations, and which gate killed each

| # | mutation | killed by |
|---|---|---|
| 1 | `HeavyFirst` never set | 25 — every flag-on gate and every flag-on baseline |
| 2 | `HeavyFirst` set unconditionally | **44** — all eight V1 baselines, all five `street-geometry.json` entries, `StreetCostTests`, and a long tail of block/kerb/route gates |
| 3 | heavy-first tie break to the **oldest** push (`>=`) | `HeavyFirstBreaksTiesTheWayTheStackWould` + 7 flag-on baselines |
| 4 | heavy-first pops the **lightest** | 14 |
| 5 | a split pushes its head before its tail | the flag-off baselines, `StreetCostTests` and the geometry gates |
| 6 | the hoisted `_connectPass.Run()` deleted | `GeneratedNetworkIsStructurallySane` + every baseline |
| 7 | the connect pass back on the **queue-empty exit only** | **3, all new**: the budget-exit fixture (both orderings) and the call-site scan |
| 8 | `PolishStreetPoints` back to a list-only removal | 1 — `PolishStreetPointsTakesItsDeadJunctionsOutOfTheOctreeToo` |
| 9 | the pop observer never invoked | 12 — the pop floor in every flag-on and flag-off ordering test |
| 10 | the budget never checked | 1 — `TheBudgetIsSpentOncePerRunAndNotPerPassOverTheQueue` |
| 11 | `RemovePoint` no longer refuses a junction that carries strokes | 1 |
| 12 | `RemovePoint` leaves `InStore` set | 1 |
| 13 | the connect pass runs **before** the drain | the baselines and the scan |
| — | `RemovePoint` without `_octreeSP.Remove` | **3**, including the end-to-end generator control |

### 9.5 What the plan got wrong

- **§3a: "`Generate()` cannot be called twice"** — it can now, and B2.6 depends on it: a
  second `Generate()` on a generator whose budget is spent is the shape a tiered ordering
  would take, and asserting that it adds nothing is how the budget is shown to be one
  allowance. What was true is the reason given: it rebuilt the pipeline and ran the
  connect pass on both exits.
- **§3a: "still needs the lift step and the ghost fix"** — the ghost fix is here, and the
  plan's description of it was right but understated the *reason*. `PolishStreetPoints`
  is not the dangerous caller; it is the harmless one, because it runs after `Generate()`
  has returned and nothing queries the point octree afterwards. What makes the primitive
  worth writing before WP-B3 is that **a removal that only touches the list looks
  correct**, and the first caller that runs during generation would inherit that silently.
- **The budget claim in the test suite**, §9.2 — not the plan's, but the plan leans on
  `Yelukhdidru@3000` as the seed that exercises everything, and on this it does not.

### 9.6 Still open, for WP-B3

- `ConnectComponentsPass` still does not run the constraint pipeline (§7.8), unchanged.
- The heavy-first city has **no `street-geometry.json` baseline** — only V2 of the
  network. Blocks, estates and buildings on the flag-on city are unrecorded, which is
  fine while nothing places a structure and is the first thing WP-B5 will need.
- The linear scan in `Pop()`. Measured peak pending: 470 on `Yelukhdidru@3000`, so it is
  worth nothing to fix today and worth measuring again if a ruleset ever makes the queue
  much longer.

---

## 10. WP-B3a as built (2026-09-05) — and the four things the plan and the brief got wrong

**No baseline moved**: `street-fingerprints.json`, `street-fingerprints-gradesep.json`,
`street-geometry.json` and `street-cost-baseline.json` are byte-identical to `a135898e`, TALE
is 200/200, and 1361 xUnit against 1275 before. **Twenty-four mutations were driven and all
twenty-four killed**, three of them only after a gate was written for them — §10.6.

### 10.1 What is built, and the one piece of arithmetic that matters

`engine.streets.StructureProfile` — `PinnedJunctionsOf` (every junction a `Ramp`, `Bridge` or
`Tunnel` touches) and `Design` (the height each ramp's lifted end must have). `GradePolicy`
gains **`MaxRampGrade` = 0.10**, returned by `MaxGradeFor` for a **`Ramp` and nothing else**;
so there is still exactly one expression for "how steep may this be", and every stroke of
every shipped city still goes down the weight interpolation. `GradeRelaxer.Relax` becomes
three steps: an anchor pass, the design, and the sweep — which is now `RelaxAround`, taking
the boundary and what is left of the sweep budget.

⚠️ **The table all of this writes into is GROUND height**, and a junction's road stands
`StreetPoint.LevelElevation` above it. So the design is

    heights[deck] = groundAtFoot + foot.LevelElevation ± MaxRampGrade·length − deck.LevelElevation

and never `groundAtFoot ± MaxRampGrade·length`. The two agree perfectly on every structure
whose feet are on level 0, which is every structure the game will build first — §7r's
"every generated city sits at `Pos = Vector3.Zero`" in a new coat. It is killed by fixtures
whose ground deck is level 1, 2 or −1.

⚠️ **`resistance` is NOT the mechanism, and the plan's "immovability may be cheap" is wrong.**
The split is `wA = rB / (rA + rB)`. An infinite resistance at ONE end gives `wA = 0` as
wanted — but a structure's own stroke has both ends pinned, and `∞/∞` is **NaN**. Any
finite stand-in leaves a residual correction each sweep, which B3a.3's *exact* grade
forbids. So the boundary is an explicit set, and the sweep skips a stroke with two pinned
ends outright.

### 10.2 ⚠️ THE FINDING: a structure designed from the terrain under its feet drags the city into the noise

The plan says a structure's junctions are immovable and says nothing about **what value**
they are immovable at. The obvious reading — the height already in the table, i.e. the raw
terrain sample — was built first and measured. Over six generated cities on the shipped
terrain, lifting the longest straight-through corridor onto a real `OverpassBuilder` chain:

| seed | foot drift from the relaxed city |
|---|---|
| `seed000@500` | −4.05 m, +7.22 m |
| `seed011@500` | +4.97, +3.47 |
| `Yelukhdidru@800` | −2.85, **−16.09** |
| `seed000@1500` | −1.54, **−27.05** |
| `seed017@2400` | −8.33, −9.03 |
| `Yelukhdidru@3000` | **−14.97**, +1.74 |

A foot is an ordinary junction of the ordinary city: an approach street leaves it, a block
corners on it, a building stands on that block. Pinning it at the raw sample takes all of
that down with it — the structure moving the city rather than standing on it, which is the
opposite of what option B is for.

**The fix is an ANCHOR PASS**, and it is not new machinery: relax the network *as if the
structures were not there*, which is `Relax` itself over the strokes that are not part of
one, and design from the feet that produces. It recurses no further, because the filtered
list contains no structure. Foot drift after: **exactly 0.000 m on all twelve feet**, and

> ⚠️ **adding a structure moves NOT ONE junction of the city** — 0 of 23, 0 of 27, 0 of 64,
> 0 of 274, 0 of 785, 0 of 1379, worst 0.0000 m, asserted as exact equality.

That is the property WP-B3b's own before/after measurement will rest on: "the blocks moved"
has to mean the structure moved them.

⚠️ **And it makes B3a.1's second clause vacuous, which is worth saying plainly.** Once the
feet stand where the city already settled them, the structure's presence creates no
over-limit stroke at all, so there is nothing for a neighbour to absorb. The boundary rules
in `RelaxAround` are a **guarantee** that no sweep can bend a designed structure, not a step
some city depends on — and they are therefore driven directly against `RelaxAround` (the
production method, not a lookalike) rather than through a fixture pretending a real city
exercises them.

### 10.3 The sweep budget is one allowance, and finding that out found something older

WP-B2.6 budgeted `maxGenerations` once rather than per ordering tier. The same call is made
here: the anchor pass and the final sweep share `policy.MaxSweeps`. Without the split, a
structure hands the whole city a **second** relaxation and it settles further — measured at
up to **7.5 m** on `Yelukhdidru@3000`, 1021 of 1379 junctions moving, which would have made
"adding a structure moves nothing" false for a reason having nothing to do with structures.

⚠️ **Measured on the way, and pre-existing: `GradeRelaxer` exhausts its whole 32 sweep budget
on every generated city.** `Relax` returns its sweep count precisely so "a caller that wants
to complain about it" can, and `RelaxedStreetHeight` does not look at it. Every
terrain-following city in the game is running an unconverged relaxation and nothing anywhere
says so. Not fixed here — it is not WP-B3a's, and converging it would move the shipped city.

One consequence of the split: on a real city the anchor pass spends the entire allowance, so
the final sweep runs **zero** sweeps. The structure is still designed and still standing; the
city is still exactly the city. It is the reason §10.2's "vacuous" is doubly true.

### 10.4 B3a.4 — the residual, and ⚠️ the 20 m grid is the SMALLER half

Measured through `ClusterConformElevationOperator.Grade`'s own arithmetic on its own
`GroundResolution + 1` grid, read back with `CacheEntry.GetElevationPixelAt`'s own two
triangle rule, over the shipped terrain. Split three ways, because the split is the finding:

* **designed → field** — what `StreetHeightField`'s weighted **mean** asks for at that point.
  It is not the structure's own height wherever another stroke is inside the 60 m radius, and
  under a bridge there always is one, 8 m below.
* **field → grid** — what the 20 m elevation grid can carry of that. This is ledger 2.1, the
  standing §2c limit, and the thing the plan expected to dominate.
* **designed → grid** — the total.

Sampled every 5 m along the whole ramp–deck–ramp chain:

| seed | total p50 / p95 / max | field part p50 / max | **grid part p50 / max** | cut demanded, min…max |
|---|---|---|---|---|
| `seed000@500` | 0.483 / 4.019 / 4.505 | 0.422 / 3.675 | **0.234 / 0.853** | −7.2 … +17.8 |
| `seed011@500` | 0.970 / 2.500 / 2.519 | 0.971 / 2.805 | **0.050 / 0.305** | −5.0 … +19.6 |
| `Yelukhdidru@800` | 1.167 / 3.292 / 3.858 | 0.579 / 2.180 | **0.511 / 2.443** | −9.1 … +24.2 |
| `seed000@1500` | 2.010 / 4.057 / 4.379 | 1.541 / 4.000 | **0.284 / 0.955** | +1.5 … +27.1 |
| `seed017@2400` | 2.206 / 6.003 / 6.305 | 1.838 / 5.810 | **0.245 / 0.625** | −30.2 … +9.0 |
| `Yelukhdidru@3000` | 0.971 / 2.668 / 3.173 | 0.996 / 2.557 | **0.392 / 1.619** | −1.7 … +20.1 |

**At the twelve FEET — the junctions the city actually stands on — the field term is 0.000 m
at eleven of them and 0.216 m at the twelfth, and the whole residual is the grid: 0.006 to
0.885 m.** Against the same six cities' own control, every ordinary junction with no
structure anywhere: p50 0.17–0.42 m, p95 1.2–2.3 m, worst 32.3 m. **A pinned structure's feet
sit on the graded ground better than the median junction of the city they stand in.**

So the answer to B3a.4 is: **the 20 m grid is not what limits this.** It contributes
0.05–0.51 m at the median and at most 2.44 m, and where it matters most — the feet — it is
the only term and it is under a metre. What is left over is `StreetHeightField`'s weighted
mean averaging the deck's designed ground against the ground road 8 m below it, which is a
property of the field's blend and not of the elevation resolution; a finer grid would not
touch it. That is a real input to WP-B3b, and to whether the underpass wants its ground
excluded from the field at all.

The **cut** the structure demands of the terrain — designed ground against raw noise — runs
−30.2 to +27.1 m, which is the number ledger 2.1's warning was about; the grid carries it to
within 2.44 m by grading the whole city site rather than cutting a corridor.

### 10.5 ⚠️ The deck, not the ramp, is what WP-B3b has to refuse

Both ramps climb `MaxRampGrade` from their own feet, so **whatever the two feet disagree by
lands on the deck**, and nothing bounds it. Over the same six lifts the deck comes out at

    +4.2 %   +11.4 %   +4.3 %   −4.7 %   +23.7 %   +21.6 %

and 23.7 % is not a bridge, it is a ramp and a half. WP-B3a deliberately does not refuse it —
refusing a corridor is placement — but this is the number to refuse on, and it is not the one
§2's buildability table measures. §2 asked whether two ramps FIT in the end spans; this asks
whether the two ends are at similar enough heights for the deck between them to be a deck.

### 10.6 The mutations

**Twenty-four driven, and all twenty-four killed** — three only after a gate was written for
them, and each of the three named something the existing gates genuinely could not see.

| # | mutation | killed by |
|---|---|---|
| 1 | `PinnedJunctionsOf` finds no structure | **44** — every flag-on gate in the file |
| 2 | pins only a structure stroke's `A` end | 3 |
| 3 | the design drops `foot.LevelElevation` | 5 — the level-1/2/−1 fixtures only |
| 4 | the design drops `deck.LevelElevation` | 21 |
| 5 | foot and deck end swapped | 19 |
| 6 | a tunnel's ramp climbs instead of descending | 5 |
| 7 | the climb uses a fixed grade instead of `MaxGradeFor` | 21 |
| 8 | two ramps on one deck junction: last by Sid wins | 1 |
| 9 | ⚠️ **the design accepts any structure, not only a `Ramp`** | **survived** — a well-formed bridge has both ends on the deck's own level, so the wider guard finds no foot and refuses anyway. Killed by `ADeckStraddlingTwoLevelsIsStillNotARamp`, a deliberately malformed deck, which is the only shape that can tell the two rules apart |
| 10 | `MaxGradeFor` loses its ramp branch | 22 |
| 11 | `MaxGradeFor` catches `Bridge`/`Tunnel` too | 2 |
| 12 | `MaxGradeFor` catches anything that is not a `Street` | 13 — §0.7's `ConnectorBridge` trap |
| 13 | a stroke with two pinned ends is corrected anyway | 14 |
| 14 | ⚠️ **a stroke with one pinned end keeps the resistance split** | **survived twice.** First because the settled assertion cannot see it: after 32 sweeps the geometric series has run and the two rules agree to five decimals. Then, after a one-sweep test was written, **because the pinned end is `B` on one approach and `A` on the other and those are two lines** — the new test asserted only the west one. Killed by asserting both ends, which is §7q's symmetric-survivor lesson in a new place |
| 14b | the same, on the other branch | 1 |
| 15 | the sweep sees no boundary at all | 15 |
| 16 | no anchor pass — design from the raw terrain | 15 |
| 17 | the anchor pass is not filtered | infinite recursion; the test host dies part way through the run. A crash rather than an assertion, and named as such |
| 18 | each pass gets its own sweep budget | 8 |
| 19 | the design runs before the anchor pass | 29 |
| 20 | the anchor pass's sweeps are not counted | 2 |
| 21 | ⚠️ **the unheighted counter deleted** | **survived** — pre-existing behaviour with no test at all, and the brief asked for it to be kept. Killed by capturing the log through `Logger.SetLogTarget`, plus a control that a network with every height present says nothing |
| 23 | the unheighted `Warning` never reached | 1 |


### 10.7 What the plan got wrong

- ⚠️ **§3b: "under B, `LevelElevation` stops being `[BsonIgnore]`, costing its own
  `DbVersion` bump."** It does not and it cannot. `LevelElevation` is a getter over `Level`
  with no setter, and `Level` is already persisted — there is nothing to un-ignore and no
  bump is owed. `ClusterStorage.DbVersion` is untouched by WP-B3a.
- ⚠️ **§3b: "`resistance` is already a per-junction map, so immovability may be cheap."**
  §10.1: it is `∞/∞` on a structure's own stroke, and anything finite leaves a residual the
  exact-grade AC forbids.
- ⚠️ **B3a.1: "its neighbours absorb the correction."** True of the sweep, vacuous in a real
  city — §10.2.
- ⚠️ **B3a.4: "how much of that the 20 m grid cannot cut."** The question presumes the grid
  is the limit. It is the smaller half everywhere and the only term at the feet, where it is
  under a metre — §10.4.

### 10.8 Found and NOT fixed

- ⚠️ **`GradeRelaxer` never converges on a real city** and `RelaxedStreetHeight` discards the
  return value that says so (§10.3). Pre-existing; converging it would move the shipped city.
- ⚠️ **The deck span is unbounded** (§10.5). WP-B3b.
- **`StreetHeightField` grades the ground under a deck toward a weighted mean of the deck's
  designed ground and the road beneath it** — the whole residual above the feet (§10.4). It
  may be that a structure's deck junctions should not contribute to the field at all; that is
  a decision, and it belongs with whoever decides what a deck looks like.
- **`Yelukhdidru@400` cannot carry a structure at all**: its longest straight-through
  corridor is 112.8 m against the 160 m two ramps need at 10 %, and `Yelukhdidru@100`
  generates nothing. Recorded by a test so that a ruleset change which quietly makes small
  cities liftable is visible rather than silent.
- **`StructureProfile`'s malformed-ramp branch** (a ramp that does not change level, two
  ramps claiming one deck junction) is reachable from no builder in the tree and is covered
  by fixtures only — deliberately, and named here so it is not mistaken for tested-by-data.
