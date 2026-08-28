# Three-dimensional street topology

**Status:** Phase A steps 1–5 landed — the seam, the flag, the terrain source, gradient
relaxation and per-street collision — plus the junction-seam defect they turned up
(§7). A terrain-following city now renders and drives. Quarters, buildings and the
corridor-conforming pass (§2c) are what remain before it is playable.
**Follows:** the streets generator rework (WP-0 … WP-5) — levels, ramps, deck geometry,
deck collision and both gates are in place.

The question this answers: how do we get cities with real three-dimensional street
topology, rather than a flat plateau with the occasional bridge bolted on.

---

## 1. The city is deliberately flat, and that is the lever

`engine/elevation/ClusterBaseElevationOperator.cs` levels the **entire cluster
rectangle to its average height**, and its own comment says so:

> *"This operator evens out everything below the average height of a city to the
> average. Doesn't really make sense, just a test."*

That is why `GenerateClusterStreetsOperator` can get away with a single
`h = AverageHeight + CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE` for a whole city. The
streets are not ignoring the terrain; the terrain has been ironed flat underneath them.

So "flatten only parts of the terrain" is not a new subsystem. It is replacing one
placeholder operator that is already wired in (`GenerateClustersOperator.cs:282`) and
already has the right shape — it receives an elevation segment and rewrites it.

---

## 2. Street heights are a graph problem, and most of the machinery exists

**A junction is one node, so it has one height.** If junctions take a height and
strokes interpolate between their two ends, the network is automatically consistent:
no stroke can disagree with itself, and two streets meeting at a junction meet at
exactly one height. That means *3D streets need no bridge decisions at all* — bridges
are what you add later, where you would rather two streets did **not** meet.

Two pieces are already built:

- `_shearOntoSlope` tilts a stroke's surface from `hA` to `hB`. It was written for
  ramps, but it does not know or care where the two heights came from. A street
  running downhill is the same code path.
- `DeckCollider` produces a tilted collider for any stroke whose ends differ in height,
  again without caring why.

What is missing is where `hA` and `hB` come from, and that splits into two steps.

### 2a. Sample

Each junction takes the terrain height under it. Cheap, and immediately produces 3D
cities — but with whatever gradients the terrain happens to have, including ones no
road would ever be built on.

### 2b. Relax

Gradient limiting is a relaxation over the stroke graph, and it is exactly the kind of
pure, testable pass the reworked architecture is good at — a sibling of
`ConnectComponentsPass`:

```
repeat until converged (or N passes):
    for each stroke:
        grade = |hB - hA| / planLength
        if grade > maxGradeFor(stroke):
            move hA and hB toward each other by the excess,
            weighted so the heavier road moves less
```

Weighting by road class is not a trick, it is how roads are actually designed:
an arterial is held to a shallower maximum grade than a service alley, so the alley
does the bending. `Stroke.Weight` and `IsPrimary` already carry that hierarchy.

Junction heights are shared by every stroke meeting there, so the relaxation converges
on a network that is consistent by construction.

### 2c. Conform

After relaxation, `streetHeight - terrainHeight` at each junction says what the ground
has to do:

| difference | meaning |
|---|---|
| ≈ 0 | at grade, nothing to do |
| street above terrain | embankment, or a viaduct if it is large |
| street below terrain | cutting |

The elevation operator then flattens a **corridor** — a band roughly
`streetWidth + shoulder` wide along each stroke, blended out over some distance — to
the street height, instead of flattening the whole city rectangle. Same operator, same
place in the pipeline, given the street graph instead of a rectangle.

### The ordering problem, which is the real risk here

Elevation operators currently run **before** streets: streets read `AverageHeight`,
which the elevation operator computes. Making the terrain depend on the streets creates
a cycle.

The way out is two passes, and it should be decided before any code is written:

1. a coarse base elevation, roughly what exists now but *without* the flattening —
   enough for junctions to sample;
2. after the street graph exists, a corridor-conforming pass that rewrites the terrain
   along the streets.

