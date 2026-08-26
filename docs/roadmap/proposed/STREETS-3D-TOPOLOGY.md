# Three-dimensional street topology

**Status:** Design sketch, nothing implemented.
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

## 6. What I would prototype first

The corridor-conforming pass (§2c), because it is the only part whose fit with the
existing operator pipeline is genuinely uncertain. Sampling and relaxation are
self-contained and testable against the street graph alone; if the conforming pass
turns out not to fit, it changes the shape of the whole phase, and it is better to know
that before the rest is built on top of it.
