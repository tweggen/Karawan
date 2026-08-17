# Streets Generator Rework — Architecture Proposal

**Status:** Proposed
**Scope:** `engine.streets.Generator` and the topology-mutation half of `StrokeStore`
**Priorities:** 1. Maintainability, 2. Extensibility to multilayer (bridges/tunnels), 3. Cost parity with the current implementation. Data-driven ruleset as a bonus.

---

## 1. Diagnosis: why the current Generator is hard to maintain

The *algorithm* is fine. `Generator.Generate()` is a stochastic growth process of the
classic "global goals + local constraints" family (Parish & Müller 2001, refined in
Kelly & McCabe's *Citygen*): pop a candidate segment from a work queue, check it against
the existing network, repair or reject it, commit it, emit successor candidates.
That family is the right choice — it is incremental, seedable, cheap
(one octree neighbourhood query set per candidate), and it produced the street layouts
the rest of the engine is built around.

The *structure* is the problem. Five distinct responsibilities are fused into one
1200-line method and cannot be tested, reasoned about, or extended in isolation:

1. **Candidate validation** — bounds, minimum length, point-snapping, angle
   separation, stroke proximity — implemented as one `while (continueCheck)` loop
   steered by `doAdd` / `continueCheck` flags and `break`/`continue` at ~10 exit
   points (`Generator.cs:585-1009`).
2. **Topology surgery** — the intersection-split case rewires `stroke.A/B` of a
   *stored* stroke inline (`Generator.cs:804-1001`), reaching around `StrokeStore`'s
   invariants. The pasted crash stack trace at `Generator.cs:932-941` is the fossil
   record of exactly this kind of surgery going wrong.
3. **Successor generation** — forward/left/right/random emission with hard-coded
   probability properties, duplicated four times with copy-paste variations
   (`Generator.cs:1072-1193`).
4. **Forensic scaffolding** — the "Option A"/"Option B" orphan-point tracking
   (`_createdStreetPointIds`, `_orphanedPointOrigins`, `_markNewPointsForStroke`,
   `_cleanupFailedStrokePoints`, `_reportOrphanedPoints`) exists *only* because
   `StreetPoint` objects are allocated eagerly, before a candidate has passed
   validation. The safeguards compensate for a structural flaw instead of removing it.
5. **Post-processing** — connected-component analysis and orphan-bundle bridging
   (`_connectOrphanedBundles` and helpers, ~240 lines) live in the same class.

On top of that: dead `#if false` blocks, an `if(false)` branch, and `TXWTODO`s that
mark decisions nobody can safely revisit because the control flow is too entangled.

Finally, **planarity is a hidden global assumption**: every crossing becomes an
intersection split, and `StreetPoint.Pos3` hardcodes `Y = 0`. Bridges and tunnels are
not merely unimplemented — the current structure actively forbids them.

---

## 2. Target architecture

Keep the algorithm family (cost parity demands it), but factor it into the four
roles the literature already names, plus a data-driven rule layer. New code lives in
`engine.streets.generation`; the public entry point (`ClusterDesc._generateStrokes`)
keeps working against a thin façade so downstream code never notices.

```
                 ┌──────────────────────────────────────────────────┐
                 │                 StreetGenerator                   │
                 │  (thin driver: queue loop, budget, determinism)   │
                 └──┬───────────────┬───────────────┬───────────────┘
   seeds from       │               │               │
   ruleset ────► Frontier ──► ConstraintPipeline ──► NetworkBuilder ──► ExpansionRules
                 (queue of      (ordered list of      (ONLY place        (data-driven:
                 candidates)    ICandidateConstraint;  that mutates       emit successor
                                pure decisions)        StrokeStore)       candidates)
                                                          │
                                              ┌───────────┴───────────┐
                                              │ post passes:          │
                                              │ ConnectComponentsPass │
                                              │ PolishPass            │
                                              │ ReportPass (opt-in)   │
                                              └───────────────────────┘
```

### 2.1 Candidates are values, not entities

The single highest-leverage change. A candidate is a plain immutable record — no
`StreetPoint`, no `Stroke`, no IDs, nothing allocated in the stores:

```csharp
public enum StrokeKind : byte { Street, Ramp, Bridge, Tunnel, ConnectorBridge }

public readonly record struct StrokeCandidate(
    int AnchorPointId,      // existing StreetPoint (already in store), or -1 for seeds
    Vector2 AnchorPos,      // used when AnchorPointId == -1
    Vector2 TargetPos,      // proposed far end
    float Weight,
    bool IsPrimary,
    sbyte Level,            // 0 = ground, +1 bridge deck, -1 tunnel, …
    StrokeKind Kind,
    short RuleId            // which expansion rule emitted it (debug/telemetry)
);
```

`StreetPoint`/`Stroke` objects come into existence **only** at commit time, inside
`NetworkBuilder`. A rejected candidate is dropped on the floor — there is nothing to
clean up. The entire Option A/Option B orphan machinery (~150 lines plus per-candidate
bookkeeping) is deleted, not migrated: orphan points become impossible *by
construction* instead of being detected after the fact.

### 2.2 Constraint pipeline: one check = one class

Each check currently inlined in the `while (continueCheck)` loop becomes a small,
individually testable class with an explicit verdict — the flag-and-break steering
becomes a typed result:

```csharp
public abstract record Verdict
{
    public sealed record Accept : Verdict;
    public sealed record Reject(string Reason) : Verdict;
    public sealed record Restart(StrokeCandidate Modified) : Verdict;   // e.g. snapped endpoint
    public sealed record SplitAndRestart(                              // intersection found
        StrokeCandidate Head, Stroke ToSplit, Vector2 SplitPos,
        StrokeCandidate? Tail) : Verdict;
}

public interface ICandidateConstraint
{
    Verdict Check(in StrokeCandidate cand, StrokeStore store, in GenerationContext ctx);
}
```

The driver loop is then trivially readable and provably bounded:

```csharp
// inside StreetGenerator
Verdict RunPipeline(StrokeCandidate cand)
{
    for (int pass = 0; pass < MaxRestartsPerCandidate; ++pass)   // replaces continueCheck
    {
        foreach (var constraint in _pipeline)
        {
            switch (constraint.Check(cand, _store, _ctx))
            {
                case Verdict.Reject r:           return r;
                case Verdict.Restart m:          cand = m.Modified; goto restart;
                case Verdict.SplitAndRestart s:  return s;    // handled by NetworkBuilder
                case Verdict.Accept:             continue;    // next constraint
            }
        }
        return new Verdict.Accept();
        restart: ;
    }
    return new Verdict.Reject("restart budget exhausted");
}
```

The initial pipeline is a 1:1 extraction of today's checks, in today's order, with
today's thresholds — behavior-preserving:

| Order | Class | Replaces |
|---|---|---|
| 1 | `BoundsConstraint` | `_inBounds` + `_willStrokeEndpointBeValid` bounds/edge part |
| 2 | `MinLengthConstraint` | "too short" check (`Generator.cs:595`) |
| 3 | `SnapToNearbyPointConstraint` | `FindClosestBelowButNot` endpoint merge (`:653`) → `Restart` |
| 4 | `AlreadyConnectedConstraint` | `AreConnected` check (`:679`) |
| 5 | `AngleSeparationConstraint` | both angle-array scans (`:691-735`), deduplicated |
| 6 | `StrokeNearPointConstraint` | `GetClosestPoint` check (`:742`) → `Restart` or `Reject` |
| 7 | `PointNearStrokeConstraint` | `GetClosestStroke` check (`:773`) |
| 8 | `IntersectionConstraint` | `IntersectsMayTouchClosest` + split decision (`:804`) → `SplitAndRestart` |

Each is ~20-40 lines, exercises pure geometry + read-only store queries, and can be
unit-tested with a handful of strokes in a `StrokeStore` — no engine, no cluster.

### 2.3 Topology mutation lives only in the store layer

`NetworkBuilder` (or equivalently, new atomic operations on `StrokeStore`) is the
only code that creates/removes stored strokes and points:

```csharp
public sealed class NetworkBuilder
{
    public Stroke Commit(in StrokeCandidate cand);          // materialize + AddStroke
    public StreetPoint SplitStrokeAt(Stroke s, Vector2 pos); // remove, split, re-add — atomic
}
```

`SplitStrokeAt` encapsulates the remove/copy/rewire/re-add dance currently open-coded
in the generator (`Generator.cs:919-968`). Invariants (octrees, `_setStrokes`
adjacency set, `InStore` flags, angle arrays) are maintained in one place; the
generator can no longer half-rewire a stored stroke. `Stroke.A`/`Stroke.B` setters
should assert the stroke is unattached, turning the historical crash class into an
immediate, local failure.

### 2.4 Expansion rules as data — the DSL

Successor emission (today: four near-identical hard-coded blocks plus a thicket of
probability properties) becomes a table of rules evaluated after each commit. This is
where the requested data-driven ruleset fits, and it uses the engine's existing Mix
config idiom (referenced from `models/nogame.json` like every other satellite file):

```jsonc
// models/nogame.streets.json
{
  "streetGen": {
    "params": {
      "minPointDistance": 30, "minStrokeDistance": 30,
      "weightMin": 0.2, "weightMax": 1.3,
      "newStrokeMinimum": 60, "newLengthMin": 75,
      "angleMinStrokes": 40
    },
    "seeds": [
      { "kind": "randomInner", "countFrom": "rnd8>>5 + 1", "weight": "inner" },
      { "kind": "corner", "corner": "bl", "angleDeg": 45, "weight": "outer" },
      { "kind": "corner", "corner": "br", "angleDeg": 135, "weight": "outer" },
      { "kind": "corner", "corner": "tr", "angleDeg": 225, "weight": "outer" },
      { "kind": "corner", "corner": "tl", "angleDeg": 315, "weight": "outer" }
    ],
    "rules": [
      { "name": "forward",  "prob": 252, "dir": "forward",
        "weightProbs": { "dec": 5,   "inc": 10 }, "keepPrimary": true },
      { "name": "branchR",  "probExpr": "150 / (1 + 4*(1-w))", "dir": "right",
        "weightProbs": { "dec": 190, "inc": 3 },  "keepPrimary": false },
      { "name": "branchL",  "probExpr": "150 / (1 + 4*(1-w))", "dir": "left",
        "weightProbs": { "dec": 190, "inc": 3 },  "keepPrimary": false },
      { "name": "random",   "probExpr": "80 - 60*w", "dir": "randomAngle",
        "weightProbs": { "dec": 5,   "inc": 10 }, "keepPrimary": true }
    ]
  }
}
```

Implementation notes, to keep this cheap and deterministic:

- **Parse once, run compiled.** The JSON is parsed at cluster-generation setup into an
  array of `ExpansionRule` structs; `probExpr` supports only the two shapes already in
  the code (affine in normalized weight, and the branch hyperbola) — a tiny expression
  whitelist, not a general evaluator. No parsing, boxing, or dictionary lookups inside
  the generation loop.
- **Probabilities stay integers 0..256**, drawn from the single seeded
  `RandomSource`, preserving the existing cross-platform determinism scheme.
  Rule evaluation order = array order in the file; determinism therefore survives
  rule edits predictably.
- **Defaults in code.** If the config section is absent, a static default table
  identical to today's constants is used (same pattern as animation packs / group
  types). Tests pin the default table.
