# Phase B — the crossing policy

**Status:** proposed. **Revised after review, which returned *rethink before writing code*.**
Three design decisions are open (§3) and must be settled before WP-B1.
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
`OverpassBuilder` is referenced in production by **two comments and no code**, and no
shipped ruleset can set a level.

**Wrong in the first draft, each verified against the tree:**

1. ⚠️ **`ClearanceConstraint` and `SpanLengthConstraint` are built but NOT in the
   pipeline.** `Generator._buildPipeline` lists eight constraints and neither is among
   them; `GenerationContext.RampClearance` is assigned only in `MultilayerTests`.
   Consequence: `IntersectsMayTouchClosest` filters on level, so a ground candidate
   crossing a **ramp** — which `OverpassBuilder` records at `Level = groundLevel` — gets a
   Split verdict on it, and `SplitStrokeAt` has no `Kind` guard and calls `AddStroke`
   directly, **bypassing `_checkLevels`**. That yields a "Ramp" between two level-0
   junctions, a half-length ramp climbing the full 8 m, and a ground junction with the
   ramp surface ~4 m overhead. The rule that exists to prevent this is inert.
2. ⚠️ **`IntersectionConstraint` is not where crossings are decided.** Measured: junctions
   created by a split are **1 / 1 / 0 / 11 / 1** across five cities, against **2 / 11 / 64 /
   256 / 66** four-arm crossings. Crossings form by **snapping** — a candidate's B lands
   within 30 m of an existing junction, `SnapToNearbyPointConstraint` moves it there, and
   the forward rule continues from that junction on the far side. **There is no moment when
   "two streets cross"**; a crossing accretes over two candidates. The proposed seam sees
   at most 4 % of them.
3. **"Level filtering in every `StrokeStore` query"** — four neighbourhood queries are
   filtered. `QueryStreetPoints` (used by `Placer`), `GetStreetPoints` and `GetStrokes` are
   not. Harmless today; "every" is the kind of word this project has had to retract before.
4. **`OverpassBuilder` hard-codes `IsPrimary = true`** on all three members, so a structure
   over a secondary road silently promotes it.
5. **`ConnectComponentsPass` is level-blind and bypasses `NetworkBuilder`** — it picks
   orphan points from *all* points, builds `ConnectorBridge` strokes with `Level`
   defaulting to 0, and calls `_strokeStore.AddStroke` directly, so it can join a deck
   junction to the ground with a non-ramp and no throw. It runs **after** the queue drains,
   i.e. after any policy. (That resolves the ordering question the first draft left open.)
6. **`ClusterStorage.DbVersion = 1039`.** Enabling this invalidates every cached cluster.
   Phase A's flag had no such cost — heights are computed at load. This one does, and
   flipping without a bump leaves every already-visited city ground-only forever.

---

## 1. The distinction that must not be blurred

From `STREETS-3D-TOPOLOGY.md` §3: `Level` is a **topological deck index** — *"do these two
meet?"* Terrain height is **continuous** — *"how far apart are they?"* Collapsing one into
the other repeats the `Pos3` mistake this project already made once.

**Height informs the policy; the policy sets `Level`; `Level` drives the filtering.**

---

## 2. Buildability — measured, and it reshapes the phase

The first draft argued buildability might kill the phase, against `STREETS-3D-TOPOLOGY.md`
§5, whose four predicates all ask whether a crossing *wants* separating and none whether it
*can be*. **That reasoning was sound and the conclusion is sharper than it guessed — but
the cause is not terrain, it is the ruleset.**

`SuccessorEmitter` emits `60 + 40·w²` clamped to ≥ 75 m, so **no candidate is longer than
128 m**. Measured over five cities, stroke length is median 75 m, max 137 m.

Roads buildable at `2·max(DeckHeight/g, 30) + 30`, Yelukhdidru/3000 (490 roads through 256
crossings, 200 primary):

