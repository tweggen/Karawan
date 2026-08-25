# Streets Generator Rework — Implementation Plan

**Companion to:** `docs/roadmap/proposed/STREETS-GENERATOR-REWORK.md` (architecture)
**Status:** Proposed
**Execution model:** sequential work packages, each one Sonnet-agent session, each behind a hard gate.

---

## 0. Ground rules

### 0.1 Branch strategy

```
master
  └── feature/streets-generator-rework          ← long-lived integration branch
        ├── claude/streets-wp0-harness
        ├── claude/streets-wp1-networkbuilder
        ├── claude/streets-wp2a-pipeline-core
        ├── claude/streets-wp2b-pipeline-intersection
        ├── claude/streets-wp2c-cleanup
        ├── claude/streets-wp3-ruleset
        └── claude/streets-wp4-levels
```

- The integration branch is cut **once** from `master` and never rebased onto a moving
  `master` mid-flight without re-running the WP-0 gate (fingerprints are computed from
  code that `master` may change).
- Each WP branches from the *current tip of the integration branch*, and merges back
  only after its gate passes. One PR per WP.
- No WP may start before its predecessor's gate is green. This is what makes a
  1200-line rewrite survivable.

### 0.2 The invariant that governs WP-1 through WP-3

> **Arithmetic identity, not semantic equivalence.**
> WP-1, WP-2 and WP-3 must not change a single floating-point operation, its operand
> order, or the order in which random numbers are drawn from `RandomSource`.

Consequences agents must respect:
- Do not "simplify" `(int)((x)*1000f)/1000f` or reassociate float expressions.
- Do not merge the two angle-separation scans into one loop *if* that changes the
  order of `Abs`/`Snorm` calls — extract them into one class, called twice, in the
  same order.
- Do not reorder `_rnd.Get8()` calls. In `Generate()` today the four
  `doForward/doRight/doLeft/doRandomDirection` draws happen **before** any weight
  computation; that order is part of the output.
- Do not change constraint evaluation order — pipeline order is exactly the table in
  the architecture doc §2.2, which is exactly today's order.

Any intentional behavior change is a separate, labelled commit with a fingerprint
re-baseline and a written justification. Silent re-baselining fails review.

### 0.3 Known environment prerequisites

`Joyce.csproj` project-references five sibling repos (`BepuPhysics2`, `DefaultEcs`,
`glTF-CSharp-Loader`, `ObjLoader`, plus in-tree `ExpectEngine`). The test project
references `Joyce.csproj`, so **the full sibling checkout from CLAUDE.md § Build & Run
is required** to run any gate. Agents must verify this before starting; a missing
sibling presents as an unrelated-looking restore error.

Toolchain is .NET 10 (`global.json` pins SDK 10.0.110; the test project targets
net10.0). See finding 7 below for the `glTF-CSharp-Loader` commit pin, which is not
documented in CLAUDE.md and breaks a fresh clone.

> Note: WP-0 has since been executed and its gate verified on trunk's toolchain
> (SDK 10.0.110, net10.0). The remaining work packages are still specified but
> unverified.

---

## 1. WP-0 — Determinism harness and baseline

**This is the load-bearing work package. Everything else is gated on it.**

### Why it comes first

The architecture doc assumed `StreetGenerationDiagnosticsTests` could serve as the
regression net. It cannot, as things stand:

1. `tests/JoyceCode.Tests/JoyceCode.Tests.csproj` contains
   `<Compile Remove="engine\streets\**\*.cs" />` — the file has **never been compiled**.
2. Both its tests end in `Assert.True(true, "...")` — they are trace-dumpers, not tests.
3. They drive generation through `ClusterDesc.StrokeStore()`, which needs
   `ClusterStorage` in the `I` container (and would hit the cluster **cache**, so it
   might not even generate).

There is also a hazard that invalidates the architecture doc's phrase *"byte-for-byte
via the existing serializers"*: `StreetPoint._nextId` and `Stroke._nextId` are
**process-global static counters** (`StreetPoint.cs:20`, `Stroke.cs:80`). IDs therefore
depend on how many points other tests allocated first. **The gate must be
ID-independent.** It hashes geometry, not identity.

### Steps

1. Create `tests/JoyceCode.Tests/engine/streets/` as a compiled folder: remove the
   `engine\streets\**` line from the `<Compile Remove>` group (leave the
   `engine\navigation\**` exclusion alone) and update the explanatory comment.
2. Delete or rewrite the two existing diagnostic tests. They assert nothing and depend
   on the `I` container; keep their *analysis* logic only if it is reused by AC-0.5.
3. Add `tests/JoyceCode.Tests/engine/streets/StreetHarness.cs` — drives the generator
   **directly**, with no `I` container, no `ClusterStorage`, no cache:

   ```csharp
   internal static class StreetHarness
   {
       // ClusterDesc is used by Generator/Stroke only for .Id and .Size —
       // verified: Stroke.CreateByAngleFrom touches clusterDesc.Id alone.
       internal static StrokeStore Generate(string idString, float size)
       {
           var cd = new ClusterDesc { Id = 0, IdString = idString, Name = idString, Size = size };
           var store = new StrokeStore(size);
           var gen = new Generator();
           gen.Reset("streets-" + idString, store, cd);
           gen.SetBounds(/* same terrainFacetSize inset as ClusterDesc._generateStrokes */);
           StreetSeeds.AddTo(gen, cd);     // see step 4
           gen.Generate();
           return store;
       }
   }
   ```