- Aihao gets this section for free through the generic `JsonPropertyEditorViewModel` —
  street-generation tuning becomes editable in the IDE without a dedicated editor.

### 2.5 Multilayer: bridges and tunnels

The layer model rides on three small extensions rather than a new system:

1. **`sbyte Level` on `StreetPoint`, `Stroke`, and `StrokeCandidate`** (default 0;
   LiteDB/JSON-additive, old cached clusters load as all-ground).
2. **Constraints become level-scoped.** The key insight: *a bridge is precisely a
   crossing that the intersection constraint is told to ignore.*
   - `IntersectionConstraint`, `SnapToNearbyPointConstraint`,
     `StrokeNearPointConstraint`, `PointNearStrokeConstraint`,
     `AngleSeparationConstraint` all filter neighbourhood query results to
     `stroke.Level == cand.Level`. Same-level crossings split as today; cross-level
     crossings pass through untouched — that *is* the overpass.
   - `StrokeStore` keeps per-level octrees (`Dictionary<sbyte, …>` of the two
     existing octree types, alongside per-level point/stroke sub-lists). For an
     all-ground cluster this degenerates to exactly today's two octrees —
     zero added cost for the common case. Queries within a level get *cheaper*
     (smaller trees) the moment a second level exists.
   - New, layer-only constraints slot into the same pipeline: `ClearanceConstraint`
     (a level-±1 stroke must keep lateral distance from same-level ramps/portals),
     `SpanLengthConstraint` (bridges/tunnels have min/max span). They are additions
     to a list, not surgery on a loop.
