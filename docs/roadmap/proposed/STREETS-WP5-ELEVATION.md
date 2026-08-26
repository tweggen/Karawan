# WP-5 — Downstream elevation for multilayer streets

**Follows:** `STREETS-GENERATOR-REWORK{,-PLAN}.md` (WP-0 … WP-4, done)
**Status:** Proposed. Foundation landed; the rendering work is not started.

The generator can build multilayer networks. Nothing downstream knows what a level
means, so a bridge currently generates and then **renders flat** — which is why bridge
rules ship off. This work package is what makes a deck visible, walkable and drivable.

---

## 1. The finding that shapes this work

The rework plan said WP-5 would turn `StreetPoint.Pos3`'s hardcoded `Y = 0` into
`Y = ElevationOf(Level)`. **That would have been a bug**, and it is worth stating
plainly because it is the natural thing to reach for.

`Pos3` is not a rendering coordinate. It is the key both octrees in `StrokeStore` are
built on, and the coordinate every neighbourhood query is expressed in
(`StrokeStore.cs` lines 68, 70, 173, 243, 313, 414, 428, 624). Folding deck height
into it would mean:

- **`minPointToCandPointDistance` silently becomes a 3D distance.** "30 m apart" would
  start counting the 8 m of deck height, so junctions stacked on different levels would
  read as 30 m apart when they are directly on top of each other in plan.
- **Cross-level neighbours would drop out of range on their own**, in a way that
  overlaps confusingly with the explicit level filtering added in WP-4a. Two mechanisms
  doing the same job by different rules is how the original generator got into trouble.
- **The duplicate-point guard in `AddPoint` would stop firing.** It looks for points
  within `1e-8` and exists to catch two junctions at the same spot; two decks stacked
  exactly would no longer collide.

So the rule for this work package:

> **`Pos3` is INDEX space and stays planar. Elevation is WORLD space, and only geometry,
> navigation and rendering touch it.**

**Landed already** (with the ID-wraparound fix, since it is small and unblocks the
rest):

- `engine/streets/StreetLevels.cs` — `DeckHeight = 8f`, `ElevationOf(sbyte)`. Height
  *above the ground surface*; terrain height stays the caller's business.
- `StreetPoint.LevelElevation`, plus a comment on `Pos3` recording that it is an index
  coordinate so the next reader does not "fix" it.
- `QuarterGenerator` skips junctions above level 0. Quarters, estates and buildings are
  ground-level things; a face traced through a raised deck is not a city block but the
  hole under a bridge, and the ground face beneath it is traced separately. Currently a
  no-op, since every junction is on level 0 until a multilayer ruleset is enabled.

---

## 2. What remains

### WP-5a-gate — The geometry gate — **DONE**

The generator has been gated since WP-0; the renderer had nothing. Every elevation
change so far leaned on `StreetLevels.ElevationOf(0)` being exactly zero to argue the
ground path could not have moved. WP-5a-ii changes vertex emission itself, so that
argument stops working and this had to come first.

`StreetGeometryHarness` runs the emission methods with no engine. What made that
possible: both took a `world.Fragment` only to ask whether the thing being built
belonged to that fragment — a caller's decision, now hoisted out. The geometry then
depends on nothing but a cluster, a stroke store and a material.

`StreetGeometryFingerprint` hashes vertices, normals and UVs in **emission order**,
unlike the network fingerprint which sorts: a mesh is an ordered thing, since triangles
are built from consecutive vertices.

**Mutation-tested, and it found a hole.** Shifting street height by 1 cm failed 8 of 13;
perturbing a normal and a UV in `_streetTriangle` each failed 4. But perturbing a normal
inside the `damax > dbmin` branch — the "a and b ends overlapping" case for very short
strokes — **passed**, because none of the four seeds reached it. Instrumenting that
branch and scanning found `seed008@500`, now in the seed set; with it the same mutation
fails. A degenerate path like that is exactly what a vertex-emission change breaks.