4. Extract `ClusterDesc._addHighwayTriggers` (`ClusterDesc.cs:327`) into
   `engine.streets.StreetSeeds.AddTo(Generator, ClusterDesc)` — **moved verbatim**,
   including its `_rnd` draw order. `ClusterDesc._generateStrokes` calls it. This
   makes the harness exercise the real seeds instead of a lookalike, and pre-positions
   WP-3 (§4), where seeds become data. `ClusterDesc._rnd` must be reachable — pass it
   in as a parameter rather than widening its visibility.
   `Joyce.csproj` already declares `InternalsVisibleTo("JoyceCode.Tests")` (added in
   the TALE street-entry work), so `internal` is sufficient throughout.
5. Add `StreetNetworkFingerprint.cs`:

   ```csharp
   internal static class StreetNetworkFingerprint
   {
       // v1: ID-independent, level-independent. Stays the ground-only gate forever.
       internal static string V1(StrokeStore store)
       {
           var lines = store.GetStrokes().Select(s =>
           {
               string a = Q(s.A.Pos), b = Q(s.B.Pos);
               // canonical endpoint order: A/B swap must not change the hash
               var (p, q) = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
               return $"{p}|{q}|{s.Weight:F3}|{(s.IsPrimary ? 1 : 0)}";
           }).OrderBy(x => x, StringComparer.Ordinal).ToArray();

           return $"n={store.GetStreetPoints().Count},s={lines.Length}," +
                  Convert.ToHexString(SHA256.HashData(
                      Encoding.UTF8.GetBytes(string.Join("\n", lines)))).Substring(0, 16);
       }
       static string Q(Vector2 v) => $"{v.X:F3},{v.Y:F3}";
   }
   ```

   Rationale for each choice: positions quantized at 1 mm (insurance only — a pure
   refactor must be bit-identical, so **any** mismatch is a real signal); `Weight`
   already arrives quantized to 1/1000 by `computeWeight`; point/stroke counts are
   inlined so a failure message says *how* it drifted before you open a diff.