Fragments are generated on demand and cached, so pass 2 has to be expressible as a
fragment operator that runs after the cluster's street graph is available. That is the
part I would prototype first, because if it does not fit the operator pipeline cleanly,
everything above it is academic.

---

## 3. Bridges, once the ground is no longer flat

Two things change, and the first is the interesting one.

**Many separations become free.** If two streets cross at a point where their relaxed
heights already differ by more than the clearance, there is nothing to build: simply do
not create a junction. No ramps, no deck — the terrain did the work. Real cities are
full of these, and they read as natural precisely because nobody designed them as
structures.

**A distinction that must not be blurred.** `Level` is a *topological* deck index: it
answers "do these two meet?", and WP-4a's query filtering depends on it. Terrain height
is *continuous* and answers "how far apart are they?". They are different quantities,
and collapsing one into the other would repeat the `Pos3` mistake this project already
made once — index space and world space silently becoming the same thing.

The clean arrangement: **height informs the policy; the policy sets `Level`; `Level`
drives the filtering.** A crossing the policy decides to separate gets a level
difference, and everything already built then behaves correctly.

---

## 4. What makes a crossing want separating

Every input below is already available where `IntersectionConstraint` runs, which is
the point — this is a policy over existing data, not new machinery. It fits the WP-3
rule table as another entry kind.

| input | why | data |
|---|---|---|
| **Hierarchy gap** | The dominant real-world driver. A motorway does not stop for a residential street. | `Weight` ratio, `IsPrimary` |
| **Absolute weight floor** | Nobody builds a flyover between two alleys. | `Weight` |
| **Junction spacing** | Access management: an arterial with a junction every 50 m is a bad arterial. If the heavy road already has one within N m, separate instead. | `GetAngleArray()`, distance along |
| **Crossing angle** | A very oblique at-grade junction is an ugly sliver with bad sightlines. | `Stroke.Angle` |
| **Terrain height difference** | Free separation, per §3. | relaxed junction heights |

The angle input is not a new idea in this codebase — it is a **finished thought that was
abandoned**. The original `PointNearStrokeConstraint` computed `angleVice` and
`angleVersa` and then discarded both behind `if (true || ...)`, with the comment:

> *"We might want to check here, if it is perpendicular to the stroke as opposed to
> parallel. If it is perpendicular, we might be able to keep it, it might be a
> meaningful route."*

WP-2b removed the dead operands but recorded the intent. This is where it belongs.

---

## 5. Suggested sequencing

**Phase A — 3D cities, no bridges.** Sample, relax, conform. Every cluster changes and
caches are invalidated once. No crossing policy at all: shared junction heights keep
the network consistent, so there is nothing to decide. This is the big visual win and
it is independent of everything in §4.

**Phase B — crossing policy.** Terrain difference first (it is free and it is the most
natural-looking), then hierarchy, then spacing and angle. Each is one predicate in the
rule table, each can land separately, and the V2 fingerprints gate them.

**Phase C — structures.** Deck undersides, edges, piers, abutments where a viaduct
stands clear of the ground. Purely visual, and deliberately last: a floating slab is
unfinished, not wrong.

**Interaction to keep in view:** quarters, estates and buildings are traced as faces in
plan and then placed at one height. A quarter spanning a slope needs a pad — probably
the mean of its bounding junctions, cut or filled — and `QuarterGenerator` currently
has no notion of height at all. Phase A will make that visible immediately, so it
should be planned as part of Phase A rather than discovered during it.

---

## 6. What landed, and what it cost

Steps 1–3 of the staging below are in. Deliberately inert: with the flag off, the V1
network baselines, the geometry baselines and all 200 TALE tests are unchanged.

| step | state |
|---|---|
| 1 — height seam, flat default | **done.** `IStreetHeightSource`, `FlatStreetHeight`, `StreetHeightSources.For` |
| 2 — flag to skip flattening | **done.** `joyce.DisableClusterFlattening` |
| 3 — terrain source | **done.** `TerrainStreetHeight`, cached per junction |
| 4 — gradient relaxation | **done.** `GradeRelaxer`, `GradePolicy`, `RelaxedStreetHeight` |
| 5 — ground colliders per stroke | **done.** `IsNeededFor(stroke, groundIsFlat)`, floor plane suppressed, walking height follows |

