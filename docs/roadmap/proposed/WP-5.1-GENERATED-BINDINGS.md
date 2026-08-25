# WP-5.1 — Generated GL bindings

**Status:** ✅ Generator written, output compiles, completeness verified — **not yet swapped in (WP-5.2)**
**Date:** 2026-08-07
**Branch:** `platform/wp-5.1`
**Decision this implements:** S2a in its *narrow* form — generate only the surface actually
bound, not Silk's whole API. Chosen by the owner 2026-08-07 after
[`WP-5.0-GL-BINDINGS.md`](WP-5.0-GL-BINDINGS.md).

---

## 1. Why the first step was measurement, again

WP-5.0 established the expensive part of this work: not entry-point *names*, but Silk's
**overload expansion**, which `gl.xml` cannot describe. The narrow-form decision says to emit
only what our call sites bind to — so the generator's input is *"which overloads, exactly?"*,
and nothing else could be built until that was known.

A text search cannot answer it. It finds names; it cannot tell you that
`_gl.BindBuffer(...)` resolves to `BindBuffer(BufferTargetARB, uint)` at one call site and
`BindBuffer(GLEnum, uint)` at another. Roslyn resolves each invocation to a specific
`IMethodSymbol`, so it can.

`wp-5.1/surface/` is that probe. Output: [`surface.json`](wp-5.1/surface/surface.json).

## 2. The target, measured

| | WP-5.0 (text search) | **WP-5.1 (Roslyn)** |
|---|---|---|
| call sites into `Silk.NET.OpenGL` | 225 | **339** |
| distinct members | 73 | **81** |
| **distinct signatures to emit** | not measurable | **99** |
| enum types | 24 | **30** |
| enum members | ~180 | **114** |

**WP-5.0's numbers were wrong in both directions**, and this supersedes them:

- Call sites were **undercounted** (225 → 339). The regex only matched a fixed set of
  receiver spellings; calls reached through other expressions were invisible to it.
- Enum members were **overcounted** (~180 → 114). The regex swept in non-GL types that merely
  looked GL-ish, which is exactly why that survey needed the hand-maintained `NOT_GL` exclusion
  list. The Roslyn probe needs no such list: a symbol either lives in `Silk.NET.OpenGL` or it
  does not.

Nothing built on the old figures; they were only ever used to argue S2a vs S2b, and the
conclusion there does not turn on them.

### What "narrow form" buys, numerically

Silk exposes **466 overloads** behind the members we touch. We bind **99 signatures**. Generating
the used surface rather than replacing Silk's is a **4.7× smaller job** — this is the decision
made concrete.

## 3. The dual typed-enum / `GLEnum` surface is pervasive

**17 of 81 members need more than one overload.** Almost every case is the same shape ADR §11c
named: the codebase calls the same entry point with a *typed* enum in one place and the
catch-all `GLEnum` in another.

```
GL.GetInteger   (2 overloads, 36 sites)  void GetInteger(GLEnum, out int)
                                         void GetInteger(GetPName, out int)
GL.Disable      (2 overloads, 18 sites)  void Disable(EnableCap) / Disable(GLEnum)
GL.Enable       (2 overloads, 17 sites)  void Enable(EnableCap)  / Enable(GLEnum)
GL.TexParameter (2 overloads, 15 sites)  void TexParameter(GLEnum, GLEnum, float)
                                         void TexParameter(TextureTarget, TextureParameterName, int)
GL.BindTexture  (2 overloads, 10 sites)  void BindTexture(GLEnum, uint) / (TextureTarget, uint)
GL.Uniform1     (3 overloads,  6 sites)  void Uniform1(int, float) / (int, int) / (int, uint)
```

`GLEnum` alone contributes **55 of the 114 enum members** — by far the largest single type, and
the one with no counterpart in any other binding.

**Consequence for the generator:** it cannot emit one method per `gl.xml` command. It must emit
the specific set of shapes recorded in `surface.json`, including both the typed and `GLEnum`
forms wherever the code uses both. That is the hand-written policy from WP-5.0's prototype,
now driven by measured data instead of judgement.

## 4. Two things the probe found that the generator must handle

