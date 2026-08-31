# Three-dimensional street topology

**Status:** Phase A is complete for the *road surface and everything that moves on it* —
the seam, the flag, the terrain source, gradient relaxation, per-street collision, traffic
and pedestrians, city blocks, the conforming pass (§2c), the kerb (§7e), the satnav
guideline (§7f/§7g), the block topology off-by-ones (§7h/§7i) and the pavement winding
(§7j, §7k). A terrain-following city renders, drives, is walked on, and its pavements are
level across their width.

**What Phase A did NOT touch is everything that STANDS on that surface.** Buildings,
shops, quest markers, trams and the initial coin placement were all written against a flat
city and none of them has been revisited. Those, plus the pavement cross-slope, are
carried in **[`CITY-3D-OPEN-POINTS.md`](CITY-3D-OPEN-POINTS.md)**, which is the file to
read before picking this work up. The intercity network is still on Phase A's follow-up
list, and Phase B (the crossing policy) has not been started.

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

### 2c. Conform — **and it is not a corridor.** *(done, see §7d)*

After relaxation, `streetHeight - terrainHeight` at each junction says what the ground
has to do:

| difference | meaning |
|---|---|
| ≈ 0 | at grade, nothing to do |
| street above terrain | embankment, or a viaduct if it is large |
| street below terrain | cutting |

This section used to say the elevation operator flattens a **corridor** — a band roughly
`streetWidth + shoulder` wide along each stroke, blended out over some distance. **That is
not achievable at the resolution the elevation grid runs at, and nobody should propose it
again without changing the grid first.**

> **The resolution finding.** `MetaGen.GroundResolution = 20` over
> `MetaGen.FragmentSize = 400` is **one elevation sample every 20 m**. A street is 8–22 m
> wide (`Stroke.StreetWidth`), so `streetWidth + shoulder` is *about one cell*. Cutting a
> band that narrow into a grid that coarse does not produce a cutting; it produces
> terracing — one row of samples dropped, with a 20 m wall on either side. The shape being
> asked for is not representable. A real cutting, with batters and a shoulder, needs a
> finer grid inside cities, and that is a separate and much larger change (every fragment
> in a city gets more samples, and the terrain mesh, the cache and every operator above it
> pay for it).

What was built instead is **grading the city site toward the street height field**: every
elevation sample inside the city takes a weighted mean of the street heights near it and
is blended toward that mean, with the influence falling off over a few cells. Same
operator, same place in the pipeline, given the street graph instead of a rectangle — it
is only the *shape* the operator writes that changed. The result is ground a road sits
naturally on. Sharp cuts and embankments are explicitly not attempted.

### The ordering problem, which was the real risk here

Elevation operators run **before** streets: streets read `AverageHeight`, which the
elevation operator computes. Making the terrain depend on the streets creates a cycle.

The way out was already in the tree, and it is the **layer mechanism**. Elevation
operators register at ordered layer strings and each reads the layers strictly *below* its
own; `Loader.GetHeightAt` already took a `layer` parameter, defaulting to
`Cache.TOP_LAYER`. So the two passes are two layers:

```
/000002/fillGrid            base terrain
/000100/flattenCluster/...  ClusterBaseElevationOperator - average, biome, flatten
/000150/conformCluster/...  ClusterConformElevationOperator - the conforming pass
/000200/intercityTrails/... the intercity network
```

`TerrainStreetHeight` samples `/000150` rather than `TOP_LAYER`, which resolves to the
flattening layer and below — the terrain as it was before any city conformed it. Street
generation therefore cannot reach the conforming operator, and the conforming operator is
free to ask for the street graph. Everything else — rendering, `GetWalkingHeightAt`, the
hover probe's terrain fallback — still reads `TOP_LAYER` and sees the conformed result.

Fragments are generated on demand and cached, and the pass is deterministic in the stroke
graph rather than in what has been visited, so a fragment recomputed later comes back
identical.

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

**Phase A — 3D cities, no bridges. *Done.*** Sample, relax, conform. Every cluster changes
and caches are invalidated once. No crossing policy at all: shared junction heights keep
the network consistent, so there is nothing to decide. This is the big visual win and
it is independent of everything in §4.

**Phase B — crossing policy.** Terrain difference first (it is free and it is the most
natural-looking), then hierarchy, then spacing and angle. Each is one predicate in the
rule table, each can land separately, and the V2 fingerprints gate them.

**Phase C — structures.** Deck undersides, edges, piers, abutments where a viaduct
stands clear of the ground. Purely visual, and deliberately last: a floating slab is
unfinished, not wrong.

**Blocks, which Phase A did have to solve.** See §7c: a quarter is now a tilted planar
pad fitted to its own corner junctions, and everything standing on it reads that same
plane.

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
| 6 — the ground conforms to the roads | **done.** `ClusterConformElevationOperator`, `StreetHeightField`, and a sampling layer for `TerrainStreetHeight` (§7d) |

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

**`ClusterDesc.GroundHeightAt(position)`** is the one place "where is the ground" is
answered for anything that moves. Flat city, the average - exactly, since the terrain
really has been ironed to it. Otherwise the terrain under the point. Deliberately NOT
the street surface: streets are relaxed to buildable gradients, so they cut into hills
and stand proud of dips, and the two converge only once a corridor-conforming pass (§2c)
rewrites the ground along the roads. Where a caller has a `StreetPoint` in hand -
`GenerateNavMapOperator`'s car lanes, `SpatialModel`, `TalePopulationGenerator`, the
junction annotations - it asks the height source instead and gets the exact answer.

`ClusterDesc.StreetHeightSource` is double-checked rather than lock-guarded, because
`WalkController` now reaches it every frame and taking the cluster lock there would
contend with street and quarter generation for a field written once.

**Junctions get no collider of their own.** Each stroke's box spans junction centre to
junction centre, so the boxes of the streets meeting at a junction all reach its middle
and overlap. The outer corners of a wide junction are the gap that leaves. *Closed by
"The junction cap gets a slab" below.*

### The ship hovers; it does not rest

`HoverController` holds the player's ship `ClusterNavigationHeight` (3 m) above a single
ground sample at its centre, driven by a velocity servo. Two consequences worth having
written down. (Since *The ship asks the physics world what it is over* below, that sample
is a floor rather than the whole answer — a downward raycast can raise the target onto a
built surface, and never lower it.)

**The 3 m clearance is what masks uneven ground.** The sample is one point, so a ridge
the centre misses is not seen at all; the clearance absorbs most of what would otherwise
be an intersection, and the ship reads as levitating. Sampling around the hull and taking
the highest would fix the clipping, and was **deliberately not done**: it raises the
effective hover height over any uneven ground, and every constant in that controller is
tuned against the current single sample. Not worth the retune for the clipping it buys.

**The division of labour is: physics for what is built, distance-over-ground for the
terrain.** Streets, ramps, decks, quarter floors and buildings all have colliders, so the
solver owns them. The terrain has none — outside a city the hover loop *is* the collision.

That only works if the hover loop does not overrule the solver, and it used to: it
assigned `Pose.Position.Y` outright on every frame the ship was below the sampled ground,
which no contact can argue with. It drove straight through a deck, and in a
terrain-following city it hauled the ship out of every road cutting it entered — a
cutting being below the ground beside it by definition. That is now a force, plus a
rescue for the one case forces cannot recover from: below the ground **and falling**.
Both conditions are needed. Depth alone catches a ship legitimately parked in a cutting;
falling alone catches it mid-bounce.

Note this changes the default flat path too, mildly: the ship may now dip below its hover
height for a few frames rather than being snapped back. In a city the fragment floor plane
catches it 1 m down; outside one the servo does.

**Not covered by a test.** `HoverController` needs a booted engine and a physics
simulation, and `nogameCode` has no test harness — the existing drift tests reach it by
scanning source, which suits a cross-cutting rule and not a tuning constant.

**The player's car was missed first time round, and that is what the drift test is for.**
`HoverController` does not rest the car on anything - it drives towards
`Loader.GetNavigationHeightAt` and hard-sets Y when it falls below - so the per-street
colliders never entered into it and the car sailed over the hills at a constant altitude,
with nothing failing and nothing in the log.
`ClusterGroundHeightTests.OnlyKnownSitesAssumeACityIsFlat` now scans both source trees and
fails on any read of `AverageHeight` outside a listed set, each entry carrying its reason.
It fails in both directions: an entry that stops matching is a converted subsystem whose
to-do was never struck off. Verified by mutation - reverting exactly the car bug fails it.

The list doubles as the outstanding work: quarters and everything traced on them
(quarter floors, buildings, trees, shops, `SpatialModel` estates,
`QuarterLoopRouteGenerator`), and the intercity network, which spans clusters and has its
own elevation operator.

**Not covered by a test:** that the floor plane is suppressed when the ground is not
flat. It is a one-line condition inside code that needs a fragment and a physics
simulation to run; the per-stroke decision either side of it is tested, the suppression
itself is not.

### When the ship does touch, it slides

Reported from play: *"when the car is touching ground, it is high friction and isn't
realistically capable to move. In the position where it is kept hovering it looks
better."* Hovering is the wanted state; contact was the broken one. Two mechanisms, and
the arithmetic decides between them.

**The hover height and the road disagree, and the disagreement is signed.** The hover
loop aims at `ClusterDesc.GroundHeightAt` + `ClusterNavigationHeight` — terrain + 3 m —
while the surface under the ship is `StreetHeightSource.GroundHeightAt(junction)` +
`CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE` — relaxed street + 2 m. `GradeRelaxer` takes only
the excess gradient off, so on ground steeper than 5–14 % the road cannot follow it: over
a 50 m stroke on a hillside rising 10 m, an arterial limited to 5 % may climb 2.5 m, and
the remaining 7.5 m becomes cut at one end and **fill** at the other. Wherever the fill
exceeds 1 m the road stands ABOVE the height the hover loop is aiming at, and the loop
then does the worst possible thing: it commands a descent, into the road, for as long as
the ship drives along it. In a cutting the sign flips and the ship hovers clear — which
is the state that "looks better", and is the same state a flat city is always in.

**Friction was 1.0 for every pair in the world**, from the Bepu demo the narrow-phase
callbacks were copied from. A body pressed onto a surface resists tangentially with μ
times its normal load, and here the numbers cross: the descent command was a constant
`LevelDownThrust` = 16 m/s, so a ship held above its target carried a sustained normal
load of 500 kg × 16 m/s² = 8000 N, against a maximum forward thrust of 500 kg ×
`LinearThrust` × 255/256 = 7470 N. Friction beat the engine. That is not "sticky", it is
immobile, which is exactly what was reported.

Both were fixed, because they fail in different places.

- **`engine.physics.HoverHeightServo` makes the command asymmetric.** Climbing keeps full
  authority — the terrain has no collider, so being slow to rise is being inside the hill
  — but descending is trimming and is now proportional to the height error, saturating at
  `LevelDownThrust` only beyond 2.4 m. A surface standing 1 m proud is leaned on at
  6.7 m/s instead of 16, half a metre proud at 3.3. It also ends the undershoot: with a
  constant command the ship was only told to stop descending once it had ARRIVED, and
  sank 13 cm past its own hover height each time; proportional, it arrives.
- **Friction is a property of the bodies now.** `CollisionProperties.Friction` defaults to
  1.0, so every NPC, static and piece of debris in the world is resolved exactly as
  before; the pair takes the LOWER of the two, so a body that declares itself slippery is
  slippery against everything. The player's ship declares 0.05 — it is a hover vehicle
  with nothing to grip, and `HoverController` already does the work a tyre would by
  cancelling the part of the velocity that is not along the nose.

Neither removed the cause, and the report came back: *"I still feel the car 'sticks' to
some street, does not hover over it. How would it know the height of the street at the
probe point to have the car hover?"* — see the next section, which fixes that.