**Fixed along the way, both pre-existing:** the caller's `nGeneratedStreets` /
`nIgnoredStrokes` counters were inverted (trace-only), and `I.Register` collisions
between test classes now go through a shared idempotent `TestContainer` — the geometry
harness and the Assimp fixture both need `ObjectRegistry<Material>`, and whichever ran
second used to fail with "Already registered" instead of its own result.

Known limit: the gate covers the branches these five seeds reach. That is now a
measurable property rather than an assumption, and the instrumentation recipe above is
how to extend it.

### WP-5a — Street geometry carries elevation — **PARTLY DONE**

Done: junction caps and stroke surfaces are raised by their level. This turned out to
be two lines rather than a rewrite, because **streets are built at one flat height per
cluster rather than following the terrain** — every vertex derives from a single
`v3Cluster` with that height baked in, so raising it raises the whole surface.
`GenerateClusterStreetsOperator` lines 96 and 280.

The fragment's physics floor is deliberately left on the ground: it is one plane for
the whole fragment, and a raised deck needs its own collision surface (WP-5c).

**WP-5a-ii — ramps — DONE.** The surface is built flat at the A end's height, exactly
as before, and then tilted onto its slope by `_shearOntoSlope` over the vertices that
stroke emitted. Done as a pass rather than at each of the fifteen emission sites, which
is possible because **the UV projector's two axes are both planar**: a vertex's Y
cannot affect its UV, so moving Y afterwards disturbs nothing else. Normals in the
range are replaced with the slope normal, or a climbing surface lights as though it
were flat.

`hA == hB` for every flat stroke, so the pass returns immediately and the ground path
is untouched — which the geometry gate proves rather than argues.

Mutation-tested: removing the shear fails the 4 ramp tests **while all 16 ground
geometry tests keep passing**, which is what shows the change is ramp-only; inverting
the slope normal fails the 2 lean-direction tests.

One test assumption corrected on the way: a straight ramp is emitted as a **single
quad**, so it has no vertices part way up and "is there a vertex at mid height" cannot
distinguish a slope from a step. Linearity against each vertex's own distance along
the ramp is what does.

Still deferred: a deck has no underside, edges or supports — it is a floating slab
seen from below. Visible progress, and not wrong, only unfinished.

Original scoping follows.

#### Original scoping

`GenerateClusterStreetsOperator` builds the road surface from strokes and from
`StreetPoint.GetSectionArray()`. Every vertex it emits needs
`terrainHeight + LevelElevation`, and a deck needs an underside and edges — a ground
street is a surface laid on terrain, a deck is a slab with a bottom.

- Ramps interpolate between their two endpoints' elevations along their length.
- A deck's supports are the obvious follow-on and can be deferred; a floating slab is
  visible progress and is not wrong, only unfinished.
- `GetSectionArray()` computes junction corners in plan. It needs no change, but its
  consumers must apply the junction's elevation.

**Gate:** a bridge-free cluster renders byte-identically (compare emitted vertex
buffers for a fixed seed); a two-level cluster renders a deck at +8 m.

### WP-5b — Navigation — **DONE**

One line, because two things fall out on their own. `GenerateNavMapOperator` builds
lanes with `Vector3.Distance` and splits long ones with `Vector3.Lerp`, so once a
junction's `Position` carries its deck height, **a ramp's cost is automatically its
sloped length** — routing cannot get a discount for climbing — and a long ramp's
intermediate junctions land part way up it.

Sidewalk junctions stay at ground height on purpose: they come from quarter
delimiters, and quarters are traced on the ground only, so a deck has no pavement
until something generates one.

Not directly tested: the operator needs a booted engine. The arithmetic it performs is
pinned instead (`StreetLevelsTests`), and the TALE suite — 200 tests over real
pathfinding — is what demonstrates the ground-only path is unchanged.

#### Original scoping

