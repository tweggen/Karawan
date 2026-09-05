# Phase B — the crossing policy

**Status:** proposed, not started.
**Follows:** Phase A (`STREETS-3D-TOPOLOGY.md` §7a … §7s) — cities follow terrain, and every
surface that stands on them agrees with it.
**Precedes:** Phase C — structures (deck undersides, piers, abutments). Deliberately last:
*a floating slab is unfinished, not wrong.*

The question this answers: when two streets meet, do they actually **join**, or does one
pass over the other?

---

## 0. What already exists — this is a policy, not machinery

Established by reading the tree, not by assumption. **WP-4's multilayer support is complete
and tested; the only missing piece is the decision.**

| piece | state |
|---|---|
| `Stroke.Level` / `StreetPoint.Level` (`sbyte`) | built |
| `StrokeKind` — `Street`, `Ramp`, `Bridge`, `Tunnel`, `ConnectorBridge` | built |
| `StreetLevels.DeckHeight = 8f`, `ElevationOf(level)` | built |
| Level filtering in every `StrokeStore` query — crossing, snapping, near-point, near-stroke | built, `MultilayerTests` |
| `ClearanceConstraint` — a ramp occupies two decks at once | built |
| `OverpassBuilder.Build(from, to, deckKind, rampFraction, weight)` → ramp/deck/ramp | built |
| `NetworkBuilder.CommitChain` — atomic; a chain that fails anywhere leaves nothing | built |
| `NetworkBuilder._checkLevels` — only a ramp may change level, adjacent decks only | built, throws |
| Geometry, colliders, `RoadSurface`, `DeckCollider` handle ramps | built (§7o, §7s) |
| V1 fingerprint (ground-only, omits `Level`) and V2 (includes it) | built |

> **`OverpassBuilder` is called from tests and from nowhere else.** No shipped ruleset
> produces a non-zero `Level`. The machinery is finished and never fires.

So Phase B adds: **a decision procedure, a seam to invoke it from, and a setting to turn it
on** — and nothing else.

---

## 1. The distinction that must not be blurred

Carried forward from `STREETS-3D-TOPOLOGY.md` §3, because it is the reason this is a policy
over existing data:

> `Level` is a **topological deck index** — *"do these two meet?"* Terrain height is
> **continuous** — *"how far apart are they?"* Collapsing one into the other repeats the
> `Pos3` mistake this project already made once.

**Height informs the policy; the policy sets `Level`; `Level` drives the filtering.**

---

## 2. ⚠️ Buildability comes before desirability — and it may kill the phase

`STREETS-3D-TOPOLOGY.md` §5 sequences Phase B as *"terrain difference first, then hierarchy,
then spacing and angle"*. **Every one of those is a question about whether a crossing
*wants* separating. None asks whether it *can be* separated, and that is the binding
constraint.**

A ramp climbs `DeckHeight = 8 m`. At a 10 % ramp grade that is **80 m of run per ramp**, so
a structure needs roughly **200 m** end to end before the deck has any length at all. Most
city streets are far shorter.

**WP-B0 is therefore a measurement, not code**, and it gates the whole phase: over the
shipped world, how many crossings could physically carry an overpass at a buildable ramp
grade? If the honest answer is "a handful", the phase needs rethinking before any policy is
written — plausible responses being a smaller `DeckHeight`, a steeper ramp grade for minor
roads, or restricting structures to primary roads, which are the long ones.

**Do not write the policy before this number exists.** Building five predicates and then
discovering that 1 % of crossings qualify is the expensive order.

---

## 3. Work packages and their gates

Every WP lands separately. **`joyce.EnableGradeSeparation` defaults to `false` throughout
WP-B0 … WP-B5**, so all existing baselines stay byte-identical until WP-B6 deliberately
moves them — the same pattern `joyce.DisableClusterFlattening` used through Phase A, and it
worked.

### WP-B0 — can it be built at all?

Measurement only. No production code.

| AC | criterion |
|---|---|
| B0.1 | Over the shipped world's cities, the distribution of **candidate crossing spans** is reported — how much straight run is available end to end at each crossing an overpass could use. |
| B0.2 | For ramp grades of 5 %, 10 % and 14 % (the `GradePolicy` range), the **fraction of crossings that could carry a structure** is reported per city and world-wide. |
| B0.3 | The same fractions are reported **restricted to crossings where at least one arm is `IsPrimary`**, since those are the long roads. |
| B0.4 | A written recommendation: proceed as planned, or change `DeckHeight` / ramp grade / scope first. **This AC is a decision, and it goes to the owner, not into code.** |

### WP-B1 — the seam

| AC | criterion |
|---|---|
| B1.1 | A new `VerdictKind` (working name `Structure`) carries an unattached chain from a constraint back to the driver, which validates every member through the pipeline and commits via `NetworkBuilder.CommitChain`. |
| B1.2 | A `ICrossingPolicy` with one implementation, `NeverSeparate`, is consulted where `IntersectionConstraint` decides. `NeverSeparate` is the default. |
| B1.3 | **With the setting off, `street-fingerprints.json` (V1 and V2), `street-geometry.json` and `street-cost-baseline.json` are byte-identical**, asserted, and TALE is 200/200. |
| B1.4 | A chain that fails any constraint leaves the store **exactly** as it was — asserted by fingerprinting the store before and after a deliberately unbuildable proposal. |
| B1.5 | The constraint **order** is unchanged. `ICandidateConstraint`'s own warning applies: order is part of the generated output. |

