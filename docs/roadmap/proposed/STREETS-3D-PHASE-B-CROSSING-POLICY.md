# Phase B — the crossing policy

**Status:** implementation plan, twice reviewed. **WP-B1 is DONE (2026-09-05, §7 below).**
**§7.3's corridor mid point is FIXED (2026-09-05, §8) — the one change in this phase that
deliberately moves a baseline.** **WP-B2 is DONE (2026-09-05, §9).**
**WP-B0 may proceed.** **WP-B3a is DONE (2026-09-05, §10).**
**WP-B3b is DONE (2026-09-05, §11) — structures are placed.**
**The relaxation converges (2026-09-06, §12), which re-measured every number above it.**
**WP-B4 is DONE (2026-09-06, §13) — the crossing has to be worth lifting: 20 structures
across the seven pinned seeds on the shipped terrain, and the flag is still off. Two
decisions in §13.2 and §13.4 are the owner's.**
**WP-B5 is DONE (2026-09-06, §14) — a deck and its ramps are absent from the block graph,
a structure's footprint is excluded from the estate it now stands in, and two structures
may not cross. Flag off AND flag on, not one baseline byte moved. WP-B6 is unblocked and
is the only thing left in Phase B.**
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

### WP-B4 — hierarchy, the deck, spacing and angle — ✅ **DONE 2026-09-06 (§13)**

The plan's original tables, kept because §13 answers them one by one:

| AC | criterion | how it is met |
|---|---|---|
| B4.1 | Weight ratio **and** an absolute weight floor: two alleys never separate, whatever their ratio. | ⚠️ **Half.** The floor is shipped, derived from `GradePolicy.WeightMin`, and is refused by **0 of 667** real crossings - because a flag-on city has no alley in it at all (§13.2). The **ratio is NOT shipped as a refusal**, with the measurement that says why and the decision left to the owner. |
| B4.2 | The heavier road takes the deck and the lighter passes underneath - asserted on identity, not on which was the candidate. | ✅ Asserted on `Kind` and on the deck's `Level`: `Bridge` when the corridor is the heavier, **`Tunnel`** when the road it crosses is. Until now every structure was a bridge whatever the two weighed (§13.6). |
| B4.3 | A heavy road that already has a junction within `N` m separates instead of adding another. | ✅ `N` is the ruleset's own longest street, 127.5 m, and it is a **walk through bends** rather than an arm length. ⚠️ Its whole window is 28 m wide (§13.3). |
| B4.4 | Crossing angle: an oblique crossing separates. **This resurrects a finished thought that was abandoned.** | ⚠️ Resurrected, and **it says the opposite of what this line says it says** - the abandoned code discarded the near-PARALLEL case. §13.4 has both readings and both counts. |
| B4.5 | Every predicate has a positive control: `if (false)` around any of them must fail a test. | ✅ Twenty-two mutations, one survivor - §13.8. |

### WP-B5 — blocks — ✅ **DONE 2026-09-06 (§14)**

| AC | criterion | how it is met |
|---|---|---|
| B5.1 | A `Ramp`/`Bridge`/`Tunnel` is absent from the block graph, and blocks merge across a lifted corridor. | ✅ `BlockGraph.IsBlockEdge` names the three kinds; the trace refuses to START on one and refuses to LEAVE a junction along one, and a mutation that removes either half alone is caught by a different set of tests. |
| B5.2 | The block census before and after, per city — **what the merge costs**. | ✅ §14.1. ⚠️ **The block count goes UP on four of seven flat cities**, because the faces the old trace discarded for `hasNullSection` come back and outnumber the merges. |
| B5.3 | Three properties asserted, not counted: no delimiter on a structure or at `Level != 0`, no block containing a junction in its interior, no self-intersecting block. | ✅ §14.2 — and ⚠️ **the second one is not true of the shipped city**, so it is asserted on the block graph's 2-core instead, which is exactly the junctions on a cycle. |
| B5.4 | A structure-footprint exclusion on the estate, and **measure what it moves**. | ✅ §14.4. 39 of 229 buildings in the flat cities stood on a structure's carriageway, worst by 5841 m²; zero after. It moves shop fronts and no buildings. |
| B5.5 | The two-decks rule decided and costed. | ✅ **Refuse** — level 2 needs 179.7 m of arm against a longest stroke of 179.2 m anywhere, so `ArmTooShort` would refuse it regardless (§14.5). Cost: **0 corridors**, gated by fixtures both ways round. |
| B5.6 | Flag off: every baseline byte-identical; `StreetCostTests` in its gate; TALE 200/200. | ✅ And flag ON too — WP-B5 changes nothing about the network itself. 1613 xUnit against 1532, TALE 200/200. |

### WP-B6 — turn it on

Unchanged in intent. B6 records that the `DbVersion` bump **deletes the whole
`worldcache`** (cluster list included — verify it regenerates identically) and that
**save games resolve strokes by id with `FirstOrDefault`**, so a regenerated network
silently resolves the same id to a different stroke. §14.9 adds two consumers that are
still wrong with the flag on: TALE makes a location on every deck end, and
`GenerateNavMapOperator` draws a pedestrian crossing that pairs an ordinary arm with a
ramp.

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

---

## 11. WP-B3b as built (2026-09-05) — structures exist, and five things the plan and the brief got wrong

**Flag off, nothing moved**: `street-fingerprints.json`, `street-geometry.json`,
`street-cost-baseline.json` and `street-relaxed-heights.json` are byte-identical to
`818ae1bf`, TALE is 200/200, and 1450 xUnit against 1365 before. **Flag on, the recorded V2
fingerprint moved on six of eight seeds** — that is the point, and §11.6 records old → new.

### 11.1 THE HEADLINE — what a city gets, on the shipped terrain

| seed | crossings considered | **structures placed** | refused |
|---|---|---|---|
| `seed000@500` | 10 | **0** | ArmTooShort 5, InteriorTBranch 4, DeckGrade 1 |
| `seed011@500` | 9 | **0** | InteriorTBranch 4, ArmTooShort 3, DeckGrade 1, Overlaps 1 |
| `Yelukhdidru@400` | 1 | **0** | InteriorTBranch 1 |
| `Yelukhdidru@800` | 26 | **2** | InteriorTBranch 8, Overlaps 8, ArmTooShort 4, DeckGrade 4 |
| `seed000@1500` | 140 | **1** | InteriorTBranch 54, ArmTooShort 44, Overlaps 21, DeckGrade 20 |
| `seed017@2400` | 366 | **6** | InteriorTBranch 159, ArmTooShort 83, Overlaps 74, DeckGrade 43, WouldDisconnect 1 |
| `Yelukhdidru@3000` | 549 | **9** | InteriorTBranch 204, ArmTooShort 130, Overlaps 124, DeckGrade 75, DeckClearance 4, RampClearance 3 |

**Eighteen structures across seven cities**, and D2's "a handful of correct structures is the
goal" is met rather than argued about. The deck grades that came out are **−4.48 … +5.52 %**,
against the **+4.2 / +11.4 / +4.3 / −4.7 / +23.7 / +21.6 %** §10.5 measured when nothing was
refusing anything.

**A structure is one crossing, lifted.** An interior junction whose arms include a nearly
opposed pair of ordinary streets has that pair **replaced** by a ramp–deck–ramp chain between
the two far ends; the junction stays exactly where it is and keeps its other arms, and the
road that used to cross now passes underneath. The two lifted strokes are removed: keeping
them would put a ground road and a ramp along one line, which is the single thing
`ClearanceConstraint` exists to forbid, and would leave the at-grade crossing the structure
was built to remove.

### 11.2 ⚠️ The deck grade bound — TWO EXISTING NUMBERS AND NO NEW ONE

The brief asked for a bound and a justification. `GradePolicy.MaxDeckGradeFor` is

    min( MaxGradeFor(deck) , MaxRampGrade )

— i.e. **the road's own limit, and never steeper than the ramps that reach it**. A deck is not
a special kind of climb the way a ramp is; it is the road, carried in the air, so it is held
to exactly what that road would be held to on the ground: `MaxGradeFor`'s interpolation over
weight, 5 % for the heaviest corridor and 14 % for the lightest. The cap then binds below
weight **0.69**, because a deck that out-climbs its own ramps is a ramp with a longer name.
So a heavy corridor gets 5 %, a light one 10 %, and **every value in between comes from a
number that already existed and was already argued for** — `MaxGradeFor` stays the one
expression for "how steep may this be", which is what §10.1 built it to be.

⚠️ **And it is the rule that does the work.** Over the seven seeds the deck's own grade
refuses **144** corridors, against 3 for the ramps' plan clearance and 4 for the deck's
vertical one. §2's corridor-fit table cannot see this at all — it asks whether two ramps FIT,
and geometry alone still admits what it predicted: **the same cities on LEVEL ground place 1,
1, 0, 6, 21, 49 and 88**, because there no corridor is refused for its deck grade. Nine
against eighty-eight is the clearest statement available of what the bound is doing.

### 11.3 ⚠️ THE FINDING THE PLAN DID NOT HAVE: a lift can take a city apart

A grade separated crossing **has no slip roads**. The through road and the road beneath it
stop being connected to each other at that point, and the lift removes two edges while adding
a path between their far ends — so a junction that reached the rest of the network only
through one of them can be cut off.

On the shipped terrain this never happens: every one of the seven seeds is **one component**
with its structures on it. **The same `seed017@2400` on level ground, where the deck grade
refuses nothing and fifty structures go in, comes out in two pieces.** §9's *"a rule can be
invisible to unlimited real data"* one more time, and this time the data that cannot see it is
the terrain data the whole work package is measured on. `StructureRefusal.WouldDisconnect`
refuses it, at a cost of one corridor in seed017 (which the deck grade was going to refuse
anyway) and nothing anywhere else.

### 11.4 The interior-T-branch rule: EXCLUSION, and what it really costs

