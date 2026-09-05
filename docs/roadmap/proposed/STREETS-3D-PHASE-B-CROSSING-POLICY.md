# Phase B — the crossing policy

**Status:** implementation plan. Three design decisions **settled by the owner** (§3).
**Follows:** Phase A (`STREETS-3D-TOPOLOGY.md` §7a … §7s).
**Precedes:** Phase C — structures. Deliberately last: *a floating slab is unfinished, not wrong.*

The question this answers: when two streets meet, do they actually **join**, or does one
pass over the other?

---

## 0. What exists — and what the first draft of this plan got wrong

The first draft claimed *"WP-4's multilayer support is complete; the only missing piece is
the decision."* **That is false**, and the corrections matter more than the claim did.

**Built and correct:** `Stroke.Level` / `StreetPoint.Level` (`sbyte`); `StrokeKind`;
`StreetLevels.DeckHeight = 8f`; `OverpassBuilder.Build` producing an unattached
ramp/deck/ramp chain; `NetworkBuilder.CommitChain` (atomic) and `_checkLevels` (throws
unless only a ramp changes level, adjacent decks only); V1 omits `Level`, V2 includes it.
`OverpassBuilder` is referenced in production by **two comments and no code**.

**Wrong in the first draft, each verified against the tree:**

1. ⚠️ **`ClearanceConstraint` and `SpanLengthConstraint` are built but NOT in the
   pipeline.** `Generator._buildPipeline` lists eight constraints and neither is among
   them; `GenerationContext.RampClearance` is assigned only in `MultilayerTests`. A ground
   candidate crossing a **ramp** — recorded at `Level = groundLevel` — gets a Split verdict
   on it, and `SplitStrokeAt` has no `Kind` guard and calls `AddStroke` directly,
   **bypassing `_checkLevels`**. The rule that exists to prevent this is inert.
2. ⚠️ **`IntersectionConstraint` is not where crossings are decided.** Junctions created by
   a split: **1 / 1 / 0 / 11 / 1** across five cities, against **2 / 11 / 64 / 256 / 66**
   four-arm crossings. Crossings form by **snapping**, over two candidates. Any seam there
   sees ≤ 4 % of them.
3. **Level filtering covers four `StrokeStore` queries**, not "every" one.
   `QueryStreetPoints` (used by `Placer`), `GetStreetPoints`, `GetStrokes` are unfiltered.
4. **`OverpassBuilder` hard-codes `IsPrimary = true`** on all three members.
5. **`ConnectComponentsPass` is level-blind and bypasses `NetworkBuilder`** — it can join a
   deck junction to the ground with a non-ramp, no throw. It runs **after** the queue drains.
6. **`ClusterStorage.DbVersion = 1039`** must be bumped, or every already-visited city
   stays ground-only forever.

---

## 1. The distinction that must not be blurred

`Level` is a **topological deck index** — *"do these two meet?"* Terrain height is
**continuous** — *"how far apart are they?"* Collapsing them repeats the `Pos3` mistake.

**Height informs the policy; the policy sets `Level`; `Level` drives the filtering.**

---

## 2. Buildability — measured, and it reshapes the phase

`SuccessorEmitter` emits `60 + 40·w²` clamped to ≥ 75 m, so **no candidate is longer than
128 m** (stroke length median 75 m, max 137 m over five cities).

Roads buildable at `2·max(DeckHeight/g, 30) + 30`, Yelukhdidru/3000 (490 roads, 256
crossings, 200 primary):