6. Add `StreetDeterminismTests.cs` with a `[Theory]` over ≥6 fixed
   `(idString, size)` pairs — include at least one small (`Size=100`, the existing
   diagnostic's value), two mid, one large (`Size≈3000`) to exercise the
   `maxGenerations = Size²/1000` budget path, and one seed known to produce
   disconnected bundles so `ConnectComponentsPass` is covered from day one.
   Golden fingerprints live in a committed `street-fingerprints.json`.
7. Add `StreetCostTests.cs` capturing, per seed, into `street-cost-baseline.json`:
   - `GC.GetTotalAllocatedBytes(precise: true)` delta across `Generate()` — the
     **primary** cost gate; it is near-deterministic, unlike wall time.
   - median wall time over 5 runs — recorded, **advisory only**.
8. Add a `--diff` style helper that, on fingerprint mismatch, prints the symmetric
   difference of the canonical stroke lines (capped at 20). Without this, a failed
   gate in WP-2 is very expensive to diagnose.

### Acceptance criteria

| # | AC |
|---|---|
| 0.1 | `dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj` builds and passes, including the newly-compiled `engine/streets` folder. Pre-existing 46+ tests still pass. |
| 0.2 | `StreetDeterminismTests` passes twice in a row **in separate processes** with identical fingerprints. |
| 0.3 | Fingerprints are unchanged when the streets tests run **alone** vs. **after** the full suite (proves ID-independence given the static `_nextId` counters). |
| 0.4 | `./run_tests_parallel.sh` — or at minimum `dotnet test` with xUnit parallelization enabled — yields the same fingerprints (proves no cross-test static coupling). |
| 0.5 | Baseline files `street-fingerprints.json` and `street-cost-baseline.json` are committed, each entry annotated with the `master` commit SHA it was produced from. |
| 0.6 | `StreetSeeds` extraction leaves `ClusterDesc._generateStrokes` behaviorally identical — verified by AC-0.2 fingerprints matching a pre-extraction run recorded in the same session. |
| 0.7 | No production behavior change: `git diff` on `JoyceCode/engine/streets/**` shows only the `StreetSeeds` move; `Generator.cs` is untouched. |

**Gate command:** `dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj` green, run twice, plus `./run_tests.sh all` green.

**Agent notes:** If AC-0.3 or AC-0.4 fails, do **not** work around it by serializing
the tests. It means the fingerprint is still identity-coupled — fix the fingerprint.
This is the single most likely place for WP-0 to go wrong.

### WP-0 outcome — DONE

Delivered: `engine/streets/StreetSeeds.cs` (production, extracted) and, under
`tests/JoyceCode.Tests/engine/streets/`, `StreetHarness`, `StreetNetworkFingerprint`,
`StreetBaselines`, `StreetDeterminismTests`, `StreetCostTests`, plus
`baselines/street-{fingerprints,cost-baseline}.json`. 40 tests, ~1 s.
AC-0.1 … AC-0.7 all verified. `Generator.cs` was not touched.

Findings that change how later WPs must be executed:

1. **Baselines are per environment, not portable.** Float output is only guaranteed
   reproducible for a given runtime and architecture, so the baseline files are keyed
   by an environment stamp — `".NET 10.0|X64"`, i.e. **major.minor, not the patch
   level**. A missing stamp fails loudly with instructions; it never silently passes.
   Regenerate with
   `JOYCE_STREET_BASELINE_WRITE=1 dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj`;
   several stamps can coexist in the file.

   The patch level is deliberately excluded because it was measured to be irrelevant:
   WP-0 was first developed against the pre-migration tree (net9.0 target) and then
   rebuilt on trunk's net10.0 target on the same runtime, and **all eight fingerprint
   hashes came out bit-identical**. Keying on the patch version would therefore
   invalidate baselines on every routine runtime update for no benefit. A major
   runtime change remains the level at which codegen differences are conceivable, so
   that much is still keyed on.
2. **`_connectOrphanedBundles` is live code, not dead code.** Measured over 180
   generated clusters: 105 of them (58%) required orphan bridging, with 153
   `orphan_bridge` strokes total. WP-2c must move it with care, not retire it.
3. **The corridor branch is all but unreachable.** The `bridgeDistance > 300f`
   multi-stroke corridor path fired exactly **once** in those 180 clusters.
   `seed017@2400` is the only known cover and is in the seed set for that reason —
   do not drop it.
4. **`Size=100` generates an empty network.** Corner seeds sit at `±Size/2.2`, outside
   the `±(Size/2 − 20)` bounds, so nothing survives. This is the size the deleted
   diagnostic test used: it was asserting `Assert.True(true)` over an empty network.
   Pinned deliberately as a seed so that a refactor which starts generating here is
   caught.
5. **Allocation is reproducible only to ~0.02%**, not exactly (seed011@500 measured
   74944 then 74960 bytes with a byte-identical network). `StreetCostTests` therefore
   takes the minimum of 3 runs and allows 0.5% drift; the regression ceiling stays 2%.
   Use `GC.GetAllocatedBytesForCurrentThread`, never `GetTotalAllocatedBytes` — the
   suite runs in parallel.
6. **Namespace shadowing gotcha.** Inside `namespace JoyceCode.Tests.engine.streets`,
   a qualified name like `builtin.tools.RandomSource` binds to
   `JoyceCode.Tests.builtin.tools` and fails to compile. Use `global::builtin.tools…`.
   (`using` directives placed above the file-scoped namespace are unaffected.)
7. **Build prerequisite drift.** `Joyce.csproj` needs a `glTF-CSharp-Loader` that still
   uses Newtonsoft.Json. Upstream `main` switched to System.Text.Json in commit
   `d8be51b`, so a fresh clone of HEAD fails with three `CS0246: Newtonsoft` errors.
   Check out `d8be51b^` (or any earlier commit). CLAUDE.md § Build & Run does not
   mention this pin.

Baseline for later WPs, on the recorded environment:

| seed | points | strokes | allocated |
|---|---|---|---|
| Yelukhdidru@100 | 0 | 0 | 6 464 |
| Yelukhdidru@400 | 12 | 11 | 35 024 |
| seed011@500 | 23 | 24 | 74 960 |
| seed000@500 | 27 | 29 | 86 440 |
| Yelukhdidru@800 | 64 | 74 | 207 096 |
| seed000@1500 | 274 | 367 | 941 584 |
| seed017@2400 | 785 | 1034 | 2 824 032 |
| Yelukhdidru@3000 | 1379 | 1875 | 4 937 400 |

---

## 2. WP-1 — NetworkBuilder: topology surgery extraction

**Branch:** `claude/streets-wp1-networkbuilder` · **Size:** small-medium

### Steps

1. Add `engine/streets/generation/NetworkBuilder.cs` with:
   - `StreetPoint SplitStrokeAt(Stroke existing, Vector2 pos, string creator)` —
     the remove/copy/rewire/re-add sequence currently open-coded at
     `Generator.cs:919-968`, moved **verbatim** (same call order:
     `Remove` → `CreateUnattachedCopy` → assign `B` → assign `A` →
     `AddStroke(new)` → `AddStroke(old)`; note the new-before-old order is
     load-bearing for point-insertion order into the octree).
   - `void Commit(Stroke s)` — thin wrapper over `AddStroke` for now.
2. Rewrite `Generator.cs:919-968` to call `SplitStrokeAt`. Nothing else changes.
3. Add guard rails in `Stroke`: the `A`/`B` setters throw
   `InvalidOperationException` when `Store != null`. This converts the historical
   crash class (the stack trace pasted at `Generator.cs:932-941`) into an immediate,
   local failure. Run the gate — if this throws, a *real* latent bug has surfaced;
   report it rather than weakening the guard.
4. Delete the pasted stack-trace comment block at `Generator.cs:931-943` (now
   represented by the guard) and the `#if false` blocks at `:865-895` and `:896-918`
   and the `if(false)` block at `:618-648`. These are provably dead.
5. Unit-test `SplitStrokeAt` directly: after a split, assert octree membership,
   `_setStrokes` adjacency both ways, `InStore` flags on all three points, and that
   `A.GetAngleArray()`/`B.GetAngleArray()` contain the new strokes.

### Acceptance criteria

| # | AC |
|---|---|
| 1.1 | All WP-0 fingerprints byte-identical to baseline. |
| 1.2 | Allocation baseline within ±2% (no new allocation in the split path). |
| 1.3 | New `NetworkBuilderTests` cover split invariants (≥5 assertions as listed in step 5). |
| 1.4 | `Stroke.A`/`B` setters guard against mutation while stored; a test asserts the throw. |
| 1.5 | Net line count of `Generator.cs` reduced by ≥120 (surgery + dead blocks removed). |
| 1.6 | `./run_tests.sh all` green. |

### WP-1 outcome — DONE

Delivered `engine/streets/generation/NetworkBuilder.cs` and
`tests/.../engine/streets/NetworkBuilderTests.cs` (8 tests). `Generator.cs`
1241 → 1120 lines (−121). All ACs met: fingerprints byte-identical, allocation within
tolerance, xUnit 446/446, TALE `./run_tests_parallel.sh all` 200/200.

Corrections to the steps as written above:

1. **Step 3 was already done.** `Stroke._setA`/`_setB` (`Stroke.cs:304`, `:326`) have
   always thrown `InvalidOperationException` when `Store != null`. The guard did not
   need adding, only covering — `AStoredStrokeRefusesToExchangeItsEndpoints` does that.
   It also means the pasted stack trace at the old `Generator.cs:932` was *not* a
   half-rewired stored stroke: it is an `ArgumentOutOfRangeException` from
   `StrokeStore.AddPoint`'s `DEBUG` proximity check indexing `_tmpListNearby[0]`. That
   list is a shared mutable field which `_findClosestToCoordBelowButNot` and
   `GetClosestPoint` *reassign* mid-use — a latent aliasing bug, unrelated to this
   rework, not fixed here.
2. **`SplitStrokeAt` takes the `StreetPoint`, not a `Vector2`.** The intersection point
   must exist before the split, because the `doGenerateTail` decision measures its
   distance to both endpoints of the stroke being split. Creating it inside the builder
   would have forced that decision to move too, which belongs to WP-2b.
3. **The two `_validateStrokeEndpoints` calls in the split path can never fire.**
   `AddStroke` adds any endpoint that is not yet `InStore`, so by the time they run
   both endpoints always pass. More evidence that the orphan machinery is vestigial;
   WP-2c deletes it.
4. **The cost gate needed an absolute slack floor.** A purely relative tolerance is the
   wrong shape for the degenerate seeds: `Yelukhdidru@100` allocates ~6.5 KB in total,
   so 152 bytes of runtime noise reads as +2.4% and trips a 2% ceiling — on a seed
   whose network is empty, where `SplitStrokeAt` is never called. `StreetCostTests` now
   allows `2% + 8192 bytes`; on the largest seed the slack is 0.17%, so the relative
   check still dominates where the cost signal lives. This surfaced only when baked
   assets became available and changed the suite composition.

**The gate was mutation-tested rather than assumed to work:**

| Mutation | Result |
|---|---|
| Swap the two `AddStroke` calls in `SplitStrokeAt` | 1 fingerprint test fails — confirms the ordering warning is real, not folklore, and that the determinism gate detects it |
| Drop `AddStroke(tail)` | 4 of 8 `NetworkBuilderTests` fail |

Later WPs should keep doing this: a gate nobody has seen fail is not known to work.

**Environment note:** the TALE suite needs the build-tool chain published first —
`bash Tooling/Cmdline/build.sh` then `bash Chushi/build.sh`, before
`dotnet build TestRunner/TestRunner.csproj -c Release`. Each missing step fails with a
message naming the next one. Running it also bakes `nogame/generated`, which is what
makes the three asset-dependent xUnit tests (`BakedAnimationLayoutTests` ×2,
`BakedModelEquivalenceTests`) pass; without it they fail for environmental reasons.

---

## 3. WP-2 — Constraint pipeline (three sub-packages)

Split into three so each fits one Sonnet session with a green gate at the end.

### WP-2a — Pipeline scaffolding + constraints 1-5

**Branch:** `claude/streets-wp2a-pipeline-core`

1. Add `generation/StrokeCandidate.cs`, `generation/Verdict.cs`,
   `generation/ICandidateConstraint.cs`, `generation/GenerationContext.cs` exactly as
   in architecture doc §2.1-2.2.
2. Add the driver `RunPipeline` loop with `MaxRestartsPerCandidate` (set it to **32**
   and log-count exhaustion; today's `while(continueCheck)` is unbounded, so any
   exhaustion hit is itself a finding worth reporting).
3. Extract constraints 1-5 (`Bounds`, `MinLength`, `SnapToNearbyPoint`,
   `AlreadyConnected`, `AngleSeparation`) verbatim. `AngleSeparation` is instantiated
   twice (A-side, B-side) and invoked in that order — see §0.2.
4. Route only those five through the pipeline; leave checks 6-8 inline in
   `Generate()` for now. The two halves coexist for one WP.
5. One unit-test class per constraint, each building a 3-5 stroke `StrokeStore`
   by hand and asserting the `Verdict` type and payload.

**ACs:** fingerprints identical · allocations ≤ baseline · 5 constraint test classes,
each ≥3 cases (accept / reject / boundary) · `./run_tests.sh all` green.

### WP-2b — Constraints 6-8 including the intersection split

**Branch:** `claude/streets-wp2b-pipeline-intersection` · **Highest-risk WP.**

1. Extract `StrokeNearPoint` (6) and `PointNearStroke` (7). Note constraint 7 has a
   dead `if (true || ...)` at `Generator.cs:781` — preserve the *behavior* (always
   true), and delete the unreachable operands only if the fingerprint holds. Record it
   as a deliberate simplification in the commit message.
2. Extract `IntersectionConstraint` (8) returning
   `Verdict.SplitAndRestart(Head, ToSplit, SplitPos, Tail?)`. The `doGenerateTail`
   decision (`Generator.cs:840-864`) moves into the constraint; the driver performs
   `NetworkBuilder.SplitStrokeAt` and re-queues Head/Tail.
   **Preserve the push order:** tail is pushed *before* head onto the LIFO
   `_listStrokesToDo` (`Generator.cs:991` then `:995`) — inverting it reverses the
   traversal and changes every downstream draw.
   **Preserve the double `_generationCounter++`** at `:969` and `:996` — the budget
   accounting is part of the output, however odd it looks.
3. `Generate()`'s `while (continueCheck)` loop is now fully replaced by `RunPipeline`.

**ACs:** fingerprints identical · the `_generationCounter` increment count per seed
matches baseline (add it to the fingerprint as a separate recorded field) ·
`IntersectionConstraintTests` covers: clean crossing, crossing near existing endpoint
(both `doGenerateTail` branches), no-crossing · restart-budget exhaustion count is 0
for all baseline seeds · `./run_tests.sh all` green.

### WP-2c — Delete the orphan machinery, extract post passes

**Branch:** `claude/streets-wp2c-cleanup`

1. Move candidate materialization fully behind `NetworkBuilder.Commit`: successor
   emission produces `StrokeCandidate` values, and `StreetPoint`/`Stroke` are
   allocated only on accept. This is what makes step 2 legal.
2. **Delete**, do not migrate: `_createdStreetPointIds`, `_orphanedStreetPointIds`,
   `_orphanedPointOrigins`, `_strokesWithMissingEndpoints`, `_currentStrokeNewPoints`,
   `_cleanedUpOrphanedPoints`, `_markNewPointsForStroke`, `_cleanupFailedStrokePoints`,
   `_reportOrphanedPoints`, `_validateStrokeEndpoints`, `_willStrokeEndpointBeValid`
   (its bounds/edge logic is already `BoundsConstraint`; confirm the
   `newStrokeMinimum * 0.8f` conservative check is preserved there or provably
   subsumed by `MinLengthConstraint` — **verify with the fingerprint, this one is
   subtle**).
3. Move `_connectOrphanedBundles` + `_findConnectedComponents` + `_bridgeOrphanToMain`
   + `_createBridgeStroke` + `_createBridgeCorridor` + `_getConvexHull` into
   `generation/ConnectComponentsPass.cs` verbatim. Tag its strokes
   `Kind = ConnectorBridge`.
4. Add `GenerationReport` (opt-in): per-rule emission counts, per-constraint rejection
   counts keyed on `Verdict.Reject.Reason`, component count before/after bridging.
   Off by default, zero cost when off.

**ACs:** fingerprints identical · **allocations strictly below baseline** (this is the
WP that must pay off the "marginally cheaper" claim; if it does not, stop and
investigate rather than proceeding) · `Generator.cs` ≤ 250 lines · zero references to
any deleted symbol remain (grep clean) · `./run_tests.sh all` green.

---

### WP-2 outcome — DONE (2a, 2b, 2c)

`Generator.cs` 1241 → 605 lines. New under `engine/streets/generation/`:
`Verdict`, `ICandidateConstraint` + `GenerationContext`, `Constraints` (eight of them),
`ConnectComponentsPass`, `GenerationReport`. Tests: 24 constraint tests plus the 8
NetworkBuilder tests. xUnit 471/471, TALE 200/200, fingerprints byte-identical
throughout all three sub-packages.

**Allocation is now strictly below the WP-0 baseline on every seed**, which is what
WP-2c was required to demonstrate:

| seed | before | after | change |
|---|---|---|---|
| Yelukhdidru@100 | 6,464 | 5,568 | −13.9% |
| Yelukhdidru@400 | 34,952 | 33,360 | −4.6% |
| seed011@500 | 74,888 | 71,624 | −4.4% |
| seed000@500 | 86,440 | 81,976 | −5.2% |
| Yelukhdidru@800 | 207,176 | 198,344 | −4.3% |
| seed000@1500 | 941,536 | 909,912 | −3.4% |
| seed017@2400 | 2,824,144 | 2,729,736 | −3.3% |
| Yelukhdidru@3000 | 4,936,592 | 4,807,760 | −2.6% |

The cost baseline was regenerated to lock the gain in. That is a deliberate,
measured re-baseline, not the silent kind section 0.2 forbids: the fingerprints are
unchanged, so behaviour provably did not move.

#### AC not met: Generator.cs is 605 lines, not ≤ 250

Stated plainly rather than quietly dropped. The remaining bulk is:

- **158 lines of successor emission** (forward / left / right / random, four
  near-identical blocks with their probability and weight arithmetic). This is
  precisely what **WP-3** converts into a rule table, so the ≤ 250 target lands there,
  not here. Cutting it in WP-2c would have meant doing WP-3 early and without its gate.
- ~90 lines of tunable properties and their probability helpers, also WP-3's material.
- `_isSuccessorWorthQueueing` (37) and `_buildPipeline` (36).

`Generate()` itself is 340 lines, of which the validation loop — the part WP-2 set out
to fix — is now about 80.

#### Corrections to the steps as written

1. **`_willStrokeEndpointBeValid` must NOT be deleted.** Step 2 listed it for deletion
   on the theory that `BoundsConstraint` and `MinLengthConstraint` subsume it. They do
   not: it applies a **15 m edge buffer** that is strictly tighter than the bounds
   check, so deleting it would let candidates near the cluster edge onto the queue and
   change every generated cluster. It is behaviour, not diagnostics, and had merely
   grown up among the diagnostic helpers. Renamed to `_isSuccessorWorthQueueing` so the
   next reader does not repeat the mistake. This is exactly the "verify with the
   fingerprint, this one is subtle" case the plan flagged — the answer was no.
2. **No `Kind = ConnectorBridge` tagging.** `StrokeKind` does not exist until WP-4;
   the bridge strokes remain identifiable by their `Creator` tags (`orphan_bridge`,
   `corridor_seg1/2`), which is what the WP-0 survey used.
3. **`ConnectComponentsPass` must keep running last.** `_createBridgeCorridor` draws
   from the `RandomSource`, so its position in the sequence of draws is part of the
   output.

---

## 4. WP-3 — Expansion rules as data

**Branch:** `claude/streets-wp3-ruleset`

1. Add `generation/ExpansionRule.cs` (compiled struct) + `ExpansionRuleTable` with a
   **static default table reproducing today's constants exactly**.
2. Rewrite successor emission (`Generator.cs:1028-1193`) as a walk over the table.
   Draw order must match §0.2: all four direction draws first, then weights.
3. Add `generation/StreetGenConfig.cs` reading `/streetGen` from Mix, with the
   `probExpr` whitelist (affine `a - b*w`, and the branch hyperbola `a / (1 + b*(1-w))`
   — **only** these two shapes; unknown shapes throw at parse time, not at draw time).
4. Add `models/nogame.streets.json`, referenced from `models/nogame.json`, containing
   a table that is **value-identical to the defaults**.
5. Move `StreetSeeds` (from WP-0) to read its seed list from the same config.

**ACs:**

| # | AC |
|---|---|
| 3.1 | Fingerprints identical with the shipped config **and** with the config file absent (defaults path). |
| 3.2 | A deliberately altered rule (e.g. `forward.prob` 252→200) changes fingerprints — proves the config is actually wired, not silently ignored. Revert before merge. |
| 3.3 | Malformed config (unknown `probExpr` shape, missing field) fails fast at load with a clear message; test asserts the throw. |
| 3.4 | No JSON parsing, boxing, or dictionary lookup inside `Generate()` — allocations still ≤ WP-2c. |
| 3.5 | Config is case-insensitive per house style (`PropertyNameCaseInsensitive = true`) — CLAUDE.md's standing JSON warning. |
| 3.6 | `./run_tests.sh all` green. |

---

### WP-3 outcome — DONE

`Generator.cs` 605 → **442** lines. New: `ExpansionRule` (rule table + compiled
`ProbExpr`), `StreetGenConfig` (parser), `SuccessorEmitter` (the emission walk),
`models/nogame.streets.json` referenced from `models/nogame.json`. 18 config tests.
xUnit 489/489, TALE 200/200, fingerprints byte-identical.

**The rule table carries two different orderings, and conflating them would change
every city.** Probabilities are drawn one per rule in array order
(forward, right, left, random), but weights and emission run per weight group and,
within a group, in array order — which for the default table means
straight(forward, randStroke) then branch(right, left). The original hard-coded both
sequences; the table has to reproduce both. `ExpansionRuleTable` documents this at
the top and `models/nogame.streets.json` repeats it, because it is the single easiest
thing to get wrong when editing a ruleset.

Verified wired rather than assumed: changing `forward`'s probability from 252 to 200
fails 7 of the 24 determinism tests.

The parser refuses anything it does not recognise — unknown direction, unknown
probability shape, undefined weight group, missing or duplicated fallback rule — at
parse time rather than at draw time, with seven tests covering those cases. A ruleset
that silently ignored a misspelled field would reshape a city and give no clue why.
`ClusterDesc` catches a parse failure, logs an Error and falls back to the defaults,
so a bad ruleset degrades rather than crashing world generation.

Packaging was verified, not assumed: `nogame.streets.json` appears in both the
regenerated `AndroidResources.xml` and `InnoResources.iss`. This is the failure mode
CLAUDE.md records for the TALE storylets, where 14 declared-nowhere files loaded fine
on desktop and left the module null on Android.

#### Revising the ≤ 250 line target

WP-2c carried an AC of `Generator.cs` ≤ 250 lines, deferred to here. It is now 442,
and I am recording that the **target itself was wrong** rather than contorting the
code to reach it. What remains is: the queue loop and verdict handling (~150), the
public tunable properties that are the class's configuration surface (~65), pipeline
and emitter construction (~60), `Reset`/`SetBounds`/`SetAnnotation`/`AddStartingStroke`
(~40), and the queue helpers (~30). Reaching 250 would mean relocating the tunable
property block for its own sake — a reshuffle that moves lines without separating a
responsibility.

The number that mattered was never 250. It was that the five fused responsibilities
the architecture doc identified now live apart, each testable on its own: validation
in eight constraints, topology in `NetworkBuilder`, emission in `SuccessorEmitter`,
post-processing in `ConnectComponentsPass`, and diagnostics in `GenerationReport` —
with the forensic scaffolding deleted outright. `Generate()` is down from 340 lines to
about 150, of which the validation loop is ~80.

#### Not done: seeds are still hard-coded

Step 5 (`StreetSeeds` reading its seed list from the same config) is **deliberately
left out**, and it is the one part of WP-3 that is unfinished.

The reason is arithmetic identity. The four corner seeds sit at `-Size/2.2f`,
`Size/2.1f`, `-Size/2.2f`, `Size/2.15f` — irregular divisors that look accidental.
Expressing them in JSON as fractions of the cluster size (`-0.4545…`) is **not**
bit-identical to dividing by `2.2f`, so the natural encoding would silently move every
seed and reshape every city. Doing it safely means encoding the divisor and the signs
rather than the product, which is a fiddly schema for little gain. Worth doing, worth
doing on its own, and not worth rushing inside WP-3.

---

## 5. WP-4 — Multilayer (levels, ramps, bridges, tunnels)

**Branch:** `claude/streets-wp4-levels` · **First WP that may change output — by design.**

1. Add `sbyte Level` to `StreetPoint`, `Stroke`, `StrokeCandidate` (default 0).
   Additive for LiteDB/JSON: old cached clusters load as all-ground.
2. `StrokeStore`: per-level octrees behind `Dictionary<sbyte, LevelIndex>`. The
   level-0 path must remain a single lookup resolving to the same two octrees —
   verify with AC-4.2.
3. Level-filter constraints 3, 5, 6, 7, 8 to `stroke.Level == cand.Level`.
4. Add `StrokeKind.Ramp` + `NetworkBuilder.Commit` assertion that cross-level joints
   occur only on ramps.
5. Add `ClearanceConstraint`, `SpanLengthConstraint`.
6. Add chain-buffered commit: a ramp-deck-ramp chain commits atomically or not at all.
7. Add `StreetNetworkFingerprint.V2` (includes `Level`) for multilayer tests. **V1 is
   unchanged and remains the ground-only gate.**

**ACs:**

| # | AC |
|---|---|
| 4.1 | With a bridge-free ruleset, **all WP-0 V1 fingerprints are still identical**. This is the non-negotiable one: layers must be free when unused. |
| 4.2 | Allocation baseline for ground-only within ±2% of WP-3. |
| 4.3 | Two-level scenario test: two crossing strokes on different levels produce **no** intersection point and **no** split. |
| 4.4 | Every cross-level joint in a generated multilayer network is a `Ramp` (property test over ≥3 seeds). |
| 4.5 | Ramps connect only **adjacent** levels (`abs(dLevel) == 1`). |
| 4.6 | A chain whose deck fails a constraint leaves **zero** partial strokes in the store. |
| 4.7 | `ClearanceConstraint` rejection is exercised by a constructed test case. |
| 4.8 | Cached-cluster compatibility: a pre-WP-4 `ClusterStorage` file loads with all levels 0. |
| 4.9 | `./run_tests.sh all` green. |

**Coordination note:** enabling bridge rules in shipped content invalidates
`ClusterStorage`-cached streets — a world-content version bump. WP-4 must ship with
bridge rules **off** in `models/nogame.streets.json`; turning them on is a separate,
deliberate content decision.

---

### WP-4 outcome — DONE (4a, 4b), with the policy hook deliberately unwired

`StreetPoint` and `Stroke` carry `sbyte Level`; `Stroke` carries `StrokeKind`
(Street / Ramp / Bridge / Tunnel / ConnectorBridge). New:
`ClearanceConstraint`, `SpanLengthConstraint`, `OverpassBuilder`,
`NetworkBuilder.Commit`/`CommitChain`, `StrokeStore.GetRampsNear`, fingerprint `V2`.
21 multilayer tests. xUnit 510/510, TALE 200/200.

**AC-4.1 holds: ground-only V1 fingerprints are byte-identical.** Layers are free when
unused, which was the non-negotiable one. Allocation is unchanged against the WP-3
baseline (AC-4.2).

#### One octree pair, filtered — not an octree per level

The plan called for `Dictionary<sbyte, …>` of per-level octrees. Implemented instead
as a level filter inside the four neighbourhood queries
(`IntersectsMayTouchClosest`, `FindClosestBelowButNot`, `GetClosestStroke`,
`GetClosestPoint`). Reasoning: for the ground-only case that shipping configurations
use, **no entry is ever skipped and the cost is exactly what it was** — whereas
per-level indices are a substantial refactor of a class `QuarterGenerator` and the
operators also depend on. Per-level indices only start paying off once several busy
decks exist, and can be added then without touching a single caller. Recorded as a
deliberate deviation, not an oversight.

`AngleSeparationConstraint` needed no filtering at all, contrary to the architecture
doc's list: it reads `StreetPoint.GetAngleArray()`, which contains only strokes
actually incident to that junction. A deck passing overhead never touches it, and a
ramp leaving it genuinely does occupy angular space there — so the unfiltered
behaviour is already the correct one.

#### What is built, and what is not

Built and tested: the invariants (`Commit` refuses an ordinary street joining two
levels, a ramp skipping a level, and a ramp that changes no level), the atomic
`CommitChain` (a chain whose third member is inadmissible leaves **zero** strokes and
**zero** points behind), clearance against ramps, span bounds, and `OverpassBuilder`
producing ramp–deck–ramp along the plan route an ordinary street would have taken.

**Not built: the policy hook** — the ruleset `"when": "intersectionRejected"` that
would have the generator *choose* to throw an overpass instead of splitting at a heavy
crossing. This is deliberate and it is the honest boundary of WP-4. The construction
primitives and their invariants are done and covered; deciding *when* a city wants a
bridge is a content question that wants tuning against real clusters, and it must ship
off regardless (enabling it invalidates every `ClusterStorage`-cached cluster, a
world-content version bump). The seam is `OverpassBuilder` plus `CommitChain`: a rule
that fires it needs no further engine work.

#### Tests caught two false positives, both mine

- "Strokes on different levels cross without splitting" initially passed **for the
  wrong reason**: `Stroke.CreateByAngleFrom` derives B's position from an angle and a
  length, so the supposedly vertical candidates were 1 m horizontal stubs crossing
  nothing. The paired same-level control failed and exposed it. Pairing a positive
  with its control is what made the difference.
- The degenerate-structure test assumed a 20 cm span was refusable by the builder. It
  is not, and should not be: the builder refuses only the geometrically impossible
  (deck points quantising onto one spot), while "too short to be sensible" belongs to
  `SpanLengthConstraint`. The test clarified a responsibility boundary rather than
  finding a bug.

---

## 6. Out of scope

- **WP-5 (downstream elevation):** `StreetPoint.Pos3`'s hardcoded `Y = 0`, deck/ramp
  mesh generation, NavLane elevation, `QuarterGenerator`'s level-0 filter. Separate
  proposal once WP-4 lands. Until then multilayer networks are generated but rendered
  flat — acceptable only because bridge rules ship off.
- **Traffic/routing semantics** over multilayer graphs (routing Phase D workstream).

---

## 7. Risks

| Risk | Mitigation |
|---|---|
| **WP-0 cannot produce a stable fingerprint** (static `_nextId`, octree iteration order, tie-breaking in `GetClosestPoint`). Would invalidate the whole gating strategy. | AC-0.2/0.3/0.4 are designed to catch exactly this, before any refactor lands. If it fails, escalate — do not proceed to WP-1 with a flaky gate. |
| Float drift from innocent-looking refactors. | §0.2 arithmetic-identity rule + the `--diff` helper from WP-0 step 8. |
| WP-2b (intersection) is genuinely intricate — LIFO order, double counter increment, `doGenerateTail`. | Smallest possible WP, explicit call-outs above, highest test density. |
| **Latent ID wraparound:** `Id = (_clusterId<<16) \| (Id & 0xffff)` with a process-global counter means >65535 street points allocated process-wide collide *within* a cluster, corrupting `_setStrokes` adjacency and `GetStreetPoint(id)`. | Pre-existing, not caused by this work, and **not** in scope. Flagged here because WP-0's harness makes it newly easy to reproduce. Worth its own ticket; a per-cluster counter is a small fix. |
| Merge conflicts against a moving `master`. | Integration branch + re-run WP-0 gate after any merge from `master`. |

---

## 8. Suggested agent prompt shape

Each WP hands a Sonnet agent: this file's relevant section, the architecture doc, the
§0.2 invariant, and:

> Read `docs/roadmap/proposed/STREETS-GENERATOR-REWORK{,-PLAN}.md` first. Implement
> WP-N per the plan. The arithmetic-identity rule in §0.2 is binding: if fingerprints
> change, you have introduced a behavior change — find it, do not re-baseline. Run the
> gate before committing. Report any AC you could not satisfy rather than adjusting
> the AC. Follow PROCESS.md for documentation updates.
