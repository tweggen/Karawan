# Phase B — the crossing policy

**Status:** implementation plan, twice reviewed. **WP-B0 and WP-B1 may proceed.**
**WP-B2 and WP-B3 are blocked** on two owner decisions (§2a) and one mechanism rework (§3a).
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

### 2a. ⚠️ Two decisions for the owner, before WP-B2

- **D1 — ramp grade.** Accept ramps at 10–14 %, steeper than the roads they carry? At 5 %
  the phase produces nothing. (Real interchanges do use steeper ramps than their mainline,
  so this is defensible — but it should be chosen, not defaulted into.)
- **D2 — scope.** Because there are ~30 heavy strokes per city, **stage 1 is a NEW arterial
  ruleset, not a filter over today's strokes.** That means **every flag-on city is a
  different city before any bridge is built**, and B6.4's "genuine change vs re-hash" will
  be dominated by that rather than by structures. Is that the intent, or should Phase B be
  scoped to "make the machinery correct and prove one hand-built overpass survives the
  pipeline", leaving arterial generation to its own phase?

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

**The change that makes it satisfiable: `GradePolicy.MaxGradeFor` becomes `Kind`-aware.**
For a `Ramp`, permitted ground rise is `MaxRampGrade·L − DeckHeight`, so **the relaxer
itself bounds the ramp's total grade** and pins the ground under a deck junction relative to
its foot. A pure-function change in a class with exhaustive tests. Then B3.3 is a *relaxer
property*, and B3.4 becomes a post-relaxation **report** (B3.5) rather than a refusal.

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

### WP-B1 — make the built machinery real (flag off; **safe to proceed**)

These are live defects in shipped code and stand alone whatever happens to the rest of the phase.

| AC | criterion |
|---|---|
| B1.1 | `ClearanceConstraint` and `SpanLengthConstraint` are **in the pipeline**, and their **placement in the order is stated** — a `Reject` before `SnapToNearbyPoint` discards a candidate that would have snapped clear. Mutation: dropping either must fail a test **driven through `Generate()` over a store already holding an overpass**, not a unit test on the constraint (they passed while out of the pipeline). |
| B1.2 | **No ground stroke is ever split on a `Ramp`/`Bridge`/`Tunnel`**; no junction on one except the builder's. Positive control: the same candidate crossing an ordinary street **does** split. |
| B1.3 | `ConnectComponentsPass` filters orphan candidates **by level** and goes through `NetworkBuilder`. Fixture: nearest main junction is a deck junction; control: a ground one is nearer. (As drafted it would convert a silent bad join into a **throw during world generation**.) |
| B1.4 | The refusal lives in the **verdict** — better, the ramp is invisible to the intersection query by `Kind` — with the `_checkLevels` throw as backstop only. |
| B1.5 | `RampClearance` is supplied **only with the flag on**: `GetRampsNear` news two lists per call, so an unconditional supply keeps fingerprints but trips `StreetCostTests`' 2 % ceiling. |
| B1.6 | Flag off: V1, V2, `street-geometry.json` byte-identical; cost within the existing gate; TALE 200/200. |
| B1.7 | A drift scan keeps `ElevationOf` the single deck-elevation expression (already true — this pins it). |

### WP-B2 — the staged generator — **BLOCKED on §3a rework and D2**

Must additionally specify: the stage-2 re-seeding walk (or the priority-queue variant), a
`RemovePoint` that also clears `_octreeSP`, the interior-T-branch rule, and `Build`'s new
ramp-length signature. B2.2's "draws RNG in a stated order" becomes a **recorded V2 baseline
per seed with the flag on**, not documentation.

### WP-B3 — structures — **BLOCKED on D1 and §3b**

`GradePolicy.MaxGradeFor` becomes `Kind`-aware first; B3.3 then becomes a relaxer property
on a sloping fixture, and clearance becomes a post-relaxation **report** whose distribution
decides whether §3b option B is needed.

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