A junction with three arms loses its through pair and is left with one: a road that now ends
nowhere, hanging off a junction that is no longer a junction. The plan offered exclusion or
re-attachment at a foot. **Exclusion**, for two reasons: re-attaching moves the stub's
junction by a ramp length, taking its blocks, its estate and its buildings with it, in service
of a road that is not why the structure is being built; and what would then pass under the
deck is a dead-end stub, i.e. nothing worth flying over. A crossing worth lifting is one where
the road underneath goes somewhere.

⚠️ **The refusal tally overstates the cost by about 2.4×, and counting it was worth doing.**
The rule refuses 204 corridors in `Yelukhdidru@3000` — but only **85** of them have arms long
enough to hold two ramps at all, 71 of 159 in `seed017@2400`, 23 of 54 in `seed000@1500`, and
**0 of 4, 0 of 4 and 0 of 1** in the three small cities. So the exclusion's own cost is those
85/71/23, before the deck grade refuses ~55 % of what survives geometry — a handful of
structures per large city, not two hundred.

### 11.5 The clearance report (old B3.5), and the deck model does not need decoupling yet

Twenty crossings pass under a deck across the seven cities. Clearance runs **4.53 … 20.30 m**
against a nominal 8, i.e. **0.57× to 2.5×**, and the spread is entirely the ground: a deck is
pinned one deck height over the ground **at its own ends**, and what passes underneath is at
whatever height the ground is in between. So the fixed `level · DeckHeight` model is not what
limits this; the terrain under the crossing is.

**Nothing is below `MinDeckClearance`, and that is a refusal rather than luck** — four
corridors of `Yelukhdidru@3000` are refused for it. The floor is **derived, not chosen**:
`MetaGen.ClusterNavigationHeight` is the height the game's own traffic flies at over the
ground, so a deck that does not clear it is a wall across the road.

### 11.6 What moved, and what did not

**Flag off: not one byte.** All four recorded baseline files are identical to `818ae1bf`.

**Flag on, `street-fingerprints-gradesep.json`** — recorded on a FLAT city, deliberately: on
level ground nothing is refused for its deck grade, so the geometry rules stand alone and the
fingerprint gates them with nothing in the way.

| seed | before | after |
|---|---|---|
| `seed000@500` | `n=29,s=34,h=97ABED7C7FF3FA97` | `n=31,s=35,h=D18B8C78C4ADCCB7` |
| `seed011@500` | `n=28,s=31,h=899EF900895842CC` | `n=30,s=32,h=D3AB6B3DAF7F0C95` |
| `Yelukhdidru@400` | `n=13,s=12,h=5F724760E94FFB5B` | **unchanged** |
| `Yelukhdidru@100` | `n=0,s=0,h=E3B0C44298FC1C14` | **unchanged** |
| `Yelukhdidru@800` | `n=63,s=79,h=D745E1F1F870C6D1` | `n=75,s=85,h=F7C14761D85A6876` |
| `seed000@1500` | `n=247,s=350,h=59C046B7ADE50270` | `n=289,s=371,h=9198C8462FC0DF65` |
| `seed017@2400` | `n=613,s=883,h=1177475AC6745821` | `n=711,s=932,h=AC42116B1440395B` |
| `Yelukhdidru@3000` | `n=930,s=1379,h=E10F9F863B39C786` | `n=1106,s=1467,h=1795C32E638B1870` |

The arithmetic checks out on its face: `Yelukhdidru@3000` gains 176 junctions (88 structures ×
two deck ends) and 88 strokes (88 × three members, less 88 × two removed arms).

**`ClusterStorage.DbVersion` is NOT bumped** — that is WP-B6's.

**Blocks move on the flag-on city, as §3c predicted**: quarters go 445 → 382 on
`Yelukhdidru@3000` (a lift merges the four blocks round a crossing), and *up* on three of the
others — 82 → 89, 221 → 228, 2 → 3 — because a ramp and a deck are themselves traced as block
edges. That is WP-B5's subject and is recorded here rather than acted on.

### 11.7 Why the refusal can be exact, which §3b said it could not be

§3b: *"there is no relaxed height at generation time; `RelaxedStreetHeight` called during
generation returns the partial store and caches it permanently."* True of
`RelaxedStreetHeight` — and beside the point, because the quantity a refusal needs is not the
relaxed height of the finished city. It is the **anchor** height, which §10.2 built:
`GradeRelaxer.Relax` first relaxes the network *as if the structures were not there*. That is
a pure function of (ground strokes, sampled terrain, policy), and the placer computes it
directly, from the same unrelaxed `TerrainStreetHeight` the game uses, over exactly the ground
strokes the finished network will have.

So the grades refused on **are** the game's, and it is asserted rather than argued:
`TheFinishedCityCarriesExactlyTheGradesThePlacerJudged` relaxes the finished store and finds
every ramp at **10.000 %** and every deck at the grade the report recorded, to five decimals,
on seven cities that were never told what answer to give.

The one thing that makes this work is the **fixed point**: lifting a corridor removes its two
arms, which changes the ground network the anchor pass runs over — so refusing one changes the
heights for the others. The candidate set is re-measured until it stops shrinking, in memory,
with nothing committed until it has.

### 11.8 Three smaller things

- **`OverpassBuilder.Build` takes a ramp LENGTH**, `RampLengthFor(policy, groundLevel,
  deckKind)` = the climb divided by `MaxRampGrade` = 80 m. It used to take a fraction of the
  run, which makes a long corridor's ramps steeper than a short one's — the opposite of a
  design. Phrased through `StreetLevels.ElevationOf` rather than `DeckHeight`, so WP-B1.7's
  single-expression scan still holds, which also makes it right for a tunnel.
- **`IsPrimary` is carried, not asserted.** It was hard-coded `true` on all three members;
  §0.4 says it is an orientation bit `SuccessorEmitter` flips per branch, so the chain takes
  it off the arm it replaces. Asserted **both ways round**, because a hard-coded `false` would
  pass a test that only ever built from a secondary road.
- **`StrokeStore.GetStrokesNear`** is `GetRampsNear` with the kind filter lifted out, so §7.1's
  five-term expression — the four endpoint distances *and* the crossing test — has exactly one
  copy. A second copy of that test is how the two come to disagree, and §7.1 is a work
  package's worth of evidence that the endpoint terms alone miss the case the rule exists for.

### 11.9 ⚠️ The trap that cost the most: a junction's identity changes when it joins the store

A `StreetPoint` carries a **process-global provisional id** until `_assignLocalId` gives it
the network's own, and a network's own start at 1. So in a freshly started process a deck end
invented by `OverpassBuilder` and a real junction of the city carry the same number — and
every height here is looked up by that number.

It surfaced as a `KeyNotFoundException` (the table was built before the commit and read
after), which is the *harmless* half. The other half is silent: one junction answering another
junction's question. `_reserveIdsFor` hands each invented junction the id the store itself
would hand it, continuing from the highest issued, and
`AnInventedJunctionNeverBorrowsAnExistingJunctionsIdentity` pins it.

### 11.10 The mutations

**Thirty-two driven; ONE survivor, and three needed a gate written for them.**

| # | mutation | killed by |
|---|---|---|
| 1 | the interior-T-branch rule removed | 15 |
| 2 | a two-arm bend counts as a crossing | 11 |
| 3 | the deck's overhang past the crossing dropped | 5 |
| 4 | the deck grade never refuses | 19 |
| 5 | the ramp grade never refuses | 1 — the malformed-chain fixture, which is the only thing that can reach it |
| 6 | the deck clearance never refuses | 5 |
| 7 | a chain that misses what it crosses is built anyway | 1 |
| 8 | the ramps are never checked for clearance | 5 |
| 9 | connectivity is never checked | 4 |
| 10 | two structures may claim one junction | 8 |
| 11 | the fixed point runs one round | 19 |
| 12 | ⚠️ **the invented junctions' ids are not reserved** | **survived** — see below |
| 13 | the `Warning` deleted | 2 |
| 14 | the anchor pass keeps the lifted arms | 14 |
| 15 | the road a structure replaces is not removed | 15 |
| 16 | any stroke kind is a corridor to lift | 3 — §0.7's `ConnectorBridge` trap |
| 17 | "straight through" loosened to a dot of −0.5 | 14 |
| 18 | candidates walked in descending junction id | 15 |
| 19 | ⚠️ **a grade drops the deck elevation** | **survived**, then 1 — below |
| 20 | the two crossing parameters swapped | 4 |
| 21 | `MinDeckClearance` = 0 | 1 |
| 22 | `IsPrimary` hard-coded `true` again | 6 |
| 23 | the ramp length ignores the policy | 2 |
| 24 | two ramps may swallow the whole deck | 1 |
| 25 | any deck kind accepted | 1 |
| 26 | the deck bound loses its ramp cap | 2 |
| 27 | the deck bound is just the ramp grade | 12 |
| 28 | placement runs with the flag OFF too | **43** — every flag-off baseline |
| 29 | ⚠️ **placement runs before the connect pass** | **survived**, then 1 — below |
| 30 | `ClusterDesc` hands over the RELAXED source | 1 |

**19 — a grade that drops the deck elevation.** It survived everything, and the reason is
worth keeping: **a DECK has both ends on one level, so the term cancels there** — the report's
own deck grades, the refusal, and the end-to-end gate over seven cities all agree with or
without it. Only a **ramp** joins two decks, and on a ramp the term IS the grade: 10 %
reported as 0. The malformed-chain fixture could not see it either, because its broken "ramp"
also has both ends on the ground. Killed by making the expression internal
(`StructurePlacer.RoadGradeOf`) and driving it on a ramp whose two ends stand on level ground,
where the ground says flat and the road climbs a whole deck.

**29 — placement before the connect pass.** Every pinned seed places exactly the same
structures either way, so the recorded fingerprint says nothing at all about the order. It
still matters in both directions: `ConnectComponentsPass` does not run the constraint pipeline
(§7.8), so a `ConnectorBridge` laid *after* a structure could cross a ramp with nothing
checking; and a lift running after the bridging cannot disconnect what the bridging repaired.
Killed by a call-site scan, in the shape and for the reason of B2.5's own.

