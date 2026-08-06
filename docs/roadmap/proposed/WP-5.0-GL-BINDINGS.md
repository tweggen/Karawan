# WP-5.0 / WP-5.0b — GL binding options, costed

**Status:** ✅ Both prototypes built and measured — **awaiting the human's choice**
**Date:** 2026-08-06
**Branch:** `platform/wp-5.0`
**Plan:** [`IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md`](IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md) §5, Phase 5

Plan §5: *"Do not start WP-5.1 until WP-5.0 and WP-5.0b are both reported and the human has
chosen."* This is that report. It recommends nothing until §7 — the numbers come first.

Everything below was measured. Nothing is estimated.

---

## 1. The surface we actually depend on

The plan estimates "80 entry points and ~10 enum types". Measured against
`Splash.Silk/*.cs` (24 files) and confirmed by reflection over `Silk.NET.OpenGL` 2.23.0:

| | measured | plan's estimate |
|---|---|---|
| distinct GL entry points | **73** | 80 |
| total GL call sites | **225** | — |
| distinct enum types | **24** | ~10 |
| distinct enum members used | **~180** | — |
| of which `GLEnum` (the catch-all) members | **43** | — |

The entry-point estimate was good. **The enum estimate was 2.4× low**, and the `GLEnum`
usage was not accounted for at all — it matters disproportionately (§4).

### The number the estimate misses

Behind those 73 names, Silk exposes **466 overloads** — an average of **6.4 per entry
point**. For scale, `Silk.NET.OpenGL.GL` has 524 method names and **5,658 overloads**
total.

This is ADR §11c stated numerically: matching the *name* is trivial; matching the
**overload expansion policy** is the work.

---

## 2. WP-5.0 — self-generation from `gl.xml`

Registry: `KhronosGroup/OpenGL-Registry` `xml/gl.xml`, 2,774,652 bytes, sha256
`FBA2EAA6…9426`, 3,301 commands.

### The harness (designing it was part of the WP)

The plan warned the naive version does not work, and it was right. Three things had to be
solved before either side compiled:

1. **Namespace collision.** Generated code cannot be `Silk.NET.OpenGL` while the Silk
   package is referenced. It also cannot be `Karawan.GL` — a namespace segment named `GL`
   shadows the `GL` class. Silk sidesteps this with namespace `Silk.NET.OpenGL` + class
   `GL`; the prototype uses `Karawan.Graphics.OpenGL`.
2. **Alias chaining is illegal C#.** The first harness used
   `using GLNS = <ns>;` plus `using GL = GLNS.GL;`. A using-alias cannot reference another
   using-alias. Replaced with a plain `using <namespace>;`, which is also what a real
   migration would touch.
3. **The sample must be real.** The first sample was written from memory of the API and
   failed to compile against Silk itself — `GetInteger` takes `GLEnum` and has no
   `(uint, out int)` form, `CullFace` takes `TriangleFace`, and `GetIntegerv` does not
   exist on Silk's `GL`. The sample is now copied verbatim from `GlStateSaver.cs`,
   `BufferObject.cs`, `SilkThreeD.cs` and `SkProgramEntry.cs`.

The harness compiles **one** sample against both bindings, varying exactly one line.

### Result — AC-5.0

```
BASELINE : sample + Silk.NET.OpenGL 2.23.0     Build succeeded.
CANDIDATE: same sample + generated bindings    Build succeeded.

diff baseline/CallSites.cs candidate/CallSites.cs
1c1
< using Silk.NET.OpenGL;
---
> using Karawan.Graphics.OpenGL;

  call-site lines changed: 0
```

**AC-5.0: claim 3 holds — 0 call-site lines changed.** One `using` line per file is the
entire diff. Reported as a measured 0, not "~0".

### The caveat that matters more than the number

The prototype emits 5 entry points and 3 enum groups (162 members) from `gl.xml`
mechanically — **plus 4 overloads that had to be hand-written**, because `gl.xml` cannot
describe them:

| hand-written | why `gl.xml` cannot help |
|---|---|
| `BindBuffer(GLEnum, uint)` | registry has one `glBindBuffer`; Silk exposes typed *and* catch-all forms |
| `Enable(GLEnum)` | same |
| `GetInteger(GLEnum, out int)` | Silk **renames** `glGetIntegerv` and adds an out-param form |
| `GetInteger(GLEnum, Span<int>)` | and a `Span` form |

