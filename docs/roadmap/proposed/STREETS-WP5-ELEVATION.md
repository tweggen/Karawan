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

### WP-5a — Street geometry carries elevation

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

### WP-5b — Navigation

`GenerateNavMapOperator` turns junctions into `NavJunction` and strokes into
`NavLane`, and hardcodes `new Vector3(sp.Pos.X, 0f, sp.Pos.Y)` — the same planar
assumption in a place where it *is* wrong, because navigation is world space.

- `NavJunction.Position` gains the elevation.
- Lane length on a ramp is the 3D length, or route costs will prefer ramps.
- Pathfinding is otherwise unaffected: ramps are ordinary edges, so a route over a
  bridge falls out of the existing search with no special case.

**Gate:** a ground-only navmap is unchanged; a route across a two-level cluster uses
ramps and its length matches the 3D geometry.

### WP-5c — Physics and placement

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