**12 — ⚠️ THE SURVIVOR, and it is a real hole rather than an equivalence.** The rule itself is
gated: `ReserveIdsIn` is internal and asserted to hand out **exactly the ids the store would
hand out**, because "no invented junction collides with an existing one" is satisfied by luck
whenever the process-global provisional counter sits above the network's range — which in a
test host that has already built a dozen cities it always does. **Deleting the CALL SITE still
passes everything**, and no test in this repository can catch it: the collision needs the
counter in a state only a freshly started process reaches, and forcing it would mean either
allocating 65536 junctions to wrap the counter's low 16 bits or predicting its value across
xUnit's parallel collections. Recorded rather than papered over. §7p's *"a containment test
cannot tell a guess from a refusal"* is the same lesson: here it cannot tell a reservation from
a lucky counter, and the difference is only visible in a process this suite never creates.

### 11.11 What the plan and the brief got wrong

- ⚠️ **The brief's "the buildability criterion is the measured grades — both ramps AND the
  deck".** The deck half is exactly right and is the whole story. **The ramp half is
  unreachable**: both ramps are designed at `MaxRampGrade` from their own feet, so on any
  chain `OverpassBuilder` produces the measurement is 10.000 % by construction. It is kept as
  a backstop against a chain `StructureProfile` REFUSED — and driven directly, on a
  deliberately malformed chain, rather than counted as covered.
- ⚠️ **The plan's §3b, "a post-relaxation refusal must un-build something already on disk".**
  It need not: §11.7.
- ⚠️ **Neither the plan nor the brief mentions connectivity**, and it is the one thing that
  can make a placed structure actively harmful — §11.3.
- ⚠️ **The brief's "~15 ramp-fit crossings per large city, and the intersection may be smaller
  still".** The intersection is smaller, but not for the reason implied: geometry admits 88 in
  `Yelukhdidru@3000`, and it is the deck grade rather than the fit that reduces it to nine.
- **§10.8's "`Yelukhdidru@400` cannot carry a structure at all"** is confirmed and the refusal
  is now visible, but for a different reason than §10.8 recorded: its one straight-through
  crossing is a **T-branch**, and it is refused there before its arms are ever measured.

### 11.12 Found and NOT fixed

- ⚠️ **The placement pass samples terrain while `ClusterDesc._lo` is held.**
  `RelaxedStreetHeight` deliberately samples outside its lock for exactly this reason, and the
  placer cannot: it runs inside `Generate()`, which runs inside `_generateStrokes`, which runs
  inside the lock. It is contention rather than deadlock (the elevation operators below the
  conform layer read only plain `ClusterDesc` properties), and it is on the flag-on path only —
  but it belongs in WP-B6's list.
- **`GradePolicy` is constructed with defaults in two places** — `StreetHeightSources.For` and
  `Generator` — so a tuned policy in one would not reach the other. Harmless while both are
  defaults, which is every shipped configuration.
- **A deck may span only ONE crossing.** §2 notes that corridor length decides how many
  crossings one deck can cover; a multi-crossing deck is not built, deliberately, and the
  single-crossing rule is what produced the eighteen above.
- **Nothing joins the two roads at a lifted crossing.** A real interchange has slip roads; this
  has none, and §11.3 is the guard rather than the fix.
- **`StrokeStore.RemovePoint`, built by WP-B2.4 "for the lift", is not used by the lift.** The
  interior-T-branch rule keeps every junction, so nothing is ever removed. It remains
  `PolishStreetPoints`' one removal primitive and B2.4's reasoning still holds.

---

## 12. The relaxation converges (2026-09-06) — §10.8's first entry, and four things the brief got wrong

`GradeRelaxer` exhausted its whole 32-sweep budget on every generated city and
`RelaxedStreetHeight` discarded the return value that said so. Since `1c54a9e4` the
terrain-following city **is** the shipped city, so every city in the game was being built on
an unconverged relaxation, silently, and had been for as long as the pass had existed.
Recorded in §10.3/§10.8 as found-and-not-fixed on the grounds that converging it moves the
shipped city; the owner has now authorised that move.

**What moved:** `street-relaxed-heights.json` and nothing else. `street-fingerprints.json`,
`street-fingerprints-gradesep.json`, `street-geometry.json` and `street-cost-baseline.json`
are byte-identical, because all four record the FLAT city and a flat city has no over-limit
stroke to correct. `ClusterStorage.DbVersion` is untouched — that is WP-B6's.

### 12.1 What the non-convergence actually was — monotone, and not close

The brief asked whether the largest correction decreases monotonically, plateaus, or
oscillates. **Monotone, with no exceptions at all**: over the seven non-empty seeds the
largest correction in a sweep decreased on *every* sweep — **0 increases in 50 to 308
sweeps**. It was not stuck and it was not oscillating; it was a geometric decay with an
asymptotic ratio of 0.47 to 0.93 per sweep, i.e. **far too slow and nothing else**.

⚠️ **But "how many sweeps does it need" has two answers, and the smaller one is the wrong
question.** Measured to the criterion the code actually used — *the largest correction
applied in a sweep is under a centimetre* — it needs 50 to 308. Measured to the criterion
this pass exists to guarantee — *no stroke is more than a centimetre of rise over its own
limit* — the same rule needs **82 to 1106**. The gap is the finding: a damped sweep's
corrections shrink geometrically whether or not the network has become buildable, so the
old exit test called `Yelukhdidru@3000` settled with **312 of its 1875 strokes still over
their limit**, one of them by 0.58 m. **The convergence test was measuring the wrong
quantity**, and that was not in the brief's list of possibilities.

**What the shipped city actually had**, per seed, at the 32-sweep cut-off (strokes over their
own limit by more than a centimetre / worst grade / worst excess in metres of rise):

| seed | over limit | worst grade | worst excess |
|---|---|---|---|
| `seed000@500` | 8 of 29 | 7.8 % | 0.39 m |
| `seed011@500` | 10 of 24 | 10.0 % | 0.42 m |
| `Yelukhdidru@400` | 9 of 11 | 12.2 % | 0.73 m |
| `Yelukhdidru@800` | 39 of 74 | 11.0 % | 1.07 m |
| `seed000@1500` | 179 of 367 | 18.6 % | 7.08 m |
| `seed017@2400` | 417 of 1034 | 24.1 % | 7.03 m |
| `Yelukhdidru@3000` | **743 of 1875** | **31.2 %** | **7.88 m** |

`GradePolicy` allows 5 % to a weight-1.3 arterial and 14 % to the lightest alley, so 31.2 %
is not a rounding error — it is more than twice the steepest street the ruleset admits, and
it was in the shipped game.

### 12.2 ⚠️ The brief's hypothesis: half right, and it does not help

> *a single global `busiest` divisor over-damps every junction except the busiest one, so
> convergence is linear and slow rather than absent. The standard formulation divides each
> junction's accumulated correction by its own stroke count.*

The diagnosis is right and **the remedy is not enough**. Three Jacobi variants and the rule
that landed, all with the same exit test (worst residual excess under `ConvergenceEpsilon`)
and no tolerance skip, so the only thing being compared is the damping:

| rule | sweeps needed, min … max over the seven seeds |
|---|---|
| Jacobi, one global divisor (what shipped) | 82 … **1106** |
| Jacobi, divided by each junction's static degree | 44 … 488 |
| Jacobi, divided by its **active** strokes this sweep | 28 … 260 |
| **successive projection (what landed)** | **11 … 85** |

Per-junction damping is roughly **2.3× faster and still 8× over budget**. And the code
comment it would have replaced was right too: it warned that dividing the two ends of a
stroke by different numbers stops the pair cancelling and creeps the network uphill —
measured, mean height drift **+0.33 m** on `Yelukhdidru@3000` and **+1.27 m** on
`Yelukhdidru@400`, against ±0.001 m for the global divisor. So the hypothesis buys a
factor of two and costs a metre of drift.

⚠️ **Over-relaxation was tried and is worse than useless here.** ω of 1.5, 1.9, 2.5 and 4
all improve the rate and none gets a Jacobi sweep inside the budget (`Yelukhdidru@3000`
needs 133 at ω = 4); at ω = 6 and 8 it **diverges**, reaching 10²⁰ within thirty sweeps on
five of seven seeds. There is no ω that both converges and fits.

⚠️ **And "just raise `MaxSweeps`" — the naive fix the brief warned against — is naive for a
reason nobody had, which is that it is not 4× but 35×.** The 1106 above is what the shipped
rule needs on the largest city.

### 12.3 What landed: successive projection, and it removes a constant rather than adding one

`GradeRelaxer.RelaxAround` corrects each over-limit stroke **as it is visited**, to exactly
its own limit, splitting the correction between the two ends by resistance exactly as
before; the strokes visited after it read the heights that produced. The
accumulate-a-whole-sweep-then-divide-by-`busiest` machinery is gone — **`busiest`, the
`degree` map, the `delta` dictionary and its per-sweep `OrderBy` all deleted** — because a
projection that touches one stroke at a time cannot overshoot, and overshoot is the only
thing damping was ever for.

Two other changes, both of which use an existing number rather than a new one:

- **`ConvergenceEpsilon` now measures the residual excess, not the correction applied.** The
  same 0.01 m; the question it answers is *is any road steeper than the policy allows*
  instead of *have the corrections got small*. §12.1 is why.
- **A stroke within `ConvergenceEpsilon` of its limit is left alone.** The tolerance does
  both jobs, which makes "settled" and "this sweep changed nothing" the *same statement*.
  ⚠️ **That is not cosmetic**: `Relax` is two calls on a city with a structure in it (the
  anchor pass, then the sweep around the boundary), and with the tolerance applied only to
  the exit test the settled anchor pass still applied its last millimetres, so
  `AddingAStructureMovesNoOtherJunctionOfTheCity` failed by up to **4.3 mm** on junctions
  the far side of the city from the structure. Applying it to the correction as well
  restores exact equality.