- **`GL.GetApi` is part of the surface** — `GetApi(INativeContext)` and
  `GetApi(IGLContextSource)`, 3 call sites. These take types from `Silk.NET.Core`, not
  `Silk.NET.OpenGL`. The generated binding needs an equivalent factory and its own minimal
  context abstraction; WP-5.0's prototype already sketched one (`DelegateProcContext`).
- **`Shader` is referenced as a non-enum type**, alongside `GL`. Worth confirming what binds to
  it before assuming the generator only needs a `GL` class.

## 5. Method note: a silent-zero failure, avoided

The first version of this probe used `MSBuildWorkspace`. It failed to load the multi-targeted
sibling projects (`DefaultEcs`), produced a compilation **with no references**, and reported
**zero** GL call sites — indistinguishable from "this code uses no GL".

That is precisely the failure mode this programme keeps running into: a tool that succeeds and
reports nothing. The probe now builds its compilation explicitly from MSBuild's own resolved
reference list (`dotnet msbuild -t:ResolveReferences -getItem:ReferencePath`, 200 references,
**0 diagnostics**) and **exits non-zero if it resolves zero call sites**, so the failure cannot
be mistaken for a result again.

## 6. The generator

[`wp-5.1/gen.py`](wp-5.1/gen.py) → `Splash.API.OpenGL/generated/GL.g.cs` (1,138 lines), namespace
`Splash.API.OpenGL`.

Where each input is authoritative — this split is the point of the exercise:

| input | role |
|---|---|
| **`gl.xml`** | the **specification**. Every emitted enum value is checked against it; the support entry points are parsed from it. A versioned, regenerable artifact. |
| **`surface.json`** | the **mapping**, extracted from Silk *once*: which C# name and shape corresponds to which native entry point. `gl.xml` cannot express this. Checked in, so **Silk is not needed to regenerate**. |
| **`CONVENIENCES`** in `gen.py` | the 14 signatures with **no native counterpart** — hand-written, because they are pure API policy. |

Output composition:

```
30 enum types, 114 members     values from Silk, all 114 verified against gl.xml
85 native entry points         emitted from the recorded NativeApi EntryPoint
14 support entry points        parsed from gl.xml; needed by the conveniences,
                               but used by no call site directly
14 conveniences                hand-written: Gen*/Delete* singular wrappers,
                               string marshalling, Span forms, GetApi
```

`Splash.API.OpenGL.csproj` has **no package references at all**. That is the deliverable: the bindings
depend on a specification, not on a wrapper.

### Silk's entry points are recorded in metadata

Silk tags every method `[NativeApi(EntryPoint = "glBindBuffer")]`. The probe reads it, so the
generator never guesses a native name from a C# one — which matters, because the mapping is not
mechanical:

```
GetInteger      -> glGetIntegerv        (rename)
TexParameter    -> glTexParameterf      (merge: one C# name, several native ones)
GetStringS      -> glGetString          (rename)
```

**85 of 99 signatures carried an entry point; 14 did not.** Those 14 are Silk conveniences with
no 1:1 native call. That is the overload-expansion cost from ADR §11c, finally enumerated
exactly rather than estimated.

## 7. Verification

[`wp-5.1/verify`](wp-5.1/verify) reflects over the compiled bindings and checks them against
`surface.json`. Compiling only proves the output is valid C#; this proves it is *complete*.

```
SIGNATURE COMPLETENESS
required                    : 97
present (drop-in)           : 97
differ by design (WP-5.2)   : 2
UNEXPECTEDLY MISSING        : 0

ENUM COMPLETENESS      114 / 114
ENUM VALUES vs SILK    114 matching, 0 differing
RESULT: complete
```

### The 2 deliberate differences — and the churn they imply

`GetApi(INativeContext)` and `GetApi(IGLContextSource)` take **`Silk.NET.Core`** types.
Accepting them would preserve exactly the dependency this work removes, so the generated
bindings offer `GetApi(Func<string, IntPtr>)` and `GetApi(IGLProcAddress)` instead.

**So call-site churn is not literally zero.** It is **3 call sites of 339** — all of them the
context plumbing at the seam WP-0.2 already isolated. WP-5.0 measured 0 churn on a sample that
did not include `GetApi`; this is the corrected figure, and it is still ~1 % against OpenTK's
~83 sites.

## 8. What is NOT done