Three things worth recording.

**The flattening operator also computes `AverageHeight`.** `ClusterBaseElevationOperator`
sets `_clusterDesc.AverageHeight` at line 38 and only then flattens. Nearly thirty sites
across streets, quarters, buildings, navigation and TALE read that field as "the height
of the city", so unwiring the operator would put every city at zero. The flag therefore
skips the height *write* and keeps the average. The biome write stays too — "this is
city" is true whatever shape the ground has.

**All street height already funnelled through three expressions**, so the seam was a
small change rather than a rewrite, and `_shearOntoSlope` needed no change at all: it
never knew whether a height difference came from a deck or a hill.

**The physics floor plane is gone when the city follows terrain** (step 5). A flat city
still gets one plane per fragment - cheap, complete, and exactly right because every
street really is at that height. A terrain-following city has no height to put one at, so
each street carries its own collider instead. Emitting both would be worse than either:
the plane would cut through the roads it was meant to replace.

---

## 7. The junction seam — found by the step-1 tests, now fixed

`_shearOntoSlope` gave every vertex a height from its projection onto the stroke's
**centreline**. A junction's corner points are not on the centreline. At an oblique bend
one corner sits well before the junction centre and its partner well after it — measured
axial positions of **0.858 and 1.142** of the stroke length at a 15° bend, on a 160 m
stroke.

So each of the two strokes meeting at a junction read a *different* height at a corner
both of them owned, and the road split open. Measured worst case **1.8 m at an 8 %
grade**, scaling linearly with gradient.

It had never fired because it needs `hA != hB` **and** a bend. At a straight junction the
corners are pure lateral offsets and project to exactly 0 and 1, and every ramp
`OverpassBuilder` makes is straight. That is also why `RampGeometryTests` could not see
it: its linearity assertion computes `along` the same way the implementation does, so in
this one respect it restates the implementation rather than checking it. Fourth instance
of that pattern in this project.

**The fix.** The caller already lays a stroke out in three parts — a wedge filling the A
junction up to `damax`, the carriageway, and a wedge filling the B junction from `dbmin`.
Those two bounds are exactly the junction footprints, so the pass now takes them and
holds each footprint flat at its own junction's height, spreading the rise over the
carriageway between. A flat cap and a flat footprint meet exactly, and two strokes
sharing a junction now agree there by construction.

Ramps are **bit for bit unchanged**, which is the neat part: at a straight junction
`damax` is 0 and `dbmin` is the full length, so the reparametrisation is the identity.

The slope normal is taken over the run that actually climbs rather than the plan length,
or a road lights as though it were shallower than it is. Every vertex including the flat
wedges keeps that normal, so shading stays continuous across the road instead of creasing
at the junction line.

Covered by `TwoStrokesAgreeOnTheHeightOfTheJunctionTheyShare`,
`TheJunctionCapMeetsTheStrokesThatEndThere`, `TheRoadIsFlatWhereItMeetsABentJunction`,
`HeightRisesMonotonicallyAlongABentStroke` and `TheSlopeNormalMatchesTheGradientOfTheSurface`.

---

## 7a. Gradient relaxation (step 4)

`GradeRelaxer.Relax(strokes, heights, policy)` is a pure function — no terrain, no
fragments, no engine — so it is tested exhaustively and cheaply. `RelaxedStreetHeight`
wraps `TerrainStreetHeight` and runs it once over the whole cluster, because relaxing one
junction moves its neighbours: there is no per-junction answer until the network settles.

Four decisions worth keeping:

- **Only the excess comes off.** Correcting to the limit rather than to flat is what lets
  a city keep the shape of the ground it stands on. Relaxing all the way to zero also
  satisfies "every grade is now legal", so a test asserting only that cannot tell the two
  apart — a mutation proved it, and `TheCorrectedGradeStopsAtTheLimitRatherThanGoingFlat`
  now closes it.