**`GradePolicy.MaxSweeps` 32 → 256.** Not a licence to iterate: the measured worst case is
80 (`Yelukhdidru@3000`, the largest city the game builds) and 256 is three times that. The
per-seed counts are pinned by `TheSweepsAShippedCityNeedsAreRecorded` so that a ruleset
change which quietly doubles them is visible long before it hits the cap.

**It is also cheaper.** Timed over 20 runs on `Yelukhdidru@3000`: **12.55 ms → 3.80 ms**,
because a projection sweep has no dictionary to build and no `OrderBy` over 1379 entries to
run 32 times.

**And the report exists.** `RelaxedStreetHeight.TableFor` is the production expression —
sample every junction, relax, and `Warning` naming the cluster, its size and its stroke
count if the budget ran out. It is a `Warning` and not a `Trace` because a debug category
decides how much *detail* to keep and never whether a problem is reported. Extracted as a
static taking its inputs because `_ensureRelaxed` reaches `ClusterDesc.StrokeStore()`, which
needs `ClusterStorage`, `MetaGen` and the event queue in the container; the two lines left
outside it are held by a call-expression scan.

⚠️ **Deliberately NOT also a property on the class.** An `Exhausted` field only this class
could read would be state nothing drives, and the fact is observable where it belongs — in
the log, which is what the test asserts on.

### 12.4 What it cost: the order the strokes are visited in

This is the one property the round gave up, and it was given up on measurement.
`GradeRelaxerTests.TheOrderStrokesAreVisitedInDoesNotMatter` asserted it explicitly, with a
comment naming Gauss-Seidel as the thing that would lose it.

Measured: relaxing the same city with the stroke list reversed moves junctions by **0.36 to
0.80 m at the median and 6.8 to 10.5 m at the worst** on the four largest seeds. That is
real. It is also smaller than what the property was buying: the shipped answer stood **up to
15.9 m** from where its own algorithm would have taken it, so what was being protected was
the order-independence of a number that was not the answer. And the four rules in §12.2's
table agree on the answer they reach to within a decimetre at the median — they differ only
in whether they get there.

⚠️ **The old gate would still have PASSED**, which is why it is replaced rather than deleted:
a six-stroke chain of equal weights is symmetric enough that both directions land on the
same heights. A gate that says something untrue and does not fail is worse than no gate.
`OneVisitingOrderGivesOneAnswer` replaces it (one fixed `Sid` order, one answer, exact
equality) and `ADifferentSidOrderIsADifferentAnswer` states the loss where it can fail — the
same star built with its spokes created in the opposite order, which is the only way a test
can renumber `Sid`s.

### 12.5 The blast radius, measured

**Every junction of every terrain city moves.** New heights against old, per seed:

| seed | junctions moved | p50 | p95 | worst | mean drift |
|---|---|---|---|---|---|
| `seed000@500` | 26 / 27 | 0.51 m | 1.28 m | 1.34 m | +0.002 m |
| `seed011@500` | 23 / 23 | 0.29 m | 1.52 m | 3.24 m | +0.008 m |
| `Yelukhdidru@400` | 11 / 12 | 0.67 m | 1.87 m | 1.87 m | −0.000 m |
| `Yelukhdidru@800` | 60 / 64 | 0.56 m | 1.45 m | 1.89 m | −0.015 m |
| `seed000@1500` | 259 / 274 | 1.21 m | 7.22 m | **13.86 m** | −0.062 m |
| `seed017@2400` | 679 / 785 | 0.48 m | 4.47 m | 10.17 m | +0.011 m |
| `Yelukhdidru@3000` | 1146 / 1379 | 0.61 m | 5.37 m | **16.53 m** | +0.016 m |

Substantial rather than cosmetic — a median junction moves half a metre and the worst moves
sixteen — and the **mean drift is within 6 cm of zero everywhere**, so the city settled
rather than sliding down the mountain. Through the heights this reaches blocks, buildings,
shops, TALE locations, nav lanes and the conform pass, none of which has a baseline of its
own for the terrain city.

**After: every stroke of every seed is inside its own limit.** 0 over, worst excess 0.00 m,
against the table in §12.1.

**The flat city does not move**, asserted as exact equality over whole generated cities
rather than argued: 0 junctions of 27 / 23 / 12 / 64 / 274 / 785 / 1379 changed, and the
relaxation exits after one sweep having found nothing to do.

### 12.6 ⚠️ The structures WP-B3b places change, and not in one direction

The anchor pass **is** this relaxation, so a corridor's two feet stand at different heights
and the deck between them is a different deck. Per seed, considered / placed / refused for
deck grade:

| seed | before | after |
|---|---|---|
| `seed000@500` | 10 / 0 / 1 | unchanged |
| `seed011@500` | 9 / 0 / 1 | unchanged |
| `Yelukhdidru@400` | 1 / 0 / 0 | unchanged |
| `Yelukhdidru@800` | 26 / 2 / 4 | unchanged |
| `seed000@1500` | 140 / 1 / 20 | 140 / **2** / **18** |
| `seed017@2400` | 366 / 6 / 43 | 366 / **4** / 43 |
| `Yelukhdidru@3000` | 549 / 9 / 75 | 549 / **11** / **73** |

**Eighteen structures became nineteen** — and it is not a uniform shift, which is the honest
shape of it: a settled city is a different set of foot heights, not a flatter one. Deck
grades of what was built move from −4.48 … +5.52 % to **−5.32 … +4.82 %**; the deck-grade
refusal loosens from 144 to **140** and the clearance refusals tighten from 7 to **10**.

⚠️ **The clearance under a deck got tighter at the bottom end**: 24 crossings pass under a
deck instead of 20, over **3.39 … 27.52 m** against 4.53 … 20.30 m. 3.39 m is 1.13×
`MinDeckClearance` where the old worst was 1.51×, so a settled city passes traffic under a
deck with less to spare. Nothing is below the minimum; it is worth knowing.

Every terrain city still comes out in **one component**, and `seed017@2400` now refuses one
corridor for `WouldDisconnect` on the shipped terrain — §11.3 said that rule had only ever
fired on level ground.

### 12.7 The mutations

**Twenty driven, eighteen killed, one survivor that is provably equivalent, and one that
could not be compiled** — which is itself a result. One of the eighteen was killed only on
the second attempt, and it is the interesting one.

| # | mutation | outcome |
|---|---|---|
| 1 | correct anything over the limit, no tolerance skip | 31 failed |
| 2 | exit on `worst < ConvergenceEpsilon` instead of `0f == worst` | ⚠️ **survived — equivalent** |
| 3 | `MaxSweeps` back to 32 | 13 failed |
| 4 | the `B` end of an unpinned stroke not corrected | 49 failed |
| 5 | re-introduce a damping factor of ½ | 22 failed |
| 6 | `worst` never updated | 49 failed |
| 7 | `sweeps > MaxSweeps` instead of `>=` | 1 failed |
| 8 | the exhaustion `Warning` deleted | 1 failed |
| 9 | the report does not name the city | 1 failed |
| 10 | a both-pinned stroke corrected anyway | 24 failed |
| 11 | pinned `A`: the free end takes only its share | 1 failed |
| 12 | the resistance split replaced by half and half | 12 failed |
| 13 | the anchor pass gets its own allowance | 2 failed |
| 14 | the tolerance ten times looser | 22 failed |
| 15 | the sweep count discarded again | 1 failed |
| 16 | pinned `B`: the free end takes only its share | 1 failed |
| 17 | the report demoted from `Warning` to `Trace` | **does not compile** |
| 18 | the height source bypasses `TableFor` | 1 failed (the scan) |
| 19 | the sweep loop stops after one sweep | 48 failed |
| 20 | a stroke too short to have a grade corrected too | ⚠️ **survived the first round** |

**(2) is equivalent and provably so.** With the tolerance skip in place, a stroke is only
corrected when it is `ConvergenceEpsilon` or more over its limit, so `worst` is either
exactly 0 or at least `ConvergenceEpsilon` and the two tests cannot disagree. It is written
as `0f == worst` regardless, because that is the statement being made — *this sweep changed
nothing* — and because removing the skip then makes the loop never converge and be reported,
which is loud, where the epsilon form would quietly exit early again.

⚠️ **(20) is the survivor that named something, and it took three attempts to write a
fixture for it.** Widening `if (length < 0.001f)` to `if (length < 0f)` passed the whole
suite, because nothing in the tree can produce a stroke inside that guard's band — and the
reasons are worth having: an *exactly* zero length stroke never reaches the guard, since
`Stroke.Length` throws below 1e-6 m and the relaxer reads `s.Length` on the line above;
`StreetPoint.SetPos` quantises to a **0.1 m** grid, a hundred times the guard's ceiling; and
`StrokeStore.AddPoint` refuses a point "considerably close" to one it already has. The
guard's whole reachable surface is a caller writing `StreetPoint.Pos` directly and handing
the strokes to `Relax` as a plain list, which is what `AStrokeTooShortToHaveAGradeIsLeftAlone`
does. Not harmless if it ever were reached: the limit is grade times length, so such a stroke
is over its limit by whatever its two ends differ by and drags them together every sweep.

⚠️ **(17) is the most interesting non-result: the report CANNOT be demoted to a `Trace`.**
`Trace` has a `ref DebugInterpolatedStringHandler` overload that an interpolated argument
prefers, so `Trace(_dc, $"..." + ...)` does not compile at all — `error CS1620: Argument 2
must be passed with the 'ref' keyword`. The CLAUDE.md entry about the suppressed
`Warning(Dc, $"...")` overload is the same machinery seen from the other side: here it makes
the wrong log level unwritable.

**(11) and (16) are the §7q symmetric-survivor lesson honoured rather than re-learned** —
the pinned end is `A` on one approach and `B` on the other, two separate lines, and
`AFreeEndTakesTheWholeExcessAndNotItsShare` asserts both.