| deck / ramp grade | within ONE candidate | crossing span | **corridor** | primary corridor |
|---|---|---|---|---|
| 8 m / 5 % (the primary's own `GradePolicy` limit) | **0 / 490** | 0 | 306 (62 %) | 114 / 200 |
| 8 m / 10 % | 5 (1 %) | 8 | 414 (85 %) | 166 / 200 |
| 8 m / 14 % | 297 | 326 | 469 | 191 / 200 |
| 5 m / 10 % | 334 | 391 | 476 | 195 / 200 |

> **A structure replacing one candidate cannot exist at any grade the heavy road is held
> to.** The usable run is the **corridor** — median **418–450 m** through typically four
> junctions.

`DeckHeight = 8 m` is not the lever: ≈5 m clear plus deck depth, and the 4 m the tests use
as a floor leaves no truck clearance.

---

## 3. The three decisions — SETTLED

### 3a. Seam → **two-stage generator** (option C)

Plan the arterial skeleton **including its separations first**, then grow the ordinary
network around it. Chosen over a post-pass because **no side street can ever attach where a
deck will be** — a post-pass has to orphan the side arms of every intermediate junction it
lifts — and because Phase C's piers and abutments need the structure known early.

⚠️ **The cost, and it drives the whole design: restructuring the loop changes the order
strokes are popped, and order is output.** So the two-stage path is **gated**: with
`EnableGradeSeparation` off, `Generate()` runs exactly the loop it runs today, untouched.
Two code paths, deliberately, because the alternative is a flag-off gate that cannot hold.

### 3b. Deck elevation → **keep `level · DeckHeight`, refuse what does not clear** (A + D), **prepared for B**

Terrain-difference / "free separation" is **out of scope**: `Level = 1` adds 8 m *on top of*
whatever the hill did, and the ramp climbs `8 + ground(deckStart) − ground(from)`, so rising
ground makes it steeper, not shallower.

Nothing bounds real clearance today — `GradeRelaxer` knows nothing of `Kind` or `Level` and
relaxes the ground under a deck junction like any other. So a structure whose clearance
fails a post-check is **refused**, and the refusal *rate* is the measurement that decides
whether option B is needed.

**"Prepared for B" is a concrete requirement, not a sentiment**: exactly one expression owns
deck elevation, and every consumer reads it — the §7o `RoadSurface.HeightAtJunction`
pattern, which replaced five copies. Today it would return `ElevationOf(stroke.Level)`;
under B it returns a stored per-deck value, and that is a one-place edit.

### 3c. Blocks → **decks and ramps are absent from the block graph** (option A)

No pseudo-vertex is needed: with the deck and ramps out of the graph and the corridor's
ground strokes gone, the remainder is planar again by itself.

⚠️ **This merges the blocks either side of a lifted corridor — and an estate IS a block**,
so a merged block means a new estate, a new `ClipperOffset` footprint, a new building, new
shop fronts and new TALE locations. **That is a cost of the phase, stated here rather than
discovered at WP-B4.** Physically it is right: a viaduct does not bound a city block.

---

## 4. Work packages

The policy is **injected into `Generator` like `RuleTable`** — not read from
`GlobalSettings`, which is process-global and untestable, as the height-source comment
records. The global is read once in `ClusterDesc._generateStrokes`.

### WP-B0 — the gate (measurement and a design note; no production code)

| AC | criterion |
|---|---|
| B0.1 | **minSide, span and corridor** distributions reported separately. The unit matters: span gives the wrong answer. |
| B0.2 | Buildable fractions at 5 / 10 / 14 % ramp grade, per city and world-wide, per unit. |
| B0.3 | The same restricted to corridors whose arms are `IsPrimary`. |
| B0.4 | **Block census** on a sample of lifted corridors: quarters before/after, delimiters on a Ramp/Bridge/Tunnel, blocks containing a junction in their interior, self-intersecting blocks. |
| B0.5 | **How many blocks merge, and what that moves** — estates, footprints, buildings, shops, TALE locations — for a representative city. This is §3c's cost, quantified. |

### WP-B1 — make the built machinery real (flag off throughout)

| AC | criterion |
|---|---|
| B1.1 | `ClearanceConstraint` and `SpanLengthConstraint` are **in the pipeline**, `RampClearance` supplied. Mutation: dropping either must fail a test. |
| B1.2 | **No ground stroke is ever split on a ramp**; no junction on a ramp or deck except the builder's own. |
| B1.3 | `ConnectComponentsPass` never joins two levels with a non-ramp, and goes through `NetworkBuilder`. |
| B1.4 | `SplitStrokeAt` **refuses** a non-`Street` target rather than bypassing `_checkLevels`. |
| B1.5 | One expression owns deck elevation (§3b); every consumer reads it. Mutation: a second copy must fail a scan. |
| B1.6 | Flag off: V1, V2 and `street-geometry.json` byte-identical; cost **within the existing `StreetCostTests` gate** (2 % ceiling with tolerance — not a byte comparison); TALE 200/200. |
| B1.7 | Constraint **order** for the eight existing checks unchanged; the two added ones are no-ops with the flag off, asserted. |

### WP-B2 — the two-stage generator

| AC | criterion |
|---|---|
| B2.1 | **Flag off runs today's single loop, unmodified** — asserted by the B1.6 baselines still holding after the two-stage path exists. |
| B2.2 | Stage 1 emits the arterial skeleton; stage 2 grows around it. Both stages are deterministic and **draw RNG in a stated order**. |
| B2.3 | Nothing in stage 2 attaches to a deck or ramp — the property C was chosen for. Mutation: removing the guard must fail. |
| B2.4 | `maxGenerations` is budgeted across both stages, not applied twice. |

### WP-B3 — structures

| AC | criterion |
|---|---|
| B3.1 | `MaxRampGrade` named, with its derivation written down. |
| B3.2 | A corridor shorter than the bound is **never** lifted — **plus a positive control**: a fixture just over the bound **must** lift. |
| B3.3 | Every emitted ramp is within `MaxRampGrade`, measured on its **own emitted geometry**, on a **sloping** source so the ground term is non-zero. |
| B3.4 | **Clearance**: deck `RoadSurface` minus crossed-road `RoadSurface` at the plan crossing, in a **terrain** city, ≥ a stated bound; a structure that fails is refused. |
| B3.5 | **The refusal rate is reported** — it is the measurement that decides whether §3b option B is needed. |
| B3.6 | Successors emitted from the far foot only; no stroke has `Level ≠ 0` except Bridge/Tunnel members. |
| B3.7 | A refused structure is **visible** — `GenerationReport` gains structure and refusal counts; a refusal is a `Warning`. "No bridges appeared" must not be indistinguishable from "the policy never fired". |
| B3.8 | `OverpassBuilder` stops hard-coding `IsPrimary`. |

### WP-B4 — hierarchy, spacing, angle

| AC | criterion |
|---|---|
| B4.1 | Weight ratio plus an absolute floor: **two alleys never separate**. |
| B4.2 | Which road takes the deck asserted on `Kind` — `OverpassBuilder` always lifts the *candidate*, so "the heavier takes the deck" means `Tunnel` for the other. |
| B4.3 | A heavy road with a junction already within `N` m separates instead of adding another. |
| B4.4 | An oblique crossing separates. **Resurrects a finished thought**: `PointNearStrokeConstraint` computed `angleVice`/`angleVersa` and discarded both behind `if (true \|\| …)`. |
| B4.5 | Each predicate has a **positive control**; `if (false)` around any of them must fail a test. |

### WP-B5 — blocks

| AC | criterion |
|---|---|
| B5.1 | Decks and ramps absent from the block graph: no delimiter on a Ramp/Bridge/Tunnel or at level ≠ 0. |
| B5.2 | No block contains a junction in its interior; no block self-intersects. **All three failed on the first lifted crossing.** |
| B5.3 | Merged blocks are counted and their downstream movement reported against B0.5's prediction. |

### WP-B6 — turn it on

| AC | criterion |
|---|---|
| B6.1 | Structures per city and their kinds, for the shipped world. |
| B6.2 | **Connectivity, falsifiably** — component count cannot change, so instead: for each removed at-grade crossing, shortest path between its four neighbours before/after as a detour distribution; plus every junction reachable by A* over car lanes, which catches a deck lane with no ramp lane. |
| B6.3 | `ClusterStorage.DbVersion` bumped. |
| B6.4 | Every baseline that moves recorded old → new, per city, classified as a genuine network change or a re-hash. |
| B6.5 | TALE 200/200 run **after** the flip. |

---

## 5. Consumers to check rather than assume

- **TALE** — `SpatialModel.ExtractFrom` makes a `street_segment` location per `StreetPoint`,
  **deck junctions included**, so NPCs get scheduled onto a bridge with `GetSectionArray()`
  corners nudged off the deck edge.
- **`Placer` / `citizen.SpawnOperator`** nearest-junction lookups are **unfiltered** by level.
- **`GradeRelaxer`** may relax the ground under a deck junction to a grade the ramp above
  cannot meet.
- **Pedestrian crossings** — drawn per junction; a separated crossing has none. Expected to
  follow automatically, *which is exactly the kind of expectation this project has been
  wrong about.* Assert it.
- **Nav lanes over ramps** — a deck lane needs the deck elevation as well as
  `NavJunction.GroundHeight`. Check which it gets.
- **Flat cities** — `EnableGradeSeparation` is independent of `DisableClusterFlattening`, so
  hierarchy/spacing/angle would fire in a flat city too. Decide whether they should.

---

## 6. How this work has gone

Every one of these bit in Phase A: **measure before diagnosing** (the obvious diagnosis was
wrong in about half the rounds); **mutation-test every gate** — `if (false)` round a branch
passes a source scan, a scan matching a bare identifier is satisfied by a field declaration
or a comment, `X = 0f * Call(...)` passes a scan looking for the call, a scan sees the name
of a call but not how many of its results are used, **a containment test cannot tell a guess
from a refusal**, and **a rule can be invisible to unlimited real data** when no baseline
city contains the shape it governs; and **a `Trace` in a `catch` is a silent failure**.

The review of this plan's first draft is the lesson restated: two of its central claims were
wrong, and only measurement showed it.

**Specific to Phase B:** this is the first change in the workstream that moves the street
**network** rather than a surface on it. Blocks, estates, buildings, shops, TALE locations
and nav are all derived from it.