So for **5** entry points, **4** hand-written policy decisions. That ratio is the real
cost driver. Extrapolating it across 73 entry points and 466 overloads is what the plan's
~500 LOC estimate omits, and it is why ADR §11c calls that estimate 2–4× optimistic. On
this evidence **2–4× is itself optimistic** for a generator that must match Silk exactly.

> A generator matching only what our 225 call sites *bind to* — rather than Silk's whole
> surface — is a much smaller job. That is a real option and is not the same project.

---

## 3. WP-5.0b — OpenTK, costed honestly

Measured against `OpenTK.Graphics` **5.0.0-pre.15** by reflection, then by porting the
same sample and compiling it (`Build succeeded.`).

### Churn — AC-5.0b

| | Silk → generated | Silk → OpenTK |
|---|---|---|
| call-site lines changed | **0** | **13 of 35 code lines = 37 %** |
| per-file `using` change | 1 | 1 |
| sample compiles | ✅ | ✅ |

Extrapolating 37 % across **225 call sites ≈ 83 call sites needing edits**, plus the
structural changes below.

### Why the churn is structural, not cosmetic

**1. `GL` is a static class in OpenTK.** `OpenTK.Graphics.OpenGL.GL` is `abstract sealed`
with **1,826 static methods**; `Silk.NET.OpenGL.GL` is an *instance* with **5,652 instance
methods**. Every `_gl.Foo(…)` becomes `GL.Foo(…)`. That is **all 225 call sites**, plus
every `GL` field, constructor parameter and `SetGL`-style injection point in
`Splash.Silk` — and `Platform.SetExternalGL` / `SilkThreeD.GetGL`, the seam WP-0.2 just
finished tidying.

**2. 9 of 73 entry points are spelled differently**, and they are high-traffic ones:

| Silk | calls | OpenTK |
|---|---|---|
| `TexParameter` | 14 | `TexParameteri` / `TexParameterf` / `TexParameteriv` / … |
| `Uniform1` | 5 | `Uniform1i` / `Uniform1f` / `Uniform1iv` / … |
| `UniformMatrix4` | 3 | `UniformMatrix4f` / `UniformMatrix4fv` / … |
| `Uniform3`, `Uniform4` | 4 | ditto |
| `PixelStore` | 2 | `PixelStorei` / `PixelStoref` |
| `GetProgram` | 1 | `GetProgrami` / `GetProgramiv` |
| `GetInternalformat` | 1 | `GetInternalformati` / `…i64` / `…iv` |
| `GetStringS` | 1 | no prefix match at all |

**31 call sites**, and the edit is *not* mechanical: choosing `…i` vs `…f` vs `…iv`
requires knowing each argument's type. A regex cannot do this.

**3. `GLEnum` does not exist in OpenTK.** Silk's catch-all has 1,380 members; our code
uses **43 of them**. Each becomes a specifically-typed enum, and *which* type is a
per-call-site judgement. In the sample, `_gl.BindBuffer(GLEnum.ArrayBuffer, …)` became
`GL.BindBuffer(BufferTarget.ArrayBuffer, …)` — obvious there, not obvious everywhere.

**4. 4 of 24 enum types are renamed or absent**: `BufferTargetARB` → `BufferTarget`,
`BufferUsageARB`, `BlendEquationModeEXT`, `GLEnum`.

**5. Integer width differs.** OpenTK's `BindBuffer` takes `Int32` where Silk takes `uint`;
`CreateProgram` returns `int`, not `uint`. Handle types ripple outward from the call site.

### The dependency question, which is not about churn at all

- **OpenTK 5 is prerelease.** Latest is `5.0.0-pre.16`. `GLLoader.LoadBindings`, the
  entry point S2b is built on, is a 5.x API. The stable line is 4.9.4, with a different
  API.
- **It is already churning under us.** `pre.11` … `pre.15` ship `net8.0`.
  **`pre.16` ships `net10.0` only** — Karawan is on `net9.0`, so *the newest OpenTK
  prerelease cannot be referenced by this project at all*. That is not a hypothetical
  future risk; it happened before this report was written, which is why the measurements
  above use `pre.15`.

Adopting a prerelease dependency that has already dropped our target framework once sits
awkwardly beside the ADR's governing principle — *depend on specifications and formats,
which are regenerable; not on wrappers, which get abandoned* (§1). `gl.xml` is the
specification. OpenTK is a wrapper, and a moving one.

---

## 4. Side by side