### 12.8 Two existing gates superseded, old text recorded

- **`GradeRelaxerTests.TheOrderStrokesAreVisitedInDoesNotMatter`** → §12.4. Old text:
  *"Visiting order must not change the answer, which is what Jacobi buys and what a
  Gauss-Seidel version of the same loop would quietly lose."*
- **`StructureHeightTests.AFreeEndTakesTheWholeExcessAndNotItsShare`** asserted
  `-60 + 45/2` and `100 - 77/2`, with a comment reading *"the fixture's busiest junction has
  two strokes, so one sweep applies half of whatever it was given"*. The halving was the
  damping divisor; there is none, so it is `-60 + 45` and `100 - 77` and one sweep now puts
  the free end exactly on the limit. **The property under test is unchanged** — whole
  excess, not the split share — and the factor of two belonged to the mechanism.

Two comments elsewhere said "GradeRelaxer exhausts all 32 sweeps on every generated
network"; both are corrected in place with the old claim kept, and `STREETS-3D-TOPOLOGY.md`
§7a's *"Jacobi, with one damping divisor for the whole graph"* bullet is superseded there.

### 12.9 What the brief got wrong

- ⚠️ **"Does `largest` oscillate?"** No, and it never could: it decreased on every one of
  50–308 sweeps on every seed. The failure mode was slowness, full stop.
- ⚠️ **"Is there a stroke or junction that never settles — two limits in conflict, or a
  stroke whose limit cannot be met at all?"** No. The system is a set of difference
  constraints `|h_B − h_A| ≤ L`, which is always feasible (all heights equal satisfies it),
  and run with the exit test disabled the shipped rule does reach feasibility — at 500 to
  5000 sweeps. Nothing is in conflict; the budget was three per cent of what the rule
  needed. **What looked like a stuck configuration in the first measurement was the exit
  test firing early** (§12.1).
- ⚠️ **"Per-junction damping converges far faster and is also the more correct
  formulation."** 2.3×, not far, and it introduces the drift the original comment warned
  about — §12.2. Neither the hypothesis nor the code comment it disputes was wrong; both
  were arguing about a sweep that never finished.
- ⚠️ **"`street-fingerprints-gradesep.json` may move."** It cannot: it records
  `GenerateHeavyFirst`, which builds on FLAT ground, and so does `street-geometry.json`. The
  terrain city has exactly one baseline — `street-relaxed-heights.json` — and it is the only
  file that moved.

### 12.10 Found and NOT fixed

- **A ruleset with a genuinely pathological site would now spend 256 sweeps before saying
  so.** At 3.8 ms per 80 sweeps on 1875 strokes that is ~12 ms, once, at generation time —
  costed and accepted, and the `Warning` names the cluster so it is findable.
- **The tolerance leaves a stroke up to 1 cm of rise over its limit**, by construction and
  by name. Over a 100 m street that is 0.01 % of grade.
- **`GradePolicy` is still constructed with defaults in two places** (§11.12) and `MaxSweeps`
  is now one of the numbers that would differ if they ever diverged.
- **The 20 m conform grid, `Yelukhdidru@400`'s uncarryable corridor, the unbounded deck span
  and everything else in §10.8 and §11.12** are untouched by this round.

---

## 13. WP-B4 as built (2026-09-06) — worth lifting, and five things the plan and the brief got wrong

WP-B3b decided whether a structure **can** stand at a crossing. WP-B4 is the four predicates
that ask whether the crossing is **worth** separating: hierarchy, which road takes the deck,
junction spacing, crossing angle.

**Flag off, nothing moved**: `street-fingerprints.json`, `street-geometry.json`,
`street-cost-baseline.json` and `street-relaxed-heights.json` are byte-identical to
`9a3d37c4`, TALE is 200/200, and 1532 xUnit against 1488 before. **Flag on, four of the
eight recorded V2 fingerprints moved** — §13.7 has old → new.

### 13.1 THE HEADLINE — the count after each predicate, added one at a time

Structures over the seven pinned seeds, on the shipped terrain, per seed
`seed000@500 / seed011@500 / Yelukhdidru@400 / @800 / seed000@1500 / seed017@2400 /
Yelukhdidru@3000`:

| policy | per seed | total | tunnels |
|---|---|---|---|
| WP-B3b as it stood | 0/0/0/2/2/4/11 | **19** | 0 |
| + B4.1 hierarchy (floor) | 0/0/0/2/2/4/11 | **19** | 0 |
| + B4.2 the heavier road takes the deck | 0/0/0/2/2/4/9 | **17** | 2 |
| + B4.3 junction spacing | 0/0/0/2/3/3/9 | **17** | 1 |
| + B4.4 obliquity — **all four** | 0/0/0/2/3/5/10 | **20** | 1 |

Each predicate alone, from the same starting point: B4.1 → 19, B4.2 → 17, B4.3 → 19,
B4.4 → 22.

⚠️ **THE FEARED COLLAPSE DID NOT HAPPEN, AND THE REASON IS NOT THAT THE PREDICATES ARE
WEAK.** The brief expected a hierarchy predicate to take 19 to nearly zero. It takes it to
19, because **the hierarchy it was to measure does not exist in the city that carries
structures** (§13.2). And the total ends up *above* where it started, because a refusal can
free a junction for a neighbour (§13.5).

### 13.2 ⚠️ THE BIGGEST FINDING: a heavy-first city has no alleys in it at all

The brief's premise, and §2's: *"the weight distribution is brutal — 1308 of 1875 strokes in
Yelukhdidru@3000 sit at exactly 0.200, 94 % below 0.5, and only ~35 reach 1.0"*. True — **of
the FLAG-OFF city**. The city that can carry a structure is the flag-on one, and it is a
different city:

| seed | flag off | flag on |
|---|---|---|
| `seed000@1500` | 367 strokes, p50 **0.630**, min 0.333, 48 at ≥ 1.0 | 371, p50 **1.200**, min 0.972, 364 at ≥ 1.0 |
| `seed017@2400` | 1034, **494 at exactly 0.200**, p50 0.207, 22 at ≥ 1.0 | 932, **0 at 0.200**, min 0.852, 852 at ≥ 1.0 |
| `Yelukhdidru@3000` | 1875, **1308 at exactly 0.200**, p50 0.200, 35 at ≥ 1.0 | 1467, **0 at 0.200**, min 0.786, 1406 at ≥ 1.0 |

**Not one stroke of any flag-on city sits at `WeightMin`.** WP-B2's heavy-first ordering does
not merely decide *when* a candidate is judged — the heavy candidates drain first, fill the
space, and the light branches behind them are refused by the ordinary proximity rules when
their turn finally comes. §9 recorded that the ordering *changes the city that comes out*;
that it changes what the city is **made of** was not recorded anywhere, and WP-B4 found it
only by going looking for an alley and failing to find one.

Two consequences, and both are the answer to a question the brief asked:

- **The absolute floor does the work in principle and nothing in practice.** *"Two alleys
  never separate"* is refused by **0 of 667** crossings across the seven cities. It is not
  wrong, it is invisible to real data — §9's lesson again — so it is gated by fixtures both
  ways round and the invisibility is itself asserted.
- ⚠️ **The ratio cannot express hierarchy on this ruleset, because there is none to
  express.** At the 667 crossings that reach the test the corridor's weight runs
  **0.85 … 1.30** (p50 1.20) and the road beneath **0.87 … 1.30**, so the ratio between them
  is **1.00 … 1.37, p50 1.08**. Measured cost of shipping it as a refusal, of the nineteen:
  ≥ 1.0001 (refuse a tie) leaves **10**, ≥ 1.05 leaves 8, ≥ 1.1 leaves 6, ≥ 1.25 leaves
  **0**. That is not a hierarchy filter, it is the ±10 % jitter of `SuccessorEmitter`'s
  weight draw dressed as one.

**So the ratio is NOT shipped as a refusal, and that is a deviation from B4.1 stated rather
than smuggled.** Two reasons, one measured and one structural: the numbers above, and that
refusing a crossing of two *equal* roads refuses the classic interchange — a motorway
crossing a motorway is the most separated junction there is. What the ratio does instead is
decide **which** road takes the deck, which is B4.2, and the same comparison serves it.

### 13.3 B4.3 — the spacing bound is the ruleset's own, and the window it leaves is 27.8 m

`Generator.MaxJunctionSpacing` derives from `EmitterSettings.LengthAtWeight` at
`weightMax` — **the longest street the ruleset lays**, `127.5 m` for the shipped numbers
(⚠️ 127.5 and not the 127.6 the arithmetic gives on paper: the emitter truncates to a
decimetre and `1.3f * 1.3f` is a hair under 1.69). A junction farther away than any single
street the ruleset can lay is a road that is not being interrupted here, and an ordinary
at-grade junction serves it. That expression now has exactly one copy, read by
`SuccessorEmitter` to emit and by the placer to bound.

⚠️ **The window this rule can act in is 27.8 metres wide, and that is the ruleset's doing.**
The same corridor must hold an 80 m ramp plus the deck's 19.7 m overhang, so a crossing is
worth lifting between **99.7 m and 127.5 m** of junction spacing and nowhere else. Below
99.7 `ArmTooShort` has already refused it; above 127.5 this rule does. Measured over the
seven cities: junction spacing at a crossing runs 30.4 … 267.6 m with p50 106.5, and **63**
corridors are refused for it.

⚠️ **And it has to be a WALK, not `min(armA.Length, armB.Length)`.** A `StreetPoint` with
two arms is a **bend**: nothing crosses there and nothing stops, so the walk goes through
it. Measured over the 667 crossings that reach the test, the two answers have medians
**106.5 m against 83.4 m** and maxima **267.6 m against 128.1 m** — and the arm-length
reading cannot say anything `ArmTooShort` has not already said, since it is bounded below by
the same 99.7 m and above by what the ruleset can lay. The walk terminates on distance
rather than on step count, because the question is never "how far exactly" but "farther than
this".