### WP-B2 — buildability predicate

The `can`, before any `wants`.

| AC | criterion |
|---|---|
| B2.1 | `MaxRampGrade` is a named constant with its derivation written down, not a literal. |
| B2.2 | A crossing whose available run is shorter than `2 · DeckHeight / MaxRampGrade` is **never** separated, asserted over whole generated cities. |
| B2.3 | Every ramp `OverpassBuilder` produces under the policy is within `MaxRampGrade`, measured on its own emitted geometry — not on the intent. |

### WP-B3 — terrain difference (free separation)

| AC | criterion |
|---|---|
| B3.1 | Where the two relaxed junction heights already differ by a stated fraction of `DeckHeight`, the crossing separates with **shorter ramps**, because the hill has done part of the work. The saving is measured, not asserted. |
| B3.2 | The predicate reads **relaxed street heights**, never raw terrain and never `AverageHeight`. Guarded the way §7b's `JunctionCollider.SurfaceHeightOf` is. |
| B3.3 | In a **flat** city this predicate never fires — it is identically zero there — asserted rather than argued. |

### WP-B4 — hierarchy

| AC | criterion |
|---|---|
| B4.1 | Weight ratio and an absolute weight floor: **two alleys never separate**, whatever their ratio. |
| B4.2 | The heavier road takes the deck and the lighter passes underneath — asserted on identity, not on which was the candidate. |

### WP-B5 — spacing and angle

| AC | criterion |
|---|---|
| B5.1 | A heavy road that already has a junction within `N` m separates instead of adding another. |
| B5.2 | Crossing angle: an oblique crossing separates. **This resurrects a finished thought that was abandoned** — the original `PointNearStrokeConstraint` computed `angleVice`/`angleVersa` and discarded both behind `if (true \|\| …)`; WP-2b removed the dead operands and recorded the intent. |

### WP-B6 — turn it on

| AC | criterion |
|---|---|
| B6.1 | Structures per city, and their kinds, reported for the shipped world. |
| B6.2 | **The navigable network does not fragment**: the number of connected components is unchanged, and no junction that was reachable becomes unreachable. A separated crossing removes a junction two streets used to turn at — that is the risk this phase carries. |
| B6.3 | Every baseline that moves is recorded with old and new values, per city, and each is classified as a genuine network change rather than a re-hash. |
| B6.4 | TALE 200/200, with the tale suite run **after** the flip, since NPC routing crosses these junctions. |

---

## 4. Consumers to check rather than assume

- **Pedestrian crossings.** `GenerateNavMapOperator` draws them per junction. A separated
  crossing has no junction, so none should be drawn — *expected to follow automatically,
  which is exactly the kind of expectation this project has been wrong about.* Assert it.
- **Nav lanes over ramps.** A ramp is a stroke, so it becomes a lane. Routing must climb a
  deck and come back down. `NavJunction` carries `GroundHeight` and consumers add their own
  offset (§7m) — the deck elevation is `StreetLevels.ElevationOf`, a *different* term.
  Check which of the two a lane on a deck gets.
- **The satnav guideline.** §7r/§7s made it follow `RoadSurface`. A ramp's surface is
  already handled (both sections say ramps are unchanged float for float) — confirm under
  a policy that actually emits them.
- **Blocks.** `QuarterGenerator` traces rings from strokes. What does it do when a ring
  would close through a deck? This is the least-understood consumer and deserves its own
  measurement.
- **`ConnectComponentsPass`.** Runs before or after the policy? Order decides whether it
  can repair a fragmentation the policy caused.

---

## 5. How this work has gone, and what that implies here

From `CITY-3D-OPEN-POINTS.md`, because every one of these has bitten:

1. **Measure before diagnosing.** The obvious diagnosis was wrong in about half of Phase A's
   rounds, including twice where the plausible cause was real, present, and still not what
   the player was seeing.
2. **Mutation-test every gate.** Every round produced a survivor and it was always the one
   that mattered. Specifically earned: `if (false)` round a branch passes a source scan; a
   scan matching a bare identifier is satisfied by a field declaration or a comment;
   `X = 0f * Call(...)` passes a scan looking for the call; **a scan sees the name of a
   call, not how many of its results are used**; **a containment test cannot tell a guess
   from a refusal**; and **a rule can be invisible to unlimited real data** when no baseline
   city contains the shape it governs.
3. **A `Trace` in a `catch` is a silent failure.**

The third is worth restating for this phase specifically: a proposed structure that fails
validation must be **visibly** refused, not silently dropped, or "no bridges appeared" will
be indistinguishable from "the policy never fired".

**And one specific to Phase B:** this is the first change in the whole workstream that moves
the **street network itself** rather than the surfaces on it. Everything downstream — blocks,
estates, buildings, shops, TALE locations, nav — is derived from that network. The V2
fingerprints exist for exactly this, and B6.3 is where the cost gets stated rather than
discovered.