**Deliberately NOT fixed: the ship's collision body**, which `HoverModule` documents at
length as a cylinder of zero length — `BB.Y - BB.Y` — and which was corrected once (#48)
and reverted (#49). It is relevant here: a zero-height cylinder is a flat disc of radius
1.4 m, and it rests on a road with its whole face, which is the largest contact manifold
and the most friction leverage the shape could possibly offer. It is still not worth
touching. Every constant in the hover controller has been tuned against that disc, the
angular runaway it was blamed for was never demonstrated, and this fix does not depend on
the shape either way.

**What is and is not covered by a test.** The command is arithmetic and is tested, and so
is the closed loop the ship flies: every term touching its vertical velocity is a constant
in `HoverController` with no dependency on the world, so `HoverHeightServoTests` integrates
exactly those terms and catches both the undershoot and the shove. Contact resolution
itself needs a running simulation and is not covered; what stands in for it is
`PairFrictionTests`, which pins the default, the combination rule, and — by scanning source,
since neither site is reachable from a test — that the callback still derives the
coefficient from the bodies and that the ship still declares one.

**Still open, deliberately:** the `damax > dbmin` branch — two junction footprints
overlapping on a very short stroke — returns before the shear, so those four vertices stay
flat at the A end's height. Harmless while everything is flat; over terrain it is a small
mis-heighted stub at a very short stroke, and it wants its own decision rather than being
guessed at here.

### The ship asks the physics world what it is over

The two mechanisms above made contact *benign*. They did not make it *rare*, because the
ship was still being commanded to fly below the surface it was standing on, and the
report came back accordingly. The mechanism the report named is the fix, and it already
exists in the tree: **the walking player finds the floor with a downward raycast**
(`WalkController`, the cast from head height), and there was never a reason the ship
could not ask the same question.

`HoverController._probeSurfaceBelow` casts straight down from `SurfaceProbeAbove` = 2 m
over the ship, `SurfaceProbeBelow` = 24 m deep, and reports the top of the nearest
surface. `engine.physics.API.RayCastSync` takes the simulation lock itself, so it is safe
from the logical thread and must not be wrapped in another. It is one ray per frame for
one entity, against a controller that already casts several for the walking player. The
ray starts *above* the ship for the same reason `WalkController`'s starts at head height:
a ray beginning inside the deck the ship is standing on begins inside a convex shape and
reports nothing.

**It is an addition, not a replacement, and that is the whole safety argument.** The ray
sees things with COLLIDERS. The terrain has none — `Fragment._createGround` adds no
physics at all — so over open country, off the edge of the world, and over any part of a
city that was never built on, the ray reports nothing and the answer has to be the
terrain height that has always been flown there. So `engine.physics.HoverSurfaceProbe`
combines the two with a **maximum**: a built surface may raise the target, never lower
it. That also settles the cutting, where the road is *below* the terrain beside it and
taking the ray's answer would fly the ship down into the cutting walls.

**The clearance is derived, and a flat city comes out identical.** This is the arithmetic
the change is gated on. In a flat city every drivable surface — the fragment floor plane
(top face at `AverageHeight + CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE`), the quarter floors
(`ClusterStreetHeight`, the same 2 m), a deck collider on a level stroke — is at the same
height, and the existing target is `AverageHeight + ClusterNavigationHeight`. So

    SurfaceClearance = ClusterNavigationHeight - CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE

and `surface + SurfaceClearance` reproduces the old target *exactly*, which makes the
maximum a no-op on every frame of the default game. The tempting alternative — reusing
`ClusterNavigationHeight` as the clearance, since it is the number that says "hover
height" — would raise the entire flat city by 2 m.

**The ray hovers over the world, not over the traffic in it.**
`HoverSurfaceProbe.IsHoverSurface` rejects a body whose `SolidLayerMask` is *only*
moving-kind layers (`Player`, `Npc`, either side's weapons, `Collectable`, `QuestMarker`)
and accepts anything with a bit outside that set. Deliberately not a plain intersection
test: a house declares no mask at all and keeps the default `Layers.All`, which intersects
everything, so an intersection test would reject most of the city. The reason for
excluding NPCs and vehicles at all is the servo's asymmetry — the *climb* keeps full
authority by design, so a pedestrian walking under a parked ship would not nudge it, it
would launch it. Things that move are the solver's business, and a contact is how a hover
ship should learn about something that can walk away. For the same reason a collider with
no properties is accepted only when it is `Static` or `Kinematic`: unlabelled statics are
the city's own fabric (the floor plane and every deck collider are raw statics), while an
unlabelled *dynamic* is a loose object, and hovering over loose objects is a pogo stick.
`WalkController` accepts all three because a player asking whether they are about to fall
genuinely does not care.

The terrain sample has NOT gone away and still owns two things: the one-sided shove and
the rescue both test `heightAtTarget`, not the probed target, because both are about being
inside the hill and only the terrain can say that. A ship standing on a road the probe has
raised the target to is not below anything; it has arrived.

**What is and is not covered by a test.** The combination is arithmetic and is tested —
`HoverSurfaceProbeTests` pins the flat-city identity on the TERM and not merely on the
result, the fill case, the cutting case, the no-hit fallback and the layer filter. The
cast itself and the hit filter need a booted engine and a running simulation and are NOT
exercised; a source scan stands in for them, as it does for the friction sites, and
asserts that the cast is the SYNC one, points down, starts above the ship, rejects the
ship's own body, and still feeds `HoverSurfaceProbe.HoverTargetHeight`.

**Superseded:** the earlier note in this section that the height reference was
deliberately left alone because there is no thread-safe positional road query. That was
true of `StrokeStore` — `GetClosestStroke`/`GetClosestPoint` still take a junction or a
stroke and still share `_tmp*` scratch buffers — and it was the wrong place to look. The
road already exists in the physics world as a collider; asking *it* needs no new
generation-side query and gives the height of whatever the ship is really over, road or
deck or roof. §2c (the corridor-conforming pass) is still worth doing, but it is no longer
what this depends on.

**Still open, deliberately:** the probe is one ray at the ship's centre, so the hover
height still follows a single point and the existing tilt response still reads the
TERRAIN two metres ahead rather than the road. Both are the same trade the single ground
sample has always been — every constant in the controller is tuned against one sample —
and a second ray was not added because nothing in the report needs one.

### The junction cap gets a slab

Reported from play, and it is the gap recorded at the top of this section: *"The car
still is a bit stuck on at least this junction (the surface BETWEEN the individual
branches of a junction)."* `_generateJunction` fills that area with a fan over
`StreetPoint.GetSectionArray()` and the fan had geometry and no collider at all, so
`engine.streets.generation.JunctionCollider` now gives it one — under the same two
conditions and the same fragment-ownership rule as the stroke colliders, so a flat city,
where the fragment floor plane already covers every junction on the ground, emits nothing.

**The diagnosis was half right, and the measurement is why the other half is written
down.** The obvious story is that the wedge between two branches stands on nothing. It is
true and it is smaller than it looks: sampling every cap of the baselines on a grid, the
fraction covered by no stroke box at all is **0.1 % at the median** — three or more boxes
converging on one point really do overlap across most of a junction — though about a third
of junctions have some, and the worst junction of the 3000 m city has **92 m² of cap
standing on nothing**. The bigger half is what the boxes put there where they *do* reach.
`_shearOntoSlope` holds the road MESH flat over each junction footprint and spreads the
rise over the carriageway between, precisely so that the flat cap and the road meet;
`DeckCollider` is tilted across the stroke's whole length, junction centre to junction
centre. So inside a junction the collider climbs while the picture is level, and two
branches of different slope cross there and leave a ridge nothing renders. The cap reaches
**7.6–10.6 m into its own strokes' boxes at the median and 28.4 m at the worst**, and
`GradePolicy` allows 5 % to an arterial and 14 % to an alley: **0.4 m to 4 m** of invisible
step, which is what a ship gets stuck on. A flat slab at the junction's one height wins
wherever a box has climbed above the road.

**The shape is the cap itself, not a disc over it.** A junction is one node with one
height, so a horizontal disc of the cap's own radius needs no orientation and would be the
cheapest thing that could work. Measured, the circumscribed disc is **2.75× the cap's plan
area at the median** (a three-arm cap is a triangle, and a triangle is 2.42× its own
circumcircle by construction), 5.3× at the 99th percentile and **34× at the worst
junction**, where two nearly collinear strokes push a section point 42 m out and the disc
becomes an 85 m pancake. That surplus is an *invisible apron* reaching several metres past
the road onto ground a terrain-following city may have put well below it — the artefact
this change exists to remove, not to introduce. So the slab is a Bepu `ConvexHull` over
the cap's own corners, top face on the road as `DeckCollider`'s is, one hull per junction
per fragment (about thirteen for the largest city in the baselines).

**Fewer than three section points means no cap.** The mesh emits a fan over two section
points and their own midpoint, so both of its triangles are degenerate — which is why
`_generateJunction`'s guard reads "fewer than two" and still draws nothing at two. Nothing
is missing there: two strokes meeting head on hand their surfaces to each other and their
boxes overlap across the junction.

**The height lives in `JunctionCollider.SurfaceHeightOf`, not at the call site**, and that
is the one mutation that survived the first round. Reading `AverageHeight` instead of the
junction's own relaxed height compiles, leaves a flat city bit for bit identical — so every
other test still passed — and floats a pancake at the mean height over every junction of a
terrain-following city, which is worse than the gap it was meant to close.
`ClusterGroundHeightTests` cannot catch it either: the operator is already on that allow
list for its flat floor plane, and the list is per file. The test now compares the slab
against the cap MESH, built by a separate expression in `_generateJunction`, over a sloping
source and with every other junction raised onto a deck — so either side drifting fails.

**What is and is not covered by a test.** The decision, the arithmetic and the cap's
agreement with the mesh are pure and are tested, and the "no slab anywhere in a flat city"
claim is asserted over whole generated cities rather than a fixture. The Bepu emission —
hull construction, the static, its release — needs a booted engine and a running
simulation and is NOT exercised; the fragment-ownership guard on the new loop is held by
the same source scan as the stroke loops, extended to per-junction loops.

**Still open, deliberately:** `DeckCollider` still tilts across the junction footprints
rather than flattening over them the way the mesh does, so under the cap the two disagree
by the same 0.4–4 m. Making the collider follow `damax`/`dbmin` would need those two
numbers, which are computed inside `_generateStreetRun`, and the slab covers the case from
above. Nor is the stroke collider's own height expression routed through a checkable
helper; the same `AverageHeight` mutation would survive there.

### Nav lanes are the NPC height source

Reported from play, with the design steer that fixes it: *"NPCs do not seem to honour
street height. I believe we do not want to afford raycasting for NPCs, so we need to come
up with a linear function for their elevation based on street nodes."*

**That function already existed and was being discarded.** `GenerateNavMapOperator` gives
every car-lane `NavJunction` the height of the `StreetPoint` it *is* — the relaxed street
height, cut and fill included, not a terrain sample near it — and every sidewalk junction
the height of the junction its quarter delimiter belongs to. Lanes measure themselves with
`Vector3.Distance` and split themselves with `Vector3.Lerp`, so interpolation along a lane
is already **linear in the street nodes, at no per-NPC cost and with no raycast anywhere**.
Then `StreetRouteBuilder` gave every waypoint of a route ONE Y, computed once at the
route's start and from `ClusterDesc.GroundHeightAt` — the TERRAIN — so a route across a
hill came out flat.

**The offsets are the trap, and they are why `NavJunction` now carries the ground.**
`Position.Y` is ground + `ClusterNavigationHeight` (3 m), which is the *vehicle* hover
reference; a walker's feet go at ground + `ClusterStreetHeight` (2 m) +
`QuarterSidewalkOffset` (0.15 m). Subtracting one constant to add another is exactly how
two heights drift apart with nothing failing, so the junction stores `GroundHeight` and
each consumer adds its own offset through `NavJunction.NavigationHeightOf` /
`WalkingHeightOf`. Every junction in the engine is now built by `NavJunction.At`,
`NavJunction.Between` or `NavJunction.AtNavigationHeight` — a source scan enforces it,
because an object initialiser sets whichever half its author was thinking about and leaves
the other at zero, which is *invisible*: right position, right route, and every NPC walking
over it dropped to 2.15 m above sea level. Both engine sites were of that shape before.

**The flat-city arithmetic.** A flat city's junctions stand on `AverageHeight` exactly, so
the lane is at `AverageHeight + 3` and the old route height was `AverageHeight + 2 + 0.15`
= `AverageHeight + 2.15`. `WalkingHeightOf(AverageHeight)` reproduces that number, so the
change is inert there — pinned on the term as well as on the value, since a conversion that
was merely close would also pass a test that only compared a waypoint against its own
junction. Reusing `ClusterNavigationHeight` because it is the number that says "how high a
nav junction is" would put every NPC 0.85 m into the air.

**`PedestrianRoute.WaypointFor` is in Joyce because `StreetRouteBuilder` is not.** The test
assembly references `Joyce` and not `nogameCode`, so the piece that decides a waypoint —
the lane end's own walking height, offset 1.5 m onto the right-hand sidewalk — moved into
`builtin.modules.satnav`, where a hand-built chain of lanes over a hill can be walked
directly. A source scan stands in for the call site.

**The two ends of a route are still terrain samples**, and they are the only two: the
walker and the destination are wherever they happen to be, not on a junction. The
destination now samples at the DESTINATION rather than reusing the start's height, which is
the same defect in miniature; so does `GoToStrategyPart`'s straight-line fallback, which has
no lanes to take a height from at all.

**What the other consumers turned out to be.** `NavMeshRouteGenerator` only forwards to
`StreetRouteBuilder`. `TaleEntityStrategy`, `TaleWalkBehavior` and `WalkBehavior` never
touch a waypoint's Y — `SegmentNavigator` lerps between segment positions in 3D, so the
per-waypoint heights flow through on their own. `QuarterLoopRouteGenerator` was already
correct, one `Quarter.GroundHeightAt` per waypoint since §7c. `Route.cs` synthesises a
junction where it truncates the last lane at the target, and that one is now built through
the factory so it carries a consistent ground height.

**Found and NOT changed:** `SpatialModel._computeStreetEntryCandidates` collects pedestrian
lane ENDPOINTS as NPCs' standing points, so a street location's entry candidates carry
`NavJunction.Position` — the *vehicle* clearance, 0.85 m above where a walker stands.
`TaleSpawnOperator` places a spawning NPC there, and `GoToStrategyPart` corrects it on the
first frame it moves, so it is a spawn pop rather than a persistent error. Converting it to
`WalkingHeight` would move NPCs in the default flat city, which this line of work is gated
against, so it wants its own decision.

**What is and is not covered by a test.** The conversions, the split-lane interpolation and
the waypoint are pure and are tested, including that a route over a hill is not flat and
that the same route in a flat city is unchanged. `GenerateNavMapOperator` itself needs a
stroke store, a quarter store and the container and is NOT exercised: dropping
`streetPoint.LevelElevation` from a car junction's ground height survives every test, which
is tolerable only because no shipped ruleset raises a junction off the ground. Putting a
sidewalk junction back on `AverageHeight` does fail, via `ClusterGroundHeightTests`.

---

## 8. What was prototyped first, and how it turned out

The conforming pass (§2c), because it was the only part whose fit with the existing
operator pipeline was genuinely uncertain. It fits, and it needed no new machinery at all:
one more elevation operator at one more layer, plus a `layer` argument at one call site
that `Loader.GetHeightAt` already accepted. See §7d.

What did *not* survive contact was the shape it writes. The corridor was not rejected on
taste; it is not expressible on a 20 m elevation grid, and that is recorded in §2c so
nobody re-proposes it without changing the grid first.


---

## 7c. City blocks are tilted pads (Phase A, blocks)

A quarter, its floor mesh, its buildings, its trees, its shop fronts and the doors NPCs
walk to all used one height for the whole city. `Quarter.GroundHeightAt(v2)` replaces it
with **one plane per block**, least-squares fitted to the heights of its own corner
junctions.

**Why a plane and not a surface following the terrain.** The property that matters is not
that a block is at some plausible height but that *everything on it agrees* about which
height. A plane is exactly reproducible at any point by any caller — the mesh emitting a
corner, the operator placing a house, the TALE model placing a shop door — with no
reference to each other and no shared surface to sample. Nothing else on the table has
that.

**Why tilted and not flat.** A flat pad at the mean is what a terraced hillside city
really looks like, and it was the first candidate. It steps at every block edge by up to
half the fall across the block, and nothing renders that step — you would drive off a
street into a wall that is not there. Tilting removes the step.

**What §7c originally claimed here, and what is true instead — see §7e.** It said the
block meets the streets around it "to within the fit residual, which is zero whenever the
corners happen to be coplanar". Measured, the corners are **never** coplanar on a slope:
a block's corners are section points displaced from their junctions by different amounts
in different directions, so even an exactly planar hillside gives a residual of 0.02 m at
the median and 1.66 m at the worst corner of the 3000 m city. The block's **floor** no
longer reads the pad at its boundary for that reason; the pad is what the block's
*interior* stands on, which is where the buildings are, and there the two still agree —
exactly at the centroid, by construction.

**Measured before deciding, not assumed:** `TriangulateNonPlanarTests` pins what LibTess
does with a non-coplanar outline — every vertex keeps its own height, no vertices are
invented, and naming the sweep normal changes nothing. That was run first, because it
decided whether a non-planar block floor was even on the table. It is not needed for the
plane, and it is kept because it is the evidence for the choice.

**Traps worth keeping.**

- The flat path **short circuits before the fit**, because a least-squares plane through
  equal heights does not reproduce its input bit for bit. From ten corners up, 20.1 comes
  back as 20.1000022. The whole line of work is gated on the flat path being untouched.
  A four-corner test fixture cannot show this — with four, or even seven, the
  sum-then-divide happens to round-trip and a version with no short circuit passes.
- Corners **collinear in plan** cannot determine a tilt, so the fit falls back to their
  mean rather than solving a singular system.
- `Estate` gained a back-reference to its `Quarter`, set in `AddEstate`, so that anything
  standing on an estate rather than a block — a polytope, a tree — can find the pad.

**Deliberately still on the average:** `GenerateShopsOperator`'s intensity sample. It is
a probability-field *coordinate*, not a position, and feeding it the terrain would make
which shops exist depend on the ground under them — a generation change, and not one
this work is about.

---

## 7d. The ground conforms to the roads (§2c, done)

`ClusterConformElevationOperator` grades the city site toward the street height field, so
that a road sits on ground rather than slicing through the hillside it crosses and
standing proud of the dip it spans.

### The layer, and why it sits below intercity

The cycle dissolves through the existing layer mechanism — see §2c for the stack. The
conforming operators register under `ClusterConformElevationOperator.Layer` =
`/000000/000150`, one per cluster, and `TerrainStreetHeight` samples **that same constant**
rather than `TOP_LAYER`. One constant for both halves on purpose: a layer string is read
as "the first layer strictly below this one", so the registrations sorting *after* it and
the flattening layer sorting *before* it are the two facts that make "streets read
unconformed ground" true, and splitting them into two spellings is how that stops being
true without anything failing to compile.

**Below `/000200/intercityTrails`, not above.** `IntercityTrackElevationOperator` hard-sets
the terrain to its line's own constant height along a narrow band. That is an absolute
override and not a shape a city may smooth away — and keeping intercity last also means
its relationship to the city terrain is exactly what it has always been: it overwrote the
flat plateau before, and it overwrites the graded site now.

**The recursion terminates, and the layer is why.** The operator needs
`clusterDesc.StrokeStore()`, which triggers street generation, which samples junction
heights — but through `TerrainStreetHeight`, which reads strictly *below* the conforming
layer and therefore reaches flattening and the base grid and stops. The search
`Cache._getNextFactoryEntryBelow` runs is a strict descent through a sorted list, so even
where two cities overlap, the higher-keyed one may read the lower-keyed one's conformed
ground and never the reverse. And `ClusterDesc._triggerStreets_nl` returns immediately once
the cluster has left `Created`, so the one remaining re-entry — a cluster operator that
reads `TOP_LAYER` while the cluster is still generating — bottoms out at depth two.

### What it writes

For each elevation sample, `StreetHeightField.TryHeightAt` answers with the streets'
opinion and how strongly they hold it, and `Blend` moves the terrain that far:

- **Kernel: smoothstep, `t²(3 − 2t)` with `t = 1 − d/R`.** Zero derivative at *both* ends.
  At the far end the graded ground meets untouched terrain tangentially instead of
  creasing along a circle around every street; at the near end the road sits in a flat pad
  rather than on the apex of a tent. On this grid a crease lands on a single row of samples
  and reads as exactly the terracing the corridor was rejected for.
- **Radius: three elevation cells, 60 m.** A property of the *grid*, not of roads. Below
  about two cells there are no samples inside the falloff to carry a ramp and what comes
  out is the step again; and a skirt of a few tens of metres is a plausible batter for the
  couple of metres of cut and fill `GradeRelaxer` leaves behind, so three is not merely the
  smallest number that works. It is expressed in cells, so that refining the grid inside a
  city has to come past it.
- **A weighted mean of the streets in range, not the nearest one.** Nearest-wins jumps as
  the winner changes and that seam runs down the middle of every block. Two strokes meeting
  at a junction agree there by construction, so the mean is exact where it matters most and
  only smooths between streets that genuinely differ.
- **The influence is the LARGEST single weight, not the sum.** A sum exceeds one wherever
  streets are dense and would have to be clamped, which puts a hard edge wherever the clamp
  starts biting; the largest weight is already in [0, 1] and reaches 1 exactly on a road.
- **The target is the ground height under the junction, with no offset.** That is the
  quantity §7b said the two would converge on: after this pass `ClusterDesc.GroundHeightAt`
  (the terrain) and `IStreetHeightSource.GroundHeightAt` (the relaxed junction) agree near
  a road, so pedestrians, quarter pads and street surfaces stop disagreeing by the cut or
  fill.

**Only the height is written.** `ClusterBaseElevationOperator` wrote `Biome = 1` across the
city rectangle below, and that still means "this is city" whatever shape the ground has.

**Determinism.** Strokes are iterated in `Sid` order, as `GradeRelaxer` does, so the
floating point addition order is fixed; the field is built once per city from the *whole*
stroke graph rather than from the strokes overlapping the fragment being computed, so two
neighbouring fragments cannot disagree along their shared edge and a fragment recomputed
after a different set of neighbours comes back bit for bit identical.

**Cost.** One AABB rejection per stroke per elevation sample, on a box already grown by the
radius: 441 samples per fragment × the cluster's stroke count. For the largest city in the
baselines (3000 m, 1875 strokes) that is about 830 000 four-comparison rejections per
fragment, a few milliseconds, once, at generation time; a 500 m city has 29 strokes and it
is not measurable. Deliberately *not* `StrokeStore.GetClosestStroke`/`GetClosestPoint`,
which take a junction or a stroke rather than a position and share `_tmp*` scratch buffers.

### A sample-placement bug, found and fixed here only

A segment carries `GroundResolution + 1` samples spanning `FragmentSize`, so the step is
`FragmentSize / GroundResolution` and the last sample sits on the far edge — which is
exactly the spacing `CacheEntry.GetElevationPixelAt` reads them back at.
`ClusterBaseElevationOperator` and `IntercityTrackElevationOperator` both divide the span
by the sample *count* instead, placing every sample about five percent short and restarting
the error at each fragment. **They were left alone**: each writes a constant inside a
rectangle, so the only consequence is a city boundary ragged by a sample width, and fixing
them would move the flat-city baseline. Here it would have put a step in the graded ground
along every fragment seam, so the conforming pass uses the correct spacing.

### What is and is not covered by a test

`StreetHeightField` is pure — a list of segments, a height at each end and a distance
falloff, with no terrain, no fragments and no engine — so the kernel, the blend and the
positional query are tested directly, including the determinism of the iteration order.
The write itself is tested through an extracted `Grade` that takes only elevation segments.

**The flat default path is proved, not asserted**, twice over: the operator is not
registered at all unless the flag is on (`GenerateClustersOperator`, scanned for), and the
operator short-circuits on `IStreetHeightSource.IsFlat` before it touches `StrokeStore()`,
which a test drives with a fake elevation provider and asserts hands every field of every
pixel through unchanged.

**The operator also checks its own AABB**, which the "first layer below" search would
normally do for it. `Cache.ElevationCacheGetAt` runs the TOP layer's operator for *every*
fragment in the world with the intersection test disabled (`if (true /* || …intersects */)`),
and this is the top layer in any world with no intercity network registered above it —
without the check, the first fragment loaded anywhere would generate a city's whole street
graph to grade ground a kilometre away from it. Covered by a test using a *non-flat* source,
so the flat short circuit cannot be what passes it.

**Not covered:** `ElevationOperatorProcess` on a terrain-following city, which calls
`StrokeStore()` and therefore generates a whole city and needs the `I` container, the
engine and the elevation cache; and the layer *resolution*, which lives in the
process-global `elevation.Cache` singleton that a test may not register operators into
without leaking them into every other test in the assembly. Source scans and a direct test
of the three string comparisons stand in for those.

### Still open, deliberately

- **The grid inside cities is still 20 m.** Real cuttings and embankments want a finer one,
  and that is the change §2c now says must come first.
- **The residual.** Within the radius the ground is the streets' weighted mean, so where two
  streets genuinely differ the ground splits the difference rather than meeting each of them
  exactly. That error is bounded by the *variation* between neighbouring relaxed streets,
  where the error before this pass was the whole cut or fill.
- **The two sibling operators' sample placement**, above.
- **`ClusterBaseElevationOperator` still writes `aver + 1.5`** for the flat path while
  `GroundHeightAt` returns `aver`. The flat path papers over the 1.5 m with `IsFlat`, and
  untangling it would move the flat baseline for no visible gain.

---

## 7e. The kerb (block boundaries meet the road exactly)

Reported from play of a terrain-following city: *"sidewalks still hover in the air and/or
are in the ground."*

There is no separate sidewalk geometry. `GenerateClusterQuartersOperator` extrudes the
block polygon up by `MetaGen.QuarterSidewalkOffset` (0.15 m); the **top face is the
pavement and the sides are the kerb**. So the block's outline *is* the kerb line, and its
height has to be the road's height there.

### The pairing, which is what was wrong

`QuarterGenerator` traces a block as a ring of **edges**, and the two halves of a
`QuarterDelim` belong to **different junctions**:

- `StreetPoint` is the junction the edge *leaves* (`spCurr`), and `Stroke` runs from it;
- `StartPoint` is a section point of the junction the edge *arrives at* (`spNext`) — the
  block's corner, offset outwards by roughly half a carriageway.

Everything that wanted "how high is this corner" paired `StartPoint` with `StreetPoint`,
so a corner took the height of a junction **at the far end of a whole street**. Measured
over the generated cities, that is **70–97 m away at the median and 135 m at the worst**,
against **7–12 m** (46 m worst) for the junction the corner really stands on; and
**2936 of 2936 corners** across four cities are a section point of the *next* delimiter's
junction and of no other, so there is nothing marginal about it.

What it does to the kerb, measured over `seed000`/1500 m and `Yelukhdidru`/3000 m with the
relaxed street heights the game uses:

| terrain | kerb range | corners with the pavement **below** the road |
|---|---|---|
| flat | 0.15 m exactly | 0 % |
| plane, 1 % | −1.12 … +1.47 m | 41–45 % |
| plane, 5.8 % | −7.21 … +7.86 m | 48–49 % |
| rolling, 30 m at 600 m | −20.7 … +20.7 m | 49 % |

Both engine sites were of that shape: the block pad's least-squares fit, and — added one
commit earlier — the **sidewalk `NavJunction`**, which is where an NPC's feet go. So the
pavement and the walker were consistently wrong together, which is why neither showed up
as a disagreement.

`QuarterDelim` now carries **`CornerStreetPoint`**, written together with `StartPoint` by
`SetCorner` (the two cannot be set apart — `StartPoint` is `get; private set;`), and a
source scan forbids taking a height from a delimiter's own `StreetPoint`.

**Superseded by §7i.** That was a workaround for a delimiter that straddled two edges. §7i
fixes the delimiter itself: `StreetPoint` *is* the corner's junction now, `CornerStreetPoint`
and the source scan are gone, and the description of the two halves above holds only for the
code as it stood before that.

### Snapping the boundary, and what it costs

Fixing the pairing is not enough on its own. The pad is a **plane** through corners that
are not coplanar, so it still answers at a corner with a fit residual — measured, 0.02 m
at the median, 0.36 m at p99 and 1.66 m at the worst corner on a 5.8 % plane, which still
inverts the kerb at 5 % of corners. So the floor's boundary takes
`Quarter.CornerGroundHeightAt` — the corner's own junction height, exactly — and the kerb
is exactly `QuarterSidewalkOffset` everywhere. The outline is therefore non-planar;
`TriangulateNonPlanarTests` already pins that LibTess keeps every vertex's height and
invents none, and `ExtrudePoly.BuildStaticPhys` builds convex hulls.

**The trade, stated rather than assumed.** `Quarter.GroundHeightAt` — the pad — is still
what buildings, trees, shop fronts and TALE doors stand on, and it is no longer exactly
the floor. It costs nothing where it matters: the fit is parametrised about the centroid
of the corners, so the plane **at the centroid is the mean of the corner heights
identically**, and a triangulation of those same corner heights reads the mean there too.
Measured over whole cities the difference at the centroid is 0.0000 m. The pad and the
floor part company at the **kerb**, where nothing stands, by the residual above, and
coincide in the **middle of the block**, where everything does. §7c's claim that everything
on a block agrees is weakened exactly that far and no further.

### The flat city

Untouched, and by two independent routes. `Quarter.GroundHeightAt` short-circuits on
`IsFlat` before the fit, and `FlatStreetHeight.GroundHeightAt` returns `AverageHeight`
itself — so `CornerGroundHeightAt` and the old pad read give the same float, and every
corner of every block still comes out at `AverageHeight + ClusterStreetHeight`. Asserted
over whole generated cities with an average that has no exact binary form.

### Covered, and not

The kerb, the pairing, the outline's plan geometry and offset, the sidewalk junction's
height and the flat city are all asserted against **real generated cities**. The mesh
emission and `BuildStaticPhys` need a fragment and a physics world and are not exercised;
`FloorOutlineOf` and `SidewalkJunctionFor` were hoisted out of them for that reason.

### Found and NOT fixed

- **The delimiter's `Stroke` and `StreetPoint` are off by one from the edge its corners
  span.** *(Fixed, see §7i — where the measurement also corrected the list of consumers:
  `GenerateShopsOperator` reads only `StartPoint` and was never a consumer of the pairing,
  and no shop front moves. `CornerStreetPoint` folds back into `StreetPoint` there.)*
- **`GenerateNavMapOperator` files each sidewalk corner under `delim.StreetPoint.Id`** for
  crossing generation. *(Fixed, see §7h — where the measurement also corrected this
  description: no crossing lane was actually drawn between the wrong corners, because the
  crossing loop re-derives them by position. Only the emission order moved.)*
- **No deck elevation term** in the block floor. Blocks are traced on the ground only
  (`QuarterGenerator.Generate` skips a non-zero start level), asserted rather than assumed.

---

## 7f. The satnav guideline is on the road (pre-existing, unrelated to elevation)

Reported from play: *"Navigation guideline … is a bit off street."*

`LocalPathfinder._transportType` defaulted to `TransportationType.Pedestrian`, and
`builtin/modules/satnav/Route.cs` named no type at all — neither for the pathfinder nor for
either `TryCreateCursor`. So the route the **player** follows, in a hover ship, was planned
over the **pedestrian** network: the sidewalk lanes that `GenerateNavMapOperator` traces
round every block from the quarter boundaries. The guideline ribbon is centred on whichever
lane the route used, so it has always been drawn on the pavement. Nothing about this is
elevation; it predates all of Phase A.

**The default is gone rather than corrected.** A default that is right for one of two
networks is how this happens again, and there is no answer that is right for both. So
`LocalPathfinder`, `NavCluster.TryCreateCursor`, `NavClusterContent.TryCreateCursor`,
`satnav.Module.CreateRoute` and `Route` all take the type as a required argument, and
`engine.quest.ToSomewhere.TransportType` is `required` — a new quest navigation target
cannot compile without saying which network it is for. All four shipped sites sense the
player through `MainPlayModule.PhysicsStem`, the hover ship's own physics name, so all four
are `Car`.

**Both halves must be given the same type**, and getting that wrong is worse than it looks:
`NavClusterContent.TryCreateCursor` returns `Nil` rather than a lane of the wrong kind, so a
pedestrian cursor into a car A* does not route badly, it finds no route at all.

### `RoutePlan`, and the survivor that produced it

`Route.Search` hops onto the engine's logical thread, so nothing in it was exercised.
Mutation testing found it: putting `TransportationType.Pedestrian` back at the two
`TryCreateCursor` calls — the defect, in its exact original location — passed the entire
suite. So the planning moved into `builtin.modules.satnav.RoutePlan` (two cursors, the A*,
and the truncation of the last lane at the target), which needs only a `NavCluster` and is
tested end to end over a fixture with a car lane and a pavement lane on the same street.
`Route` keeps the thread hopping and one line, which is scanned for.

The truncation went with it and gained its first test on the way.

**Deliberately unchanged:** `TransportationTypeFlags`'s parameterless constructor still
defaults to `Pedestrian`, and `NavLane.AllowedTypes` still initialises through it. That is
"what may use this lane", a different question, and both engine emission sites pass the
type explicitly.

---

## 7g. The satnav guideline lies on the road (pre-existing, unrelated to elevation)

Reported from play: *"Navigation guideline is hovering in the air."* Arithmetic, and wrong
in the flat game too.

`ToSomewhere._onJunctions` built the ribbon quads at `nl.Start.Position`, which is
`NavJunction.GroundHeight + MetaGen.ClusterNavigationHeight` — **3 m, the vehicle *hover*
reference, not a surface** — and the parent transform then took a flat `0.5f * UnitY` off.
Net: ground + 2.5 against a road surface at ground +
`CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE` = ground + 2.0. Half a metre, always.

The 0.5 was almost certainly a z-fighting margin. It was applied to the wrong base, so it
became a floating ribbon instead of a lift.

`builtin.modules.satnav.RouteRibbon` now owns the arithmetic: `SurfaceHeightOf` starts from
the junction's `GroundHeight` — which is exactly why `NavJunction` carries the ground rather
than a position with an offset baked in (§7 "Nav lanes are the NPC height source") — and
adds the offset for whichever surface the route is over. A **car** ribbon takes
`CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE`, the term the road mesh and `JunctionCollider` are
built at; a **pedestrian** ribbon takes `NavJunction.WalkingHeightOf`, one kerb higher,
because the pavement is the block floor's extruded top face. Nothing shipped asks for the
second since §7f made every quest guideline `Car`, but a ribbon that silently used the
carriageway height would be sunk into the kerb slab it is drawn on.

The parent transform's offset is gone; a blanket shift is what turned a margin into a bug.

### The lift, and why 0.1 m

`Sdl3WindowBackend` asks SDL for a **16-bit** depth buffer, and the play camera runs
near = 1, far = √3·1000 + 100. The depth quantum on a coplanar surface is then roughly
z²/65535:

| distance | quantum |
|---|---|
| 20 m | 6 mm |
| 50 m | 38 mm |
| 100 m | 0.15 m |
| 200 m | 0.61 m |

So **no fixed lift keeps a long route off the road at its far end**, and the choice is only
about the near end — the part a driver reads. 0.1 m holds out to about 80 m and is a tenth
of the clearance the player's own ship keeps over the same surface
(`HoverSurfaceProbe.SurfaceClearance` = 1 m), so it cannot read as floating. Past that the
far end of a route may shimmer against the road; that is depth precision rather than height,
and the honest fix is a 24-bit depth buffer, which is a platform change.

### The slope comes for free, and the centring was checked rather than assumed

Each end of a quad takes **its own** junction's surface height and the along-vector is the
difference of the two, so a ribbon over a climbing road climbs with it with no extra term.
Asserted on all four corners of a quad between junctions 6 m apart in height.

The 4 m quad is built as `start + (Width/2)·right` extending `−Width·right`, i.e. centred on
the lane, which is what §7f's change makes matter — it is now centred on the *carriageway*.
Confirmed by test (opposite Z of equal magnitude, and across ⟂ along), and the width is
pinned on the number.

### Not covered

`ToSomewhere._onJunctions` itself runs inside a queued main-thread action in a module that
needs a booted engine, a physics world and the satnav module. A source scan checks that it
builds through `RouteRibbon.QuadFor` and adds no offset of its own.

### Found and NOT fixed

`Vector3.Normalize` of the plan direction produces NaN for a lane with no horizontal extent.
Pre-existing, and the generator emits no such lane.

---

## 7h. A pavement corner belongs to the junction it stands on (crossings)

Recorded in §7e as found and not fixed, and now measured rather than argued — which
changed what the defect is.

`GenerateNavMapOperator` groups every block corner by a junction id so that pedestrian
crossings can be drawn at each junction between the two corners flanking each arm. It filed
each corner under `delim.StreetPoint.Id`. A `QuarterDelim` is an **edge**, so that is the
junction at the far end of it — **70–97 m away at the median** of the generated cities,
against **7–12 m** for the junction the corner really touches. Measured over the corner
lists themselves: a filed corner sat **70.3–97.3 m from the junction it was filed under at
the median and 134.9 m at the worst**; after the correction, **6.9–12.4 m at the median and
46.5 m at the worst**, i.e. half a carriageway.

### What it did NOT do, which is the part §7e got wrong

**No crossing lane moves. Not one, in any of the four baseline cities.** The crossing loop
does not use the filed list to find its corners: for a junction with three or more arms it
asks `StreetPoint.GetSectionPointByStroke` for the two section points flanking each arm and
looks *those* up by position in the city-wide corner table. The filed list is used only to
decide **which junctions are considered at all**, and that set is provably unchanged — a
block is a closed ring, so the junctions its delimiters leave and the junctions its corners
stand on are the same set rotated by one — and by the **one-arm dead-end branch**, which
connects every corner in the list pairwise. That branch never fires: dead-end junctions
exist in the graph (8 / 18 / 43 / 121 of them across the four cities) but every block traced
through one is discarded before it reaches here, so **no junction with one arm has a filed
list at all**.

| city | crossing lanes before | after | lanes only before | only after |
|---|---|---|---|---|
| `seed000` 500 m | 4 | 4 | 0 | 0 |
| `Yelukhdidru` 800 m | 14 | 14 | 0 | 0 |
| `seed000` 1500 m | 222 | 222 | 0 | 0 |
| `Yelukhdidru` 3000 m | 1411 | 1411 | 0 | 0 |

So this is a latent defect, not a live one: the list is a claim about which corners belong
to a junction that nothing checked, standing one dead-end block away from being read.

### What DOES move in the default flat city: the emission order

The filing decides the order the crossing loop runs in, and therefore the order crossing
lanes land in `NavClusterContent.Lanes` and in every junction's `StartingLanes` — which is
what an A* breaks ties on. The dictionary was a plain `Dictionary<int, …>`, so that order
was the insertion order, i.e. a function of which block the quarter store traced first *and*
of which half of a delimiter the corner was filed under.

It is a `SortedDictionary` now, by junction id, matching the car-lane junction table twelve
lines above it in the same method. That makes the order a property of the cluster rather
than of the trace, and it is the one thing here that is not invariant: **1408 of 1411
crossing lanes in the 3000 m city and 222 of 222 in the 1500 m city come out at a different
position in the list**, over an identical set. The TALE suite, which exercises pedestrian
routing heavily, is 200/200 across that.

### Covered

`FileCornerUnderItsJunction` is hoisted out of the operator for the same reason
`SidewalkJunctionFor` was — the operator needs a stroke store, a quarter store and the
container and is not exercised — and for one more: **the height and the crossing filing must
name the same junction for a corner**, and two inline reads of a delimiter is how they came
to differ. Both are asserted against real generated cities: every filed corner is a section
point of the junction it is filed under and no further than half a carriageway from it;
every crossing corner the operator will look up is in that junction's own list; and the key
set is unchanged, with the ring rotation that makes it so asserted per block.

A source scan forbids **any** read of a delimiter's own `StreetPoint` anywhere under
`builtin/modules/satnav`, because putting either the filing or the height back inline in the
operator would restore the defect with every other test still green. That is exactly what
the mutation run showed: re-inlining the filing fails only the scan.

---

## 7i. A block delimiter describes one edge

The other of §7e's two "found and NOT fixed" items, and the one that actually moves the
shipped flat city.

`QuarterGenerator` traces a block as a ring and filled each `QuarterDelim` in from **two
different steps of that trace**: `StartPoint` from the junction the step *arrived* at, and
`StreetPoint`/`Stroke` from the one it *left*. So a delimiter described two edges a street
apart, and which one a consumer got depended on which field it happened to read.

### Measured, because it is what decided which way round to fix it

Over the four baseline cities, for every boundary segment `delims[i].StartPoint →
delims[i+1].StartPoint`:

| | angle to the segment | distance to the segment's midpoint |
|---|---|---|
| `delims[i+1].Stroke` (the *next* delimiter's) | **0.00°** median | 4.9–8.9 m median, 10.4 max |
| `delims[i].Stroke` (its own) | 60–76° median | 35–51 m median, 67 max |

**2936 of 2936 edges**, no exceptions: a boundary segment runs alongside the *next*
delimiter's stroke at half a carriageway, and across its own at half a street. And
`delims[i+1].StreetPoint == delims[i].CornerStreetPoint` on all 2936, which is the identity
the fix uses.

### The fix, and why this way round and not the other

`QuarterDelim` is now **corner + the edge leaving it**: `StartPoint` is the corner,
`StreetPoint` is the junction it stands on, `Stroke` is the street from there to the next
corner. All three are `get; private set;` and written by one `SetEdge`, so a delimiter
cannot be assembled from two steps of the trace again — the precedent being `SetCorner` and
`NavJunction.At`. `CornerStreetPoint`, which §7e added as a workaround for exactly this,
folds back into `StreetPoint`: there is now one junction per delimiter and it is the right
one, so the source scan that policed the two is gone and the three private setters stand in
its place.

The generator change is one call site: all four values — `spNext`, `strokeNext` and the
section point — are already in hand at the same step, so the delimiter is simply built from
the *arriving* end throughout instead of straddling.

**Two repairs were possible and they are not equivalent.** The other is to move the corner
*back* to the junction the step left, which keeps `StreetPoint`/`Stroke` where they are.
That rotates the ring of corners by one — and the ring is the estate polygon, which
ClipperOffset shrinks into the building, which `_addShops` walks to make shop fronts, whose
order a random index then picks from. It moves buildings and shops. Moving the labels
forward instead leaves the polygon **bit for bit identical**: the outline of all 2936
corners across the four cities hashes the same before and after, so blocks, estates,
buildings, shop fronts, shops, quarter AABBs, quarter floors and the kerb do not move at
all.

### What DOES move in the default flat city

| consumer | effect | measured |
|---|---|---|
| `Placer` with `Reference.StreetPoint` + `AnyQuarter`, hence `car3.CharacterCreator.ChooseStreetPoint` | the junction picked for a given random draw is the next one round the block | **100 % of delimiters**, 75–106 m median, 136.5 m max |
| `TaxiNpcSpawnerModule` (`GetDelims().First().StreetPoint.Pos3`) | same, once per taxi quarter | 75–107 m median, 136.5 m max |
| `citizen.SpawnOperator.SpawnCharacterAt` | the nearest street point is **unchanged** (same candidate set), but its delimiter index moves one back, so the NPC starts its loop at the corner of the junction nearest the spawn instead of the next corner along | 66–89 m median, 136.3 m max |
| `citizen.EntityStrategy` (Placer-placed NPCs) | **nothing.** `QuarterDelimIndex` is the random draw and is unchanged, and the citizen path never reads `pod.StreetPoint` | — |
| `GenerateHouseDescriptionsOperator` | **nothing.** It averages `StreetPoint.Pos` over the whole ring, which is invariant under a rotation | ≤ 0.0002 m, floating-point summation order only |
| `QuarterLoopRouteGenerator`, `SegmentNavigator` | the segment's `StreetPoint` and `Stroke` labels become the segment's own; nothing in the game reads either | positions unchanged |

So the visible change is **where traffic and force-spawned pedestrians appear**: one
junction round the block, in a city where every junction of a block is as good as any
other. This is the first change in this line of work that moves the shipped flat city's
content, and §7h moved its crossing emission order; **"the flat city is bit for bit
unchanged" no longer holds unqualified from here.**

### Consumers examined and deliberately left

- **`GenerateShopsOperator`** was named as a consumer of the pairing and **is not one**. Its
  `_hasPedestrianAccess` reads `StartPoint` only, walking the polygon ring; it never touches
  `Stroke` or `StreetPoint`. Correct as written, before and after.
- **`Placer`** itself is genuinely ambiguous: it wants "a street point of this quarter", the
  candidate set is the same either way, and no consumer today reads its `StreetPoint` and
  `QuarterDelimIndex` together. It moves as a consequence of the type being fixed, not as a
  correction — but the pod it hands back is now internally consistent, which is what makes
  reading the two together safe for the next caller.

### Covered

- `QuarterDelimTests`, against generated cities: a delimiter's stroke touches its own
  junction and its far end **is** the next delimiter's junction (identity, not proximity);
  the boundary segment is under 0.5° off that stroke and within half a carriageway of it;
  and the previous delimiter's stroke — the wrong answer that used to be given — is 60°+ off
  at the median. Plus a reflection test that none of the three properties has a public
  setter.
- `QuarterLoopRouteTests`: every segment of an NPC's block loop names the junction it starts
  at and the street it runs along, and starts beside its own corner rather than the next.
- `QuarterFloorTests` gained `ThePadSitsOnTheCornersOfARealBlock`, because the pad's
  per-corner pairing was covered only by a fixture: reading a neighbouring junction still
  produces a plane, still leaves the plan geometry right and is still invisible in a flat
  city. It failed 2 tests before that and 5 after.

**Metric separation does not work here and that is worth keeping.** Two arms of a junction
can be nearly collinear, so the previous delimiter's stroke can be 0.00° off the same
segment; and a short street brings a neighbouring junction to within 25.5 m of a corner
whose own junction is 25.7 m away. Every discriminating assertion is therefore on identity —
section-point membership or `Assert.Same` on the junction — with the distances and angles
compared as medians only, to show the tests are not vacuous.

### Found and NOT fixed

`SegmentNavigator.NavigatorBehave` writes `_position.StreetPointId` from the **old**
`StreetPoint` on the line before it overwrites `StreetPoint`, so the id lags the object by
one update. Pre-existing, unrelated to the pairing, and nothing reads either.

---

## 7j. The pavement faces upwards (why "very few sidewalks")

Reported from play of a terrain-following city, after §7e put the kerb on the road:
*"I'm afraid I'm only seeing very few sidewalks."*

The suspicion going in was burial: the block floor is a **0.15 m slab**, the conforming pass
of §7d reaches three cells (60 m) and a block is 28 m deep at the median, so the terrain in
the middle of a block is only about half graded — and terrain that rises more than
`ClusterStreetHeight + QuarterSidewalkOffset` = 2.15 m above the surrounding junction
heights simply covers the pavement up.

**That is real, it was measured, and it is not what the player was seeing.**

### What the ground actually does, measured against the shipped terrain

The terrain here is not a guess. `nogame.terrain.GroundOperator` seeds a diamond-square
skeleton at fragment resolution over a 90 km world and `ElevationBaseFactory` refines each
fragment through `engine.elevation.Tools.RefineSkeletonElevation` — both reachable from a
test, so the measurement runs against the elevation grid the game really builds. Over four
3000 m windows of it:

| | |
|---|---|
| gradient over one 20 m cell | **15.6–16.6 % median**, 42.6–45.2 % p90, > 100 % max |
| relief inside a 200 m window (one block) | **57–60 m median**, 81–87 m p90, 111–129 m max |

So the ground under a city is genuinely mountainous at block scale, and `GradeRelaxer` has
a lot to take out: the relaxed junction height differs from the raw terrain by 2.9–6.5 m at
the median and up to 32 m.

### Burial, quantified

Sampling every block of the baselines on a 2 m grid, against terrain conformed by
replicating `ClusterConformElevationOperator.Grade` on the real 20 m lattice and
interpolating it as the terrain mesh does, with the floor's top face taken from the actual
LibTess triangulation of the outline:

| city | area buried | kerb rim (≤ 5 m) buried | blocks fully buried | median burial |
|---|---|---|---|---|
| `seed000`/500 | 1.6 % | 0.0 % | 0 of 3 | 0.33 m |
| `Yelukhdidru`/800 | 12.4 % | 3.3 % | 0 of 10 | 1.44 m |
| `seed000`/1500 | 24.7 % | 7.2 % | 0 of 82 | 2.30 m |
| `Yelukhdidru`/3000 | 20.7 % | 3.6 % | 0 of 445 | 2.57 m |
| `Yelukhdidru`/3000, elsewhere | 18.2 % | 3.1 % | 0 of 445 | 2.51 m |

**No block is fully buried anywhere**, and the strip that reads as "the sidewalk" — within
5 m of the kerb — is 3–7 % covered, because the conforming influence is ~0.95 that close to
a road. A fifth of the block INTERIOR is under a mound, which is a real artefact and is
where buildings stand, but it is not a missing pavement. **Deliberately not fixed** (below).

### What it actually was: the top face pointed down

`GlThreeD` runs `glEnable(GL_CULL_FACE)` with `glCullFace(BACK)` and `glFrontFace(CCW)`, so
a triangle wound the wrong way round is not drawn at all. `builtin.tools.Triangulate.ToMesh`
took **one** `v3Normal` and used it for two unrelated jobs: the plane LibTess sweeps in, and
whether to write per-vertex normals. `ExtrudePoly` therefore passed
`PairedNormals ? Vector3.Normalize(_path[0]) : Vector3.Zero` — and a zero normal makes
LibTess **derive the projection plane from the polygon itself**. For a ring that is no
longer planar, which after §7e is every block on a slope, that derivation flips.

Measured over the block outlines of the baseline cities:

| | facing up | facing down |
|---|---|---|
| `Yelukhdidru`/3000, flat | 437 | **8** |
| `Yelukhdidru`/3000, 5.8 % plane | 234 | **211** |
| `Yelukhdidru`/3000, rolling | 219 | **226** |
| `seed000`/1500, 5.8 % plane | 51 | **31** |
| with the plane named explicitly, every case | **445 / 445** | 0 |

**About half of a hillside city's pavements were being culled away.** Every mesh was
complete, every vertex was in the right place, nothing threw, and nothing was logged. The
parent investigation had already established that "445 of 445 blocks triangulate fully" —
which was true and was the wrong question.

`GenerateClusterQuartersOperator` was the **only** `ExtrudePoly` caller with a cap that does
not set `PairedNormals` — houses and the L-system's `extrudePoly` both do, and both have
always named their plane. The coupling meant the one caller that said "no vertex normals
please" was also the one caller that said "guess my plane".

### The fix

`Triangulate.ToMesh` takes the sweep plane and the vertex normal as **separate** arguments,
and **refuses a zero plane** rather than falling back to the guess — a default that is right
for one of two callers is how this recurs. `ExtrudePoly` passes `vu`, the normalised
extrusion direction it already computes, for both the ceiling and the floor.

`ExtrudePoly`'s constructor also stopped resolving `engine.physics.API` out of the `I`
container; the lookup moved into `BuildStaticPhys`, which is the only half that needs it.
That is why any of this is testable: `BuildGeom` — the geometry the class exists for — could
not previously be called at all without a booted engine, which is a large part of why a
winding bug lived in it undetected.

### The other half: block floors were culled at 400 m

`InstanceDesc.CreateFromMatMesh(mmmerged, 400f)`, against `100000f` for the road mesh in
`GenerateClusterStreetsOperator` and `3000f` for the fragment's own terrain — and 400 m is
shorter than a fragment's diagonal. `DrawInstancesSystem` culls on the camera-to-instance
distance plus the merged AABB's radius (median 302 m, max 421 m over the 3000 m city), so
pavements stopped at roughly 700–820 m while `PlayerViewer` keeps fragments out to 1131 m
and the roads on them drew the whole way. Roughly nine of the twenty-five loaded fragments
showed pavements; twenty-five showed roads.

`MaxDrawDistance` is now derived from `PlayerViewer.LoadNSurroundingFragments` — the worst
case is the camera at one corner of its own fragment and the fragment at the opposite corner
of what the loader keeps, `(N + 1/2) × FragmentSize` along each axis — with one fragment of
slack so the bound is not a knife edge. It cannot cull a fragment the loader has decided to
keep, which is the property worth having: that geometry has already been generated and paid
for. It costs nothing: a whole fragment's block floors merge to **339 vertices and 765
indices** at the worst fragment of the 3000 m city.

### Exceptions are visible now

`_generateQuarterFloor` caught **every** exception from `BuildGeom` and `BuildStaticPhys`
into a `Trace`, which is filtered off by default — so a block that failed to build produced
no geometry and no evidence. Those are `Error` now, and a source scan walks each `catch`
body by brace count and fails if it reports nothing or reports through `Trace`. The scan
counts braces rather than lines on purpose: an eight-line window passed until a comment
explaining the level pushed the `Error` call past it.

### What the flat city loses, exactly

Not nothing, and the delta was measured block by block rather than argued. Naming the plane
changes the cap of **exactly the blocks that were coming out backwards** — 8 of 445 in the
3000 m city, 1 of 82, 1 of 10, 1 of 3 — and every other block's cap comes back with the same
vertices in the same order and the same indices. Nothing moves: the flat outline is still
`AverageHeight + ClusterStreetHeight` at every corner (`QuarterFloorTests`), and the cap is
still exactly that outline raised by one kerb, before and after
(`ThePavementIsTheOutlineRaisedByExactlyOneKerb`). What changes is that eight block floors
that were being culled are drawn.

The same is true of the other `PairedNormals`-less cap in the tree, the rooftop
`powerline(P,h)` of `nogame.cities.HouseInstanceGenerator`: over 200 random orientations, 94
end caps change, **every one of them from facing backwards to facing forwards**, and none
the other way. So the whole flat-city delta is "a face that was culled is now drawn"; no
vertex moves anywhere.

### Covered

- `QuarterFloorFacingTests`, over all four baselines × {flat, 5.8 % plane, rolling}: every
  triangle of every block's pavement has a positive Y normal. The flat case is not
  redundant — eight blocks of the shipped flat city failed it.
- `TheKerbFacesOutOfTheBlock`: the sides are wound from the ring's index order and never go
  near the tessellator, so they are the control; tested against each edge's own outward
  direction rather than a centroid, since no block is convex.
- `ExtrudePolyCapTests` sweeps a powerline section along **24 non-vertical** axes and checks
  both caps look out of the extrusion. Hard-coding `UnitY` as the cap plane is right for
  every pavement in the game and wrong for every powerline; without this it survives.
- `TriangulateNonPlanarTests` gained the facing assertions its earlier version was missing,
  a `clockwise` reversal test, the zero-plane refusal, and a test that the vertex normal and
  the sweep plane are independent.
- `TheFragmentsPartitionThePlane` and `EveryBlockIsClaimedByExactlyOneFragment`:
  `Fragment.IsInsideLocal`'s half-open comparison is what makes a block emit exactly once,
  and `Fragment.PartitionContains` is now a static so a test can ask the real thing.
- `BlockFloorsAreDrawnAsFarAsTheirFragmentIsLoaded` recomputes the loader's reach
  independently rather than comparing against the operator's own expression.

**Mutation survivors found.** (1) Widening `PartitionContains` to be closed on both sides
survived the whole-city coverage test entirely — no generated block's centre lands exactly
on a 400 m boundary, so a rule that would draw a block twice is invisible to any amount of
real data; `TheFragmentsPartitionThePlane` asks about the boundary directly and catches all
three comparison mutations. (2) Hard-coding `Vector3.UnitY` as the cap plane survived every
pavement test; `ExtrudePolyCapTests` exists for it.

### Found and NOT fixed

- **Terrain buries a fifth of the block interior.** The numbers are above. Fixing it
  properly is the change §2c already deferred: the elevation grid is one sample every 20 m
  and a real cutting needs a finer grid inside cities. Widening
  `StreetHeightField.RadiusInCells` would flatten the countryside around every city instead
  of grading it, and raising the block floor above its own kerb would undo §7e. Nothing here
  should be done piecemeal.
- **`GenerateClusterQuartersOperator` does not skip `quarter.IsInvalid()`**, which every
  other quarter consumer does (`GenerateHousesOperator`, `GenerateTreesOperator`,
  `GeneratePolytopeOperator`, `SpatialModel`, `GenerateNavMapOperator`, the taxi spawner).
  It draws more, not fewer, so it is not this bug; the baselines produce no invalid quarters
  at all, so there is nothing to measure it against.
- **`Triangulate.ToConvexArrays`** — the physics half — passes no normal at all and hard
  codes `ContourOrientation.Clockwise`, so it has the same latent projection guess. It feeds
  `ExtrudePoly.BuildStaticPhys`, which builds convex **hulls** from the polygons, and a hull
  does not care which way its input was wound. Left alone.
- **A 500 m city has three blocks.** Not a rendering matter, but worth writing down: the
  block counts over the baselines are 3 / 10 / 82 / 445 for 500 / 800 / 1500 / 3000 m.


---

# §7k — The pavement is level across its width (2026-08-31)

Reported from play with the design steer: *"sidewalks shall be up/downwards only in the
direction of walking, not in the direction to the street. I understand that we might have
non-perpendicular setups."*

## What the surface was

There is no sidewalk object. `GenerateClusterQuartersOperator` extrudes the block polygon
up by `QuarterSidewalkOffset`, so the **cap is the pavement**, and since §7c every corner of
that polygon sits at its own junction's road height. The cap is a single LibTess fan over
the ring with **no interior vertices at all** — measured, the tessellated cap has exactly as
many vertices as the input ring, min 3, median 4–5, max 16, for a block up to 150 m across.
So a block whose corners differ in height is a warped quad and which way each triangle tilts
is decided by the sweep.

Measured **within a pavement's own width** (0.25 w → 0.75 w in from the kerb at each edge
midpoint, read barycentrically off the cap's own triangles) over the baselines on rolling
ground: cross-fall **7.5 % median, 16 % p95, 63 % worst**, against an along-edge slope of
7.0 %. The surface is tilted diagonally at about 45° to the street. A real footway is 2 %.

> CITY-3D-OPEN-POINTS reported 11 % / 33 % / 178 % for the same thing. That was measured
> with a **3 m** step, which exceeds the pavement width on most blocks (`sidewalkWidth` is
> 1/2/4/6 m by downtownness), so it was partly measuring the block interior.

## Why the obvious repair does not work

The ledger's Option 2 — one inset vertex per corner, at the mitre, taking that corner's
height — was implemented and measured before being discarded.

A mitre point is one width from **both** edge lines, which places it `w·cot(θ/2)` along the
leaving edge from the corner and the same distance back along the arriving one. Its two
perpendicular feet on the two edges are therefore `2·w·cot(θ/2)` apart, and the two rim
cells it serves want the outer heights at *those two different feet*. Give it either and the
surface cracks; give it the corner's own height — the average of the two — and each cell
keeps a cross-fall of `s·cot(θ/2)`:

| interior angle | 40° | 60° | **90°** | 120° | 150° |
|---|---|---|---|---|---|
| cross-fall, as a multiple of the along-slope | 2.75 | 1.73 | **1.00** | 0.58 | 0.27 |

The median block corner is **90.1–93.5°**, so the median case is *no improvement at all*,
and a sharp corner is worse than today. Measured on the real cities the construction moved
the median from 7.2 % to 6.7 %. The cross-fall is uniform over each cell, so one bad corner
contaminates a whole 66 m edge — two triangles have only one cross-gradient between them.

## The condition, stated exactly

A rim quad has no cross-gradient **precisely when every one of its vertices carries the
height the outer edge has at that vertex's own projection onto that edge**: the heights then
all lie on the plane `h = h₀ + s·x`, whose gradient runs purely along the edge. Nothing else
about the quad's shape matters — it need not be a parallelogram, or planar in plan, or
anything else.

The only thing that can violate it is a vertex shared between two edges, because the two
edges project it to different places. **So the edges do not share one.** Each edge owns a
`CapInsetEdge` — two points, both offset perpendicularly by the full width, both carrying
that edge's own interpolated height. Neighbouring cells meet only at the outer corner, where
the requirement is that both name the corner's height, which they trivially do.

Result, measured: **cross-fall 0.0 % at every percentile, on all 2823 measured edges of the
four baselines**, with 438 of 445 and 79 of 82 blocks carrying a pavement.

## The corner ramp, and the number that had to be measured

The cells meet the kerb again at each corner, so the pavement ramps back to zero width
there and that region falls to the block interior. The ramp length is **the corner's mitre
reach plus one width**. One width alone is wrong and unmistakably so: at a 90° corner the
two edges' inset points then land on *exactly the same point*, and sharper than that they
cross — which rejected **435 of 445 blocks** before the number was measured.

## What else moved

- **`Quarter.SidewalkWidth`**. The width was computed inside
  `QuarterGenerator._createBuildings`, used to inset the estate, and thrown away. The floor
  now insets its cap by the same number; if the two drifted the pavement and the building
  wall would stop meeting all the way round every block. A source scan forbids a second copy.
- **`ExtrudePoly.CapInsetEdges`**, null by default, ceiling only. The rim's winding is
  derived from the ring's own signed area about the cap plane — the property
  `Triangulate.ToMesh` already has, and worth having for the same reason §7j gives.
- **The flat city is untouched**, asserted vertex for vertex and index for index over whole
  generated cities: a flat block's corners are all at one height, so there is nothing to
  remove, and `PavementInsetOf` refuses on `IsFlat` as `Quarter.GroundHeightAt`,
  `DeckCollider` and `JunctionCollider` already do.

## Found and NOT fixed

The block **interior** now carries all of the warp that used to be spread over the whole
cap, and buildings stand on the *pad*, a third surface again. That is ledger item (a), and
it is the next thing.

---

# §7l — A building stands on its block (2026-08-31)

> Numbered §7l rather than §7k: the ledger's task note asked for "a new §7k" and that
> number was taken the same day by the pavement fix above.

Reported from play of a terrain-following city: **a building hovering in the air, with
grey/white noise on its exposed underside.** Player at `<-210.6, 50.1, 184.3>` in
`Yelukhdidru`. This is ledger item (a), and it is what §7k handed the ball to — once the
pavement rim was made level across its width, the whole of a block's warp lives in the
block INTERIOR, which is exactly where the building is.

## Three compounding causes, none of them a bug you can point at

1. **The base is one scalar.** `GenerateHousesOperator` handed the footprint to the
   L-system with `Y` forced to 0 and extruded it straight up, so a single
   `2.5f + quarter.GroundHeightAt(centroid)` IS the entire floor. The code comment
   claiming the house "tilts with" the block was false and has been removed.
2. **`GroundHeightAt` is the pad**, a least squares plane through the block's corner
   heights — not the surface the building stands on. Measured on the shipped terrain the
   residual against the real floor is median 0.00 m but p05 −3.5…−6.1 m, worst −18.3 m.
3. **The footprint is nearly the whole block.** An estate IS the block outline, and
   `_createBuildings` insets it by `Quarter.SidewalkWidth`, which is 1–6 m. Measured
   footprint diagonal on the shipped terrain: median **89.3 m**, p90 239 m, max 359 m.

So the sample was taken in the middle of a surface that rises 13 m across the thing
standing on it. **Every building in every baseline city had both a corner in the air and a
corner in the ground** — worst air median 4.3–7.1 m, worst burial median −2.6…−6.7 m.

## The decision: planar floors, and why

The owner's steer, and it is a design decision rather than a limitation:

> *"real live buildings usually have planar floors, non-planar floors exist usually out of
> later changes on the building. Shopfront entries would be usually aligned per story and
> not gradually, adding stairs (in real live). Let's for a moment ditch the stairs and
> align to stories."*

A footprint-following (per-vertex) base was offered and rejected. So the base stays one
number, and the whole of the fix is **which** number, and how it is proved.

## The guarantee, and what makes it one

`engine.streets.generation.BuildingFooting.BaseHeightOf` answers the block's **lowest
corner**, raised by `ClusterStreetHeight + QuarterSidewalkOffset`. That is a bound on the
block floor rather than a sample of it, and the bound is exact for a reason that needs no
reference to the mesh:

- an outer cap vertex is `Quarter.CornerGroundHeightAt` of its own delimiter, exactly;
- each rim inset point carries the height its own outer EDGE has at its own projection onto
  that edge (§7k), i.e. a convex combination of that edge's two corner heights;
- a piecewise linear surface over those vertices is therefore bounded below by the lowest
  corner and above by the highest.

**The premise had to be checked rather than assumed, and it is the one thing that could
leak:** if an inset point's projection ever landed *past* a corner, its height would be an
extrapolation and could fall outside the corner range. Measured over the four baselines on
three grounds: **projection overrun 0.0000 in t units, zero inset points outside their
block's corner range.** `EveryCapVertexCarriesACornerHeightOfItsOwnBlock` is that check,
and it is also the mutation guard for §7k's corner ramp — shortening the ramp to one width
pushes the insets onto each other and rejects 435 of 445 blocks, which the test catches as
"only 0 inset points".

**The bound is taken over the whole block, and that had to be measured too.** A block
carries **exactly one estate and at most one building** — 1 estate on every one of
3/10/82/445 blocks, and 3/3/81/149 buildings, never two on one estate. So a footprint IS
the block, less 1–6 m: the exact minimum of the cap over a footprint sits only
**0.19–0.61 m above the block's own minimum at the median**, 1.5 m at p90, 3.74 m at the
worst building of the four cities. That slack is what a smaller building on a larger block
would be over-sunk by, so `ABlockCarriesOneEstateAndAtMostOneBuilding` fails the day that
changes.

**No margin is subtracted.** A margin would move the shipped flat city by more than the
0.35 m below and buy nothing: `ExtrudePoly` emits the floor cap clockwise, i.e. facing
down, so it is back-face culled from above and cannot fight the pavement for the depth
buffer even where the two are coplanar.

## What it costs — burial, measured on the shipped terrain

Reproduced in `tests/.../streets/ShippedTerrain.cs`: `GroundOperator`'s diamond-square,
seed `"mydear"`, refined per fragment exactly as `ElevationBaseFactory` does, sampled with
`CacheEntry.GetElevationPixelAt`'s own two-triangle rule, then `GradeRelaxer` with the
shipped `GradePolicy`. It reproduces the ledger's independently measured figures for the
same cities to within a few per cent.

`localFloor − base` at every footprint vertex:

| city | n | min | p05 | med | p90 | max |
|---|---|---|---|---|---|---|
| seed000/500 | 16 | 0.10 | 0.10 | **4.92** | 8.58 | 9.17 |
| Yelukhdidru/800 | 11 | 0.22 | 0.22 | **6.81** | 7.36 | 7.83 |
| seed000/1500 | 392 | 0.11 | 0.38 | **7.12** | 20.36 | 42.95 |
| Yelukhdidru/3000 | 788 | 0.04 | 0.35 | **9.44** | 23.34 | 53.85 |

Burial is the accepted price of a planar floor on a block 150 m across whose kerb falls
13 m. Floating is not accepted at any price.

## The half of it the brief did not name: sinking eats the building

Sinking to the minimum with the design height unchanged pulls the roof down with the
floor. Measured before `HeightOf` existed: **the roof of 64 of the 149 buildings of
Yelukhdidru/3000 fell below the block floor somewhere over its own footprint** (22 of 81,
1 of 3, 1 of 3 in the others), and the median 24 m building showed **4.54 m** above ground
at its highest corner. **No building disappeared entirely** — 0 of 149, 0 of 81 — so it
was never total, but "a house must not be in the air" needs its converse.

`BuildingFooting.HeightOf` adds the block's corner **spread**, so the roof stands the
design height above the block's HIGHEST corner, which is the upper bound of the floor for
the same reason the base is the lower one. Height added: median **8.0 / 8.9 / 11.6 /
14.9 m**, p90 up to 30.8 m, max 55.7 m — and **exactly zero on a flat block**, where every
corner is at one height.

## Shops: snapped to a storey, never below the kerb

`StoreyGroundAt` is the block's lowest corner raised by whole `MetaGen.StoryHeight` steps
until it clears the pavement **in front of that shopfront** — not in front of its building,
which spans a block. Storey index measured: median 2–4, max 19; `sill − localPavement`
median 1.22–1.87 m, **below one storey always, by construction**.

The storey index is a difference of two GROUND heights, and that is not tidiness:
`ClusterStreetHeight` and `QuarterSidewalkOffset` cancel out of it, so it is exactly 0 on a
flat block rather than the ceiling of a rounding error — which is what lets all three
consumers stay bit for bit where they are in the shipped flat city.

Three things now ask that one function, each still adding its own constant to a ground
height:

| thing | was | is |
|---|---|---|
| shop window | `pad + 2.05` | `storeyGround + 2.05` |
| shop POI entity | `ClusterDesc.GroundHeightAt` (the **TERRAIN**) `+ 2.5 + 1` | `storeyGround + 2.5 + 1` |
| TALE shop door | `pad(**block centre**) + 2.15` | `storeyGround + 2.15` |

`ShopNearbyBehavior` scores in 3-D with `Distance = 16f`, so a window and an interaction
point one storey apart cost a fifth of the horizontal reach. TALE **home** doors and every
building `Position` now take `PavementHeightAt` at their own position instead of the pad at
the block centre — up to 9 m out at either end of a block.

## The default FLAT city moves once, by 0.35 m, and only the house moves

Pad = `AverageHeight`; pavement = `AverageHeight + 2.0 + 0.15`; the base was
`AverageHeight + 2.5`. **The flat city has floated every house by 0.35 m since the
L-system houses were written**, hidden wherever a shopfront quad skirted the gap by sitting
0.10 m *below* the pavement. It now stands on the pavement.

Everything else is asserted as equality rather than as a tolerance, over whole generated
cities: `HeightOf` adds exactly zero, `StoreyAt` is exactly 0, and the shopfront quad, the
shop POI and the TALE door land on the float they land on today — Vector3 addition is
commutative, so the shopfront's old `2.05f + pad` and the new `ground + 2.05f` are the same
number. `AFlatCityMovesOnlyTheHouseAndOnlyByAThirdOfAMetre`.

## The grey/white underside — diagnosed, deliberately not changed

The brief had it as "one constant UV in a texture atlas gutter". The UV is right and the
gutter is not the mechanism. `Triangulate.ToMesh` writes `Vector2.One/64f` for every cap
vertex; the house materials carry `AddInterior`; and `LIghtingFS.frag`'s `renderInterior`
short-circuits **only when the texel at `fragTexCoord` has alpha > 0.8**. At (1/64, 1/64)
it does not, so the cap runs the full interior-room raymarch — `fix`/`fiy`/`fiz` room
indices, a `frameNo`-driven window-lights seed — across a horizontal polygon. That is the
noise.

**It is not specific to the underside.** `ExtrudePoly` gives the ceiling cap the identical
constant UV, the identical plane and the identical material, and `AlphaInterpreter` builds
every L-system segment with `addFloor: true, addCeiling: true` — so **every building's ROOF
in the shipped flat city is the same construction** and has been since the houses were
written. Giving the caps a real planar projection would tile facade windows across every
roof in the game; giving them their own material means threading a second material through
`ExtrudePoly`. Both are visual, opinion-bearing changes to the default city and neither is
this one.

What this change does do is remove the sighting: the base is at or below the block floor
over the whole footprint, so the bottom cap is under the pavement everywhere except at a
single tangent point, and `ABuildingsBaseIsNeverAboveTheFloorUnderIt` /
`TheBaseIsUnderTheFloorAcrossTheWholeFootprint` are that statement.

## Mutation survivors

Eleven mutations, all caught, none survived — but two only by a **source scan**, and that
is worth writing down rather than counting as a pass:

| mutation | caught by |
|---|---|
| `BaseHeightOf` takes the block's MAX corner | 12 tests |
| `BaseHeightOf` takes the pad at the block centre | 12 |
| `HeightOf` does not compensate | 4 |
| `StoreyAt` floors instead of ceils | 4 |
| `StoreyAt` always answers the ground storey | 4 |
| `GroundAt` ignores its position | 8 |
| the shopfront ramps with the kerb instead of snapping | 4 |
| §7k's corner ramp is one width, without the mitre | 20 |
| §7k's corner ramp is the mitre without the width | 9 |
| **the house operator computes its own base again** | 3, of which only `OnlyOnePlaceDecidesWhereABuildingIsFounded` is causal |
| **the shop POI goes back to the terrain** | 3, likewise `TheShopPoiAsksTheBlock` |

The last two live in `nogameCode`, which the test assembly does not reference at all, so a
scan is the only instrument available — the same limitation §7j hit with
`_generateQuarterFloor`. Both scans assert the ABSENCE of the old expression as well as the
presence of the new one, because a second, correct copy would pass any test of the value.

## Found and NOT fixed

- **`GenerateHousesOperator._createLargeAdvertsSubGeo` is dead code**: defined, complete,
  never called from anywhere. It is the only consumer of the `height < 75f` rule.
- **Polytopes and trees still stand on the pad.** `GeneratePolytopeOperator` is
  `pad + 2.5` at the ESTATE CENTRE, which is the one place the pad is defensible - §7e
  measured the plane at the centroid to be the mean of the corner heights *identically* -
  so it is left. `GenerateTreesOperator` scatters over the block and does suffer the
  residual; both would move the flat city by another 0.35 m and neither was reported.
- **A one-storey building can carry a shop window taller than itself on a slope.** The
  window is `StoryHeight − 0.15` tall and sits at most one storey above the local pavement,
  so a building shorter than about 5.85 m of visible height can be overtopped by its own
  glass. `maxHeight` allows 3 m where `minHouseSide ≤ 2 m` or downtownness < 0.3; measured,
  p05 of building height is 6 m, so it is rare and it is not new — the same window on a 3 m
  building already reaches within 0.6 m of the roof in the flat city today.
- **The five catch blocks in `GenerateHousesOperator` were `Trace`**, i.e. silent by
  default — a swallowed building, sign or shop window with nothing in the log. Converted to
  `Error(_dc, …)` with distinct messages per site. That is a fix, listed here because it
  was found rather than sought.

---

# §7m — The quest marker rests on the road, and the citizen on the pavement (2026-08-31)

Ledger items **(b)** and the rest of **(d)**. Both are things that STAND on the city rather
than parts of it, and both were asking the TERRAIN how high the city is.

> **Re-measure before diagnosing.** The ledger's Part 1 numbers were taken on 2026-08-30,
> before §7k made the pavement level across its width and §7l put buildings on a bound.
> Every figure below is a re-measurement, and one of (d)'s three causes turned out to be
> **exactly zero** now rather than merely improved.

---

## (b) The quest marker — two causes, and neither is sufficient alone

`ToSomewhere._createTargetInstance` drew the goal cube scaled to
`(SensitiveRadius, 3, SensitiveRadius)` **centred on** `RelativePosition`, so its visible
bottom was always 1.5 m below the height the quest had chosen; and the three quest
strategies chose `Loader.GetHeightAt(pos) + ClusterNavigationHeight` — the **terrain** plus
the **vehicle hover** clearance, neither of which is a surface.

The flat city hid it by coincidence. `ClusterBaseElevationOperator` writes the ground at
`aver + 1.5f`, a constant unrelated to `CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE = 2.0`, so the
bottom landed exactly 1.0 m over the road and looked deliberate.

**Marker bottom minus the pavement of its own junction**, over every junction of the four
baselines on the shipped terrain with the conforming pass reproduced on its own 20 m grid:

| city | n | min | p05 | med | p95 | max | **below the pavement** |
|---|---|---|---|---|---|---|---|
| seed000/500 | 27 | −1.47 | −1.29 | **−0.64** | 0.11 | 0.27 | **92.6 %** |
| Yelukhdidru/800 | 64 | −4.17 | −1.78 | **−0.67** | 0.19 | 0.68 | **90.6 %** |
| seed000/1500 | 274 | −5.13 | −2.00 | **−0.64** | 0.83 | 7.43 | **90.5 %** |
| Yelukhdidru/3000 | 1379 | −9.77 | −2.03 | **−0.65** | 0.79 | 10.61 | **88.0 %** |

Note the positive tail: at the worst junction of the 3000 m city the marker floats **10.6 m
above** the pavement instead, because there the road is in a cutting the 20 m elevation grid
cannot cut and the conformed terrain stands 9.1 m over its own road.

### The three options, evaluated rather than picked

- **(A) route the strategies through `Loader.GetNavigationHeightAt`.** This is
  `ClusterDesc.GroundHeightAt + ClusterNavigationHeight`, i.e. **the same quantity again**,
  differing from what shipped only by the flat city's 1.5 m bias. It changes nothing on a
  slope and lowers every flat-city marker by 1.5 m. Rejected.
- **(C) offset `_eMeshMarker` by `+1.5 · UnitY` so the cube rests on `RelativePosition`.**
  The ledger called this the cheapest and honest option. **Measured, it does not meet the
  requirement**: it leaves the bottom at terrain + 3, which is still below the pavement at
  the worst junction of three of the four baselines — **−2.67 m** at Yelukhdidru/800 and
  seed000/1500 and **−8.27 m** at Yelukhdidru/3000. Only seed000/500, which has 27 junctions
  and almost no relief, would have been fixed by it.
- **(B) position by the marker's BOTTOM against a real surface height**, the shape §7g used
  for the ribbon. Taken — **with (C) as its mechanism**, because a cube that straddles its
  anchor forces every caller to carry a −1.5 m fudge that has nothing to do with the world.

### What the surface is, and why the answer is exact

`engine.streets.generation.CitySurface` answers the built surface at a plan position from
the **junction nearest it**, and `engine.quest.QuestMarker` owns the cube's height and the
offset that rests it on that answer — one copy of the 3 m, because the offset is half of it.

A junction is the one place in a city where "how high is the built surface here" has an
exact answer rather than a sample near one: it is one node of the stroke graph with one
height, and the deck, the junction cap, the kerb and every block corner meeting there read
that same number. `HeightAtJunction` is `JunctionCollider.SurfaceHeightOf` — the cap's own
height, already the one place that decides it — plus `QuarterSidewalkOffset`, because the
pavements of the blocks that corner there are one kerb **above** the carriageway and so are
the higher of the two.

**And the marker really is at a junction.** `engine.Placer` with
`Reference.StreetPoint` adds `sp.Pos3 with { Y = sp.LevelElevation }` to the cluster origin
and nothing else, so the nearest junction is the junction it was placed at — asserted by
**identity** (`Assert.Same`) over every junction of all four cities, never by distance, for
the reason this work stream keeps rediscovering.

The guarantee is then stated against all three surfaces a junction carries — the
carriageway, the cap, and the pavement of every block whose corner stands on that junction,
matched by `ReferenceEquals` on the `StreetPoint` — and holds at every junction of all four
baselines.

**Deliberately NOT `max(surface, terrain)`**, though that is the shape §7f used for the
hover probe. At the worst junction the conformed terrain is 9.1 m above the road, inside a
cutting the grid could not cut; taking the max would float the marker 9 m over the road the
player is driving on. The report was that the marker sinks, and the road is what it should
sit on.

### The default FLAT city moves, by 0.85 m

Anchor was `aver + 1.5 + 3 = aver + 4.5` with the bottom at `aver + 3.0`; it is now
`aver + 2.15` with the bottom on it. **Every quest marker in the shipped flat game drops by
0.85 m**, from hovering a metre over the road to resting on the pavement. That is the fourth
deliberate move of the default city in this work stream, after §7i, §7j and §7l.

Two consequences worth stating rather than discovering:

- the goal's **collision cylinder** is at `RelativePosition` too, so it drops 2.35 m. It is
  1000 m tall and centred, so it still spans everything it spanned before.
- `TrailVehicle` (the fishmonger quest) parents its marker to the CAR with
  `RelativePosition = Vector3.Zero`, and computes no height at all. The mesh offset applies
  there too, so **that marker now stands on the car instead of around it**, 1.5 m higher.
  Uniform on purpose: one rule for the marker's geometry.

---

## (d1) T-pose — naming a driver is not the same as having one

All six `EntityCreator` sites name a `BehaviorFactory` or an `EntityStrategyFactory`, and
one of them still had **no animation at all**: the niceday NPCs start in `RestStrategy`,
which attaches `NearbyBehavior`, an `ANearbyBehavior` that drives the "E to Talk" prompt and
never called `SetAnimation`. Their whole animation was `EntityCreator.InitialAnimName` — one
call, issued before `ModelCache` has necessarily attached `FromModel`, with nothing to retry
it.

So the criterion CLAUDE.md credited the missing drift test with — *"the site names one of
the three drivers"* — **would have passed on the day of the sighting**. A useful test asserts
that something SETS AN ANIMATION.

`nogame.characters.citizen.AnimationDriver` is that retry, extracted out of `IdleBehavior`
and now used by three sites: `IdleBehavior`, niceday's `NearbyBehavior`, and a new
`AnimationOnlyBehavior` for the **taxi passenger** — which has no `Body`, so `IdleBehavior`
is unusable there (its `OnAttach` takes a ref to that component and DefaultEcs would hand it
a reference into unused storage).

### The half-built character is doomed now, not merely hidden

`EntityCreator._createLogical`'s catch left a frozen character in the world and only made it
invisible, on the stated grounds that *"disposing someone else's entity from here risks a
double dispose"*. **That reason expired on 2026-08-29**, when `engine.DoomedEntitySet` made
dooming idempotent for exactly this case — two owners that cannot see each other doming the
same entity. Hiding alone left one hole: `SetVisible` resolves `TransformApi` out of the
container and takes a ref to a component, and if IT throws — which the inner catch there
proves was considered possible — the result is a visible, behaviour-less, physics-less
T-pose that stands until its fragment unloads.

---

## (d2) Below pavement level — one cause was already gone

Measured at the midpoint of every block edge, one `SidewalkOffset` in from the kerb, against
the block floor's OWN triangles read barycentrically, on the shipped terrain. Blocks that
§7k refuses a pavement inset (1 / 0 / 3 / 7 of the four cities) are excluded and named
rather than averaged away.

### 3. The satnav walker — **exactly zero, on every percentile**

| city | n | min | p05 | med | p95 | max | below |
|---|---|---|---|---|---|---|---|
| all four | 10 / 49 / 375 / 2448 | −0.00 | **0.00** | **0.00** | **0.00** | 0.00 | **0.0 %** |

The ledger had this at p05 −0.31…−0.48 m, worst −12.9 m, ~50 % below, and predicted §7k
would fix it. **It did, completely**: a sidewalk lane runs between two block corners at
exactly their two junction heights, and the pavement rim is now level across its width, so
the lane's own linear interpolation IS the pavement's ground height there. Including the
refused blocks the same measurement is min −1.21, max 0.92, 0.0–2.3 % below — that residual
is those 11 blocks and nothing else.

### 1. The loop walker — the ordinary citizen, and the worst offender

`QuarterLoopRouteGenerator` took `Quarter.GroundHeightAt`, the block's **pad**: a least
squares plane through the corner heights of a block up to 150 m across with 13 m between its
highest and lowest corner. Measured at the loop's **own waypoints**, not at edge midpoints:

| city | n | min | p05 | med | p95 | max | below |
|---|---|---|---|---|---|---|---|
| seed000/500 | 6 | −2.10 | −2.10 | −0.26 | 2.34 | 2.34 | 66.7 % |
| Yelukhdidru/800 | 29 | −6.66 | −4.58 | 0.06 | 3.62 | 5.78 | 44.8 % |
| seed000/1500 | 193 | −8.30 | −4.18 | 0.04 | 4.89 | 8.06 | 48.2 % |
| Yelukhdidru/3000 | 1447 | **−17.78** | **−6.55** | −0.04 | 6.80 | 17.02 | 51.0 % |

Worse than the ledger's −12.6 m, because the ledger sampled edge midpoints and the walker
stands at corners, where the pad's residual is largest.

It takes `BuildingFooting.PavementHeightAt` now — the §7l function, which answers from the
boundary edge nearest the point interpolated between its two corners' own junction heights:

| city | min | p05 | med | p95 | max | below |
|---|---|---|---|---|---|---|
| seed000/500 | −0.02 | −0.02 | 0.00 | 0.06 | 0.06 | 16.7 % |
| Yelukhdidru/800 | −0.59 | −0.04 | 0.00 | 0.08 | 0.09 | 10.3 % |
| seed000/1500 | −0.59 | −0.14 | 0.00 | 0.16 | 4.17 | 25.9 % |
| Yelukhdidru/3000 | −1.43 | −0.23 | 0.00 | 0.23 | 2.90 | 31.5 % |

**The obvious alternative was measured and is worse.** Taking the corner's own junction
height — literally the number the satnav walker uses at the same corner — gives p05 −0.09 /
−0.19 / −0.24 / −0.28 and puts the walker below the floor at **33–55 %** of corners against
10–32 %. The waypoint is 1.5 m in from the corner, i.e. inside §7k's **corner ramp**, where
the pavement runs back to the kerb; the nearest-edge interpolation follows that and the
corner's own value does not. What remains — the ±0.23 m and the ~2 m tails — IS the ramp,
and it is the honest residual of a per-corner waypoint on a ramped surface.

The two systems are compared against each other at the corner itself, where they are the
same quantity, and agree to 1e-3 m over every corner of every baseline. That disagreement is
the shape of every defect this pair has had: §7g found them offsetting to opposite SIDES of
the same kerb, and the height was the same story one layer down.

### 2. The terrain walker — and the comment that was wrong

`StreetRouteBuilder._walkingHeightAt` carried *"the terrain has to answer here, since there
is no road node to ask."* **There is one, and the route has already found it.**
`TryCreateCursor` snaps each end of the route to its nearest lane, and that lane's two
junctions carry exact street heights.

The terrain, at the point a walker stands:

| city | n | min | p05 | med | p95 | max | below |
|---|---|---|---|---|---|---|---|
| seed000/500 | 10 | −0.68 | −0.68 | 0.52 | 2.52 | 2.52 | 40.0 % |
| Yelukhdidru/800 | 49 | −2.33 | −1.53 | −0.08 | 1.47 | 2.25 | 51.0 % |
| seed000/1500 | 375 | −5.46 | −1.85 | 0.05 | 2.27 | 6.34 | 48.5 % |
| Yelukhdidru/3000 | 2448 | −5.34 | −1.59 | 0.04 | 1.69 | 4.49 | 48.2 % |

This is not the conforming pass failing; it is the conforming pass working as designed. It
grades the ground toward the streets with a 60 m smoothstep on a 20 m grid, and the median
block is 28 m deep to its kerb, so in the middle of a block the weight is only ≈0.53.

`builtin.modules.satnav.PedestrianRoute.EndWaypointFor` takes the lane's height at the
position's own projection onto it, clamped, and keeps the caller's plan position — only the
HEIGHT comes from the lane. Both route ends use it, each from **its own** cursor: the
destination used to be given the START pod's terrain sample at the destination's
coordinates, two ends of one hill answered by one height field. Nothing on a route is a
terrain sample any more.

`GoToStrategyPart`'s straight-line fallback has no lanes at all, so it asks the pod's own
block through `BuildingFooting.TryPavementHeightAt` and falls back to the terrain where the
position is not on it — which a travel destination often is not, and answering from the
wrong block would be worse than answering from the terrain.

---

## The default FLAT city

**One thing moves: the quest marker, by 0.85 m** (above). Everything else is asserted as
equality over whole generated cities:

- the **loop route** is unchanged float for float, position and height. On a flat block every
  corner is at the average, so `BuildingFooting`'s edge interpolation is `h + t·0`, which is
  `h` exactly, and the two constants are added in the order they were added before. The
  forward direction is now taken in plan rather than at a common height — the same vector,
  since both ends always had the same Y.
- **route ends** do not move: `ClusterDesc.GroundHeightAt` short-circuits to `AverageHeight`
  inside a flat cluster, and a flat lane's two junctions are at that same average.

---

## Mutation survivors

Sixteen mutations. **Three survived and each named something real.**

| mutation | outcome |
|---|---|
| the marker straddles its anchor again | caught, 9 tests |
| one quest keeps `GetHeightAt + ClusterNavigationHeight` | caught, 1 |
| the surface is the carriageway, not the pavement | caught, 8 |
| the nearest junction is always the first one | caught, 8 |
| the loop walker goes back to the pad | caught, 4 |
| `EndWaypointFor` ignores its projection | caught, 8 |
| `EndWaypointFor` moves the plan position to the lane | caught, 4 |
| the route destination goes back to the terrain | caught, 2 |
| `ToSomewhere` goes back to the literal cube | caught, 1 |
| the taxi passenger loses its `BehaviorFactory` | caught, 1 |
| `AddDoomedEntity` becomes another `SetVisible` | caught, 1 |
| the niceday driver is deleted | caught, 3 |
| **`GoToStrategyPart`'s pavement branch is `if (false)`** | **SURVIVED everything** |
| **`IdleBehavior.Behave` is gutted** | **SURVIVED, twice, for two different reasons** |
| **`Loader.GetCitySurfaceHeightAt` goes back to the terrain** | **SURVIVED everything** |

1. **`if (false)` round the pavement branch.** A scan can see that `GoToStrategyPart` NAMES
   `BuildingFooting.PavementHeightAt` and cannot see whether the branch that names it is
   ever taken, and the file is in `nogameCode`, which the test assembly does not reference.
   The decision moved into `BuildingFooting.TryPavementHeightAt`, where it is driven over
   the blocks of real cities. Same lesson as §7b's `JunctionCollider.SurfaceHeightOf`: put
   the arithmetic where a test can reach it and scan only the one line that reaches for it.
2. **Gutting `IdleBehavior`.** The reachability test says a creation site can reach A driver,
   and `WalkBehavior` is in the same closure — but every T-pose sighting so far has been a
   STATIONARY character, sitting in a behaviour with nothing to re-issue its clip. So the
   stronger statement is asserted too, per behaviour rather than per site. It then survived a
   **second** time because the scan tested for the string `AnimationDriver`, which the field
   declaration still contained: an animation driver is a CALL, not a mention.
3. **`Loader.GetCitySurfaceHeightAt` reverting to `cluster.GroundHeightAt`.** `Loader` needs
   the `I` container and the elevation cache and is exercised by nothing. Brace-scanned.

A fourth, found while building the drift test rather than by mutating: **identifiers in
COMMENTS leak a source-scan closure.** niceday's `EntityStrategy` carries a stale class
comment reading *"uses two sub-strategies: WalkStrategy and RecoverStrategy"*, neither of
which it has, and following it walks straight into the citizen strategy tree — so with
comments left in, deleting the niceday animation driver outright still passed, on somebody
else's driver, three hops away, named only in prose.

---

## An existing gate was superseded, not re-baselined

`NavJunctionHeightTests.TheRouteBuilderTakesEveryWaypointFromItsOwnLane` asserted
`_walkingHeightAt(startPod, fromPos)` and `_walkingHeightAt(startPod, toPos)` — the two
terrain samples — under the claim that the route ends *"are the only two that do"* and have
to. That claim is what this section refutes. The gate now asserts the same property (each
end takes its OWN position and its own end of the route) on the stronger expression, and its
comment records what it used to say. No network fingerprint and no `street-geometry.json`
baseline moved.

---

## Found and NOT fixed

- ⚠️ **Roughly half the loop walker's waypoints are OUTSIDE their own block.** Measured:
  5/10, 28/49, 193/375 and 1430/2448 corner waypoints are inside the block ring — **50 to
  58 %**. The offset is 1.5 m perpendicular to the LEAVING edge, taken at the corner, so at
  an interior angle over 90° it lands past the arriving edge; the median block corner is
  90.1–93.5°, which is exactly the coin toss those numbers show.
  `PedestrianKerbSideTests` already names this effect for the satnav walker and measures
  along the lane instead of at its end because of it. It is a plan-position defect, not a
  height one, and moving the waypoint onto the corner's bisector would move **every
  citizen's walk in the shipped flat city** — so it is left, stated, and ranked. The height
  consequence is mild: outside the kerb the walker is 0.15 m above the road rather than below
  anything.
- **`GoToStrategyPart` can only ask the pod's own block.** A travel destination on another
  block still falls back to the terrain. Fixing it needs a positional block lookup;
  `QuarterStore.GuessQuarter` exists and is documented in its own source as a "fast wrong
  implementation".
- **The marker's guarantee is at its own position, not over its own footprint.** A taxi goal
  has `SensitiveRadius = 10`, so the cube spans ±5 m around the junction and the road may
  rise up to the grade policy's 14 % over that — 0.7 m at the far corner. Stating it over
  the footprint would need the cap polygon, and no sighting has been about a marker corner.
- **`CitySurface` degrades away from a junction**, by design and by name: everything that
  asks it today is placed at one. A caller standing somewhere else should get a query built
  for it rather than let this quietly become a road lookup it is not.