⚠️ **A street CAN be longer than the bound, by up to one snap.** `SnapToNearbyPointConstraint`
may move a candidate's far end onto an existing junction up to
`minPointToCandPointDistance` = 30 m away, which is the only thing that lengthens a stroke
after it has been emitted: measured, the longest street in the seven cities is **148.4 m**
against the emitted bound of 127.5. `NoStreetExceedsTheBoundExceptByASnap` asserts exactly
that and no more, because "no street is longer than 127.5 m" is a claim that is not true.

### 13.4 ⚠️ B4.4 — the resurrected thought says the OPPOSITE of what the plan says it says

The brief: *"an oblique crossing separates. ⚠️ This resurrects a finished thought: the
original `PointNearStrokeConstraint` computed `angleVice`/`angleVersa` and threw both away
behind `if (true || …)`; WP-2b removed the dead operands but recorded the intent. Find that
history before writing new trigonometry."*

Found, at `634c9613^`:

```csharp
float angleVice  = Single.Abs(geom.Angles.Snorm(curr.Angle - si.StrokeExists.Angle));
float angleVersa = Single.Abs(geom.Angles.Snorm(Single.Pi + angleVice));
if (true || angleVice < (Single.Pi/4f) || angleVersa < (Single.Pi/4f)) {
    /* Discarding stroke ..., point b too close to stroke */
```

under a comment reading *"We might want to check here, if it is perpendicular to the stroke
as opposed to parallel. If it is perpendicular, we might be able to keep it, it might be a
meaningful route."*

`angleVersa` is `pi - angleVice`, so the condition is **"within a quarter turn of
parallel"**, and what it guarded was a **discard**. The abandoned thought kept the
perpendicular case and threw the parallel one away — the opposite of *"an oblique crossing
separates"*.

**Geometry agrees with the code and not with the plan.** A deck crossing at angle `t`
shadows the road beneath it over `width / sin t`, so at a quarter turn it already covers one
and a half road widths and below that the deck is running *along* the road rather than over
it. So `StructurePlacer.MaxObliqueDot = cos(pi/4)`, the original's own threshold, refusing a
crossing within a quarter turn of parallel: **64 corridors** over the seven cities, and
**not one** of the twenty structures they get is inside it.

⚠️ **What the plan's own reading costs, measured rather than argued about.** Invert the rule
— only an oblique crossing separates — and the first thing refused is the plain four-arm
crossroads, which is this work package's own positive control and the shape every textbook
overpass has. Over the seven cities that reading leaves **ONE** structure against twenty:
71 of the 667 crossings that reach the test are within a quarter turn of parallel, and one
of the nineteen WP-B3b placed was. **This is the one WP-B4 decision that is the owner's
rather than the data's** — the counts for other thresholds, as a *requirement* on obliquity,
are 45° → 1, 50° → 2, 55° → 3, 60° → 7, 65° → 10, 70° → 13 of nineteen.

The angle is measured against the **chord** the structure will stand on, not against either
arm: a corridor is only straight to within `MinStraightDot`, and the deck runs foot to foot.
A bent corridor's chord lies *between* its two arms, so no single fixture can disagree with
both — there are two, mirror images, and each kills one arm reading (§7q's symmetric-survivor
lesson honoured rather than re-learned).

### 13.5 ⚠️ A REFUSAL PREDICATE THAT INCREASES THE NUMBER OF STRUCTURES

B4.4 refuses 64 corridors and the total goes **17 → 20**. B4.4 alone takes 19 → **22**.

The mechanism is `OverlapsAStructure`. Candidates are walked by junction id and greedily
claim the junctions and strokes they need; a corridor refused for its own sake never claims
anything, and a neighbour that was being refused for the overlap gets them instead. Over
the seven seeds that refusal falls from **228 to 164** while the new refusals account for
127. So "how many corridors did the policy refuse" and "how many structures did the city
lose" are different questions with different answers, and only the second one is the yield.

This is also why the three new predicates are judged **before** the claim checks: they are
properties of the crossing alone — its two weights, its own neighbourhood, its own angles —
so their tallies do not move when a decision elsewhere changes.

### 13.6 B4.2 — which road takes the deck, and what shipped before it

**Plainly: every structure WP-B3b built was a `Bridge` carrying the corridor over,
whatever the two roads weighed.** The corridor is the road that runs straight through, and
`OverpassBuilder` was always asked for a bridge.

The heavier road takes the deck. The structure is always built ON the corridor — that is the
road with room for ramps — so this is a statement about the **kind**: a `Bridge` when the
corridor is the heavier, a `Tunnel` when the road it crosses is. Equal weights keep the
bridge: a span is the cheaper structure and it is what shipped. Asserted on `Kind` and on
the deck's `Level`, never on which road was the candidate.

Result: **22 of the 134 structures the seven flat cities get are tunnels**, and 1 of the 20
on the shipped terrain. The terrain number is small because the terrain refuses most
corridors on the deck's grade long before their weights are compared.

⚠️ **A tunnel's clearance is the same question mirrored, and it is the one place the two
kinds are not symmetric.** Over a bridge deck the road beneath must fit under it; over a
tunnel bore the ordinary road passes above. So the measurement changes sign — and it is a
**signed** quantity rather than a magnitude, because a bore twelve metres over the road
above it is not a tunnel and a deck twelve metres under the road below it is not a bridge.
`MinDeckClearance` refuses both.

### 13.7 What moved, and what did not

**Flag off: not one byte.** All four recorded baseline files are identical to `9a3d37c4`.

**Flag on, `street-fingerprints-gradesep.json`** — recorded on a FLAT city, per §11.6:

| seed | before | after |
|---|---|---|
| `seed000@500` | `n=31,s=35,h=D18B8C78C4ADCCB7` | **unchanged** |
| `seed011@500` | `n=30,s=32,h=D3AB6B3DAF7F0C95` | **unchanged** |
| `Yelukhdidru@400` | `n=13,s=12,h=5F724760E94FFB5B` | **unchanged** |
| `Yelukhdidru@100` | `n=0,s=0,h=E3B0C44298FC1C14` | **unchanged** |
| `Yelukhdidru@800` | `n=75,s=85,h=F7C14761D85A6876` | `n=73,s=84,h=099A56648102AD93` |
| `seed000@1500` | `n=289,s=371,h=9198C8462FC0DF65` | `n=287,s=370,h=4C8B911460734E31` |
| `seed017@2400` | `n=711,s=932,h=AC42116B1440395B` | `n=697,s=925,h=2336010F09CDA1DE` |
| `Yelukhdidru@3000` | `n=1106,s=1467,h=1795C32E638B1870` | `n=1060,s=1444,h=188C09DF977D80D2` |

The flat cities place **134** structures where they placed 166, so the four seeds that move
lose a structure or two each and the four that do not have no crossing WP-B4 changes its
mind about. **`ClusterStorage.DbVersion` is NOT bumped** — that is WP-B6's.

Terrain, after: deck grades **−5.71 … +4.82 %**, 23 crossings pass under a structure over
**3.39 … 27.52 m**, every city still **one component**.

### 13.8 The mutations

**Twenty-seven driven; TWO survivors, one provably equivalent and one that named dead code.** The interesting thing about the list is what it took to
get there: three of the fixtures below exist only because a mutation walked through the
first version of them.

| # | mutation | failures |
|---|---|---|
| 1 | the hierarchy floor deleted | 2 |
| 2 | the floor reads only the corridor's weight | 1 |
| 3 | the floor reads only the road beneath | 2 |
| 4 | the floor takes the lighter of the two | 3 |
| 5 | the floor is strict rather than inclusive | 2 |
| 6 | always a bridge — i.e. what WP-B3b shipped | 12 |
| 7 | always a tunnel | 22 |
| 8 | the LIGHTER road takes the deck | 16 |
| 9 | a tie goes to the tunnel | 20 |
| 10 | the clearance sign is always positive | 14 |
| 11 | the clearance is a magnitude rather than signed | 1 |
| 12 | the spacing rule deleted | 19 |
| 13 | spacing is the arm length, not the walk | 17 |
| 14 | spacing takes the farther of the two directions | 22 |
| 15 | the walk stops at every point, bend or not | 17 |
| 16 | the obliquity rule deleted | 17 |
| 17 | the threshold is a third of a turn rather than a quarter | 24 |
| 18 | the angle is measured from the west arm, not the chord | 5 |
| 18b | the angle is measured from the east arm | 2 |
| 19 | the spacing bound is `newLengthMin` | 30 |
| 20 | `LengthAtWeight` loses its floor | 24 |
| 21 | the bridge and tunnel counters swapped | 10 |
| 22 | the three predicates run after the claim checks | 2 |
| 23 | the walk has no early distance return | ⚠️ **survived — equivalent** |
| 24 | the walk does not notice coming back round the ring | ⚠️ **survived — the branch was dead** |
| 25 | the road beneath is judged by its lightest arm | 6 |
| 26 | the chain takes the weight of the road beneath | 10 |

**11 is the one the fixtures had to be built for.** `abs(deck − ground)` accepts a bore
twelve metres OVER the road above it and a deck twelve metres UNDER the road below it, and
the ordinary tunnel and bridge fixtures cannot see the difference — both refuse either way,
because a two-metre shortfall is a shortfall whichever sign it has. It is killed by two
fixtures whose structure is on the wrong side of what it passes by a wide margin.

**18 and 18b are §7q's symmetric-survivor lesson honoured rather than re-learned.** A bent
corridor's chord lies *between* its two arms, so a single fixture can only ever disagree
with one of them; there are two, mirror images, and each kills one arm reading. One fixture
would have left the other alive.

**22 costs only two tests, and that is the honest size of it.** Judging the three
predicates after the claim checks changes which corridors are refused *for what*, not
usually whether they are refused, so the yield mostly survives it — what it breaks is the
tallies, which is exactly the property the order exists for (§13.5).