3. **Level changes only via `Ramp` strokes.** A ramp is a stroke whose endpoints are
   on adjacent levels; it is the only stroke kind allowed to join points of different
   levels (asserted in `NetworkBuilder.Commit`). Rules emit them:

```jsonc
{ "name": "overpass", "minWeight": 1.0, "prob": 48,
  "when": "intersectionRejected",          // fed back from IntersectionConstraint
  "emit": [ { "kind": "ramp", "dLevel": 1 },
            { "kind": "bridge", "level": 1, "lengthExpr": "span + 2*clearance" },
            { "kind": "ramp", "dLevel": -1 } ] }
```

   i.e. when a heavy street's candidate is rejected (or would be split) at a heavy
   crossing, the ruleset may respond with a ramp–deck–ramp chain instead of a flat
   intersection; tunnels are the `dLevel: -1` mirror. Because the chain members are
   ordinary candidates, they run through the same pipeline — a bridge that lands in
   an invalid spot is rejected like any street, and the chain aborts cleanly (its
   pieces were never committed; commit of a chain happens only when all members pass,
   which the value-type candidate design makes trivial to buffer).

**Height is deliberately not the generator's problem.** The network stays 2D-per-level
(`Pos` remains `Vector2`); elevation is applied downstream where geometry is built,
as `terrainHeight + Level * deckHeight` with ramp interpolation. `StreetPoint.Pos3`'s
hardcoded `Y = 0` becomes `Y = ElevationOf(Level)` behind one function.