- **A junction resists by the heaviest street on it**, and a stroke's correction splits
  inversely to its two ends' resistance. That is why arterials stay flat and side streets
  fall away from them, and it is a policy over `Stroke.Weight`, which already carries the
  hierarchy.
- **Jacobi, with one damping divisor for the whole graph.** Per-junction damping works
  just as well against oscillation, but then the two ends of a stroke are divided by
  different numbers, the equal-and-opposite pair stops cancelling, and the network creeps
  uphill. Measured 1.85 m of drift on a 50 m ridge before the change; a single divisor
  leaves only the weighting able to move the overall level, which is intended.
- **A stroke with no starting height is reported, not skipped.** Silently skipping would
  leave exactly the unbuildable grade this pass exists to remove, with nothing in the log.

Flat in, flat out: every stroke of a flat network is already inside any grade limit, so
nothing is computed and the ground path cannot move.

**Note for whoever writes tests here.** A `StreetPoint`'s `Id` *changes* when it joins a
`StrokeStore` — the constructor hands out a provisional id and the store replaces it with
a network-local one. Keying a height table before `AddStroke` therefore keys it on a stale
id, the relaxer finds nothing, and the test silently does no work. It fails only
sometimes, because the provisional counter is static across the whole test assembly and
whether the two ids coincide depends on what ran first. That is how the star-junction test
was flaky for one run.

The grade numbers live in `GradePolicy` rather than in `models/nogame.streets.json`, only
because that file's parser refuses unknown fields by design. Moving them out is a
follow-up.

---

---

## 7b. Making it drivable (step 5)

Three changes, and one bug that stopped being dormant.

`DeckCollider.IsNeededFor(stroke, groundIsFlat)` — a stroke needs its own collider when
it leaves the ground **or** when there is no single height a floor plane could sit at.
The floor plane is emitted only in the flat case, so exactly one of the two mechanisms
covers any given city.

**The collider loop's missing fragment filter.** The operator runs once per fragment
overlapping a cluster but walks the whole cluster's stroke store, so every per-stroke
loop needs the same "only if this stroke's A point is in this fragment" guard the mesh
loop has always had. The collider loop was written without it. Harmless while it only
ever emitted for raised decks and no shipped ruleset makes any; a pile of duplicate
statics the moment ordinary streets need colliders.
`StreetFragmentOwnershipTests.EveryPerStrokeLoopIsFilteredToItsOwnFragment` scans the
source so the next such loop cannot be written without one.

**`Loader.GetWalkingHeightAt`** takes the terrain under the point rather than the cluster
average, or the player and every NPC spawn in mid air on a hill and inside the ground in
a valley. Note it is deliberately NOT the street height: streets are relaxed to buildable
gradients, so they cut into hills and stand proud of dips. The two agree once a
corridor-conforming pass (§2c) rewrites the terrain along the roads; until then this is
right off the road and out by the cut or fill on it.

`ClusterDesc.StreetHeightSource` is double-checked rather than lock-guarded, because
`WalkController` now reaches it every frame and taking the cluster lock there would
contend with street and quarter generation for a field written once.

**Junctions get no collider of their own.** Each stroke's box spans junction centre to
junction centre, so the boxes of the streets meeting at a junction all reach its middle
and overlap. The outer corners of a wide junction are the gap that leaves.

**Not covered by a test:** that the floor plane is suppressed when the ground is not
flat. It is a one-line condition inside code that needs a fragment and a physics
simulation to run; the per-stroke decision either side of it is tested, the suppression
itself is not.

**Still open, deliberately:** the `damax > dbmin` branch — two junction footprints
overlapping on a very short stroke — returns before the shear, so those four vertices stay
flat at the A end's height. Harmless while everything is flat; over terrain it is a small
mis-heighted stub at a very short stroke, and it wants its own decision rather than being
guessed at here.

---

## 8. What I would prototype first

The corridor-conforming pass (§2c), because it is the only part whose fit with the
existing operator pipeline is genuinely uncertain. Sampling and relaxation are
self-contained and testable against the street graph alone; if the conforming pass
turns out not to fit, it changes the shape of the whole phase, and it is better to know
that before the rest is built on top of it.