**23 survives and always will.** The walk returns as soon as it is past the distance it was
asked about, and every caller compares the answer against exactly that distance — the
placer against `maxJunctionSpacing`, the test against `Single.MaxValue`, which the early
return can never reach. It is a bound on the work, not on the answer, and it is written the
way it is because the question the method answers is *"farther than this?"* and not *"how
far?"*.

⚠️ **24 survived and named something: the "have I come back round to m" test was DEAD
CODE.** m is a crossing with at least three arms by construction, so the arm-count test
fires the moment the walk arrives back at it. **Deleted rather than given a fixture** — the
only way to reach it is to hand the method something production never produces, and §7q made
exactly this call about its own unreachable fallback. What bounds the walk is the distance
test and the iteration guard, which is what mutation 23 is about.

### 13.9 Found and NOT fixed

- ⚠️ **Two decisions are the owner's, and both are written up with numbers rather than
  opinions**: whether a **weight ratio** should refuse a crossing at all (§13.2 — it costs
  19 → 10 / 8 / 6 / 0 structures at 1.0001 / 1.05 / 1.1 / 1.25, on a ruleset whose crossings
  differ by at most 1.37), and which way round the **obliquity** rule points (§13.4 — as
  shipped it refuses the near-parallel crossing, which is what the code it resurrects
  actually did; the plan's own wording refuses everything squarer and leaves one structure
  in the world).
- ⚠️ **`joyce.EnableGradeSeparation` changes what a city is MADE OF** (§13.2), which is a
  WP-B2 property nothing recorded and which WP-B6 has to state when the flag goes on: the
  flag-on city is not the shipped city with bridges added, it is a city of arterials.
- **The spacing rule's whole admitting window is 27.8 m wide** (§13.3). It is not a defect
  of the rule but of how close the ruleset's longest street is to the length of two ramps,
  and it is the first thing an arterial ruleset would change.
- **A structure is still one crossing, one deck, no slip roads**, and everything in §10.8,
  §11.12 and §12.10 is untouched by this round.

---

## 14. WP-B5 as built (2026-09-06) — blocks, and six things the plan and the brief got wrong

Blocks are traced over the ground network only. **Flag off, nothing moved**:
`street-fingerprints.json`, `street-fingerprints-gradesep.json`, `street-geometry.json`,
`street-cost-baseline.json` and `street-relaxed-heights.json` are byte-identical to
`095dc891`, TALE is 200/200, and 1613 xUnit against 1532 before. **Flag on, not one
fingerprint moved either** — see §14.6, which is itself one of the findings.

### 14.1 THE HEADLINE — what the merge costs, and ⚠️ IT IS NOT A COST

§3c promised a number for "blocks merge across a lifted corridor" and nobody had produced
one. Quarters per city, on the WP-B4 network, before and after this work package:

| seed | flat, before → after | terrain, before → after |
|---|---|---|
| `seed000@500` | 2 → **2** | 3 → **3** |
| `seed011@500` | 3 → **3** | 3 → **3** |
| `Yelukhdidru@400` | 0 → **0** | 0 → **0** |
| `Yelukhdidru@800` | 8 → **7** | 11 → **11** |
| `seed000@1500` | 54 → **61** | 85 → **85** |
| `seed017@2400` | 152 → **165** | 232 → **233** |
| `Yelukhdidru@3000` | 264 → **282** | 378 → **379** |

⚠️ **The count goes UP on four of the seven flat cities and never down by more than one**,
which is the opposite of what "blocks merge" sounds like and of what §11.6 predicted when
it recorded 445 → 382 on `Yelukhdidru@3000`. Two effects, and the second is the larger:

- a lift **does** merge the two blocks either side of its corridor — that is real, and it
  is why `Yelukhdidru@800` loses one;
- but tracing straight **through** a ramp produced faces that were then discarded whole,
  silently, for `hasNullSection` — the trace wandered onto a deck, reached a deck end with
  one arm, and threw the face away. Those blocks come back.

So the honest statement is not "the merge costs N blocks". **Two opposite effects act on
the same count and the recovery is the larger of the two**, and nothing in the block count
alone can separate them — what says the merge is real is that no block runs along a
structure any more (§14.2), not that the count went down. Estates track quarters exactly
(one estate per block, always). Buildings and shops, same cities, before → after:

| seed | flat buildings | flat shops | terrain buildings | terrain shops |
|---|---|---|---|---|
| `seed000@500` | 2 → 2 | 0 → 0 | 3 → 3 | 51 → 51 |
| `seed011@500` | 3 → 3 | 83 → 83 | 3 → 3 | 83 → 83 |
| `Yelukhdidru@400` | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| `Yelukhdidru@800` | 6 → 6 | 301 → **251** | 7 → 7 | 389 → 389 |
| `seed000@1500` | 50 → **57** | 558 → **914** | 81 → 81 | 1554 → **1525** |
| `seed017@2400` | 74 → **80** | 922 → **1050** | 119 → **118** | 2076 → 2076 |
| `Yelukhdidru@3000` | 77 → **81** | 1088 → **1273** | 111 → 111 | 1869 → **2023** |

For reference, the same seven cities with the flag OFF — which is a different city
altogether (§13.2), not a comparison: 3/2/0/10/82/221/445 quarters, 3/2/0/3/81/134/148
buildings, 69/40/0/113/1336/3015/2904 shops. Those are unchanged to the last unit and are
now asserted per seed by `TheFlagOffBlockCensusIsUnchanged`, because no baseline file
records a block census and `street-geometry.json` covers only five of the seven.

### 14.2 The three properties, asserted

The brief's requirement, and each of them failed on the fixture that settled §3c.

| property | flag off | flag on, flat | flag on, terrain |
|---|---|---|---|
| a delimiter on a `Ramp`/`Bridge`/`Tunnel` | 0 | **0** (was 118 in 24 blocks on `Yelukhdidru@3000`) | **0** (was 31 in 9) |
| a delimiter at `Level != 0` | 0 | **0** (was 78) | **0** (was 20) |
| a block whose outline crosses itself | 0 | **0** (was 10 / 8 / 1) | **0** (was 4 / 2) |
| a junction on a CYCLE inside a block | 0 | **0** | **0** |

⚠️ **The third of those needed re-stating before it could be asserted, and the brief's
phrasing — "no block contains a junction in its interior" — is NOT TRUE and never was.**
Measured on the flag-OFF cities before touching anything: `Yelukhdidru@3000` has four
junctions inside three of its blocks and `seed017@2400` two inside two. They are **dead-end
spurs whose own face was discarded for `hasNullSection`**, one of them a two-armed bend on
a two-stroke stub — pre-existing, unrelated, and a gate phrased the brief's way would have
failed on the shipped city before any structure existed. Trap 3 of the brief, in a place
the brief did not expect it.

Two further kinds of junction stand inside a block legitimately once structures exist: a
**deck end**, which carries no block edge at all and is inside the merged block by
construction — that is what a viaduct standing in a block means — and the **dead-end
spurs** above, of which the flag-on cities simply have more.

So the property that is both true and strong is **the block graph's 2-CORE**: peel every
junction with fewer than two block-graph arms until none is left, and what remains is
exactly the junctions that lie on a cycle. **No block may swallow one of those**, because
that is a road the face jumped over instead of turning at — which is precisely §3c's
sixteen-corner face traversing the ground road on both sides. Zero on all seven seeds,
both flags, both grounds, with the 2-core running 7 to 1201 junctions.

### 14.3 ⚠️ THE FINDING: the corner a block turns at is NOT in the section map

`QuarterGenerator` asked `StreetPoint.GetSectionPointByStroke` for each corner, and that
map is keyed on pairs of arms **adjacent in the junction's section array**. The section
array is the junction CAP — and a ramp leaving a foot has a carriageway, so it is in it.

So at every ramp foot the two ordinary arms a block turns between are **not adjacent
there**, the lookup misses, and `hasNullSection` throws the block away. Skipping structures
in the walk without noticing this would have discarded a block at every foot in the city —
silently, with a `Trace`, presenting as "there are fewer blocks now", which is exactly what
this work package was expected to produce anyway. It is §11.4's shape one more time: a
refusal that looks like a result.

The corner is the **mitre of the two arms the block actually turns between**, and it needs
no new trigonometry: `StreetPoint.SectionPointBetween` is the expression
`_computeSectionArrayNoLock` was already using, hoisted out of its loop and made public, so
the section array is now filled **from it**. Where the two arms are adjacent — every
junction of every flag-off city — it is therefore the same float, by construction rather
than by agreement, and that is asserted as exact equality over every adjacent pair of every
junction of the seven cities.

And the "no corner here" rule moves with it: it is **fewer than two BLOCK arms**, not
"the key is missing". Measured before relying on it, over the seven flag-off cities: the
number of junctions with an empty section array **equals** the number with fewer than two
arms, exactly, on every seed. So flag off the two rules are the same rule.

### 14.4 The exclusion: the structure ends up inside the block, and something builds on it

§3c's first gap. The estate is the block outline, `_createBuildings` insets it by the
pavement width, and with the corridor merged the ramps and the deck are inside it.
Measured with the merge in place and the exclusion not yet written:

**39 of 229 buildings in the seven flat cities stood on a structure's carriageway** —
1 / 0 / 0 / 0 / 12 / 12 / 14 per seed, the worst overlapping by **5841 m²** — and 6 of 323
on the shipped terrain, worst 1885 m². A building under a deck 8 m up, or straddling a
ramp.

`BlockGraph.ExcludeStructures` subtracts each structure member's carriageway, widened by
**the block's own `SidewalkWidth`** — the same number the estate is already inset by, so
the strip left beside a ramp is the strip left beside any other road, and no new constant
is introduced. After: **zero overlap, by any area, on every building of every city**.