**Downstream containment** (explicitly out of scope here, but the boundaries are
clean):
- `QuarterGenerator` consumes the **level-0 subgraph only** — faces of a planar graph
  are only meaningful per level, and quarters/estates/buildings live on the ground.
  One filter at its input; its algorithm is untouched.
- `GenerateClusterStreetsOperator` / NavLane generation iterate strokes and junction
  sections; they gain the elevation hook and, later, deck/ramp meshes. Until then, a
  ruleset with no bridge rules produces bit-identical all-ground networks, so the
  layer capability can merge long before the renderer knows about decks.

### 2.6 Post passes and diagnostics

- `ConnectComponentsPass` — today's `_connectOrphanedBundles` + BFS + hull, moved
  verbatim into its own class, run by the driver after the queue drains. (Its
  bridge strokes get `Kind = ConnectorBridge`, making the healing visible in data
  instead of only in logs.)
- `PolishPass` — wraps `PolishStreetPoints` + section precomputation.
- `GenerationReport` — an opt-in struct collecting per-rule emission counts,
  per-constraint rejection counts (the `Reject.Reason` strings aggregate for free),
  and component stats. Replaces all inline safeguard logging; costs nothing when
  disabled. This is *more* forensic power than the current orphan tracker, at zero
  steady-state cost.