`GenerateNavMapOperator` turns junctions into `NavJunction` and strokes into
`NavLane`, and hardcodes `new Vector3(sp.Pos.X, 0f, sp.Pos.Y)` — the same planar
assumption in a place where it *is* wrong, because navigation is world space.

- `NavJunction.Position` gains the elevation.
- Lane length on a ramp is the 3D length, or route costs will prefer ramps.
- Pathfinding is otherwise unaffected: ramps are ordinary edges, so a route over a
  bridge falls out of the existing search with no special case.

**Gate:** a ground-only navmap is unchanged; a route across a two-level cluster uses
ramps and its length matches the 3D geometry.

### WP-5c — Physics and placement — **PLACEMENT DONE, COLLISION DEFERRED**

Smaller than scoped, because most of the `Pos3` reads listed below turned out to be
**already correct**, and "fixing" them would have introduced bugs:

| site | verdict |
|---|---|
| `SpawnOperator.cs:217` | reads `.X`/`.Z` only — planar by construction |
| `NarrationBindings.cs:148` | `Pos3 with { Y = 0f }` for a plan-distance comparison — deliberately planar |
| `NarrationBindings.cs:151,158`, `ClusterDesc.cs:224` | feed `Fragment.PosToIndex3` — a fragment *grid index*; elevation there would be meaningless or wrong |
| `TaxiNpcSpawnerModule.cs:63,202` | source is a quarter delimiter, and quarters are ground-only |
| `Placer.cs:293` | **fixed** — a spawn reference position, genuinely world space |
| `SpatialModel.cs:263` | **fixed** — but on `streetHeight`, since `pos.Y` is assigned outright a line later rather than accumulated |

The lesson generalises: `Pos3` appears in three different roles — plan geometry, grid
index, and world position — and only the third wants elevation. Adding it everywhere
`Pos3` occurs would have broken fragment lookup.

**Deck collision is deliberately deferred**, and its ordering has changed: it needs
the deck *mesh* to exist, and a deck with no ramps (WP-5a-ii) is unreachable. Collision
for a surface nothing can drive onto is not useful, so this now follows WP-5a-ii rather
than preceding it.

**Noticed in passing, not fixed:** `Placer.cs:304` computes
`v3OnTerrain = ...GetWalkingPosAt(v3ReferenceAccu)` and then never uses it —
`pod.Position` is assigned `v3ReferenceAccu`. Either a wasted terrain query or a
missing assignment; changing it would move every placement, so it wants its own
decision rather than being folded into this work.

#### Original scoping

`Placer`, `SpawnOperator`, `TaxiNpcSpawnerModule` and `NarrationBindings` all read
`Pos3` to put something in the world. Each needs the elevation added. The collision
surface for a deck has to exist, or vehicles fall through.

**Gate:** an NPC spawned at a level-1 junction stands on the deck, not under it.

### WP-5d — Turn the rules on

Only once a–c are real: add the overpass rule to `models/nogame.streets.json`, wired to
the seam WP-4b left (`OverpassBuilder` + `NetworkBuilder.CommitChain`). This changes
generated output, so it **invalidates every `ClusterStorage`-cached cluster** — a
world-content version bump (`ClusterStorage.DbVersion`), to be coordinated
deliberately.

**Gate:** V2 fingerprints recorded for a multilayer ruleset; V1 ground-only baselines
still pass with the rule off.

---

## 3. Sequencing and risk

a → b → c → d, each behind its own gate. a and b are independent of each other in
principle but both feed c.

The determinism gate from WP-0 keeps working throughout: **as long as bridge rules stay
off, every one of these steps must leave the V1 fingerprints untouched.** Any step that
moves them has changed ground-level behaviour and is wrong. That property is what makes
this safe to do incrementally, and it is why WP-5d comes last.

The main risk is scope creep in a: "a deck with an underside and supports" can absorb
arbitrary effort. Recommend shipping a floating slab first and treating supports as a
separate, purely visual follow-up.