⚠️ **What it moves is SHOPS and not buildings.** Not one building is removed - the cut
reshapes the footprint and `mn` never falls to zero - so the counts in §14.1 change only
through the shop fronts a shorter perimeter carries: −50 on `Yelukhdidru@800`, −140 on
`seed000@1500`, −82 on `seed017@2400`, −182 on `Yelukhdidru@3000` flat; −22 and −66 on the
terrain. That is the whole measurable cost of the exclusion.

⚠️ **And it exposed something older.** `_createBuildings` concatenates **every polygon** of
the inset solution into one ring — its own TXWTODO says so and it has done it since it was
written. Harmless while an inset of a simple polygon yields at most one piece; nonsense the
moment a subtraction splits a block in two, which produces a single self-crossing outline
and a building designed on it. `ExcludeStructures` returns at most one polygon, the
largest, chosen where the split happens. Not fixed for the general case: an inset that
splits on its own still concatenates.

The no-op path is an **identity**: given nothing to exclude, `ExcludeStructures` returns
the very list it was handed, so a flag-off city runs the code it always ran rather than an
equal-looking rebuild of it.

### 14.5 ⚠️ The two-decks rule — REFUSE, and the arithmetic decides it rather than taste

§3c's second gap: two decks over the same area are both at `Level = 1`, so they cross
without meeting and no junction joins them. The plan offered refusal or sending one to
level 2.

**Level 2 is not an alternative on this ruleset, and it is one line of arithmetic.** The
climb doubles, so `OverpassBuilder.RampLengthFor` asks for `2·DeckHeight / MaxRampGrade` =
**160 m** of ramp, and with the deck's overhang the corridor needs **179.7 m** of arm at
each end. **The longest stroke in ANY of the seven pinned cities with the flag on is
179.2 m** — one stroke, in `Yelukhdidru@400`, a city that places no structure at all.
`ArmTooShort` would refuse every level-2 corridor there is, so refusing outright is the
same outcome named honestly. Pinned by `ALevelTwoDeckWouldNeedMoreArmThanAnyPinnedCityHas`
so that a ruleset which starts laying longer streets makes this decision visible again.

`StructureRefusal.CrossesAnotherStructure` refuses a candidate whose chain crosses, in
plan, any member of an accepted one. Every member against every member, not just deck
against deck: a bridge deck crossing another structure's ramp is the same defect, because
the ramp climbs through the deck's level somewhere along its run and "which is higher
there" has no answer. It is judged with the claim checks rather than with WP-B4's three,
because it is a property of what an earlier corridor was allowed to build.

⚠️ **IT REFUSES NOTHING. not one corridor in any of the seven seeds, flat or on the
shipped terrain, and none of the 402 structure strokes the flat cities carry crosses
another.** §9's *"a rule can be invisible to unlimited real data"* for the third time in
this phase — and unlike §13.2's weight floor, which is invisible because the ruleset makes
no alleys, this one is invisible because `OverlapsAStructure` and `_rampsAreClear` between
them already keep two structures apart in every case the ruleset produces. It is gated by a
fixture both ways round (two corridors whose decks cross: one built, one refused; the same
fixture with one corridor slid 300 m east: both built), and the invisibility is itself
asserted so that the day it starts costing something, the cost is visible.

**No structure count changed anywhere** — 134 flat, 20 on terrain, exactly as WP-B4 left
them.

### 14.6 What moved, and what did not

**Flag off: not one byte**, and now also not one block, estate, building or shop front.

⚠️ **Flag ON: not one byte either, and that is worth saying.** WP-B5 changes only what is
traced over the finished network — the network itself, its structures, its heights and its
grades are untouched — so `street-fingerprints-gradesep.json` is unmoved on all eight
seeds. Every previous work package in this phase moved it. **`ClusterStorage.DbVersion` is
NOT bumped**; that is WP-B6's.

### 14.7 The mutations

**Twenty-three driven; no survivors in the final state — but THREE survived their first
gate, and each of the three named something.**

| # | mutation | failures |
|---|---|---|
| 1 | `IsBlockEdge` always true — structures back in the block graph | 18 |
| 2 | `IsBlockEdge` is "is a `Street`" — §0.7's `ConnectorBridge` trap | 8 |
| 3 | ⚠️ the start-loop filter removed | **survived**, then 6 — below |
| 4 | the `GetNextAngle` filter removed | 22 |
| 5 | `ArmCountOf` counts every arm, not the block ones | 3 |
| 6 | the block corner's two arms swapped | 30 |
| 7 | the section array's two arms swapped | 67 |
| 8 | the structure-footprint exclusion deleted | 8 |
| 9 | the SMALLEST remaining piece of a split estate is kept | 5 |
| 10 | `ExcludeStructures` does not filter non-structures | 1 |
| 11 | a footprint is the bare carriageway, no margin | 6 |
| 12 | a footprint reaches sideways only, not along | 5 |
| 13 | no structure is ever found near a block | 8 |
| 14 | every structure of the city is offered to every block | 2 |
| 15 | two structures may cross | 1 |
| 16 | ⚠️ only the two DECKS are compared | **survived**, then 1 — below |
| 17 | the crossing check ignores what an earlier chain built | 1 |
| 18 | `BlockGraph.Accept` is not `IsBlockEdge` | 25 |
| 19 | the walk's filter is applied to the outgoing arms only | 30 |
| 20 | the walk's filter is applied to the incoming arms only | 35 |
| 21 | the block trace gathers no structures at all | 8 |
| 22 | a junction with one block arm still gets a corner | 63 |
| 23 | `ChainsAreClear` drops its shared-junction test | 1 |
| — | ⚠️ the structure list cached in a field and not refreshed | **survived, and the field was DELETED** — below |

**3 — refusing to START a trace on a structure survived, and the reason is worth having.**
The walk goes out along the ramp, finds no block arm at the deck end, turns round, arrives
back at the junction it started from — and `spNext == spStart` **breaks the loop there**,
before any ordinary stroke has been traversed. So the face is discarded and nothing else
is harmed; the guard is a guard and not a mechanism. It is killed by
`TheBlockTraceNeverTouchesAStructure`, which reads `Stroke.TraversedAB`/`TraversedBA` —
the trace's own record of where it went — rather than inspecting what it produced.

**16 — comparing only the two decks survived, and no fixture built out of two whole
corridors can kill it.** Making two corridors cross at all crosses them deck to deck,
because a deck is the middle of its own chain; a deck crossing another structure's *ramp*
needs a shape the placer cannot be talked into building. `ChainsAreClear` is `internal` for
that reason and is driven directly, with **two** cases — my deck across their ramp and my
ramp across their deck — because either one alone leaves the other alive (§7q).

**⚠️ The one that could not be killed, so the code went instead.** The structure list was a
field filled in `Reset()`, and a mutation that dropped the reset passed everything: the one
production call site and the test harness both construct a fresh `QuarterGenerator`, so no
test in this repository can reach a stale field. Rather than write a test for a call
nothing makes, the field is gone — the list is a local gathered at the top of `Generate()`
and passed down — and the mutation is no longer expressible. Same decision as §13.8's dead
branch, one step further: there, unreachable code was deleted; here, unreachable *state*
was.

**⚠️ And the new tests found a pre-existing flake rather than causing one.**
`LogCapture` installs itself through `engine.Logger.SetLogTarget`, which is a process
global, while xUnit runs collections in parallel — so
`AFlagOffCityHasNoPlacementPassAtAll`, which asserts a line is **absent**, sees whatever a
concurrently generated FLAG-ON city of the same seed name is writing at that moment. Its
own class comment says assertions "look for a fragment naming their own cluster", which is
sufficient for presence and not for absence. It now keeps only what was written on the
capturing thread. That is a gate that used to fail at random instead of when something was
wrong.

### 14.8 What the plan and the brief got wrong

- ⚠️ **"blocks MERGE across a lifted corridor" — true, and the block count goes UP** (§14.1).
  What the merge costs is not what the block count does.
- ⚠️ **§11.6's "quarters go 445 → 382 on `Yelukhdidru@3000`"** was measured with the trace
  running through ramps and decks, so it is not a measurement of anything that now exists.
  On the WP-B4 network the same city goes 264 → 282.
- ⚠️ **The brief's "no block contains a junction in its interior"** is not true of the
  shipped city and never was — §14.2. Six junctions across two flag-off seeds.
- ⚠️ **Neither the plan nor the brief mentions the SECTION MAP**, and skipping structures in
  the walk without it would have discarded a block at every ramp foot, silently — §14.3.
- ⚠️ **"§3c's fix is `GetNextAngle`"** is half of it. Refusing to START a trace on a
  structure is the other half, and a mutation that removes either one alone is caught by a
  different set of tests.
- ⚠️ **"Two decks over the same area are both at `Level = 1`, so `IntersectionConstraint`
  splits one at level 1."** `IntersectionConstraint` runs during the drain and placement
  runs after it, so no candidate street ever meets a deck and that path cannot be reached.
  The exposure is entirely between two PLACED structures, which is a different rule in a
  different file — and it fires on nothing.

### 14.9 Found and NOT fixed

- **`SpatialModel.ExtractFrom` still makes a TALE `street_segment` location per
  `StreetPoint`, deck ends included**, and even adds `LevelElevation` to their height, so a
  flag-on city offers NPCs a location standing on a bridge deck with no way up to it. §5
  listed it as a consumer to check; checked, confirmed, and it belongs to WP-B6.
- **`GenerateNavMapOperator` still draws a pedestrian crossing per junction** from the
  section map, which at a foot pairs each ordinary arm with the RAMP. Not touched here;
  §5's third bullet, still open.
- **`_createBuildings` still concatenates every polygon of its inset**, for the case where
  the inset itself splits a block (§14.4).
- **A merged block is bigger, and building height is derived from `minHouseSide`**, so a
  lifted corridor makes the buildings beside it taller. Measured only as the counts in
  §14.1; nobody has looked at whether it looks right, and nothing will until the flag goes
  on.
- **Everything in §10.8, §11.12, §12.10 and §13.9** is untouched by this round.