---

## 3. Cost parity argument

- **Same asymptotics, same constants where it matters.** The dominant cost per
  candidate is unchanged: one octree point-neighbourhood query, one octree
  bounds/collision query, one ray query — exactly the calls the current loop makes,
  in the same order, with the same early-outs (pipeline order = current check order,
  and a `Reject` short-circuits just like `break` does today).
- **Fewer allocations.** Candidates are stack-friendly value records vs. today's
  two heap `StreetPoint`s + one `Stroke` per *proposed* segment (most of which are
  rejected and become garbage — plus the tracking-set churn that watches them die).
- **Rule evaluation** is an array walk over precompiled structs + one `Get8()` per
  rule — identical work to today's four inline blocks.
- **Per-level octrees** are free until a second level exists (single dictionary
  lookup resolves to the same two trees).
- The abstract `Verdict` records allocate on `Restart`/`Split` only — both rare
  (snap/split events, not per-candidate). If profiling ever cares, `Verdict` can
  become a struct + enum without changing the architecture.

Removing the always-on orphan bookkeeping (two `HashSet` inserts + a dictionary per
candidate) means the rework should come out marginally *cheaper* than the status quo.

---

## 4. Migration plan (strangler, each step shippable)

Determinism note up front: steps 1-3 are intended to be **output-identical** on a
fixed seed and are gated on that. Step 4+ changes outputs by definition (new content),
which invalidates `ClusterStorage`-cached streets — that's a world-content version
bump, to be coordinated like any generation change.

1. **Extract topology surgery.** Add `NetworkBuilder.SplitStrokeAt` /
   `Commit`; make the old `Generate()` call them; add the unattached-mutation
   asserts on `Stroke`. Pin with a determinism snapshot test (same seed →
   identical point/stroke sets, byte-for-byte via the existing serializers) using
   `StreetGenerationDiagnosticsTests` as the harness.
2. **Extract the constraint pipeline** 1:1 per the table in §2.2; delete the
   orphan-tracking machinery (now provably dead); move `_connectOrphanedBundles`
   into `ConnectComponentsPass`. Old `Generator` becomes the ~100-line driver.
   Snapshot test must stay green.
3. **Rules to data.** Introduce the compiled rule table + Mix config with defaults
   matching current constants; seeds (`_addHighwayTriggers`) move into the ruleset.
   Snapshot test must stay green with the default table.
4. **Layers.** `Level` field, per-level octrees, level filters in constraints,
   `Ramp` kind + chain-buffered commit, `ClearanceConstraint`. Ground-only rulesets
   remain snapshot-identical; add new tests for two-level scenarios (crossing
   without split, ramp level-adjacency invariant, clearance rejection).
5. **Downstream elevation hook** (`Pos3`/geometry/navlanes) — separate proposal once
   4 is in.

Each step is a normal PR with `./run_tests.sh all`; steps 1-2 also delete more code
than they add.

## 5. Test strategy

- **Unit (new, cheap):** each constraint against hand-built 3-5 stroke stores;
  `NetworkBuilder.SplitStrokeAt` invariants (octree membership, adjacency set,
  angle arrays); rule-table parsing incl. rejection of unknown `probExpr` shapes.
- **Determinism gate:** fixed seeds → serialized network hash, asserted per
  migration step (the load-bearing regression net for steps 1-3).
- **Property checks** on generated clusters: single connected component (or
  explicit `ConnectorBridge` count), no same-level crossing without a shared point,
  all cross-level joints are `Ramp`s, min-distance invariants hold.
- Existing `StreetGenerationDiagnosticsTests` continues to run as the integration
  smoke.
