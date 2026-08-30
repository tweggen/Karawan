# The three-dimensional city — open points

**Status:** open ledger. This is the file to read first when picking up the
terrain-following city work.
**Companion:** [`STREETS-3D-TOPOLOGY.md`](STREETS-3D-TOPOLOGY.md) is the design and
history document — every fix is written up there as §7a … §7j, with the measurements that
drove it. This file is only *what is still wrong* and *what to do about it*.

**Last updated:** 2026-08-30.

---

# ▶▶ RESUME HERE

Turn the city three-dimensional with:

```jsonc
// models/nogame.globalSettings.json
"joyce.DisableClusterFlattening": "true"
```

It is **off by default**, and the default flat city has been kept bit-for-bit stable
through this entire work stream — with exactly one deliberate exception (§7i, which moved
`Placer` reference junctions, and §7j, which un-culled faces without moving a vertex).
**Assume that invariant still applies to anything you write**, and prove it with a test
over whole generated cities rather than by argument.

**What Phase A finished:** the road surface and everything that moves on it. Streets
follow relaxed terrain gradients, the ground conforms to the roads, blocks are tilted
pads, the kerb meets the carriageway exactly, cars hover on a raycast probe, NPCs walk at
lane heights, the satnav guideline lies on the road, and the pavements face upwards.

**What Phase A never touched: everything that STANDS on that surface.** Buildings, shops,
quest markers, trams and the initial coin placement were all written against a flat city
and none of them has been revisited. That is what Part 1 below is.

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

<!-- PLACEHOLDER: items (a)-(f) are filled in below once the two investigations land. -->

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

## 2.7 Pedestrian standing points carry the vehicle clearance

`engine.tale.SpatialModel._computeStreetEntryCandidates` uses pedestrian lane
**endpoints** as standing points, so a street location's entry candidates carry
`ClusterNavigationHeight` (3 m, the *vehicle* hover reference) rather than the walking
height — 0.85 m too high. `TaleSpawnOperator` spawns at it and `GoToStrategyPart` corrects
it on the first moving frame. **Left because converting it moves NPCs in the flat city.**
See item (b)/(d) in Part 1 — this may or may not be what the player is seeing.

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

- **Phase B (the crossing policy) has not been started.** `StreetLevels.ElevationOf(stroke.A.Level)`
  is already in the height expression and **no shipped ruleset produces a non-zero `Level`** —
  that is the hook for bridges, tunnels and multi-level junctions, i.e. the layered 3D this
  whole work stream was the prerequisite for.
- **Debug filter migration** is ~54 % done (307/571 logger calls); ~264 remain.
- **Routing Phase D**: D2 (multi-objective A* integration) and D4 (behavioural variety) pending.
- **TALE-SOCIAL Phase D5 tuning**: five concerns documented in
  `docs/tale/docs/phases/PHASE_D_SOCIAL.md`, being absorbed into Phase E.
- **Platform**: GATE-C Linux has never been run; see
  [`PLATFORM-BACKEND-STATUS.md`](PLATFORM-BACKEND-STATUS.md).

---

# Part 4 — The decision that ends this arc

**Flip `joyce.DisableClusterFlattening` to `true` by default.** That should happen only
after Part 1 is cleared — a city whose buildings float and whose trams fly is not a
default. When it happens, the flat-city bit-for-bit invariant retires with it, and every
test that asserts it needs re-reading rather than deleting: most of them are really
asserting *"the height seam is the only thing that decides height"*, which stays true and
stays worth testing.
