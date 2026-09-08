# The three-dimensional city — open points

**Status:** open ledger. This is the file to read first when picking up the
terrain-following city work.
**Companion:** [`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) is the design and
history document — every fix is written up there as §7a … §7y, with the measurements that
drove it. This file is only *what is still wrong* and *what to do about it*.

**Last updated:** 2026-09-08 (**the owner's ten square metre floor**: a piece of buildable land
under `engine.world.MetaGen.MinBuildingArea` = 10 m² gets **no building**, which closes §7x.8's
matchboxes — buildings under a square metre **4 / 53 → 0**, smallest building anywhere
0.030 / 0.005 → **11.92 / 10.05 m²**, at a cost of **16 and 194** buildings and **2 and 13**
shopfronts of half a million. ⚠️ **The brief's expectation was backwards**: the floor binds
**more** downtown, not less — flag off it fires **only** downtown — so it ships flat everywhere
and the table in §7y.1 is why. ⚠️ **And a 1450 m² building APPEARS** on one flag-on block,
because refusing before the 30 % design draw hands the good piece the draw its matchbox
neighbour used to take. No baseline moved, no `DbVersion` bump, the player start does not move.
`STREETS-3D-TOPOLOGY.md` §7y. Earlier the same day, **measurement only**: the last open number
under item (o) — the
**522 flag-on blocks with no buildable land** against 9 flag off — turns out to be **correct
behaviour**. Every one of them is a block that is **entirely pavement**, the equivalence is
exact in both directions over all 78 500 blocks, and **every one is emptied by the pavement
inset alone**: no structure and no spur subtraction empties a block anywhere. **Ledger item (o)
did not make them** — `6c228797^`, restored into a worktree, produced exactly 9 and exactly 522
with byte-identical outlines and no building on any of them. **9 → 522 is the heavy-first
city's roads** (median street 9.85 → 18.82 m), not its structures, and the ten structure-free
flag-on cities are the control. ⚠️ **What IS wrong is on the other side of the same cliff**:
4 / 53 blocks carry a building under one square metre, smallest 0.005 m² — an owner decision,
costed in §7x.9. `STREETS-3D-TOPOLOGY.md` §7x.

2026-09-07: item **(o)** is **CLOSED**, and **PART 1b IS CLEAR**. **WP-O3**:
a building is founded on its own piece of ground rather than on the block's lowest corner —
and ⚠️ **the per-piece bound the work package was briefed to build is not a bound at all**,
because the block floor's INTERIOR is one tessellation of the ring inside the pavement rim and
a triangle may run clean across the block. The cheap rule floats **5402 / 4842** footprint
corners over the shipped world and up to **9.77 m** on a pinned baseline, which is ledger item
(a)'s own sighting put back. So `BuildingFooting` reads the floor's own cap now
(`generation.BlockFloor`, through the same `ExtrudePoly.BuildCap` the floor is drawn with) and
takes its EXACT minimum over the footprint: **0 of 342 232 footprint corners in the air** over
seventy cities and both flags, over-sink p50 **0.445 / 0.256 m** and worst **15.2 / 33.1 m**
removed, **0.000 m** after. Flat city unchanged, no baseline moved, no `DbVersion` bump. See
Part 1b and `STREETS-3D-TOPOLOGY.md` §7w. Earlier the same day: **WP-O2**, the dead-end spur
out of the estate — buildings over a `Street` **3247 → 13** and **2986 → 0**, start city 5 and
4 → **0 and 0** (§7v); **WP-O1**, the block trace over the 2-core (§7u); and the notch-or-split
measurement, §7t.10 — **mostly-notch, 99.6 % / 96.6 %**, **no threshold constant**. 2026-09-06:
item (o) reported from play and its cause established, §7t; and WP-B6 — grade separation is ON
and Phase B is complete, §15).

---

# ▶▶ RESUME HERE

**Part 4 has happened.** `1c54a9e4` set

```jsonc
// models/nogame.globalSettings.json
"joyce.DisableClusterFlattening": "true"
```

so **the terrain-following city IS the shipped city**. The flat path still exists behind the
flag and its gates still matter — most of them really assert *"the height seam is the only
thing that decides height"*, which stays true and stays worth testing — but it is no longer
the baseline being protected. Say what each city does under any change you make.

Through the whole stream up to that flip the flat city was kept bit for bit stable with six
deliberate exceptions: §7i moved `Placer` reference junctions, §7j un-culled faces without
moving a vertex, §7l dropped every house by 0.35 m onto the pavement it had always floated
above, §7m dropped every quest marker by 0.85 m onto the road, §7n brought one end of every
intercity line down onto its own track and moved the starting coins under the player, and
§7o did **not** move it at all — it is the first item here that is a pure consequence of the
terrain-following city, exactly 0.000 m in the flat one at every percentile. Each move was
measured and stated before it landed rather than discovered afterwards.

**What Phase A finished:** the road surface and everything that moves on it. Streets
follow relaxed terrain gradients, the ground conforms to the roads, blocks are tilted
pads, the kerb meets the carriageway exactly, cars hover on a raycast probe, NPCs walk at
lane heights, the satnav guideline lies on the road, and the pavements face upwards.

**What Phase A never touched: everything that STANDS on that surface.** Buildings, shops,
quest markers, trams and the initial coin placement were all written against a flat city and
none of them had been revisited. That was Part 1 below, and as of 2026-08-31 all of it is
done bar the intercity line's own shape, which is a design decision rather than a defect.

**Cleared on 2026-08-31:** (c) the pavement cross-fall, (g) the pedestrian offset, (h) the
unwritten elevation row, **(a) houses floating and sinking**, **(b) the quest marker**,
**(d) the T-posed NPCs below pavement level**, **(e) the intercity tram** and **(f) the
starting coins**. Nearly all of them had been written up wrongly here, and only measurement
caught it - see the entries, which are kept rather than deleted because what they got wrong
is the useful part.

**Reported since the flip (2026-09-02), and fixed:** the kerb did not rest on the
carriageway. See **(j)** below and §7o. And **(o)**, the street trunk running into a
building — its cause closed by WP-O1 and its symptom by WP-O2, both on 2026-09-07, with
WP-O3 (`BuildingFooting` per estate rather than per block) still to come.

**Cleared 2026-09-03:** **(k)** — half of the ordinary citizen's walk was in the road. It was
the last plan-level defect this page carried as *found and not fixed*, it was present and
identical in the flat city, and **the ledger had the angle condition backwards**: it is the
ACUTE corner that fails, not the obtuse one. See **(k)** below and §7p.

**Reported and cleared 2026-09-05:** **(m)** — the satnav guideline cut across the road's
flat-ramp-flat profile instead of following it, below the road at half of all positions.
**The owner's own diagnosis was right, the first time on this page that a first diagnosis
has been**, and there is no navmesh in the shipped game at all — the three that exist are
`#if false`. Exactly 0.000 m in the flat city. See **(m)** below and §7r.

**Cleared the same day:** **(n)** — §7r's own leftover, *the road mesh does not represent its
own surface*. ⚠️ **The obvious reading of it was wrong twice.** It is not "rows straddle a
kink" — **0 of 4 608 row spans contain a section point at all**, because the rows run
between the two innermost section points by construction — and it is not row length on its
own either: a carriageway is a hyperbolic paraboloid ruled between two kerb chords of
different slope, so a row's error is its length **times** the two sides' slope difference,
which is exactly zero in a flat city and on every straight stroke. The worse half was the one
nobody had looked at, the **end wedge**, and it turned out to need no geometry at all — three
corners admit exactly one plane, so the model was simply wrong there. ⚠️ **§7r's own claim
that "the residual is the ROAD's tessellation, not the ribbon's" was also wrong**: with the
road held to 0.02 m the guideline was still 0.85 m off it, all of it its own quads.
**Nothing in `street-geometry.json` moved.** See **(n)** below and §7s.

**Also cleared 2026-09-03:** **(l)** — a junction corner was not on either of the streets that
meet there. Present and identical in the flat city, and **the diagnosis this page and §7o
carried was wrong twice over**: the branch everyone blamed fires at 180°, not at a hairpin,
and it is *right* about that case; the damage was `geom.Line.IntersectInfinite` losing every
significant digit in absolute world coordinates. **`street-geometry.json` moved for all five
cities and is the first geometry baseline this work stream has rewritten.** See **(l)** below
and §7q.

**Cleared 2026-09-06, and it is the biggest single move this page has recorded:** the
**gradient relaxation never converged**. `GradeRelaxer` spent all 32 of its sweeps on every
generated city and `RelaxedStreetHeight` threw away the return value that said so, so since
the flip every city in the game was standing on an unconverged relaxation — **8 to 743
strokes per city steeper than their own weight permits, the worst at 31.2 % against a policy
maximum of 14 %**. ⚠️ **It was not oscillating and nothing was in conflict**: it decreased
monotonically on every sweep of every seed and simply needed 82 to 1106 of them. ⚠️ **And the
exit test was measuring the wrong quantity** — the size of the last correction rather than
whether any road was still too steep — which is why "converged" and "buildable" had come
apart. The pass is a successive projection now (11–80 sweeps, budget 256, and 3.3× faster
than the sweep it replaces), exhausting the budget is a `Warning` naming the city, and
**every junction of every terrain city moved: median 0.29–1.21 m, p95 1.3–7.2 m, worst
16.5 m, with mean drift within 6 cm of zero**. The flat city did not move at all. Full
write-up in **Phase B §12**; `STREETS-3D-TOPOLOGY.md` §7a's damping bullet is superseded
there.

**Cleared 2026-09-06, the last work package before the flag can be turned on:**
**city blocks are traced over a network that is no longer planar.** A deck and the road
under it cross where they do not meet, and the block trace followed the deck: on the
flag-on cities that was 118 block edges lying on `Ramp`/`Bridge`/`Tunnel` strokes, 78 block
corners standing on junctions at `Level = 1`, and ten self-crossing block outlines in
`Yelukhdidru@3000` alone. A structure is out of the block graph now and every one of
those counts is zero. ⚠️ **Three things this ledger and the plan had wrong.** *"The blocks merge, so there
are fewer of them"* — **the block count goes UP** on four of seven flat cities, because
tracing through a ramp used to produce faces that were then discarded whole and silently
for `hasNullSection`, and those come back. *"No block contains a junction in its interior"*
— **not true of the shipped city and never was**: `Yelukhdidru@3000` has four such
junctions today, all on dead-end spurs, so the property that can actually be asserted is
about the block graph's 2-core. And the corner a block turns at **is not in the section
map** at a ramp's foot, because the map pairs arms that are adjacent in the junction CAP
and a ramp's carriageway is part of that cap — skipping structures without noticing would
have thrown away a block at every foot in the city. Nothing moved, with the flag off **or
on**. Full write-up in **Phase B §14**.

**⚠️ THE FLAG IS ON, AND THE CITY THE GAME BUILDS IS A DIFFERENT CITY (2026-09-06,
Phase B §15).** `joyce.EnableGradeSeparation` defaults to **true** and
`ClusterStorage.DbVersion` is **1040**, which deletes every cached world. The shipped world
gets **737 structures — 643 bridges and 94 tunnels — in 60 of its 70 cities**, 8 to 23 per
large city, every city still in one component. It is **not the old city with bridges added**:
the heavy-first queue builds a city of arterials with no stroke at the ruleset's minimum
weight in it (§13.2), so every block, building, shop, TALE location and nav lane moves. An
**existing save game silently resolves the same junction id to a different junction** — 429
of 1379 ids simply gone on `Yelukhdidru@3000`, the median survivor 1430 m away — because
`StreetPointConverter` looks them up with `FirstOrDefault`; invalidating saves means bumping
`DBStorage.DbVersion`, which deletes them, and that is the owner's call. **A deck is drawn as
a road in the air with nothing under it and no slip roads** — the intended floating slab,
Phase C's subject. Full write-up in **Phase B §15**.

**Part 1 IS NOW CLEAR, with one deliberate remainder: what an intercity line IS.** The
intercity tram rides its own track; the track's own shape - graded embankment, viaduct, or
a deliberately elevated line - is a design decision the owner has not made, and the three
options are written out under (e) below and in §7n. Nothing else in Part 1 is outstanding,
so the next thing on this page is Part 4.

---

## How this work has actually gone — read this before starting

Three habits earned their keep every single round, and abandoning any of them cost a day:

1. **Measure before diagnosing, and measure on real generated cities.** The obvious
   diagnosis was wrong in roughly half of these rounds, including twice where the
   *plausible* cause was real, present, and still not what the player was seeing (§7e,
   §7j). `tests/JoyceCode.Tests/engine/streets/StreetHarness.cs` builds real cities; the
   shipped diamond-square terrain is reachable from a test. Use them.
2. **Mutation-test every gate you add.** Every round produced at least one survivor, and
   it was always the mutation that mattered most — a test fixture that could not
   distinguish the bug, an allow-list that is per *file*, a hard-coded constant that no
   amount of real data disproves.
3. **A `Trace` in a `catch` is a silent failure.** `Trace` is filtered off by default;
   `Warning`/`Error` never are. §7j found half a city's pavements missing with a complete
   mesh, no exception and nothing in the log.

A fourth, specific to this geometry: **metric separation does not work here.** Two arms of
a junction can be near-collinear and a neighbouring junction can be closer than a corner's
own. Assert on **identity** (`Assert.Same`, section-point membership), and compare
distances only as medians.

---

# Part 1 — Reported from play, not yet fixed

Six reports, all from one session of play on 2026-08-30, plus three defects the
investigation turned up on the way. Every number below is measured against **real
generated cities on the real shipped terrain** (`nogame.terrain.GroundOperator`'s
diamond-square, seed `"mydear"`, then `GradeRelaxer` with the shipped `GradePolicy`, then
`StreetHeightField` + `ClusterConformElevationOperator.Blend` on the real 20 m grid).
Baselines: `seed000`/1500, `Yelukhdidru`/3000, `seed000`/800, `Yelukhdidru`/1500 — **659
blocks, 3547 boundary edges**. Terrain baseline: gradient over one 20 m cell median
**14.9 %**, p95 47 %; relief inside a 400 m window median **67.6 m**.

**Ranked.** (c) first because it is the largest surface defect and fixing it also fixes
one of (d)'s three causes. (a) second because it is the largest *visible* one.

**As of 2026-09-03 all of them are done**, bar the design decision under (e). Seven of them
moved the default flat city, each measured and stated before it landed: §7i's `Placer`
reference junctions, §7j's un-culled faces, §7l's 0.35 m house drop, §7m's 0.85 m quest
marker, §7n's intercity tram ends and starting coins, §7p's citizen walk, and §7q's junction
corners — the last of which is also the first to move a `street-geometry.json` baseline.

---

## ✅ (c) The pavement is steeper sideways than lengthwise — FIXED 2026-08-31

> **The recommendation below (Option 2) does not work, and was measured before being
> discarded.** It is left in place because the reason is the whole content of the fix.
>
> Option 2 is one inset vertex per corner, at the mitre, taking that corner's height. A
> mitre sits one width from BOTH edge lines, which puts it `w·cot(θ/2)` along one edge from
> the corner and the same distance back along the other — so the two rim cells it serves
> want two different heights for it. Given the corner's height, each cell keeps a cross-fall
> of `s·cot(θ/2)`, and **at the median block corner of 90° that is `s` itself — the
> along-edge slope, i.e. no improvement whatsoever**; at 40° it is nearly three times worse
> than today. Built and measured: over the real cities it moved the median cross-fall from
> **7.2 % to 6.7 %**. Given either cell's height instead, the surface cracks open by 0.4 m
> on a 2 m pavement and 1.3 m on a 6 m one.
>
> **What actually decides it:** a rim quad has no cross-gradient exactly when every one of
> its vertices carries the height the outer edge has *at that vertex's own projection onto
> it* — the four heights then lie on the plane `h = h₀ + s·x`. Nothing else about the quad's
> shape matters. The only thing forcing a compromise is one vertex serving two edges, **so
> the edges do not share one**: each edge owns its own pair of inset points, and neighbours
> meet only at the outer corner, where both name the corner's own height trivially.
> Measured: **cross-fall 0.0 % at every percentile on all 2823 edges**, with 438/445 and
> 79/82 blocks carrying a pavement. The price is that the pavement ramps back to the kerb at
> each corner; that ramp must clear the corner's mitre and then add a width, because at
> exactly one width the two edges' insets land on top of each other at a 90° corner — which
> rejected 435 of 445 blocks before it was measured.
>
> Also note the numbers below were measured with a **3 m** step, which exceeds the pavement
> width on most blocks (1–6 m) and so partly measures the block interior. Measured within a
> pavement's own width the fan's cross-fall is 7.5 % median / 16 % p95 / 63 % worst, not
> 11 % / 33 % / 178 %.
>
> `engine.streets.generation.SidewalkRing`, `builtin.tools.CapInsetEdge`,
> `ExtrudePoly.CapInsetEdges`, `Quarter.SidewalkWidth`;
> `tests/JoyceCode.Tests/engine/streets/PavementCrossFallTests.cs`. Flat city unchanged
> vertex for vertex and index for index (the inset is refused on `IsFlat`).
>
> **Named in Option 2's own follow-up and now closed:** the block INTERIOR carries all of
> the warp, and buildings stood on the *pad*, a third surface again. That was (a), fixed
> the same day - see below.

### The original write-up

> *"sidewalks shall be up/downwards only in the direction of walking, not in the direction
> to the street. I understand that we might have non-perpendicular setups."*

### What the surface actually is

Confirmed by measurement, not by reading the code: over all 659 blocks the tessellated cap
has **exactly as many vertices as the input ring** (min 3, median 4–5, max 16), and **zero
cap vertices fail to coincide with a ring vertex**.

> The pavement is a single triangle fan over the block's boundary ring, spanning kerb to
> kerb across the whole block, with **no interior vertices at all**. Between 3 and 16
> vertices carry a block up to ~150 m across. `ExtrudePoly` is constructed with
> `TileToTexture = false` here, so it does not subdivide the sides either — the ring is
> the entire vocabulary.

A four-cornered block with 16 m between its highest and lowest corner is therefore a
warped quad, and which way each triangle tilts is decided by LibTess's sweep, not by
anything geometric.

**There is no sidewalk object anywhere in the codebase.** The only thing that knows a
pavement has a width is `QuarterGenerator._createBuildings` (`engine/streets/QuarterGenerator.cs:155-184`),
where `sidewalkWidth` = **1 / 2 / 4 / 6 m** by `downtownness` insets the building footprint
via `ClipperOffset` with `JoinType.jtMiter`. That number is computed, used, and thrown
away — never stored on the `Quarter`, never seen by the floor mesh.

### Measured cross-slope

For every boundary edge: take the midpoint, step **3 m along the inward perpendicular**
(3 m ≈ the median `sidewalkWidth`; both walker systems stand at 1.5 m), and read the cap's
**own triangles** barycentrically at both points.

| city | along-edge slope % (med) | **cross-fall %** med / p95 / max | **drop over 3 m (m)** med / p95 / max |
|---|---|---|---|
| seed000/1500 | 9.5 | **10.4** / 33.1 / 178 | **0.31** / 0.99 / 5.35 |
| Yelukhdidru/3000 | 13.1 | **13.2** / 41.8 / 255 | **0.40** / 1.25 / 7.64 |
| seed000/800 | 8.3 | **8.2** / 33.1 / 45.6 | **0.25** / 0.99 / 1.37 |
| Yelukhdidru/1500 | 10.6 | **11.3** / 35.1 / 353 | **0.34** / 1.05 / 10.58 |

> **On 53–56 % of block edges the pavement is steeper sideways than lengthwise.** The
> median pavement falls ~11 % across its width — one in nine — where a real footway is
> built at 2 %. Over a 3 m pavement that is a third of a metre, **twice the kerb height**.

Signed cross-fall is symmetric (p25 ≈ −11 %, p75 ≈ +12 %): it tips *toward* the road as
often as away.

### The "non-perpendicular setups" the player conceded

Interior angles at block corners: median **90.1–93.5°** — the median corner is a right
angle — but **10–16 % are sharper than 60°** (sharpest ~40°) and **7–15 % are reflex**
(the block folds inward). A mitre at 40° projects the inset vertex ≈2.9× the pavement
width from the corner; at reflex corners it self-intersects.

That is exactly the problem `ClipperOffset` already solves, correctly, for building
footprints, **twenty lines away in the same file**.

### Proposal — three options

Shared premise: **the width already exists** (`sidewalkWidth`) and **the mitre already
exists** (`ClipperOffset`, `jtMiter`, applied to this same ring). Promote `sidewalkWidth`
to a `Quarter` property computed once, so the floor and the building footprint offset by
the *same* number — if they drift, the pavement and the building wall stop meeting.

**Option 1 — separate pavement ribbon + separate hidden back-slope.** A closed ribbon
between the block ring and the inset ring, each inner vertex taking its outer vertex's
height, plus a second surface joining the ribbon to the block interior. *Cost:* ring ×2
plus an interior surface. *Breaks:* `ExtrudePoly.BuildStaticPhys` runs
`Triangulate.ToConvexArrays` on the polygon and builds one hull per convex piece — an
annulus becomes 4–16 thin slabs per block, and the `area < 10f` / `Radius < 0.1f` guards
would silently drop narrow ones on 1 m pavements. Realistically needs `BuildGeom`
decoupled from `BuildStaticPhys` — a real refactor. *Flat city:* breaks unless gated.

**Option 2 — one slab, with an inset ring of vertices at the pavement width, each inset
vertex taking its boundary vertex's height. ← RECOMMENDED.** The strip between the two
rings is level across by construction (every quad has two equal-height pairs); all the
warp moves into the block interior, where the buildings stand and nobody walks.
- *Corners:* the same `ClipperOffset` mitre. Feed the **offset result's** polygons rather
  than a naive per-vertex offset and reflex self-intersection is handled. If the inset
  collapses (block narrower than 2× width), fall back to today's single ring — the rule
  `_createBuildings` already uses when no footprint remains.
- *Cost:* ring ×2. Median 4→8 vertices, p95 10→20, max 16→32. The worst fragment's merged
  floors go from ~339 vertices to ~680. Nothing.
- *Breaks:* least of the three. `BuildStaticPhys` still receives one polygon;
  `ToConvexArrays` already handles non-convex rings (every block with a reflex corner
  exercises it today). `Quarter.GroundHeightAt` unchanged.
- **The decisive point:** sidewalk `NavJunction`s sit *on* the outer ring corners, whose
  heights do not move — so **every pedestrian lane height stays exactly as it is and
  becomes correct for the first time**, because the surface 1.5 m inward is now at the
  corner's height instead of somewhere on a warped triangle. It fixes cause 3 of (d) for
  free, with no second correction.
- *Flat city:* **this is the risk** — a flat city would gain the inset ring and its mesh
  would change. **Gate the inset on `!ClusterDesc.StreetHeightSource.IsFlat`**, exactly as
  `Quarter.GroundHeightAt`, `DeckCollider` and `JunctionCollider` already do. Then the
  default city emits an identical ring, identical tessellation, identical indices.

**Option 3 — per-edge independent quads, overlapping at corners.** Level across, but
overlapping quads z-fight on a **16-bit** depth buffer (38 mm quantum at 50 m), and reflex
corners leave wedge-shaped holes. Buys nothing over Option 2 and pays in shimmer.

**Recommendation: Option 2, gated on `IsFlat`.** Then settle two follow-ups: where the
width comes from (above), and **what the interior becomes** — once the strip is level the
interior carries all 12–16 m of warp, while buildings stand on the *pad*, a different
surface again. Making the cap's interior the pad plane outright is a separate change, and
it is the one that makes the interior warp harmless.

---

## ✅ (a) Houses float and sink; shops must stay reachable — FIXED 2026-08-31

> *"Houses are sometimes 'under' the sidewalk level in parts, sometimes in the air. I would
> say houses must not be in the air. Shops however shall be placed only in a reachable way,
> so that they are at the same level or above the sidewalk."*

### What was built, and what this write-up got wrong

**Neither 3 nor 5 as written.** The owner chose **planar floors**, explicitly and with a
reason - *"real live buildings usually have planar floors ... shopfront entries would be
usually aligned per story and not gradually ... let's for a moment ditch the stairs and
align to stories"* - so candidate 3, the footprint-following base, was offered and
**rejected**. What landed is candidate 1 (*"sink to the MINIMUM pavement under the
footprint"*), which this page dismissed as *"visually unacceptable"* and *"violating the
second constraint"* - **and it does not violate it**, because the shops do not go down with
the building. They snap up in whole storeys. Full write-up in
[`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7l.

`engine.streets.generation.BuildingFooting`;
`tests/JoyceCode.Tests/engine/streets/BuildingFootingTests.cs` (42 tests) and
`ShippedTerrain.cs`, which reproduces `GroundOperator`'s diamond-square, `ElevationBaseFactory`'s
per-fragment refinement and `CacheEntry`'s own sampling rule from inside the test assembly.

**What this page got wrong or left out, in order of how much it mattered:**

1. **"Sink to the minimum ... buries 7-13 m, 2-4 storeys of every building" understated the
   real cost and named the wrong one.** The cost is not that the building is buried; it is
   that the building is *eaten*. With the design height unchanged, the roof of **64 of the
   149 buildings of Yelukhdidru/3000 falls below the block floor somewhere over its own
   footprint**, and the median 24 m building shows **4.54 m** above the ground at its
   highest corner. No building vanishes entirely (0 of 149, 0 of 81), so it is not total -
   but "a house must not be in the air" needs its converse, and `BuildingFooting.HeightOf`
   adds the block's corner spread so the roof clears the highest corner by the design
   height. Height added: median 8.0-14.9 m, p90 up to 30.8 m, max 55.7 m, **exactly zero on
   a flat block**.
2. **"...and puts every uphill shopfront underground, violating the second constraint" is
   false, and it is the whole reason candidate 1 works.** The shopfront does not sit at the
   base. It snaps to the lowest storey at or above the pavement **in front of that
   shopfront** - measured, storey index median 2-4 and max 19, with `sill - localPavement`
   median 1.2-1.9 m and **below one storey always, by construction**. The base and the shop
   were only ever locked 0.45 m apart because one sample drove both.
3. **The estates/buildings-per-block question this page never asked has a clean answer, and
   it is what makes the simple bound legitimate.** A block carries **exactly one estate**
   and an estate **at most one building** - 1 estate on each of 3/10/82/445 blocks;
   3/3/81/149 buildings, never two on one. So the minimum over the block's own corners is
   within **0.19-0.61 m of the exact minimum over the footprint at the median** (p90 1.5 m,
   worst 3.74 m), and a per-footprint bound would buy that and nothing else.
4. **The 0.35 m flat-city move is right, and it is the ONLY thing that moves.** The
   shopfront quad, the shop POI and the TALE door are all bit-for-bit unchanged, because the
   storey index is a difference of two GROUND heights and `ClusterStreetHeight` +
   `QuarterSidewalkOffset` cancel out of it - so it is exactly 0 on a flat block rather than
   the ceiling of a rounding error.
5. **The five disagreeing height expressions are now three, and none of them is the pad.**
   Houses, polytopes and trees are a separate matter (polytopes and trees were not touched);
   the shop window, the shop POI and the TALE shop door all ask
   `BuildingFooting.StoreyGroundAt` and each still adds its own constant. The shop POI is no
   longer the one thing on a block that asks the **terrain**.
6. **"The grey/white noise is a UV in the atlas gutter" is the wrong mechanism.** The UV -
   `Vector2.One/64f`, constant for every cap vertex - is right. What it triggers is
   `AddInterior`: `LIghtingFS.frag`'s `renderInterior` short-circuits only when the texel at
   `fragTexCoord` has alpha > 0.8, and at (1/64, 1/64) it does not, so the cap runs the full
   interior-room raymarch across a horizontal polygon. And **it is not specific to the
   underside** - `ExtrudePoly` gives the ceiling cap the identical UV, plane and material,
   so every building ROOF in the shipped flat city is the same construction. Deliberately
   left: see §7l.
7. **Measured footprint diagonals came out smaller than the 100-104 m median quoted here** -
   median 89.3 m and max 358.9 m on Yelukhdidru/3000, against 100-104 m / 456 m. A plan-only
   quantity, so the difference is the baseline set (500/800/1500/3000 here against
   800/1500/1500/3000), not the measurement.

**Found on the way and NOT fixed:** `GenerateHousesOperator._createLargeAdvertsSubGeo` is
complete and **never called from anywhere**; and a one-storey building on a slope can carry
a shop window taller than its own visible height (rare - p05 of building height is 6 m - and
not new).

### The original write-up

### The base is one scalar, and the code comment claiming otherwise is wrong

`nogameCode/nogame/cities/GenerateHousesOperator.cs:557-561`:

```csharp
Vector3 v3Position = new Vector3(
    cx, 2.5f + quarter.GroundHeightAt(...), cz) + v3BuildingCenter;
```

The footprint handed to the L-system (L546-551) is `new Vector3(p.X, 0f, p.Z) - v3BuildingCenter`
— **Y forced to zero** — and `AlphaInterpreter`'s `extrudePoly` case extrudes straight up.
So the base is a **flat horizontal polygon at one height**. The comment at L553-556 ("so a
house standing on a tilted block tilts with it") **is false**; it takes the pad's *value*
at one point and nothing more. Physics matches the visual, so a floating house floats in
physics too, and the bottom segment is built `addFloor: true`, giving a floating house a
solid visible underside.

### The pad's tilt is irrelevant, because an estate *is* the block

`QuarterGenerator._createBuildings` takes the estate — literally the block outline — and
insets it by `sidewalkWidth` of only 1–6 m. **Measured footprint diagonal: median
100–104 m, p90 258–268 m, max 456 m.** So a building's own corners sit *at* the kerb,
where the pad and the floor have parted company by design. The pad-vs-floor residual is a
rounding error next to this.

### Measured, per building, `baseY − pavementY` at each footprint vertex

| city | pavement relief **under one footprint** med / p90 / max | worst AIR per building med / p90 / max | worst BURIED med / p90 / max |
|---|---|---|---|
| seed000/500 | 7.4 / 7.4 / 8.6 | 4.8 / 4.8 / 5.6 | −2.5 / −2.5 / −3.0 |
| Yelukhdidru/800 | 6.8 / 6.8 / 7.3 | 4.2 / 4.2 / 4.2 | −2.5 / −2.5 / −3.4 |
| seed000/1500 | 10.1 / 24.9 / 41.6 | 5.4 / 11.8 / 20.8 | −4.8 / −12.4 / −20.8 |
| Yelukhdidru/3000 | 12.9 / 27.5 / 52.8 | 6.9 / 14.1 / 33.3 | −6.2 / −13.7 / −23.4 |

> **Every building in every one of these cities has both a floating corner and a buried
> corner.** Minimum "worst air" across 149 buildings in the 3000 m city is 0.58 m; maximum
> "worst buried" is −0.12 m. There is no clean subset to exempt.

Fraction of buildings whose footprint relief is ≤ 3 m: **0.0–2.7 %**. ≤ 6 m: 9–33 %.

### ⚠️ In the DEFAULT FLAT city every house is already 0.35 m in the air, today

Pad = `AverageHeight`; pavement = `AverageHeight + 2.0 + 0.15`; base = `AverageHeight + 2.5`.
The gap is currently *hidden by the shopfront quad*, which
(`GenerateHousesOperator.cs:303`) puts its bottom at `pad + 2.05` — 0.10 m **below** the
pavement — so it skirts the gap wherever a shopfront exists. Elsewhere it is visible and
always has been. **Any fix that lands the base on the pavement moves every house in the
shipped flat city by 0.35 m.**

### Five height expressions disagree on the same block

| thing | expression | vs. pavement on real terrain |
|---|---|---|
| houses, polytopes | `pad + 2.5` | ±, see table above |
| trees | `pad + 2.15` | 0.35 m below the houses by construction |
| shopfront geometry | `pad + 2.05` | med −0.6…+0.16, **p10 −9.7 / p90 +9.5, min −23.5, max +33.3** |
| shop POI entity | `ClusterDesc.GroundHeightAt` (**terrain**) `+ 3.5` | med +1.2…+2.0, p99 +5.0…+7.5, min −3.8 |
| TALE doors | `pad(**block centre**) + 2.15` | med ~0, p10 −8.9, p90 +9.4, worst +31.4 |

Roughly **half the shop windows in a hillside city are below the pavement**. The shop POI
is the only thing on a block that does not ask the quarter at all. And
`_hasPedestrianAccess` (`GenerateShopsOperator.cs:243-273`) is **pure 2-D** — a midpoint
within 5 m of a boundary segment — so it cannot notice any of this. Reachability bites
because `ShopNearbyBehavior` inherits `Distance = 16f` and scores in **3-D**: ~7 m of
vertical error leaves under 15 m of horizontal reach.

### Candidates

1. **Sink to the MINIMUM pavement under the footprint.** Satisfies "never in the air"
   exactly; buries 7–13 m — **2–4 storeys of every building** — and puts every uphill
   shopfront underground, violating the second constraint. Cheap, visually unacceptable.
2. **Raise to the MAXIMUM + downward skirt/plinth.** Satisfies both constraints. One extra
   `ExtrudePoly`, 3–15 quads. But the plinth height *is* the relief: median 7–13 m, max
   53 m — a genuinely enormous retaining wall, which is what a 15 % hillside block 100 m
   across actually requires.
3. **Footprint-following base (per-vertex base Y).** `ExtrudePoly` already accepts a
   non-planar ring and `Triangulate.ToMesh` keeps every vertex's height, so the *bottom*
   L-system segment could take one directly. Gives a wedge-shaped ground floor sitting on
   the ground. **The only candidate with a clean flat-city story** (a flat block's ring is
   planar and the per-vertex value equals the pad). Risk: the L-system's `A` polygon is a
   `JsonObject` parameter, so per-vertex Y must survive `From(fragPoints)` /
   `ToVector3List` round-tripping.
4. **Subdivide the estate into building-sized lots.** The real root cause — but 30 m lots
   on a 15 % grade still leave ~4.5 m of relief, so it reduces the problem ~3–4× without
   removing it, and it changes the flat city's building layout completely.
5. **Unify the five height expressions** (independent of the above, worth doing anyway).
   Measured, the **conformed terrain is a better predictor of the pavement than the pad
   is**: `GetWalkingHeightAt(door) − pavement` is med 0.06, p10 −1.5, p90 +1.6, max 7.4 —
   against ±9 for the pad-at-block-centre.

**Suggested order: 3 + 5**, with the 0.35 m flat-city move made deliberately and once.

---

## ✅ (b) The quest marker sinks under the road — FIXED 2026-08-31

### What was built, and what this write-up got wrong

**(B), with (C) as its mechanism** — and the reason both are needed is the one thing this
page had wrong. Full write-up in
[`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7m.

1. ⚠️ **"(C) — cheapest and honest" does not meet the requirement, and it was measured
   before being discarded.** Resting the cube on the anchor it already had leaves its bottom
   at terrain + `ClusterNavigationHeight`, which is still **2.67 m** under the pavement at
   the worst junction of `Yelukhdidru`/800 and of `seed000`/1500 and **8.27 m** under it at
   the worst junction of `Yelukhdidru`/3000. Only `seed000`/500 — 27 junctions, almost no
   relief — would have been fixed by it. The anchor had to move as well.
2. **The flat-city cost of (B) is 0.85 m, not 1.5 m.** The bottom was at `aver + 3.0`
   (`aver + 1.5` flattening bias, `+ 3` hover clearance, `− 1.5` for straddling) and is now
   at `aver + 2.15`, resting on the pavement instead of hovering a metre over the road.
   (A) would have cost 1.5 m and bought nothing.
3. **The table above understates the tail in the other direction.** At the worst junction of
   the 3000 m city the marker floats **10.6 m ABOVE** the pavement, because the road there
   is in a cutting the 20 m elevation grid cannot cut. That is also why `max(surface,
   terrain)` — the §7f hover-probe shape, the obvious safety net — was rejected: it would
   float the marker 9 m over the road the player is driving on.
4. **The consequence this page did not name:** `TrailVehicle` parents its marker to the CAR
   with `RelativePosition = Vector3.Zero` and computes no height at all, so the mesh offset
   moves it too. The fishmonger quest's marker now stands ON the car instead of around it.

`engine.streets.generation.CitySurface`, `engine.quest.QuestMarker`,
`Loader.GetCitySurfaceHeightAt`; `tests/JoyceCode.Tests/engine/quest/QuestMarkerTests.cs`.

### The original write-up

Cause is exact and slightly absurd. `ToSomewhere._createTargetInstance`
(`ToSomewhere.cs:176-183`) draws a cube scaled to `(SensitiveRadius, 3, SensitiveRadius)`
**centred on** `RelativePosition`, so its visible bottom is `markerY − 1.5`.

- **Flat city:** `GetHeightAt` inside a city returns `aver + 1.5f`
  (`ClusterBaseElevationOperator.cs:114` — a magic constant unrelated to
  `CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE = 2.0`). So the cube bottom lands at **road + 1.0**
  and looks right *by coincidence*.
- **Terrain city:** the +1.5 flattening bias is gone and the conform pass pulls terrain to
  street ground, so the cube bottom lands at **road − 0.5**.

Measured over every junction of the four baselines (quest destinations are placed at
`StreetPoint`s):

| city | markerY − road med | **cube bottom − road** med / p10 / min |
|---|---|---|
| seed000/500 (27 jn) | 1.01 | **−0.49** / −1.01 / −1.32 |
| Yelukhdidru/800 (64) | 0.98 | **−0.52** / −0.97 / −4.02 |
| seed000/1500 (274) | 1.01 | **−0.49** / −1.17 / −4.98 |
| Yelukhdidru/3000 (1379) | 1.00 | **−0.50** / −1.34 / −9.62 |

So the marker's lower half-metre is under the road at the median (0.65 m under the
pavement), with a tail to −9.6 m — *"in parts under street/sidewalk level"*, exactly.
Without the conform pass the same expression would give −24…+34 m, so §2c is working; the
residual is the missing 1.5 m bias plus the 20 m grid.

**Purely visual** — the goal's collision shape is a cylinder 1000 m tall. **Confirmed: it
still is**, `ShapeFactory.GetCylinderShape(SensitiveRadius, 1000f)`, and it moved down
2.35 m with the anchor without ceasing to span everything it spanned.

Fixes: **(A)** route the three quest strategies through `Loader.GetNavigationHeightAt` —
changes nothing on a slope and **lowers every marker in the shipped flat city by 1.5 m**;
**(B)** position by the marker's *bottom* against a real surface height, the shape §7g
already used for the ribbon; **(C)** cheapest and honest — offset `_eMeshMarker` by
`+1.5 · UnitY` in its local transform so the cube **rests on** `RelativePosition` instead
of straddling it. (C) makes the terrain city match the flat city's look and raises flat-city
markers by 1.5 m.


---

## ✅ (d) T-posed NPCs below pavement level — FIXED 2026-08-31

### What was built, and what this write-up got wrong

Full write-up in [`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7m.

1. **Cause 3 is not "mostly fixed", it is EXACTLY ZERO.** Re-measured after (c) at the point
   a walker actually stands, the satnav walker is **0.00 m off the block floor at every
   percentile from min to max, on 0.0 % of edges below it**, on all four cities. The only
   residual anywhere is the 1 / 0 / 3 / 7 blocks §7k refuses a pavement inset.
2. **Cause 1 is worse than measured here, because this page sampled the wrong points.** The
   loop walker stands at CORNERS, not at edge midpoints, and the pad's residual is largest
   there: p05 **−6.55 m** and worst **−17.78 m** on `Yelukhdidru`/3000, against the −4.5 /
   −12.6 quoted below.
3. **The obvious repair for cause 1 is not the best one, and it was measured.** Giving the
   loop walker the corner's own junction height — literally the number the satnav walker
   uses at the same corner — leaves it below the floor at **33–55 %** of corners, against
   10–32 % for `BuildingFooting.PavementHeightAt`. The waypoint is 1.5 m in from the corner,
   i.e. inside §7k's corner ramp, and the nearest-edge interpolation follows the ramp while
   the corner's own value does not.
4. ⚠️ **"The terrain has to answer, since there is no road node to ask" is false, and it was
   a comment in the shipped source.** `TryCreateCursor` has already snapped both route ends
   to their nearest lane. Nothing on a walker's route is a terrain sample any more.
5. **The T-pose criterion this page proposed would have passed the broken site.** "All six
   sites now name at least one driver" was true of the niceday NPCs throughout, and they had
   no animation at all. The guard asserts that something *sets* an animation.
6. ⚠️ **Found on the way and NOT fixed — roughly half the loop walker's waypoints are
   OUTSIDE their own block.** 50–58 % are inside the ring; the rest stand 1.5 m into the
   carriageway. It is the (g)-shaped corner effect in the other pedestrian system, it is a
   plan-position defect rather than a height one, and moving it would move every citizen's
   walk in the shipped flat city.

### The original write-up

Two unrelated defects in one sighting.

### T-pose

All six `EntityCreator` sites now name at least one driver, so the 2026-08-25 fix holds.
**But naming a driver is not the same as something calling `SetAnimation`, and one site
exploits the gap:**

> **`nogame.npcs.niceday` NPCs are animated by an unretried one-shot and nothing else.**
> Their strategy starts in `"rest"` → `RestStrategy.OnEnter` attaches `NearbyBehavior`, an
> `ANearbyBehavior` that only drives the "E to Talk" prompt and **never calls
> `SetAnimation`**. Their whole animation is `EntityCreator.InitialAnimName` — one call, no
> retry. It is now *checked* (Errors with `DescribeFailure`), so a failure is loud, but
> permanent. Same shape, lower risk: the taxi passenger, deliberately (no `Body`).

Everything else self-heals (`WalkBehavior`, `IdleBehavior`, `RecoverBehavior`,
`TaleConversationBehavior` all latch on success and report via `StuckAnimationReporter`).

**Transient T-pose is possible but bounded to ~one frame:** `StrategyManager` runs
`OnAttach`/`OnEnter` synchronously, but the first `Behave` — where `SetAnimation` happens —
is on the next `BehaviorSystem` tick, while the mesh is already attached. One more
sustained case: `EntityCreator._createLogical`'s catch block (L354-389) leaves a half-built
character in the world and only hides it; if `SetVisible` itself throws you get a visible,
behaviour-less, physics-less T-pose forever.

### Below pavement level — three independent causes

NPCs are `MakeKinematic`, so they go exactly where the waypoint says; nothing rests them on
a collider. Measured at the midpoint of every block edge, 1.5 m inside the kerb (n = 3545):

| walker | med | p05 | worst | below pavement |
|---|---|---|---|---|
| **loop walker** (`QuarterLoopRouteGenerator`, the ordinary citizen) — uses **the pad** | ≈0.00 | −1.6…−4.5 m | **−12.6 m** | **46–52 %**; by more than a kerb: **24–41 %** |
| **satnav walker** (`PedestrianRoute`) | ≈0.00 | −0.31…−0.48 m | −12.9 m | ~50 %; \|Δ\|>0.15 m: 34–63 % |
| **terrain walker** (`StreetRouteBuilder` ends, `GoToStrategyPart`, `GetWalkingHeightAt`) | ≈0.00 | −1.6…−2.6 m | −13.6 m | 43–51 %; by >1 m: **7.5–16 %** |

1. **The loop walker is the pad's fit residual** (p05 −3.5…−6.1 m, worst −18.3 m). The
   worst offender, and it is the *default citizen*.
2. **The terrain walker is street-vs-terrain** (p05 −0.76…−1.48 m, worst −9.8 m). §2c
   removes most of it but cannot, on a 20 m grid.
3. **The satnav walker is nothing but (c)'s cross-slope** — ±0.3–0.5 m, exactly half the
   3 m cross-fall. **Fixed 2026-08-31 with (c)**: the rim it walks on is level across, so
   the height 1.5 m in from the kerb is the kerb's own. Note (g) below was a second,
   independent defect in the same walker and is also fixed. **Verified 2026-08-31: 0.00 m at
   every percentile.**

---

## ✅ (e) Trams — the intercity one rides its own track now — FIXED 2026-08-31, HALF BY DESIGN

### What was built, and what this write-up got wrong

**The vehicle was fixed; what the track IS was deliberately left to the owner.** Full
write-up in [`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7n. The three options for
the line's own shape are written out there, measured, and **not built** - see *Still open*
below.

1. **The city tram's one known tail is gone, and nothing was done to it.** This page
   recorded *"where a road is heavily filled the tram passes below it, min −1.5 m"*.
   Re-measured at nine points along every stroke of all four baselines - 21105 samples -
   the minimum is **+1.88 m** and **0.0 % of samples are below the road**, on every city.
   Median 11.01 m, exactly as before. The number moved because §7a and §7d moved the ground
   under it, not because anything was changed.
2. **"Median ≈ 47 m up, p95 ≈ 87 m" is 43.5 / 86.8, and the underlying spread is 23.5 not
   27.3.** Measured over the world the game actually builds, using the real
   `GenerateClustersOperator._generateClusterList` rather than a reproduction: **70 cities,
   114 lines**, `|dAverage|` median **23.53 m**, p95 66.76, max **89.34**. The vehicle above
   its own track at the higher end: median **43.53 m**, max **109.34 m**.
3. **"−143…+81 m" is −149.3…+134.2 m, and 63.8 % of the corridor is a cutting.** Track minus
   untouched terrain along all 114 corridors, 21501 samples: median **−13.10 m**, p01
   −101.5, p99 +81.9, min **−149.30**, max **+134.18**.
4. **This page did not say that fixing it MOVES THE FLAT GAME, and it does.**
   `ClusterDesc.AverageHeight` is computed from the unflattened ground either way -
   `ClusterBaseElevationOperator` skips only the height write - so the defect and its fix
   are identical in both worlds. **114 of the 228 route ends come down**, median 23.53 m,
   max 89.34 m; the other 114 do not move; none rises. Not one of the 114 pairs has equal
   averages, so not one line was ever right at both ends.
5. **The half this page did not name: the fix lowers the tram INTO the landscape.** Against
   the terrain the intercity operator does not flatten, the vehicle was above the ground
   71.7 % of the time and now is 57.3 %. That is not a regression - it is the vehicle
   finally being where its track is, and the track being a trench is the decision below.

`engine.world.IntercityLine`;
`tests/JoyceCode.Tests/engine/world/IntercityLineTests.cs` and `IntercityWorldHarness.cs`.
**The clearance is derived**: for two cities of equal average the shipped expression put the
vehicle 20 m over its track, so a matched pair does not move by a float. **No sampling was
needed** - the track is one height for the whole line, so "the track's height at the
vehicle's position" is that height wherever it is. **The layer ordering is unaffected**:
`/000200/intercityTrails` stays above the city conform pass and the fix reads what that
operator writes.

### ⚠️ Still open, deliberately: what an intercity line IS

The track's own shape is a design decision the owner has not made. Three options are laid
out with their measurements in §7n and **none is built**:

1. **A graded embankment**, relaxed against the terrain the way `GradeRelaxer` relaxes
   streets. Cheapest in new machinery (the relaxer and the height field exist); the vehicle
   then does need intermediate route points. The median terrain gradient along a corridor is
   14.69 % over 40 m against a `GradePolicy` allowing 5–14 %, so a relaxed line hugs the
   ground over most of its length.
2. **A viaduct on pylons.** The only option needing geometry that does not exist - a deck
   and pylons over ~870 km of line - and the only one that stops the network fighting the
   terrain at all.
3. **A deliberately elevated line**, closest to what is there. Costs approximately nothing;
   the open question is whether the flattening ribbon should be written at all if the line
   is in the air. **Compatible with the fix as it stands.**

### The original write-up

Both systems are **active in the shipped game** (`world.CreateTramCharacters: true`; both
intercity operators registered unconditionally).

**City tram — fine, and always was.** `characters/Tram/Behavior.cs:34-35` flies at
`ClusterDesc.GroundHeightAt(pos) + ClusterNavigationHeight + 10`, sampling **conformed
terrain per frame at its own position**. Measured against the road: **median 11.0 m**, p05
9.5, p95 12.5 — a deliberate elevated line, identical to the flat city's 11 m. **Refuted:
the city tram is not 20–30 m up.** One tail worth knowing: where a road is heavily filled
the tram passes *below* it (min −1.5 m), because the tram reads terrain and the road reads
relaxed street height.

**Intercity tram — this is the 20–30 m, and it is pure flat-city arithmetic.**
`characters/intercity/GenerateCharacterOperator.cs:112-113` builds two `SegmentEnd`s at
`ClusterX.AverageHeight + 20f` and flies a **straight chord** between two constants under a
plain `SimpleNavigationBehavior`, sampling nothing. Measured for city pairs 3–10 km apart:

- `|AverageHeight(A) − AverageHeight(B)|`: median **27.3 m**, p95 66.9 m, max 87.3 m.
- So the intercity tram runs 20 m above its track at the lower city and 20 m + that
  difference at the higher — **median ≈ 47 m up, p95 ≈ 87 m.**
- Its "track" (`IntercityTrackElevationOperator`) hard-sets `Line.Height =
  min(AverageHeight(A), AverageHeight(B))` across a ~76 m band, sitting at layer
  `/000200` **above** the conform pass so it overrides everything. Against untouched
  terrain that band is median **−13.8 m** (a cutting), range −143…+81 m — and **no track
  geometry is drawn at all**.

So: a tram flying ~47 m over a landscape that is not flat, above an invisible flattened
scar up to 143 m deep. `models/nogame.globalSettings.json` already admits the intercity
network ignores all of this.

---

## ✅ (f) Starting coins are nowhere near the player — FIXED 2026-08-31

### What was built, and what this write-up got wrong

Full write-up in [`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7n.

1. **"Hundreds of metres away" is 102.05 m.** The shipped world's start cluster is
   `Yelukhdidru` at `(-5.77, 0, 10)`, `AverageHeight` 37.76, and the player appears at
   `<92.065094, 137.76129, 209.37798>`. The column at (164, ·, 137) is 102.05 m from it in
   plan.
2. **The vertical shape was nearly right, and this page did not notice.** The column spans
   `AverageHeight + 7.2 … + 61.2` and the player falls from `AverageHeight + 100`. A 57 m
   column of coins hanging over a spawn that falls 100 m is a thing to fall THROUGH; it was
   hanging 102 m to one side of the fall. So the column stays - 19 coins, 3 m apart, the
   shipped count and spacing - and hangs under the start with its top 3 m below it.
3. **The operator could not have asked even if the ordering were different.**
   `Saver.CallOnCreateNewGame(object gs)` **never passes `gs` to its operators at all**, and
   `AutoSave.GameState` is still null at that moment. Both are recorded in §7n as found and
   not fixed.
4. **`DropCoinModule` is the ONLY consumer of `OnCreateNewGame` in the whole tree.** So
   moving when that runs was genuinely available - and was not taken, because the operator
   asking is one line and restructuring the load path for one caller is not.
   `engine.world.PlayerStart.Find()` resolves once and hands everyone the same answer, which
   matters: which estate is free depends on which fragments have generated by the time it is
   asked, and the coins and the player ask at different times.
5. **The double add is worse than "the fallback spawns at 2 × cluster.Pos" makes it sound,
   and it has a second way in.** Over the shipped world's 70 cities the doubled fallback
   lands **outside its own city for 69 of the 70**, a median **36.6 km** away; the exception
   is the start cluster, 11.5 m from the origin - so a fixture built on a city near zero,
   which is what the test harness makes, would have shown nothing. And the branch is not
   unreachable: `QuarterGenerator` builds on an estate as it traces it, and on
   **seed000/500 it succeeds on all three**, so the smallest baseline city takes the
   fallback on a freshly generated world with no fragment operator having run.
   `FindStartPosition(out, out)` is **gone**, replaced by `FindStartPose()` returning a
   `StartPose` whose one field is named `V3World`, so a caller that still adds `Pos` does not
   compile.

**Flat game delta:** all 19 coins move 102.05 m in plan and +35.76 m in Y. **The player does
not move at all** - `<92.065094, 137.76129, 209.37798>` bit for bit, asserted as equality.
The **debug cluster beam** in `joyce.ui.Clusters` drops by each city's own `Pos.Y` (median
22.94 m, max 38.69 m, exactly 0 for the start cluster), which is the random nominal
elevation `GenerateClustersOperator` draws and `ClusterBaseElevationOperator` then replaces.

**One ordering consequence, stated:** the coin operator now triggers the start cluster's
street generation, so its `ClusterCompletedEvent` fires during create-new-game rather than at
preload. `TaxiNpcSpawnerModule` is subscribed by then; `TaleModule` and `Narration` are not.
`TaleSpawnOperator` populates on demand for exactly this reason and says so in its own
source, and `Narration`'s `quest.autoTrigger` is not set anywhere in the shipped
configuration. TALE 200/200 unchanged.

`engine.world.PlayerStart`, `engine.world.StartPose`, `ClusterDesc.FindStartPose`;
`tests/JoyceCode.Tests/engine/world/PlayerStartTests.cs`.

### The original write-up

`nogameCode/nogame/world/DropCoinModule.cs:24-27` drops 19 coins in a **vertical column**
at hard-coded absolute world XZ **(164, 137)**, Y = 45…96 (57 m tall, 3 m spacing). No
cluster, no player, no terrain, no fragment. The player starts wherever
`ClusterDesc.FindStartPosition` finds the first building-free estate — hundreds of metres
away. Deterministic, and it has always been this way.

**Three separate things are wrong; only the first is what was noticed:**

1. **Hard-coded position.** (164, 137) *is* inside the start cluster, so the coins are in
   the right city, at a fixed spot in it.
2. **Ordering blocks the obvious fix.** `DropCoinModule` is an `IWorldOperator` on
   `Saver.OnCreateNewGame`, called with a brand-new `GameState` whose `PlayerPosition` is
   still `Vector3.Zero` — the start position is resolved lazily later by
   `PlayerPosition.GetPlayerPosition`. So the operator genuinely **cannot** ask where the
   player starts. Either `CallOnCreateNewGame` runs after the start position resolves, or
   `DropCoinModule` calls `ClusterDesc.FindStartPosition` itself.
3. **A latent double-add.** `FindStartPosition` returns a **cluster-relative** position in
   the success branch (L590-591) and `PlayerPosition._findStartPosition:29` adds
   `startCluster.Pos` — but the "no empty estate" fallback (L622) returns
   `Pos + vOffset`, **already absolute**, so the fallback spawns the player at
   `2 × cluster.Pos`. `joyce/ui/Clusters.cs:38` has the mirror-image bug.

**Verdict: pre-existing and independent of the terrain work.** The flag moves nothing here.
(Correct, and confirmed.)

---

## Also found on the way — not reported, worth more than some that were

### ✅ (g) Half of all pedestrian routes put the walker in the carriageway — FIXED 2026-08-31

`builtin/modules/satnav/PedestrianRoute.WaypointFor` (`PedestrianRoute.cs:45`) returns
`v3End + laneRight * SidewalkOffset` — **always 1.5 m to the right of travel**. Measured
over all 3545 block edges: `−1.5 × Cross(fwd, UnitY)` is inside the block **100.0 %** of
the time; `+1.5 ×` is inside **0.0 %** of the time. `GenerateNavMapOperator.cs:298-305`
creates block sidewalk lanes with `_createBidirectionalLanes`, so **whichever way round
the block the A\* routes, one of the two directions stands the walker 1.5 m outside the
kerb — in the roadway, at pavement height.**

`QuarterLoopRouteGenerator.cs:50` uses `-1.5f * vu3Right` and is correct. **The two
pedestrian systems offset to opposite sides.** This is present in the flat city too.

**A sign flip is not the fix**, and that is the part worth carrying forward: both
directions of a lane cover the same ground, so whichever hand is chosen, one of the pair is
in the road. The side has to belong to the LANE. `NavLane.KerbSide` is a unit vector in plan
toward the block, set on both directions when the lane is created, and zero where there is
no such side — every car lane, and **every pedestrian crossing**, which is in the
carriageway by definition and belongs on its centre line. Which side it is comes from the
block's own signed area rather than a constant: all 659 baseline blocks are traced
clockwise today, so a constant would be right, and would silently put every pedestrian
route in the city into the road the day the tracing order changed.
`tests/JoyceCode.Tests/builtin/modules/satnav/PedestrianKerbSideTests.cs`. Note
`NavJunctionHeightTests.TheWaypointStaysOnTheRightHandSidewalk` had been **asserting the
defect**.

### ✅ (h) The unwritten elevation row — VERIFIED, REFUTED, and fixed 2026-08-31

The hole is real: `ElevationBaseFactory` copied its grid with one inclusive and one
exclusive loop bound, so `Elevations[20, *]` — the last **Z** row, not column; the write
indexed `[x, y]` while every reader indexes `[ez, ex]` — stayed at a default
`ElevationPixel`, i.e. a height of exactly 0.

**Both halves of the prediction above are wrong, and measuring took ten minutes as
advertised.**

- **There is no cliff in the drawn terrain, anywhere.** `CreateTerrainOperator` takes its
  grid from `Cache._elevationCacheGetRectAt`, which copies global elevation indices
  `k·gr … (k+1)·gr−1` out of each fragment — local `0…gr−1`, never local `gr` — and takes
  the shared boundary sample from the **next fragment's local index 0**. The stitched 21×21
  the mesh is built from is complete.
- **No city ever showed it either.** Every operator above the base refills its whole target
  from `GetElevationSegmentBelow`, which is that same stitcher, so
  `ClusterBaseElevationOperator` and `ClusterConformElevationOperator` both wrote the row
  from the neighbour. The hole survived only where the base layer IS the top layer, i.e.
  **outside every cluster** — which is also why "invisible in the flat city" was true for
  the wrong reason.
- **What it did reach is `CacheEntry.GetElevationPixelAt`**, which indexes
  `elevations[ey+1, ex]` directly. A point query in the last 20 m strip of a fragment
  interpolated between a real height and zero — measured as a gap of well over 100 m. That
  is `Loader.GetHeightAt`, so `ClusterDesc.GroundHeightAt`, `GetWalkingHeightAt`, the hover
  probe's terrain fallback and debris placement all read it, and outside cities they
  disagreed with the ground that is drawn.

So it was worth fixing, but it was never "the single highest-leverage item on this page",
and it has no bearing on cities at all.
`tests/JoyceCode.Tests/engine/elevation/ElevationGridCoverageTests.cs` drives the real
`Cache`, stitcher and `CacheEntry` for all four measurements and scans the loop bounds.

### ✅ (j) The kerb did not rest on the carriageway — FIXED 2026-09-02

> *"I still can observe a small gap between the bevel of the sidewalk and the street."*

The first report against the terrain-following city as the **default**, and the first item
on this page that is **exactly zero in the flat city** — 0.000 m at every percentile of all
four baselines, so it could not have been seen before the flip. Full write-up in
[`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7o.

**Four candidates were measured before diagnosing and three are not it.** It is not a plan
gap: a block corner is a section point of its junction and the two section points bounding
one stroke lie on the same offset of its centre line, so the kerb line and the carriageway's
edge are collinear to **0.0002 m at the median and 0.02 m at p99** over 2936 boundary edges.
It is not a missing face and it is not z-fighting — half a metre is thirteen times the
16-bit depth buffer's quantum at 50 m.

**It is a height gap and it is the junction footprint.** `_shearOntoSlope` lifted the road by
ONE window along the stroke's **centre line** — flat over each junction footprint, climbing
between — while the kerb is a straight chord between two **section points**. The two agree at
both ends, which is why §7c could fix the corners and leave this, and disagree in between:
p05/p95 **−0.57 / +0.55 m**, worst **6.49 m**, exceeding the whole 0.15 m kerb at **27–31 %**
of positions and half a metre at **11 %**, sign symmetric.

**Each side of the road now climbs between its own two corners** — `engine.streets.generation.RoadSurface`,
which also becomes the ONE expression for a junction's road height, replacing five copies of
it (two of which used `MetaGen.ClusterStreetHeight` and three `CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE`,
a different constant that is also 2.0, and two of which dropped the deck term). Exact, not
close: at either end the chord parameter is 0 or 1, and in between the axial coordinate is an
affine function of position along the chord. Residual **0.000–0.003 m** worst over four
cities on four grounds. The flat city and every ramp are unchanged float for float — a
straight junction puts both section points at the same axial distance, so both sides share
one window and the rule reduces to what was emitted before.

⚠️ **Found and NOT fixed, all plan-level and all from the same root:** 11 of 2477 block edges
in `Yelukhdidru`/3000 are not on their own stroke's edge at all (up to 62 m off), two strokes'
carriageways can overlap by 45 m, and 0.2 % of kerb positions are not covered by their own
carriageway (identical in the flat city). All three were written down to
`StreetPoint._computeSectionArrayNoLock`'s `dist2 > 4000` fallback for near-collinear arms.
**⚠️ That attribution is wrong and was corrected on 2026-09-03 — see (l) below and §7q.**

### ✅ (k) Half of the ordinary citizen's walk was in the road — FIXED 2026-09-03

Recorded under (d) as found and not fixed, and the last plan-level item this page carried.
Full write-up in [`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7p.

**What this page got wrong, in order of how much it mattered:**

1. ⚠️ **The angle condition is the other way round.** Both this page and §7m recorded *"at an
   interior angle over 90 degrees it lands past the arriving edge"*. It is **acute** corners
   that fail: the shipped point is on the inward side of the arriving edge exactly when the
   two inward normals agree, and their dot product is `-cos t`, which is positive above 90
   degrees. Measured over the four baselines: **0 of 1243 acute corners inside, 1667 of 1675
   obtuse ones inside**, and every one of the eight exceptions is a corner of 90.000-90.002
   degrees. The symptom could not distinguish the two readings, since the median corner is
   90.1-94.0 degrees and either reading predicts "about half" - which is how the direction of
   an inequality survived being written down twice.
2. **"The rest stand 1.5 m into the carriageway" is wrong, and the percentage was the wrong
   statistic.** The median waypoint sat **on the kerb line** (+0.00 to +0.11 m signed), 14 to
   38 % of them within 5 cm of it, with the worst excursion **1.15 m** rather than 1.5. Along
   the walk rather than at its corners, 10 to 17 % of positions were outside.
3. ⚠️ **Reusing the pavement's own inset ring - the obvious fix, and the one proposed - does
   not work, and both reasons were measured.** `SidewalkRing`'s points belong to EDGES and
   deliberately not to corners, and a loop turns corners: joining them cuts every corner, and
   at the 6 to 16 % that are reflex the cut leaves the block, by up to **11.07 m** taking one
   point per corner and **6.20 m** taking both points of each edge. And `SegmentNavigator`
   indexes `Quarter.GetDelims()` with a SEGMENT index, so two waypoints per edge is an
   `ArgumentOutOfRangeException`, not a renumbering.
4. **What landed is the corner's own mitre** - `engine.streets.generation.PavementWalk` -
   at half the block's own `SidewalkWidth`, capped at the 1.5 m that shipped, with its length
   bounded by one pavement width. 100 % of waypoints and 100 % of sampled path positions
   inside, on all four cities; the segment between two corners runs exactly parallel to its
   own kerb. §7k rejected the mitre for the pavement SURFACE because a shared vertex wants
   two heights; a walker is a point and has one.
5. ⚠️ **No baseline city contains a 1 m pavement** - 0 of 2918 corners - so the narrowest
   pavement the game can build is untestable from generated data. §7o's `seed008` lesson
   again, covered by fixture.
6. **§7m's height table for this walker was conditional.** Its ±0.23 m was measured over the
   59 to 70 % of waypoints that landed on a block floor at all; the rest were over the road.
   Every waypoint is on the floor now, at p05 -0.17 to -0.28 and p95 +0.12 to +0.29.

**Flat-game delta: every citizen's walk moves in plan**, median 1.13-1.50 m, worst 4.11 m,
and exactly 0.000 m where a corner's two edges are collinear. Nothing moves in height. Sixth
deliberate move of the default flat city, after §7i, §7j, §7l, §7m and §7n.

`engine.streets.generation.PavementWalk`, `SidewalkRing.MitreOf`/`InwardNormalOf`/
`SignedArea2Of`/`ContainsInPlan`;
`tests/JoyceCode.Tests/engine/streets/PavementWalkTests.cs` and
`builtin/tools/QuarterLoopRouteTests.cs`.

**Found and NOT fixed:** ⚠️ `SegmentNavigator` indexes a block's delimiters with a segment
index, which is already wrong for every route that is not the block loop -
`GoToStrategyPart` hands it a street route together with the citizen's own pod, so a route
longer than its block has corners indexes past the end of `GetDelims()`. And
`PedestrianRoute.SidewalkOffset` is still a constant 1.5 m, so the satnav walker keeps the
width half of this defect (not the side half, which (g) fixed).

### ✅ (l) A junction corner was on neither of its own streets — FIXED 2026-09-03

Recorded by §7o as *found and NOT fixed*, and the last plan-level item this page carried.
Full write-up in [`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7q.

**What this page and §7o got wrong, in order of how much it mattered:**

1. ⚠️ **The branch both blamed is innocent, and its own code comment was right.** §7o wrote
   the eleven off-line block edges down to `_computeSectionArrayNoLock`'s `dist2 > 4000`
   fallback *"for near-collinear arms"*, and the brief for this round reasoned it must be a
   hairpin — two arms at a shallow angle, where the mitre `w/sin(θ/2)` runs away. Measured
   over five generated cities and 4552 section points: **all 46 corners that take the
   fallback, and all 427 that take the parallel branch, are at 179.9999–180.2983°**, which is
   the *straight-through* case the shipped comment claims (*"these are pretty in-line
   streets"*), and the averaged offset it substitutes lands **on** both edge lines there.
2. ⚠️ **The ill-conditioned-normalise hypothesis is refuted by one number.** The fallback
   normalises `nc − np`, whose length is `2 sin(θ/2)`; it was measured at **2.0000, its
   maximum**, in every one of the 473 cases. The vector is as far from zero as it can be.
3. **The defect is `geom.Line.IntersectInfinite`.** It solves by Cramer on homogeneous line
   coordinates whose constant term is `A.Y*B.X − A.X*B.Y`; 3 km from the origin that is 2·10⁶
   and the solve forms a difference of two products of 2·10⁸, so for two nearly parallel lines
   every significant digit cancels. The answer comes back **6.3 to 56.3 m from the junction —
   inside the 63.2 m guard, and accepted** — and up to **27.3 m off both of the lines it is
   supposed to be the crossing of**.
4. **Five of the eleven are not this at all.** Their corner is on its own edge lines to
   10⁻⁵ m; what is wrong is that the block ring **skips a junction**, so `delims[i].Stroke`
   is not the street that edge runs along. That is `QuarterGenerator`'s trace, it is untouched,
   and it is the new *found and not fixed*.
5. **Clipper's default mitre limit of 2 is not the number**, and it was measured before being
   discarded: over 7544 corners it cuts back 888 of them — one in eight — and leaves **1018
   block edges more than 0.25 m off their own carriageway**, which is worse than the defect.
   The limit is 3, which cuts back 42, of which 32 are degenerate however high the limit goes.
6. **The bevel — two corners at an over-long junction, one on each arm's own edge line — was
   costed and not built.** It is the only construction that puts *every* corner on both lines
   exactly, but the section array's length is contract: `QuarterGenerator` and
   `GenerateNavMapOperator` both index arms and sections together, and every block cornering
   on such a junction would gain a corner. For ten corners in eight cities that is not the
   trade.

**Both cities move.** Section points: median 0.00012 m, p95 0.0095, worst 56.07 m; 40 of 6084
move more than a metre. On `Yelukhdidru`/3000, **27 blocks of 445 have a corner that moves
more than 5 cm and six move by more than a metre** — their estate, footprint, building, shops,
TALE locations and nav crossings with them. Seventh deliberate move of the default flat city,
after §7i, §7j, §7l, §7m, §7n and §7p.

⚠️ **`street-geometry.json` moved for all five recorded cities** — four by sub-millimetre
rounding with every vertex and index unchanged, and `seed000@1500` by 12 vertices and 18
indices. Old and new hashes are recorded in §7q. **No network fingerprint moved.**

`engine.streets.generation.SectionMitre`, `SidewalkRing.MitreOf`;
`tests/JoyceCode.Tests/engine/streets/SectionMitreTests.cs`.

**Found and NOT fixed:** the five block rings that skip a junction (above, with three worked
examples); kerb coverage, which is still 0.2–1.2 % and went slightly *up* on two of ten cities
because the corrected corners move onto ground the carriageway does not reach;
`geom.Line.IntersectInfinite` itself, which now has one caller left in the shipping tree and
is a boolean rectangle test.

### ✅ (m) The satnav guideline cut across the road's profile — FIXED 2026-09-05

> *"the navmesh being partially below the street. Seems logical to me if we draw navmesh
> streetpoint to streetpoint, without considering the flat junctions, whereas the street
> level has a flat junction between."*

**The owner's own diagnosis, and it is right** — the first time on this page that the first
diagnosis has been. Full write-up in
[`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7r.

1. **There is no navmesh.** `engine.joyce.components.NavMesh`, its emission block in
   `GenerateClusterStreetsOperator` and `GenerateClusterNavLanesOperator` are all inside
   `#if false`, and the navmesh vertex emission inside `_generateStreetRun` is commented out.
   The one thing in the tree that turns a nav lane into geometry is
   `engine.quest.ToSomewhere._onJunctions`, i.e. the **satnav guideline** §7g brought down
   off the hover height. Confirmed by exhausting the callers of `AddQuadXYUV` and
   `Mesh.CreateListInstance`, not assumed.
2. **The size, measured over five cities on the shipped terrain at 984 000 positions:**
   median **0.076–0.191 m**, p95 0.45–0.93, p99 0.57–1.33, worst **2.45 m**, and **below the
   road at 48–53 % of positions**. So half the guideline was inside the road, and the 0.1 m
   lift §7g derived was used up at a quarter to a third of positions. **Exactly 0.000 m at
   every percentile in the flat city**, like (j) and unlike everything before it.
3. ⚠️ **A lift is not a licence to be wrong by less than it.** The median error was larger
   than the whole lift. That is the general lesson, and it applies to every other constant
   margin in this tree.
4. **The fix is the §7l/§7o/§7p pattern**: a car `NavLane` now carries the
   `engine.streets.generation.RoadSurface` its stroke's carriageway was emitted from, so
   ribbon and road agree **by construction** rather than by two expressions being kept in
   step. The four section points bounding a carriageway were hoisted out of
   `_generateStreetRun` into `RoadSurface.TryCornersOf` so that both read the same ones.
   After: median **0.002–0.021 m**, p95 0.16–0.25, p99 0.25–0.46.
5. ⚠️ **The residual is the ROAD's own tessellation, and that is the new open item.** The
   surface reproduces the road mesh at all 32 680 of its own vertices to 0.000000 m at the
   median and 8·10⁻⁶ m at p99; what is left is that a carriageway's rows are
   `StreetWidth() * 4` — up to **88 m** — long, so the two triangles a row is cut into
   deviate from the surface they are cut from by up to **0.92 m** mid-row. Invisible at the
   kerbs, which is why §7o measured 0.003 m. Shortening the rows moves `street-geometry.json`
   for every recorded city, so it is a decision and not a fix.
6. **The pedestrian ribbon does NOT share the defect**, and the two are not symmetric. A
   pavement lane's chord IS the block floor's outline, identically — measured over 105 000
   samples at **2.3·10⁻⁵ m worst**. A crossing is level at its junction's walking height, one
   kerb above the cap it crosses, deliberately.
7. **No other consumer of lane Y is affected**, searched rather than assumed: cars hover on a
   raycast and never touch a lane; the citizen walker uses `PavementWalk` and
   `BuildingFooting`; the satnav walker uses a *pedestrian* lane; `PipeController` is built
   from the pedestrian network; and lane `Length` feeds a cost, not a height.

**The flat city does not move** — one quad per lane at the same floats, asserted as
`Assert.Equal` over five whole cities. ⚠️ **One thing in the terrain city does move that is
not the ribbon:** `RoadSurface` interpolates through `Single.Lerp` now so that a corner over a
junction cap is that junction's height *as a float*, which moves **7–12 % of emitted road
vertices by at most 7.63·10⁻⁶ m**, one ulp at 60 m. **No `street-geometry.json` entry and no
network fingerprint moved, and no baseline file was rewritten.**

`engine.streets.generation.RoadSurface.TryCornersOf`/`OfStroke`/`SurfaceHeightAt`,
`NavLane.Surface`, `GenerateNavMapOperator.ContentOf`, `RouteRibbon.QuadsFor`/`MeshFor`,
`joyce.mesh.Tools.AddQuadCornersUV`;
`tests/JoyceCode.Tests/builtin/modules/satnav/RouteRibbonRoadTests.cs`.

**Found and NOT fixed:** the 88 m rows above — ✅ **fixed 2026-09-05, see (n) below, where
both halves of this description turned out to be wrong**: it is not row length on its own but
row length times the two sides' slope difference, and shortening the rows did **not** move
`street-geometry.json`, because a flat city has no slope difference to bound. `seed008`'s
overlapping junction footprints, where two caps give the road two heights 1.25 m apart at the
same place, are still open; so is `RoutePlan`'s truncation junction, whose synthetic
`GroundHeight` is the chord's rather than the road's (nothing reads it).

### ✅ (n) The road mesh does not represent its own surface — FIXED 2026-09-05

§7r's own leftover, and the last thing between the guideline and the carriageway. Full
write-up in [`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) §7s.

1. ⚠️ **"The error is concentrated where a row STRADDLES a kink" is refuted by one count:
   0 of 4 608 row spans contain a section point strictly inside them, on any of the five
   cities.** They cannot — the rows run from the further of the two A section points to the
   nearer of the two B ones, which is inside both sides' climbing windows by construction. So
   "a row AT each break" was not the fix.
2. ⚠️ **Nor is it row length on its own.** A carriageway is ruled between two kerb chords of
   different slope, i.e. a hyperbolic paraboloid; a row quad's two triangles depart from it by
   `length × |slopeRight − slopeLeft| / 4`. Measured per row span, the prediction and the
   measurement agree to three decimals at every percentile — median 0.039 m, p95 0.25 m,
   worst 1.000 m. Because the second factor is exactly zero in a flat city and on every
   straight stroke, the fix could be gated on it and **the flat city did not move**.
3. ⚠️ **The worse half was the one nobody had looked at, and it needed no geometry.** The end
   **wedge** — the single triangle between a junction's seam and the first full-width row —
   was worse than the rows at every percentile (median 0.064 m, p95 0.36 m, worst 0.90 m).
   Its three corners are fixed by the seams, and three corners admit exactly one plane, so
   there was no tessellation to refine: the *model* was wrong. Now 0.00003 m, at zero vertex
   cost.
4. ⚠️ **§7r's "the residual is the ROAD's tessellation, not the ribbon's" is wrong.** With
   the road held to 0.02 m the guideline was still 0.85 m off it at the worst position of
   five cities and 0.22 m at p99 — all of it the ribbon's own 4 m-wide quads, which carry the
   same twist in proportion to their width, and its own missing break where each of its two
   edges crosses a junction's seam obliquely.
5. **No vertex and no index of `street-geometry.json` moved** for any of its five recorded
   cities, and no network fingerprint moved. The cost is 46–112 % more road vertices in the
   terrain city (worst fragment 748 → 1 448) and a guideline of median 4 quads per lane
   instead of 3.
6. ⚠️ **Its five HASHES did move, and for a reason that has nothing to do with roads.**
   Mutation testing found that swapping two indices of every carriageway row passed the whole
   suite: `StreetGeometryFingerprint` hashed the vertices and merely *reported* the index
   count, so it could not see a triangle turned inside out — which is §7j, where exactly that
   happened to the pavements and nothing failed. The indices are hashed now; `v=` and `i=`
   are identical on all five, which is the evidence that no geometry moved. Old and new
   hashes are in §7s.
7. **Found on the way and fixed:** `RoutePlan` replaces the truncated last lane of every
   route with a NEW `NavLane` and copied only three of its fields, so the last lane fell back
   to the chord — §7r's defect on the segment nearest the destination, and §7r's own note on
   this said the lane was *"the real one"*. It is not.

`RoadSurface.MaxSag`/`MaxRowSpan`/`MaxSpanAcross`/`BreakpointsBetween`,
`GenerateClusterStreetsOperator._streetRow`, `RouteRibbon.BreaksAlong`;
`tests/JoyceCode.Tests/engine/streets/RoadTessellationTests.cs`.

**Found and NOT fixed:** the seam's obliquity inside one ribbon quad — the remedy is known
(split that one quad along its *other* diagonal, which is the seam) and costs no vertices, but
needs a per-quad diagonal choice `AddQuadCornersUV` does not offer; §7o's skew strokes, where
the mesh's rows and the mesh's kerbs are two different lines, now counted and bounded rather
than hidden behind a percentile; and `seed008`'s filler quads, which are now the worst thing
in the measurement by a factor of three.

### ✅ (i) The `#if false` operator and the missing drift test — the test now exists

`GenerateHouseDescriptionsOperator.cs` is inside `#if false` from line 1 and compiles to
nothing, despite being described in CLAUDE.md as a live consumer. And
`CharacterAnimationDriverTests.cs` — the drift test CLAUDE.md says guards the T-pose fix —
**existed in no commit on any branch**. Both corrected in CLAUDE.md on 2026-08-30; the drift
test was written on 2026-08-31 with (d1), and **not** to the criterion CLAUDE.md described,
which the broken site would have passed.

---

# Part 1b — Reported 2026-09-06, CLOSED 2026-09-07

## ✅ (o) A block outline is not always made of streets, so a building stands on one — FIXED 2026-09-07 (WP-O1, WP-O2, WP-O3)

Reported from play of the shipped world (both flags on their defaults):

> *"I do see a street trunk running off into a building, forward in direction of the
> viewer."*

**Cause established, size measured, NOT fixed — the repair is a design decision.** Full
write-up and every number in `STREETS-3D-TOPOLOGY.md` §7t.

**Both obvious suspects are refuted.** Grade separation is not it — the owner re-ran with
`joyce.EnableGradeSeparation=false` and it still appears, and independently **not one
building of the seventy shipped cities overlaps a `Ramp`, `Bridge` or `Tunnel` by any area
with the flag either way**, against 2211 structure strokes (so WP-B5's §14.4 exclusion,
measured on 20 structures, holds over the world's 737). Nor is the terrain-following city:
the flag-off plan network is the **same graph on all seventy cities** flat or on the
shipped terrain, asserted position by position, so every flag-off count here is the **flat
city** and this predates the whole work stream on this page.

**What is wrong.** `QuarterGenerator.Generate()` stops its face walk on `spNext == spStart`
— a **vertex** test, where a face walk must stop on the (junction, outgoing stroke) **pair**
it started from. A face passes through one junction twice whenever a dead-end spur cuts a
slit into a block; when that junction is the one the walk started at, the walk stops half
way round and the ring is closed by a straight **chord** back to the first delimiter. That
chord is not a street — median 75 m flag off, 117 m flag on, up to 147 m — and the estate,
its inset and the building on it go across whatever the chord ran over.

**Measured over the shipped world** (`GenerateClustersOperator`'s own cluster list, seeded
`"mydear"`, on the shipped terrain), flag off → flag on:

- rings that do not close: **219 of 36 327** → **274 of 33 432**, and **every one of them
  is broken at the wrap-around edge and nowhere else** (219/219, 274/274) — which is what
  separates this from §7e, whose delimiters were wrong at *every* edge;
- of the broken rings that carry a building, the building is over a `Street` carriageway in
  **146 of 146** and **190 of 191** cases, p50 622 → 1503 m², worst 2042 → 2458 m², in
  **49 → 51 of the 70 cities**;
- the control: blocks whose ring **does** close contribute **10** and **1**, and the ten
  are 1–13 m² corner slivers. So this is a correspondence, not a correlation.
- the junction **cap** is not a second cause: 2 and 8 extra cases, all already overlapping a
  stroke box.

⚠️ **WHAT THE STREET IS — the owner's follow-up names it**: *"the road stem went right into
a building… I don't know if it was a legitimate dead end, at least not legitimate judging
from the building on it."* Both halves are one thing, and §7t.2 already contains the reason:
a face is pinched at a junction **because a dead-end spur cuts a slit into the block**, the
truncation drops the slit, and the estate becomes a solid polygon over ground the spur
occupies. It **is** a legitimate dead end; the building on it is not. The spurs whose stub
carriageway lies under a building are **146** and **190** — the very counts above, so **it
is one class, not two**. ⚠️ **And the obvious second mechanism is EMPTY**: *"a spur enclosed
by a block whose ring closed correctly"* would need no chord and would be a separate defect
with a separate fix, and it happens to **0 of 9840 spurs flag off and 2 of 8100 flag on**;
every other enclosed spur (222 / 272) is inside a **broken** block. ⚠️ **That also re-reads
§14.2**, which found *"four junctions inside three blocks of `Yelukhdidru@3000` and two
inside two of `seed017@2400`"* and called them unrelated: those two seeds carry 3 and 2
broken rings, and 0 spurs of the world sit inside a block that closed, so §14.2's six are
**this defect seen from the other side**.

⚠️ **AND IT IS NOT A ONE-LINE FIX.** Completing the walk properly gives a face that visits a
junction twice (274 of 274) and contains a junction with fewer than two block arms (219 of
219, 272 of 274) — i.e. a face `hasNullSection` **discards**. So terminating correctly does
not repair these blocks, it **deletes** them: 219 / 272 blocks become holes with no estate,
no building and no pavement. The early termination is a *discard that failed to happen*.

⚠️ **The context is two orders of magnitude bigger.** **A third of the street graph's
directed block edges are already in faces that get thrown away** — 101 113 of 304 150
(33.2 %) flag off, 64 790 of 228 348 (28.4 %) flag on — because 8.8 % of junctions are
dead-end spurs and every face touching one is refused. Pre-existing and silent.

**THE DECISION.**

The 0-and-2 count above is what separates them: **(a) removes essentially the whole
reported class**, and (b) is a larger change wanted for its own reasons rather than for
this one.

- **(a) Terminate on the directed edge.** One line, correct by construction. Costs 219 /
  272 blocks (0.6 % / 0.8 %) as holes in the pavement instead of buildings across roads.
  Moves **no recorded baseline file** — `street-geometry.json` pins five cities and only
  `seed008@500` carries one, flag on, which nothing records — but moves the block census in
  `BlockGraphTests` on four flag-on seeds and two flag-off ones. **Leaves behind**: the 0/2
  spurs in a closed block, the 10/1 sliver overlaps, and the whole 33 %.
- **(b) Peel the block graph to its 2-core first**, so a dead-end spur is not a block edge
  at all. The face then has no pinch, the ring closes **and the block stands**, going round
  the spur; it also recovers much of the 33 %. ⚠️ **But it cannot be done without the estate
  exclusion, and that is not optional**: a 2-cored block contains its spurs by construction,
  so without subtracting them the way `BlockGraph.ExcludeStructures` already subtracts a
  ramp, (b) produces **exactly the reported picture on far more blocks than the 222 that
  have it today**. With the exclusion it is strictly better than (a); without it, strictly
  worse. `BlockGraphTests` already computes a 2-core for its own assertion.

⚠️ **What is NOT established: the sighting itself.** The start city
(`cluster-clusters-mydear-0`, 1000 m, named `Yelukhdidru`) has **no broken ring, no building
over a street, and no spur inside any block, in either flag state**; the nearest building to
any of its twenty dead-end tips is **43 m** away. Its objects at the two reported positions
are named in §7t.7. **The reproduction was then checked rather than trusted** — the first
cluster is hard-coded `Size = 1000f`, generation reads only `Size`, `Id` and a fresh `Rnd`,
the shipped ruleset is value-identical to the defaults with a gate saying so, and
`HouseInstanceGenerator` only ever *shrinks* its footprint, so the drawn house is inside the
polygon every measurement uses. **The class is established and the instance is not.** Ask
the owner for the screenshot's heading, and whether the position and the picture are from
the same session.

Gate: `tests/JoyceCode.Tests/engine/streets/BlockRingClosureTests.cs` (19), which records
every number above so that whichever repair is chosen has to move them deliberately.

### ⚠️ Second round, 2026-09-07: notch or split, and how big (b) really is

The owner proposed a third policy on top of repair **(b)** — when a spur cuts a slit into a
block, decide per block whether the estate is ONE piece with a notch or TWO pieces either
side of the spur — with the claim that this needs no new constant because the pavement inset
already decides it. **Measured, still not fixed; every number in `STREETS-3D-TOPOLOGY.md`
§7t.10.**

- **Mostly-notch, and it is not close.** Built exactly as `_createBuildings` builds it (inset
  by the block's own `SidewalkWidth`, then the spur corridor widened by the same width
  subtracted, `ExcludeStructures`' own rule): **one piece on 4757 of 4778 blocks flag off and
  4289 of 4439 flag on** — 99.6 % / 96.6 %. Two on 21 / 142, three on 0 / 8, empty on 0 / 0.
- **No threshold constant is needed** — inset, then count polygons — **but the ORDER is
  worth eight times the geometry.** Subtracting first and insetting the remainder insets the
  notch too and splits **163 / 646** blocks. If (b) is built it goes where
  `ExcludeStructures` already is, *after* the inset. Nothing in the tree states which order
  is intended.
- ⚠️ **(b) is twenty times bigger than the 222 / 272 above.** Those count spurs inside a
  *stored* block, and a third of all faces are discarded, so most spurs stand in ground with
  no block at all. The 2-core gives every one a block: **4778 / 4439 blocks, of which 4559 /
  4167 are holes in the pavement today.** The control: 36 113 spur-free 2-cored faces against
  36 108 stored quarters whose ring closes — five apart, and twelve on the flag-on city.
- ⚠️ **The structural claim is half wrong and the conclusion survives it.** The bare
  carriageway alone, no inset, already splits **9 / 11** blocks, so *"a dead-end spur cannot
  split a polygon by subtraction alone"* is not universal. And no one-piece block has its tip
  land bridge closed (0 of 4757, 0 of 4289) — the half that matters — but **12 of 21 and 139
  of 150 splits happen with the tip gap wide open**, pinched along the corridor's flank
  instead. Counting polygons does not care where the split is; the reasoning was wrong, the
  rule is not.
- **Only the split verdict is fragile.** A notch needs the pavement +9.4 / +7.1 m wider at
  the median to become two; a split needs only −2.21 / −0.73 m, and **4 of 21 and 58 of 150
  flip on half a metre**, 3 and 15 on a decimetre. So the notch is the default and a split is
  the geometry insisting.
- **The second estate is worth having** (median 2412 / 1671 m², one storey in 2 / 11 cases,
  `mn == 0` never reached) — ⚠️ **but `BuildingFooting.BaseHeightOf` takes the BLOCK's lowest
  corner** on the stated grounds that a block carries one estate, and splitting buries the
  higher piece by a median **7.46 / 6.21 m**, worst 21.2 / 28.1 m. Two estates need two
  footings; on a flat city this costs nothing, which is why it was measured on the shipped
  terrain.
- **A quarter of these blocks hold more than one spur** (1161 / 811, up to 8 and 9 tips), so
  *"one estate either side"* would mean three or more estates a quarter of the time with no
  way to say which side is which.

Gate: `tests/JoyceCode.Tests/engine/streets/{SpurBlocks,BlockSpurEstateTests}.cs` (18).
Nothing under `JoyceCode/` was touched.

### ✅ WP-O1, 2026-09-07: the block trace runs over the 2-core — and the symptom is WORSE on purpose

The owner chose repair **(b)** in full, as three work packages. **WP-O1 is the peel and the
termination; the estate subtraction (WP-O2) and the footing (WP-O3) are not in it.** Every
number in `STREETS-3D-TOPOLOGY.md` §7u.

- **The defect closes.** `BlockGraph.TwoCoreOf` peels every junction with fewer than two
  block arms, iteratively; `AcceptWithin` is the one predicate the trace's start filter and
  `GetNextAngle` both take; and the walk terminates on the **(junction, outgoing stroke)
  pair**. Rings that do not close: **219 → 0** flag off, **274 → 0** flag on.
- **A third of the city comes back.** Directed block edges in a stored block go
  **203 037 (66.8 %) → 260 283 (85.6 %)** and **163 558 (71.6 %) → 198 275 (86.8 %)**;
  what is discarded falls from **33.2 % → 5.0 %** and **28.4 % → 4.7 %**, and what is left is
  the outer face of each of the seventy components and nothing else. Quarters
  **36 327 → 40 891** and **33 432 → 37 609**, exactly §7t.10.1's prediction from an
  independently written face walk.
- ⚠️ **THE OUTER FACE NEEDED A RULE OF ITS OWN and nothing in the plan said so.** It used to
  be refused for a reason unrelated to being outside — it ran through some spur on the city's
  edge, so `hasNullSection` caught it. Peel the spurs away and it is a perfectly good closed
  ring; without `BlockGraph.IsInteriorFace` the generator stores one "city block" per
  component covering the whole city. The rule is the winding, measured: exactly 70 faces of
  40 961 wind the other way, one per component, and each is the largest face in its city.
  Deliberately not `SidewalkRing.SignedArea2Of` — float, untranslated, and the world puts
  cities 36 km out; the mutation that swaps it in fails 3.
- ⚠️ **WP-O1 ALONE MAKES THE REPORTED SYMPTOM TWENTY TIMES WORSE, and that is the plan.** A
  2-cored block contains its spurs and nothing subtracts them yet: buildings over a `Street`
  carriageway **156 → 3247** flag off and **191 → 2986** flag on, spurs with their stub under
  a building **146 → 4328** and **190 → 3696**, in **69** and **68** of the 70 cities. That is
  WP-O2's positive control, asserted in `TwoCoreBlockTests`.
- ⚠️ **AND IT IS NOW REPRODUCIBLE AT THE START CITY, which §7t.7 said it was not.**
  `cluster-clusters-mydear-0` had no building over a street in either flag state; it now has
  **5 flag off (worst 1231 m²) and 4 flag on (worst 1630 m²)** over 19 and 21 spurs. WP-O2
  takes it away again. It still does not explain the ORIGINAL sighting, which predates today.
- ⚠️ **THE FINDING: the block corner across a skipped arm is not WP-O1's, and six gates could
  not see it.** Where the block graph skips an arm the two arms a block turns between are not
  adjacent in the junction's angle array, so the corner is the mitre ACROSS the skipped one
  and is not a section point at all. **WP-B5 §14.3 established that for a ramp foot and it has
  shipped since 2026-09-06**; it was invisible because `QuarterFloorTests`,
  `QuarterFloorFacingTests`, `PavementCrossFallTests`, `KerbSeamTests`,
  `PedestrianCrossingTests` and `QuarterLoopRouteTests` all build a **flag-off** city, and
  `NoBlockOutlineCrossesItself` omits `seed008@500`, the one pinned seed that already had a
  self-crossing outline from it. Measured on the pre-WP-O1 tree, not inferred. Three
  consequences: the pavement stands **vertical, never back-facing** (`n.Y` is exactly
  0.000000, so the §7j class is intact); the **kerb leaves the carriageway** at such a corner
  and at its NEIGHBOUR too, so 22–25 % of the sampled kerb now carries a 2 m bound instead of
  1 cm, worst 1.674 m; and one self-crossing outline flag on. **Found and NOT fixed** — the
  repair is `RoadSurface.TryCornersOf` ending a carriageway at the corner the block turns at,
  which moves the road mesh and `street-geometry.json`.
- **NOT ONE BASELINE FILE MOVED**, including `street-geometry.json`, which the owner had
  authorised: it pins the ROAD mesh, built from the section arrays and not from the quarters.
  **`ClusterStorage.DbVersion` is NOT bumped**, checked: only `Stroke` and `StreetPoint` are
  persisted and `_findQuarters()` runs on every start regardless.
- **The gate worth more than the counts is Euler**: `quarters == E − V + C` over the 2-core,
  per seed and per flag, which says at once that no face is missed, none is discarded for
  `hasNullSection`, and only the outside is refused.
- **Mutations: eleven, one survivor** — deleting the `hasNullSection` discard, which the peel
  makes unreachable (kept as the backstop it always was; the Euler gate is what proves it
  equivalent rather than untested). Reverting the termination to the vertex test still fails
  **4**, because the peel does not remove a **bridge**: 31 stored blocks flag off and 10 flag
  on have a ring that passes through one junction twice.
- **Ten gates superseded with their old text recorded**, listed in §7u.10. Tests:
  `tests/JoyceCode.Tests/engine/streets/TwoCoreBlockTests.cs` (37 new; **1763 xUnit** against
  1744, TALE 200/200).

### ✅ WP-O2, 2026-09-07: the spur comes out of the estate — THE REPORT CLOSES

Every number in `STREETS-3D-TOPOLOGY.md` §7v.

- **The reported symptom is gone.** Buildings over a `Street` carriageway: **3247 → 13** flag
  off and **2986 → 0** flag on, and **0 of them are on a spur** in either state. Spurs whose
  stub carriageway lies under a building: **4328 → 0** and **3696 → 0**. Buildings over a
  `Ramp`, `Bridge` or `Tunnel`: 0 → 0. **The start city — `cluster-clusters-mydear-0`, the
  city the owner plays — goes from 5 and 4 to 0 and 0.**
- ⚠️ **WHAT IS LEFT IS NOT THIS CLASS, and §7t already counted it.** The 13 flag-off
  residuals all overlap a street their own block **runs along**, by **0.9–13.4 m²**, on
  blocks with a 2 m pavement. §7t measured exactly this before WP-O1 and called it *"1–13 m²
  corner slivers"* on blocks whose ring closed — **10 flag off and 1 flag on**. Same size,
  same shape, same rate against 12 % more blocks. It is the estate's inset meeting the
  carriageway rectangle near a junction and it predates all of this. The gate names it
  rather than rounding it into zero.
- **How it is done, with no new constant.** `BlockGraph.SpurCorridorsOf` names every block
  edge the peel removed — the exact complement of `AcceptWithin`, asserted per stroke, so a
  road the trace runs along is never subtracted and a road it refuses always is.
  `BlockGraph.ExcludeCarriageways` is the one expression for *this road is not buildable
  land*, and `ExcludeStructures` is now its structure-filtered wrapper: a ramp and a spur are
  removed by the same two lines with the same margin, the block's own `SidewalkWidth`.
  `QuarterGenerator.BuildableLandOf` is inset → subtract → count polygons, and
  `_createBuildings` designs **one building per polygon**. `BlockGraph.LargestOf`, the
  concatenation and its `TXWTODO` are deleted.
- **Mostly-notch, and the flag-off half lands on §7t.10's prediction.** Blocks that hold a
  spur come back in one piece **4757 of 4780** flag off, against §7t.10's independently
  reconstructed **4757 of 4778** — two blocks apart, which is the control saying the
  measurement and the shipped rule are the same rule. ⚠️ **The flag-on half does not land on
  it and the reason is stated**: §7t.10 subtracted only the spur, while a flag-on block may
  also hold a ramp, a deck or a bore, and **348** of these blocks are already in more than one
  piece before a spur is subtracted at all.
- ⚠️ **THE ORDER IS THE ONE THING THAT COULD HAVE BEEN GOT WRONG SILENTLY.** Both orders drive
  the report to zero; they differ only in how often the block splits — 21/150 against 163/646.
  It is gated as a **threshold**: on a square block the shallowest spur that still leaves one
  piece is (tip → boundary) − 2 × `SidewalkWidth` this way and − 3 × the other, measured, and
  the same fixture pins the corridor margin.
- ⚠️ **TWO THINGS REMOVING `LargestOf` COULD HAVE GOT WRONG.** (a) Clipper's flat path list
  puts a **hole** in the same list as its piece, so "one building per polygon" would have put
  a house exactly on the road that made the hole — `ExcludeCarriageways` takes a `PolyTree`'s
  top-level contours and **0 holes** is asserted over both worlds. (b) A building might not be
  a piece at all, so the gate is an **identity**: its points ARE one of `BuildableLandOf`'s
  polygons, reversed, corner for corner. 0 exceptions.
- **Recorded and NOT asserted at zero:** 1 building of 27 403 flag off and 2 of 26 054 flag on
  have an outline that **touches itself** — a Clipper pinch at the decimetre, and two of the
  three come back identically with no spur subtracted, so it is the inset's own pinch.
- **NOT ONE BASELINE FILE MOVED** and **`ClusterStorage.DbVersion` is NOT bumped**, both
  re-checked rather than carried over from WP-O1. Quarters and estates do not move on any
  pinned seed; buildings and shops do (flag-on `Yelukhdidru@3000` flat 100 → 173 buildings,
  which is `LargestOf` going away rather than the spur).
- **Mutations: eleven, none surviving in the final state — and the two that were expected to
  survive are both findings.** ⚠️ *the flat path list instead of the PolyTree's contours*
  survived the first round, because the two are the same list exactly when the hole count is
  zero — the very thing the gate asserts, so no real data can kill it; a fixture with a road
  that stops short of the block on BOTH sides now reaches the branch. ⚠️ And *ignoring the
  AABB prefilter* was predicted to be an equivalence and is not: `ExcludeCarriageways` hands
  back the SAME list when there is nothing to subtract, and once a distant structure gets past
  the filter that short-circuit stops firing and Clipper re-emits the contour.

### ✅ WP-O3, 2026-09-07: a building is founded on its own piece of ground

Every number in `STREETS-3D-TOPOLOGY.md` §7w.

- ⚠️ **THE FIX THIS WORK PACKAGE WAS BRIEFED TO BUILD IS NOT A BOUND, WHICH IS THE FINDING.**
  The brief asked for the per-piece bound to be *"the lowest of the piece's own boundary
  heights"* — `BuildingFooting.GroundAt` at each footprint corner. Ledger item (a)'s argument
  is that every vertex of the block floor's cap carries a corner height or a blend of two of
  one edge's pair, so the surface cannot leave the block's corner range — and **that is a
  statement about the whole block and says nothing about a sub-region of it**, because the
  cap's INTERIOR is one tessellation of the ring inside the pavement rim and the tessellator
  is free to run a triangle clean across the block. Measured, the cheap rule leaves **5402
  footprint corners in the air flag off and 4842 flag on** over the seventy shipped cities,
  worst **1.58 / 5.61 m**, and floats **0 / 2 / 28 / 45** buildings on the four pinned
  baselines by up to **9.77 m** — which is ledger item (a)'s own sighting put back.
- **So the answer is read off the surface.** `engine.streets.generation.BlockFloor` is the
  block floor's cap as it is DRAWN — through the very `ExtrudePoly.BuildCap` the emission goes
  through, hoisted out of `BuildGeom` so there is one expression and not two — and
  `TryBoundsOver` answers the EXACT lowest and highest the floor gets over a polygon. It is a
  bound rather than a sample because it is a minimum over the whole footprint, and it is the
  tightest one there is. `BaseHeightOf`, `HeightOf`, `StoreyAt` and `StoreyGroundAt` all take
  the `Building` now and **the block-wide overloads are deleted**, so a call site holding only
  a `Quarter` does not compile.
- **The owner's design decision is untouched**: floors stay planar, shopfronts stay aligned per
  storey, and the footprint-following base offered in item (a) stays rejected. What changed is
  *which* scalar a planar floor sits at.
- **THE HEADLINE.** Over-sink — how far the floor's own minimum over a footprint stands above
  the block's lowest corner, i.e. what the block-wide bound cost and what this removes:
  p50 **0.445 / 0.256 m**, worst **15.20 / 33.09 m** over 27 403 / 26 054 buildings; and per
  block carrying more than one building, the worst of its buildings is p50 **5.84 / 5.00 m**,
  worst **15.2 / 33.1 m**, over 5 m on **8 of 13** and **397 of 793**. **Afterwards it is
  0.000 m**, because the base IS the minimum.
- ⚠️ **§7t.10.7 PREDICTED 7.46 / 6.21 m AND 21.2 / 28.1 m** from a nearest-corner
  reconstruction; the production pieces give **5.84 / 5.00** and **15.2 / 33.1**. The medians
  come in about 20 % lower because a piece's real floor minimum is on its own boundary rather
  than at its nearest block corner; the flag-on **worst is higher** than predicted.
- ⚠️ **AND ITEM (a)'s OWN STANDARD IS ONLY HALF MET BY THE WORLD.** It recorded *"0.19–0.61 m
  at the median, 3.74 m at the worst building of the four cities"*. The median lands inside
  that band; the worst over seventy cities is **15.2 / 33.1 m**. The four-city median described
  the world and the four-city worst did not.
- **THE GUARANTEE, over the whole shipped world**: no corner of any footprint stands above the
  floor under it — **0 of 192 676 flag off and 0 of 149 556 flag on**, worst float 0.000 m,
  0 fallbacks over 40 891 / 37 609 blocks. On the four pinned baselines the same is an
  **identity** against `DrawnBlockFloor`, which reads the cap back out of the emitted mesh
  through a different call and picks it out by position.
- ⚠️ **THE THING THAT MOVED THAT NOBODY ASKED ABOUT: a shop's storey.** `StoreyGroundAt` snaps
  a shopfront to whole storeys above a reference, and "the block's lowest corner" and "the
  building's own floor" stopped being one sentence at WP-O2. Reverting the reference passes
  every other gate, because both keep the shop within one storey OF THE PAVEMENT, which is
  what the reachability gates ask; what it breaks is alignment to the building. Gated at
  **0 of 572 404 / 527 807 shopfronts** off their own building's storey grid.
- ⚠️ **ONE TERM OF THE MINIMUM CANNOT BE KILLED BY REAL DATA, and two guesses at why were
  refuted by counts.** The cap's own corners inside the polygon are needed for the enumeration
  to be exact. *"No cap vertex is inside a footprint"* is false — **164 924 / 114 207** are.
  *"Such a vertex is never the minimum"* is also false — **432 / 414** are. What holds: it lies
  ON the footprint's boundary (Clipper rounds to the decimetre), so a crossing answers with the
  same height at the same place. Kept, with a fixture that hands `TryBoundsOver` the block's
  bounding box grown by a metre, where nothing else can answer.
- **THE FLAT CITY DOES NOT MOVE AT ALL**, asserted as equality: base, height, storey index
  exactly 0, and the shopfront quad, POI and door on the floats they land on today. The blend
  is written as an offset from one corner rather than a weighted sum of three, because the
  weights add to one only to within a rounding error and the storey index has to be exactly 0.
- **NOT ONE BASELINE FILE MOVED** and **`ClusterStorage.DbVersion` is NOT bumped** — the third
  work package running, re-checked rather than carried over. `street-geometry.json` records the
  road mesh, and nothing here changes any block-floor vertex either: this reads the cap.
  **1804 xUnit** against 1780, TALE 200/200.
- **Mutations: fourteen, one survivor, and it is provably equivalent** — making `_boundsOf`
  recompute the range instead of reading what it stored on the `Building` is the same
  expression over the same immutable footprint and height source, so no output can differ; it
  is a performance measure and a real one. **The two that survived their FIRST round are both
  findings** — the cap-vertices term above, and the shop's storey reference. A third is worth
  naming: the shops operator naming the block's FIRST building instead of the shopfront's own
  is killed by a **source scan only**, and the scan names the variable rather than the call,
  because a call with the wrong argument compiles and passes everything.

### The report closes

**Item (o) can close.** The reported picture — *"a street trunk running off into a building"* —
is gone: buildings over a `Street` carriageway **3247 → 13** flag off and **2986 → 0** flag on,
**0 on a spur** in either state, and the start city the owner plays goes from 5 and 4 to 0 and
0 (§7v.2). The coupling WP-O2 created is repaired: every building is founded on its own piece
of ground, over seventy cities, on both flags, with 0 footprint corners in the air.

**What is left behind, and none of it is this class:**

- the **13 flag-off corner slivers** — an estate overlapping a street its own block runs along
  by 0.9–13.4 m². §7t counted 10 of them *before* any of this work; it is the pavement inset
  meeting the carriageway rectangle near a junction and it predates the whole page;
- the **three self-touching outlines**, a Clipper pinch at the decimetre, two of them with no
  spur involved;
- **121 / 10 blocks losing a median 1.0 m² to a neighbour's corridor**, with one unexplained
  1180 m² flag-off outlier;
- ✅ **522 flag-on blocks with no buildable land at all** against 9 flag off — **measured
  2026-09-08 and they are CORRECT**: every one is a block that is entirely pavement. See the
  section below and `STREETS-3D-TOPOLOGY.md` §7x. ✅ **And the matchbox on the other side of
  that same cliff is gone the same day** — the owner set a ten square metre floor, §7y;
- ⚠️ **the block floor's interior is a tessellation artefact and it is metres deep** — the
  finding behind §7w.1, not merely a fact about the bound. The gap between the lowest height
  anywhere on a footprint's own boundary and the lowest the floor actually gets over that
  footprint is **9.77 m** at the worst building of `Yelukhdidru@3000`, and that gap IS the
  interior tessellation. §7c confined the cross-fall to the interior on purpose and this is
  what the interior looks like from underneath;
- §7u.5's block corner across a skipped arm (a pre-existing WP-B5 §14.3 defect, flag-on only);
- ⚠️ **§7t.7's question is still unanswered**: the CLASS is established and the INSTANCE never
  was. The start city had no broken ring, no building over a street and no spur inside any
  block on the day it was reported, and the nearest building to any of its dead-end tips was
  43 m away. The owner was asked for the screenshot's heading and whether the position and the
  picture come from the same session, and has not answered. WP-O2 removed the class from the
  start city as well, so if the sighting recurs it is something else.

---

### ✅ 2026-09-08: the blocks with no buildable land are traffic islands — MEASUREMENT ONLY

The last open number under (o). WP-O2 recorded *"522 flag-on blocks have no buildable land at
all after their structures are subtracted, against 9 flag off; nothing looks at what a block
with no estate should be"* on the strength of one cell of one table. Measured now — **not one
byte under `JoyceCode/` changed, no baseline moved, `ClusterStorage.DbVersion` untouched.**
Full write-up and every number in `STREETS-3D-TOPOLOGY.md` §7x.

⚠️ **THE ANSWER IS THAT THEY ARE CORRECT, and the sentence they were recorded under is wrong
in both of its halves.**

**They are blocks that are entirely pavement.** The equivalence is exact over all 78 500
blocks of both worlds, in **both directions with 0 exceptions**: the buildable land is empty
exactly when the block's own half-width — the widest uniform inset its outline survives — is
under its `Quarter.SidewalkWidth`. They are small triangles between three roads, median
**68 m² flag on** against a block median of 7692, narrowest width a median **7.0 m** where the
pavement asks for 12, **517 of 522 with three corners**. The largest is a 1136 m² ribbon 11 m
wide and 100 m long between two parallel arterials — a boulevard median. A traffic island is
what that is, and a paved island with no building is what the generator makes of it.

⚠️ **NOT "after their structures are subtracted": every one of the 531 is emptied by the
PAVEMENT INSET ALONE.** Asked four ways through `QuarterGenerator.BuildableLandOf` itself:
empty after the inset with nothing subtracted on **9 / 522**, emptied by a structure
carriageway on **0 / 0**, by a spur corridor on **0 / 0**, and **there is no block anywhere
whose land the inset leaves and a subtraction then takes away (0 / 0)**.

⚠️ **AND LEDGER ITEM (o) DID NOT MAKE THEM — measured by restoring `6c228797^` into a
worktree, not reasoned about.** That tree tagged **exactly 9 and exactly 522** blocks
`estateTooSmall`, every one with a byte-identical outline to one of today's, and **not one of
them carried a building there either**. No regression: WP-O1/O2/O3 neither created these
blocks nor took a building off one. What a *player* saw change was **WP-B6** on 2026-09-06,
when grade separation became the default.

⚠️ **9 → 522 is the heavy-first city's ROADS, not its structures.** The street graph's own
face is never small — the smallest of the 531 is **471 / 486 m²**, and on **all 531** that face
is wide enough for the pavement. What eats it is the carriageway: `outline half-width = face
half-width − street width / 2`, and the world's median street is **18.82 m flag on against
9.85 flag off**. That is §13's *"a heavy-first city has no alleys in it"* followed through to
what stands on the network for the first time. **The control**: the ten cities that get no
structure at all with the flag on still go from **0 empty blocks of 441 flag off to 4 of 355
flag on**; and 515 of the 522 have no structure whose footprint even reaches them.

**Nothing could stand there anyway.** The most generous relaxation that keeps the block's
shape recovers a median **0.75 m²** and leaves **516 of 522** capped to one storey by
`minHouseSide <= 2`. **Two expressions already agree and one has been writing it down all
along**: `_createBuildings` tags exactly this set `estateTooSmall` (0 disagreements) and
nothing has ever read it, and §7c's `SidewalkRing.InsetOf` refuses all 531 through a rule
written for another purpose entirely.

**What a player sees**: a small paved triangle with nothing on it, which is right. No shops
and no TALE building locations, and no nav consequence — `Placer` and `GenerateNavMapOperator`
read quarters and junctions rather than estates, so an island is an ordinary block to both. Two consumers do reach it — `GenerateTreesOperator`
plants on 30 % of building-less estates at 1.6 m² per tree over the *whole block outline*, so
one of these would get ~43 trees standing on pavement; and `PlayerStart.PoseIn` starts a new
game on the first building-less estate of the start city, which one of these is by
construction — **`cluster-clusters-mydear-0` has none of them in either flag state**, so that
branch cannot be reached today and nothing protects it.

⚠️ **THE ONE THING THAT IS WRONG IS ON THE OTHER SIDE OF THE SAME CLIFF, and it is recorded
rather than fixed.** The rule refuses a block with 0 m² of land and accepts one with
**0.005 m²**: **4 blocks flag off and 53 flag on carry a building under one square metre**
(8 / 126 under four, 91 / 733 under a hundred), the smallest fifty square centimetres and
three metres tall, on exactly the same tiny triangles. `minHouseSide <= 2.0f` holds them to
one storey and nothing holds them to existing. **Whether a block should need a minimum area to
be built on is a design decision** and is the owner's, exactly as §7t.5 was; §7x.9 costs it
(no baseline file moves; `BlockGraphTests`' two census tables, `SpurEstateTests` and
`BuildingFootingWorldTests` would have to be re-taken; it would be the first threshold
constant this rule carries, which §7t.10 and §7v were built to avoid).
✅ **The owner decided the same day and it is the section below.**

Gate: `tests/JoyceCode.Tests/engine/streets/EmptyBlockTests.cs` (18 new; **1830 xUnit**
against 1812, TALE 200/200).

---

### ✅ 2026-09-08: a piece of land under ten square metres carries no building

§7x.8's matchboxes, closed by an owner decision rather than by a derivation. In the owner's
words: *"I think it's safe to assume that in real world, the smallest buildings (apart from
temporary things like tents) would be a 10m2 building, in the context of slums or small
convenience stores, but not really in city centers."* Full write-up and every number in
`STREETS-3D-TOPOLOGY.md` §7y.

**What was built.** `engine.world.MetaGen.MinBuildingArea = 10f`, beside `MetaGen.StoryHeight`
because they are the same kind of number — the two dimensions of *what a building is* that the
generator is not free to derive — with the owner's sentence quoted on it. And
`QuarterGenerator.CanCarryABuilding`, **one predicate**: the loop's own
`if (0 == polygon.Count)` is this rule with its threshold at zero, so it is replaced rather
than joined, and a polygon with no points, two points or no area falls out of the same
expression. The `estateTooSmall` debug tag follows the predicate, deliberately.

⚠️ **THE BRIEF'S EXPECTATION IS BACKWARDS AND THIS IS THE FINDING.** *"A downtown block is
large enough that the floor will rarely bind anyway"* — measured, the refusal rate rises with
downtownness in both flag states, and **flag off it fires only downtown**: 0 of 13 444 blocks
below downtownness 0.5, 18 of 14 251 above 0.7. Flag on it is 0.39 % below 0.3 against
**1.01 %** above 0.7. The mechanism is §7x's — a 6 m pavement exists only above downtownness
0.7 and the heaviest roads are downtown — so the owner's *"not really in city centers"*
describes the real world and not this generator: the matchboxes were **concentrated** in the
centres and the floor removes them exactly there. It ships **flat everywhere** anyway, because
at one per cent of blocks there is nothing a second rule would fix.

**What it costs, both flag states (grade separation is the default, so flag on is shipped):**

| | flag off | flag on |
|---|---|---|
| pieces of land refused | **20** of 40 914 | **310** of 38 797 |
| buildings | 27 403 → **27 387** | 26 054 → **25 860** |
| blocks carrying a building | 27 390 → **27 374** | 25 216 → **25 025** |
| shopfronts | 572 404 → **572 402** | 527 807 → **527 794** |
| smallest building anywhere | 0.030 → **11.92 m²** | 0.005 → **10.05 m²** |
| `estateTooSmall` | 9 → **29** | 522 → **828** |

The refused land is a median 4.00 / 2.65 m², nowhere near the boundary, and the smallest
survivor is a twentieth of a square metre over the floor — so it is a floor and not a filter
with a margin.

⚠️ **AND A 1450 m² BUILDING APPEARS, which nobody asked about.** The flag-on total falls by
**194** while 195 buildings were removed. Refusing before the 30 % design draw — which is where
the `mn == 0` gate already was — means a refused piece no longer consumes that draw. Three
flag-on blocks in the world hold both a refused piece and a good one; on
`cluster-clusters-mydear-580` the 1450 m² piece had lost its own draw to a 3.5 m² matchbox that
won one, and now takes it. Measured by rebuilding the world at `91244e45` and diffing block by
block, and visible independently as `ACornerOfTheFloorInsideAFootprintIsAlwaysOnItsBoundary`
going **up** by three.

**The knock-on is nothing new.** A building-less block is the ordinary case — 13 517 and 12 584
of them, because `_designBuilding` refuses 30 % on a draw — so this adds 16 and 191 to
thousands and reaches no branch that was not already reached on every start. All six consumers
handle it; `PlayerStart.PoseIn` is the one that could have moved the player and **does not**,
because the start city has no piece under the floor in either flag state (asserted on the
float). **No baseline file moved** (all five byte-identical to `7755a7b1`) and
**`ClusterStorage.DbVersion` is not bumped** — quarters, estates and buildings are rebuilt from
the stored stroke network on every start.

⚠️ **RECORDED AND NOT ACTED ON: area alone does not bound a sliver.** Fourteen flag-on
buildings are under **two metres thick**, the thinnest **0.32 m** with a 95.7 m perimeter round
17.2 m² of floor, and the largest of them has 83 m² — no area threshold reaches that shape.
A minimum WIDTH is a second owner decision. If it is ever taken, the instrument is the
outline's own half-width and **not** `minHouseSide`, which is short at a mitred corner far more
often than on a sliver: 1439 / 725 blocks have a building with a side under 2 m and they are
ordinary fat buildings.

Gate: `tests/JoyceCode.Tests/engine/streets/MinBuildingAreaTests.cs` (17 new; **1847 xUnit**
against 1830, TALE 200/200). Twelve mutations, **none survived**; ⚠️ two of them are killed by
exactly one fixture each and by no amount of real data — `>` for `>=` (the world's smallest
survivor is 10.05 m² and its largest refusal 9.98) and a missing `Math.Abs` (every piece is
wound the same way).

✅ ⚠️ **Fixed on the way, and the entry above had it wrong**:
`DBStorageCollectionTests.AnUnreadableCollectionSaysWhatItIsThrowingAway` is **not** a
`LogCapture` race. It asserted that LiteDB's exception names `FlatStreetHeight`, and in the
**shipped** configuration a `ClusterDesc` gets a `RelaxedStreetHeight` — which of the two
depends on `StreetHeightSources.FollowsTerrain`, i.e. on whether some **other** test in the
assembly has loaded the game's global settings first. So it passed or failed on the run order:
near-always red in a tree that has baked assets under `nogame/generated/` and never red in one
without, which is why it was seen once and called intermittent. The gate takes the type off the
objects it is about to store now.

---


# Part 2 — Carried over, known and deliberately deferred

These are not player reports. Each was found during Phase A, verified, and consciously
left. Source locations were re-checked on 2026-08-30 against HEAD `40748066`.

## 2.1 Terrain still buries block interiors

**Measured** against the shipped `GroundOperator` diamond-square terrain (gradient
**15.6–16.6 % over a 20 m cell** at the median, relief **57–60 m inside a 200 m window**):
**18–25 % of block AREA is under terrain, median burial 2.3–2.6 m, p90 ~10 m** — but only
**3–7 % of the kerb rim**, and **no block anywhere is fully buried** (0 of 445, 0 of 82,
0 of 10). So this eats block *interiors*, where buildings stand, not pavements.

The cause is resolution, not the algorithm. `MetaGen.GroundResolution = 20` over
`MetaGen.FragmentSize = 400` is **one elevation sample every 20 m**, while a street is
8–22 m wide — a road corridor is about ONE CELL, so cutting it terraces rather than cuts.
`ClusterConformElevationOperator` therefore grades with a 60 m smoothstep instead, and at
the **median block depth-to-kerb of 28 m** the grading weight is only ≈0.53.

**Do not fix this by widening `RadiusInCells`** — that flattens the countryside into the
city instead of cutting the city into the countryside. The real fix is a **finer elevation
grid inside cities**, which is a larger change and has been deferred since §2c.

Block depth-to-kerb, measured: `seed000`/1500 — 82 blocks, median 28.4 m, p90 45.4 m, max
77.1 m, 3.7 % beyond the 60 m radius. `Yelukhdidru`/3000 — 445 blocks, median 28.3 m, p90
51.4 m, p99 83.8 m, max 105.1 m, 7.2 % beyond.

## 2.2 The intercity network still ignores all of this

`IntercityTrackElevationOperator` hard-sets an absolute height along a narrow band and the
intercity network still reads `AverageHeight`. It is deliberately registered at
`/000200/intercityTrails`, i.e. **above** the city conforming pass at `/000150`, so that a
city may not smooth it away — which keeps its behaviour exactly what it was, and also
means it does not meet a terrain-following city at the city's edge.

**Still true, and now quantified** (§7n): the ribbon is median **−13.10 m** against the
untouched terrain over all 114 lines, range −149.3…+134.2 m, 63.8 % of it a cutting, and
**no track geometry is drawn at all**. What changed on 2026-08-31 is only that the vehicle
rides it instead of flying a median 43.5 m over it. The three options for the ribbon itself
are under Part 1 (e) and in §7n, and none is built.

Two smaller things found beside it and left: `Line.Width = 5f` is not the width of anything
(the band the operator writes is `2 · stepX` = 40 m either side, and `Width` is used only by
the fine intersection test, so the operator can reject a fragment its own writing pass would
have touched); and `IntercityTrackElevationOperator`'s AABB named `ClusterA` at both
stations, corrected in passing and inert because only `IntersectsXZ` is ever asked.

## 2.3 `DeckCollider` tilts where the mesh flattens

`JoyceCode/engine/streets/generation/DeckCollider.cs`. `_shearOntoSlope` holds the road
**mesh** flat over each junction footprint, while `DeckCollider` tilts across the stroke's
whole length. Inside a junction the collider therefore climbs while the picture is level.
§7b fixed the visible half of this by giving the junction cap its own flat slab
(`JunctionCollider`), but the deck itself still tilts across the footprint, and
`DeckCollider`'s own height expression is still inline rather than hoisted where a test
can reach it — which is exactly the shape that hid the `AverageHeight` mutation in
`JunctionCollider`.

## 2.4 `ClusterBaseElevationOperator` writes `aver + 1.5`, reports `aver`

`JoyceCode/engine/elevation/ClusterBaseElevationOperator.cs:48` sets
`_clusterDesc.AverageHeight = aver`; line 114 writes `epxDest.Height = aver + 1.5f` into
the elevation grid. So in the flat city the ground is 1.5 m above the height everything
else calls "the average", leaving only 0.5 m between it and the road at
`aver + CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE` (2 m). Harmless today; a trap for anyone who
assumes the two agree. **Changing it moves the flat baseline.**

Note also that this operator is where `AverageHeight` is *computed* — deleting it drops
every city to zero. The flattening flag skips only the height write.

## 2.5 Sibling elevation operators are ~5 % short

`ClusterBaseElevationOperator` and `IntercityTrackElevationOperator` divide the segment
span by the sample **count** rather than count−1, placing every sample about 5 % short and
restarting the error each fragment. Harmless for an operator writing a constant inside a
rectangle. **Fixing it moves the flat baseline.**

## 2.6 `SegmentNavigator` writes a stale `StreetPointId`

`JoyceCode/builtin/tools/SegmentNavigator.cs:345-346`:

```csharp
_position.StreetPointId = _position.StreetPoint.Id;   // the OLD StreetPoint
_position.StreetPoint = _position.QuarterDelim.StreetPoint;
```

The id is written from the previous `StreetPoint` the line before it is overwritten, so it
lags by one update. Pre-existing. Nothing *reads* it today (the only other writers are
`Placer.cs:283` and `citizen/SpawnOperator.cs:239`) — but it is `[JsonInclude]` in
`PositionDescription`, so it **is persisted into save games**, and a future reader would
get a junction one step behind.

## 2.9 `SegmentNavigator` indexes a block's delimiters with a segment index

`JoyceCode/builtin/tools/SegmentNavigator.cs:334-340`. `_position.QuarterDelimIndex` is
written as `(_idxNextSegment + count - 2) % count`, a **segment** index, and the next line
does `_position.Quarter.GetDelims()[_position.QuarterDelimIndex]`. The two are the same
number only for the block loop, which has exactly one segment per delimiter;
`_setStartSegment` reads it back the same way.

`GoToStrategyPart` hands the navigator a `StreetRouteBuilder` route together with the
citizen's own pod, whose `Quarter` is its block - so a street route with more segments than
its block has corners indexes past the end of `GetDelims()`, and a route with fewer starts
partway along itself. Pre-existing, and it is why §7p kept the walk at one waypoint per
corner rather than taking the pavement's own two-per-edge inset ring.

## 2.7 Door standing points carry the vehicle clearance

⚠️ **CLAUDE.md located this defect in the wrong function until 2026-08-30.**
`SpatialModel._computeStreetEntryCandidates` takes lane endpoints' **XZ only** and
overwrites Y with `junctionCenter.Y` (`SpatialModel.cs:443-448`), which is the correct
walking height and has been since the original commit `b62c5f2f`. **Street entry
candidates are correct.**

The real site is **`_snapToPedestrianLane` (`SpatialModel.cs:357-402`)**, which returns a
point on the lane in full 3-D and feeds **building and shop `EntryPosition`** (lines 197,
245) — shop and home doors, not street corners. Those carry `NavJunction.Position` =
ground + 3.0 against a walking height of ground + 2.15, i.e. **0.85 m too high**.
`GoToStrategyPart` corrects it on the first moving frame. **Left because fixing it moves
NPCs standing at doors in the flat city.**

## 2.8 Smaller, verified, and cheap

| item | where | note |
|---|---|---|
| Physics shapes never released | 10 `simulation.Shapes.Add` sites under `JoyceCode/` | Registry entries are never removed, for **all** collider kinds. Pre-existing and orthogonal to elevation. |
| `IsInvalid()` quarters not skipped | `GenerateClusterQuartersOperator` | The only quarter consumer that does not skip them (`ClusterDesc.cs:585`, `SpatialModel.cs:145`, `GenerateNavMapOperator.cs:275` all do). It draws *more*, not fewer, and the baselines produce none. |
| `ToConvexArrays` guesses its plane | `builtin/tools/Triangulate.cs` | The same latent projection guess §7j fixed in `ToMesh`, but it feeds convex-hull construction, which is winding-agnostic. |
| Route ribbon NaN | `builtin/modules/satnav/RouteRibbon` | `Vector3.Normalize` of the plan direction is NaN for a lane with no horizontal extent. The generator emits none. |
| Far-route shimmer | `Sdl3WindowBackend` | SDL is asked for a **16-bit** depth buffer; with near=1 and far=√3·1000+100 the quantum on a coplanar surface is ~0.15 m at 100 m and 0.61 m at 200 m. The 0.1 m guideline lift holds to ~80 m. A 24-bit request is a one-line change with a wide blast radius. |
| Taxi passenger leak | `quests/Taxi/DrivingStrategy.OnExit` | Only deletes the passenger when `_hasWaitingPerson` is set, and that flag is set inside the queued setup action of an `async void` spawn — so an early exit leaks the entity until its fragment unloads. |
| `TransportationTypeFlags()` default | `builtin/modules/satnav` | Still defaults to `Pedestrian`. **Deliberate** — that is *what may use this lane*, a different question from *what am I planning for*, and both engine emission sites pass explicitly. |

---

# Part 3 — Not elevation work, but open and worth knowing

- ✅ **Phase B (the crossing policy) is COMPLETE and the flag is ON (2026-09-06).**
  WP-B0 … WP-B6 are all done. `joyce.EnableGradeSeparation` defaults to true and
  `ClusterStorage.DbVersion` is 1040. The shipped world gets **737 structures** (643
  bridges, 94 tunnels) across 70 cities; the seven pinned seeds get 20, so reading the
  phase off them understates the world thirty-five fold. Full history in
  [`STREETS-3D-PHASE-B-CROSSING-POLICY.md`](STREETS-3D-PHASE-B-CROSSING-POLICY.md)
  §7 … §15.
- ⚠️ **Open after the flip, all recorded with numbers in §15.13**: a ramp may come down at a
  dead end (one junction in the pinned seeds); two deck ends of forty are targeted onto the
  road underneath, because `NavCluster.TryCreateCursor` has no notion of level; a pedestrian
  crossing at a ramp foot passes over the ramp mouth, at most 1.26 m above it;
  `DBStorage._readCollection` **drops any collection it cannot deserialise, silently**, and
  `ClusterDesc` is one — so the cluster list has never been cached at all; and an existing
  save resolves junction ids to different junctions.
- ⚠️ **Two WP-B4 decisions are the OWNER's and are written up in §13.2 and §13.4**, with
  numbers rather than opinions: whether a *weight ratio* should refuse a crossing at all
  (it costs 19 → 10/8/6/0 structures at ratios of 1.0001/1.05/1.1/1.25, and the ruleset's
  crossings only ever differ by 1.00 … 1.37), and which way round the *obliquity* rule
  points (as shipped it refuses crossings within a quarter turn of parallel, which is what
  the abandoned code it resurrects actually did; the plan's own wording would refuse
  everything squarer and leaves ONE structure in the world).
- ⚠️ **`joyce.EnableGradeSeparation` changes what a city is MADE OF, not just its order.**
  With it on the heavy candidates drain first and fill the space, so `Yelukhdidru@3000`
  goes from 1875 strokes with 1308 of them at the ruleset's minimum weight to 1467 strokes
  with **none** at it and a lightest weight of 0.786. Anything reasoning about the weight
  distribution has to say which city it means — §13.2.
- **Debug filter migration** is ~54 % done (307/571 logger calls); ~264 remain.
- **Routing Phase D**: D2 (multi-objective A* integration) and D4 (behavioural variety) pending.
- **TALE-SOCIAL Phase D5 tuning**: five concerns documented in
  `docs/tale/docs/phases/PHASE_D_SOCIAL.md`, being absorbed into Phase E.
- **Platform**: GATE-C Linux has never been run; see
  [`PLATFORM-BACKEND-STATUS.md`](PLATFORM-BACKEND-STATUS.md).

---

# Part 4 — ✅ DONE (`1c54a9e4`, 2026-09-01)

**`joyce.DisableClusterFlattening` is `true` by default.** The terrain-following city is the
shipped city, and the flat-city bit-for-bit invariant has retired with it. Every test that
asserts it was kept rather than deleted: most of them are really asserting *"the height seam
is the only thing that decides height"*, which stays true and stays worth testing, and the
flat path is still reachable behind the flag.

**What the flip surfaced immediately:** (j) above — a defect that is exactly 0.000 m in the
flat city and up to 6.5 m in the terrain one, and so had never been visible. Expect more of
that shape: anything whose flat-city value is a coincidence rather than a construction.

### The original write-up

> **Flip `joyce.DisableClusterFlattening` to `true` by default.** That should happen only
> after Part 1 is cleared — a city whose buildings float and whose trams fly is not a
> default. When it happens, the flat-city bit-for-bit invariant retires with it, and every
> test that asserts it needs re-reading rather than deleting: most of them are really
> asserting *"the height seam is the only thing that decides height"*, which stays true and
> stays worth testing.
