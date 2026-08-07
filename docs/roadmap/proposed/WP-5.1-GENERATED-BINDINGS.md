# WP-5.1 — Generated GL bindings

**Status:** 🔄 In progress — **step 1 of 2 complete** (required surface resolved; generator not yet written)
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

## 6. What is NOT done

**The generator itself.** This document records the target; it does not yet emit anything.
Remaining for WP-5.1:

1. Emit the 99 signatures and 30 enum types from `gl.xml` + `surface.json`, in namespace
   `Karawan.Graphics.OpenGL`, matching Silk's names.
2. Provide the `GetApi` factory and context abstraction (§4).
3. Prove it by compiling `Splash.Silk` against the generated bindings — which is WP-5.2's swap,
   but a compile is the only real check that the surface is complete.

And, unchanged from WP-5.0 §5: **nothing has been executed against a real GL context.** A
binding that compiles but dispatches to the wrong entry point looks identical to a correct one
at this stage. That is GATE-F's job, and the plan's warning stands — **capture the reference
frames before WP-5.2 merges**, or the comparison is unrunnable forever.

## 7. Reproducing

```bash
dotnet build Splash.Silk/Splash.Silk.csproj
dotnet msbuild Splash.Silk/Splash.Silk.csproj -t:ResolveReferences -getItem:ReferencePath -v:q > refs.json
dotnet run --project docs/roadmap/proposed/wp-5.1/surface -- Splash.Silk refs.json surface.json
```