- **The swap.** Nothing references `Splash.API.OpenGL` yet. `Splash.OpenGL` still compiles against Silk,
  and WP-5.2 is where that changes. Note the swap is entangled with
  `Silk.NET.OpenGL.Extensions.ImGui`, which consumes Silk's `GL` type directly — that
  dependency has to be resolved before or during WP-5.2, and it is not addressed here.
- **Anything at runtime.** Unchanged from WP-5.0 §5: **nothing has been executed against a real
  GL context.** A binding that compiles, verifies complete, and dispatches to the wrong entry
  point looks identical to a correct one at this stage. Every `glGetProcAddress` lookup in
  `GL.g.cs` is unexercised.

  This is precisely why the plan names `GlStateSaver` and `SilkRenderState` as failing
  *silently*, and why **GATE-F reference frames must be captured before WP-5.2 merges** — after
  the swap the comparison is unrunnable forever.

## 9. Reproducing

```bash
dotnet build Splash.OpenGL/Splash.OpenGL.csproj
dotnet msbuild Splash.OpenGL/Splash.OpenGL.csproj -t:ResolveReferences -getItem:ReferencePath -v:q > refs.json
dotnet run --project docs/roadmap/proposed/wp-5.1/surface -- Splash.OpenGL refs.json surface.json
python docs/roadmap/proposed/wp-5.1/gen.py surface.json gl.xml Splash.API.OpenGL/generated/GL.g.cs
dotnet build Splash.API.OpenGL/Splash.API.OpenGL.csproj
dotnet run --project docs/roadmap/proposed/wp-5.1/verify -- <Splash.API.OpenGL.dll> surface.json
```

## 7. Reproducing

```bash
dotnet build Splash.OpenGL/Splash.OpenGL.csproj
dotnet msbuild Splash.OpenGL/Splash.OpenGL.csproj -t:ResolveReferences -getItem:ReferencePath -v:q > refs.json
dotnet run --project docs/roadmap/proposed/wp-5.1/surface -- Splash.OpenGL refs.json surface.json
```

## 10. Regeneration verified end to end (2026-08-13)

Until now `GL.g.cs` had been **hand-patched twice** — once to correct
`glTexParameterI[u]iv`, and once implicitly by trusting that the correction matched what
the generator would emit. `gl.xml` is not checked in, so neither claim had been tested.

The registry was fetched and the generator run for the first time with every guard live:

```
gl.xml   https://raw.githubusercontent.com/KhronosGroup/OpenGL-Registry/main/xml/gl.xml
         2,774,652 bytes
         sha256 fba2eaa6262cededdba0dd3cd1e3b1806c24899a7c5df8158467e41c19969426
```

```
enum values verified against gl.xml : 114
  unverifiable                      : 0
enum types emitted                  : 30
native entry points emitted         : 85
support entry points from gl.xml    : 14
conveniences (hand-written)         : 14
```

Two results worth stating separately.

**The parameter-shape guard passed against the real registry.** It had only ever been
exercised against the synthetic one in `test-shapecheck.py`. The corrected `surface.json`
genuinely agrees with the specification; the `RefKind: "in"` fix was not merely
self-consistent.

**The regenerated file is byte-identical to the checked-in one.** So the hand-patch
reproduced exactly what the generator produces, and `GL.g.cs` is now demonstrably
*regenerable* rather than maintained by hand. That distinction is the whole point of
depending on a specification: a file nobody can reproduce is a fork, whatever the header
says.

The tracer regenerates identically too, which matters because it is derived from
`GL.g.cs` — if the binding drifts, the instrument that watches it drifts with it.

Full chain, all green:

| check | result |
|---|---|
| `gen.py` with real `gl.xml` | 114 enums verified, shape guard passed |
| `GL.g.cs` regenerated vs checked in | **byte-identical** |
| `GLTrace.g.cs` regenerated vs checked in | **byte-identical** |
| `test-shapecheck.py` | 8/8 |
| `differ` (signature parity vs Silk) | exit 0 |
| `dotnet build Karawan.sln` | 0 errors |
| unit tests | 234/234 |

`gl.xml` stays out of the repository deliberately — it is a fetched input, and pinning it
here by sha256 records *which* revision was used without vendoring 2.7 MB that Khronos
already versions.