| deck / ramp grade | within ONE candidate | within the crossing span | **within the corridor** | primary corridor |
|---|---|---|---|---|
| 8 m / 5 % (the primary's own `GradePolicy` limit) | **0 / 490** | 0 | 306 (62 %) | 114 / 200 |
| 8 m / 10 % | 5 (1 %) | 8 | 414 (85 %) | 166 / 200 |
| 8 m / 14 % (the lightest alley's limit) | 297 | 326 | 469 | 191 / 200 |
| 5 m / 10 % | 334 | 391 | 476 | 195 / 200 |

> **A structure that replaces one candidate cannot exist at any grade the heavy road is
> held to.** The available run is not the crossing span (median 150 m) but the **corridor**
> — the near-straight run through several junctions, median **418–450 m**, typically
> passing 4 — at the cost of removing one or two junctions of the primary beyond the
> crossing's own neighbours.

`DeckHeight = 8 m` is not the lever: it is a defensible surface-to-surface figure (≈5 m
clear plus deck depth), and the 4 m the tests use as a floor leaves no truck clearance.

**This changes the phase's shape, not its viability.** A structure is a **corridor lift**,
not a crossing decision.

---

## 3. ⚠️ Three design decisions, open

These are why the review said *rethink*. Each must be settled before WP-B1.

### 3a. The seam — corridor lift as a post-pass, not a `Verdict`

The seam is wrong twice over: it sees ≤ 4 % of crossings (§0.2) and it cannot hold the run
a structure needs (§2). A structure replaces **k consecutive primary strokes** with
ramp–deck(s)–ramp, leaving the intermediate junctions on the ground with their side arms
only. That is a **post-pass over the finished network** — a sibling of
`ConnectComponentsPass`, run *before* it, drawing no random numbers — or a two-stage
generator. It is not a constraint verdict.

If a `Structure` verdict is nonetheless kept for the genuine split cases: re-validating
three members through the pipeline lets `SnapToNearbyPoint` rewrite `rampUp.B` onto another
deck's point while `deck.A` still references the old object, so the chain silently loses
identity. Any non-Accept on any member must refuse the **whole** structure, visibly.

### 3b. The deck elevation model

Deck elevation is `level · DeckHeight` and every consumer adds it. Two consequences:

- **B3 as first drafted is not representable.** "Free separation where the hill has already
  done the work" needs a level *difference* for the filter and *zero* added elevation;
  `Level = 1` adds 8 m **on top of** whatever the hill did. It is also backwards for the
  ramp, which climbs `8 + ground(deckStart) − ground(from)` — ground rising along it makes
  it steeper, not shallower.
- **Nothing bounds the actual clearance in a terrain city.** The deck is 8 m above *its
  own* ground at each deck junction and interpolated between; the crossed road is at its
  own junctions' heights. `GradeRelaxer` knows nothing about `Kind` or `Level` — it relaxes
  the ground under a deck junction like any other and never sees the 8 m.

### 3c. Block tracing on a non-planar graph — **measured broken, not suspected**

Lifting one interior crossing of `seed000/1500` and re-tracing: quarters 82 → 79, and one
"block" has **29 delimiters**, walks `#43 Ramp → #276 Bridge → #275 Ramp` and later the
same three reversed, passes the crossed junction **twice**, self-intersects, and contains
the crossed road in its interior. In `Yelukhdidru/800` a block's boundary runs along a deck
at level 1, whose corners `Quarter.CornerGroundHeightAt` puts at **ground** height, 8 m
under the road.

`QuarterGenerator` refuses only to *start* at a non-zero level; the trace loop has no
`Kind`/`Level` check, and the deck is an edge crossing another with no vertex — face
tracing on a non-planar graph. The fix is a **planarisation step** for block tracing (the
plan crossing as a pseudo-vertex; the deck absent from the ground graph). That is new
machinery and must exist before B6.

Related, and already true today: only **13 of 64** interior crossings have all four blocks;
the rest are dropped by `hasNullSection`, silently, with a `Trace`.

---

## 4. Work packages

`joyce.EnableGradeSeparation` stays **off** through WP-B0 … WP-B5. It cannot be a plain
`GlobalSettings` read — that is process-global and untestable, as the height-source comment
records — so the policy is **injected into `Generator` like `RuleTable`**, with the global
read once in `ClusterDesc._generateStrokes`.

### WP-B0 — the gate (measurement and decisions only, no production code)

| AC | criterion |
|---|---|
| B0.1 | Report **minSide, span and corridor** distributions separately. The unit matters: span gives the wrong answer (§2). |
| B0.2 | Buildable fractions at 5 / 10 / 14 % ramp grade, per city and world-wide, per unit. |
| B0.3 | The same restricted to corridors whose arms are `IsPrimary`. |
| B0.4 | **Block census**: for a sample of lifted corridors, quarters before/after, delimiters on a Ramp/Bridge/Tunnel, blocks containing a junction in their interior, self-intersecting blocks. |
| B0.5 | **A written design note settling §3a, §3b and §3c.** This AC is a decision, and it goes to the owner. |

### WP-B1 — make the built machinery real

| AC | criterion |
|---|---|
| B1.1 | `ClearanceConstraint` and `SpanLengthConstraint` are **in the pipeline**, with `RampClearance` supplied. Mutation: dropping either from the pipeline must fail a test. |
| B1.2 | **No ground stroke is ever split on a ramp**, and no junction exists on a ramp or deck except the builder's own. |
| B1.3 | `ConnectComponentsPass` never joins two levels with a non-ramp, and goes through `NetworkBuilder`. |
| B1.4 | `SplitStrokeAt` refuses a non-`Street` target rather than bypassing `_checkLevels`. |
| B1.5 | With the policy off: V1, V2 and `street-geometry.json` byte-identical; **cost within the existing `StreetCostTests` gate** (it is a 2 % ceiling with tolerance, not a byte comparison); TALE 200/200. |
| B1.6 | Constraint **order** for the eight existing checks is unchanged; the two added ones are no-ops with the flag off, asserted. |

### WP-B2 — the corridor lift

| AC | criterion |
|---|---|
| B2.1 | `MaxRampGrade` is named with its derivation written down. |
| B2.2 | A corridor shorter than the bound is **never** lifted — **plus a positive control**: a fixture just over the bound **must** lift. (Without it the AC passes vacuously when nothing fires.) |
| B2.3 | Every emitted ramp is within `MaxRampGrade`, measured on its **own emitted geometry**, on a **sloping** source so the ground term is non-zero. |
| B2.4 | Successors are emitted from the structure's far foot only; no stroke has `Level ≠ 0` except Bridge/Tunnel members. Otherwise an elevated network sprouts from the deck with no way down. |
| B2.5 | **Clearance**: vertical distance between the deck's `RoadSurface` and the crossed road's at the plan crossing, in a **terrain** city, ≥ a stated bound. Nothing guarantees this today (§3b). |
| B2.6 | A refused structure is **visible** — `GenerationReport` gains structure and refusal counts, and a refusal is a `Warning`. "No bridges appeared" must not be indistinguishable from "the policy never fired". |

### WP-B3 — hierarchy, spacing, angle

| AC | criterion |
|---|---|
| B3.1 | Weight ratio plus an absolute floor: **two alleys never separate**. |
| B3.2 | Which road takes the deck is asserted on `Kind` — `OverpassBuilder` always lifts the *candidate*, so "the heavier takes the deck" is `Tunnel` for the other, and that must be stated. |
| B3.3 | A heavy road with a junction already within `N` m separates instead of adding another. |
| B3.4 | An oblique crossing separates. **Resurrects a finished thought**: `PointNearStrokeConstraint` computed `angleVice`/`angleVersa` and discarded both behind `if (true \|\| …)`. |
| B3.5 | Each predicate has a **positive control**; `if (false)` around any of them must fail a test. |

### WP-B4 — blocks

| AC | criterion |
|---|---|
| B4.1 | Planarisation for block tracing: no delimiter on a Ramp/Bridge/Tunnel or at level ≠ 0. |
| B4.2 | No block contains a junction in its interior; no block self-intersects. **All three of these failed on the first lifted crossing.** |
| B4.3 | Quarter count is explained, not merely recorded. |

### WP-B5 — turn it on

| AC | criterion |
|---|---|
| B5.1 | Structures per city and their kinds, for the shipped world. |
| B5.2 | **Connectivity, falsifiably**: component count cannot change (a separation splits nothing), so instead — for each removed at-grade crossing, the shortest path between its four neighbours before and after, as a detour distribution; plus every junction reachable by A* over car lanes, which catches a deck lane with no ramp lane. |
| B5.3 | `ClusterStorage.DbVersion` bumped. |
| B5.4 | Every baseline that moves recorded old → new, per city, classified as a genuine network change or a re-hash. |
| B5.5 | TALE 200/200 run **after** the flip. |

---

## 5. Consumers to check rather than assume

- **TALE.** `SpatialModel.ExtractFrom` creates a `street_segment` location per `StreetPoint`
  — **deck junctions included** — so NPCs get scheduled to stand on a bridge, with
  `GetSectionArray()` corners nudged outward off the deck edge.
- **`Placer` / `citizen.SpawnOperator`** nearest-junction lookups are **unfiltered** by level.
- **`GradeRelaxer`** may relax the ground under a deck junction to a grade the ramp above
  it cannot meet.
- **Pedestrian crossings** — `GenerateNavMapOperator` draws them per junction; a separated
  crossing has none. Expected to follow automatically, *which is exactly the kind of
  expectation this project has been wrong about.* Assert it.
- **Nav lanes over ramps** — a lane on a deck needs `StreetLevels.ElevationOf` as well as
  `NavJunction.GroundHeight`. Check which it gets.
- **Flat cities** — `EnableGradeSeparation` is independent of `DisableClusterFlattening`, so
  hierarchy/spacing/angle would fire in a flat city too. Decide whether they should.

---

## 6. How this work has gone

Every one of these has bitten in Phase A: **measure before diagnosing** (the obvious
diagnosis was wrong in about half of the rounds); **mutation-test every gate** — `if (false)`
round a branch passes a source scan, a scan matching a bare identifier is satisfied by a
field declaration or a comment, `X = 0f * Call(...)` passes a scan looking for the call, a
scan sees the name of a call but not how many of its results are used, **a containment test
cannot tell a guess from a refusal**, and **a rule can be invisible to unlimited real data**
when no baseline city contains the shape it governs; and **a `Trace` in a `catch` is a
silent failure**.

The review of this plan's first draft is itself the lesson restated: two of its central
claims were wrong, and only measurement showed it.

**Specific to Phase B:** this is the first change in the workstream that moves the street
**network** rather than a surface on it. Blocks, estates, buildings, shops, TALE locations
and nav are all derived from it.