| | **S2a — generate from `gl.xml`** | **S2b — OpenTK 5** |
|---|---|---|
| call-site churn (measured) | **0 lines** | **37 % of code lines ≈ 83 of 225 sites** |
| receiver change | none | **all 225** — instance → static |
| non-mechanical edits | none | 31 suffix choices + 43 `GLEnum` retypings |
| up-front work | a generator that must reproduce 466 overloads; 4 hand-written policies per 5 entry points measured | none — the library exists |
| ongoing maintenance | ours; `gl.xml` is spec-frozen and versioned | upstream's; prerelease, and it just dropped `net9.0` |
| dependency count | **−1** (Silk.NET.OpenGL removed, nothing added) | unchanged (Silk.NET.OpenGL → OpenTK.Graphics) |
| risk profile | large one-off cost, then stable | small one-off cost, recurring exposure |
| fits ADR §1 principle | yes — depends on the spec | no — depends on a wrapper |

---

## 5. What was NOT measured

Stated plainly, because the numbers above are narrow:

- **The prototype covers 5 of 73 entry points.** The 0-churn result is real for those
  shapes; it does not prove the remaining 68 are shape-compatible.
- **Nothing was run.** Both samples *compile*; neither was executed against a GL context.
  A generated binding that compiles and dispatches to the wrong `glGetProcAddress` entry
  would look identical here. `GlStateSaver` and `SilkRenderState` are named in the plan as
  failing *silently* on a wrong enum value — that risk is untouched by this work and is
  what GATE-F exists for.
- **The 37 % OpenTK figure comes from a 35-line sample** chosen to span the awkward
  shapes. It is representative, not a census. The structural facts behind it — static
  class, 9 renames, 43 `GLEnum` uses — are exact.
- **OpenTK 4.9.4 was not evaluated.** If the prerelease status of 5.x is disqualifying,
  someone should cost 4.x before ruling OpenTK out entirely; its API differs again.

---

## 6. Acceptance criteria

| id | Criterion | Result |
|---|---|---|
| AC-5.0 | Call-site churn measured (claim 3) | ✅ **PASS — exactly 0 changed lines**, both sides compiling, reported as a measured number |
| AC-5.0b | S2b costed | ✅ **PASS** — §3 and §4, from reflection and a compiled port |
| AC-GLOBAL-2/3 | engine, game, shaders untouched | ✅ empty — this WP adds one document |

---

## 7. Recommendation

Asked for, so given plainly — but this is the human's call under plan §5, and §4's numbers
support either choice.

**Prefer S2a (generate from `gl.xml`), and do it in the narrow form.**

1. **The churn result is decisive on its own terms.** 0 versus ~83 edited call sites, of
   which 31 need per-site type judgement, is not a close comparison. The 37 % figure also
   *understates* S2b, because it excludes the instance→static change rippling through
   every field and constructor in `Splash.Silk`.
2. **Generate only what we bind to, not Silk's whole surface.** The 466-overload figure is
   the cost of *replacing Silk*. We do not need to. We need the ~225 call sites to compile.
   Scope the generator to the shapes actually used, and the 4-hand-written-policies-per-5
   ratio applies to a much smaller number.
3. **OpenTK's prerelease churn is the argument against it.** Not theoretical: `pre.16`
   dropped `net9.0` support during this work. Swapping a maintenance-mode dependency for a
   prerelease one buys less than it appears to.
4. **Whichever is chosen, GATE-F is the real gate.** Neither prototype proves anything
   about runtime behaviour, and the silent-failure modes in `GlStateSaver` /
   `SilkRenderState` are exactly what a compile cannot catch. Capture the reference frames
   **before** WP-5.2 merges — the plan already warns this becomes unrunnable afterwards.

And the option the plan already blesses: **Phase 5 buys no correctness and no capability**
(ADR §9). Deciding not to do it, and banking Phases 0–2, remains a legitimate outcome
(§5c). Nothing in this report argues Phase 5 is *necessary* — only which route is cheaper
if it happens.

---

## 8. Reproducing this

The harness is small and self-contained; it lives under `docs/roadmap/proposed/wp-5.0/`.

```bash
python wp-5.0/gl-survey.py <repo-root>            # the 73/225/24 numbers
python wp-5.0/gen.py gl.xml Generated.cs          # generate from the registry
bash   wp-5.0/harness.sh                          # AC-5.0   : Silk vs generated
bash   wp-5.0/harness-otk.sh                      # AC-5.0b  : Silk vs OpenTK
dotnet run --project wp-5.0/apiprobe -- gl-survey.json   # the reflection numbers
```

`gl.xml` is fetched from the Khronos registry and is not vendored; the sha256 above pins
what was measured.
