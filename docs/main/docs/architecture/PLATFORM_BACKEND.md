# ADR: Platform backend strategy

**Status:** **ACCEPTED — implemented 2026-08-14.** Phases 0–5 are complete and merged; outcomes are
recorded against every §9 claim and every §11 challenge below. Two things this document proposed
were **not** done and say so: no CI exists, and ANGLE was never evaluated.
**Date:** 2026-08-04 · **Accepted:** 2026-08-14
**Author:** drafted with Claude Code, from a repo audit; conclusions revised twice under challenge
(see §10).
**Scope:** the platform layer only — windowing, input, GL bindings, audio bindings, model import.
Explicitly *not* the renderer design, the ECS, or the game.

**Implementation:** [`docs/roadmap/proposed/IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md`](../../../roadmap/proposed/IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md)
— work packages, executable acceptance criteria, and the agent-orchestration protocol. This document
is the *why*; that one is the *how*.

> There has never been a recorded decision in this repo about Silk.NET, rendering backends, or
> porting. That absence is itself part of the finding — it is how the present situation accumulated
> unexamined. The closest existing statement is `README.md:267-268`: *"Splash is the second renderer
> for joyce. It is now based on OpenGL 3 as provided by the Silk.NET framework."*

---

## 1. Context

Karawan is ~12 years old (since ~2014) and expected to run another 10+. Its infrastructure history
already includes three migrations: another VCS → GitHub; Haxe/OpenFL → C#/Raylib → C#/Silk.NET.

Targets: Windows, Linux, macOS desktop; Android **published to Google Play**. iOS possible later.

Owner's stated trust position (2026-08-04), which is the primary design input:

- **Trusted:** OpenGL, OpenAL, the FBX *format*, ImGui.
- **Not trusted:** Assimp as-is; Silk.NET, given 2.x's maintenance status and 3.0's uncertainty.

Constraint on solution shape: the engine must stay shapeable. Rewriting everything in C is out at
one end; adopting Godot/Unity is out at the other. The point of the project is to build the tool.

## 2. Problem

Two distinct problems, often conflated:

**2a. An acute blocker.** Google Play requires 16 KB memory-page-size support for apps targeting
Android 15+. The deadline was extended to 31 May 2026 and **has passed**. Silk's
`Silk.NET.Windowing.Sdl.aar` ships an SDL2 that is not 16 KB-aligned, and the binary is byte-identical
between 2.22.0 and 2.23.0. `Wuka/ANDROID_NATIVE_LIBS.md:114-115` records the remedy as **"Upstream"** —
i.e. there is no local fix. Android publishing is blocked today.

**2b. A chronic cost.** Recurring breakage attributed to Silk.NET across 3.5 years.

## 3. What the audit actually found

### 3a. Every documented Silk breakage is a *native-packaging* failure — none is a binding bug

| Breakage | Mechanism | What it cost |
|---|---|---|
| FBX animations corrupted (Apr–May 2026) | Silk **2.22→2.23**, a *managed minor* bump, silently swapped the bundled *native* Assimp 5.4.1→6.0.2 | `Joyce.csproj:18-19` held back to 2.22.0; `AssimpVersionDetector.cs` (127 LOC); `AssimpVersion.cs`; `docs/ANIMATION_ASSIMP_VERSION_COMPENSATION.md`; 5 commits |
| Android audio would fail to load | `OpenAL.Soft.Native` 1.21.1.2→1.23.1 added a **linux-arm64 glibc** `libopenal.so`; `linux-arm64` arch-matches `android-arm64`, so it silently beat the correct local build (warning XA4301) | `ExcludeAssets="native"` escape hatch; `recipes/build-openal-android.sh` |
| 1.7 MB Linux SDL2 shipped inside the APK | `Silk.NET.SDL` hard-depends on `Ultz.Native.SDL` for **all** TFMs, with no Android override | second `ExcludeAssets="native"` escape hatch |
| Play publishing blocked | the AAR SDL2 above | blocked; no local fix |

Meanwhile `Splash.Silk` has **381 commits over 3y5m, and zero of them are attributable to
`Silk.NET.OpenGL`**. The GL bugs that did occur (the SSBO/UBO capability split, animation frame
indexing, texture-channel state caching) are all the project's own renderer design and would survive
any binding swap unchanged.

### 3b. The failure mode has a precise shape

Silk.NET acts as a **native-binary distributor** whose native versions ride on its *managed* release
cadence. That is fine when the binding and the native must agree only on function signatures. It is
dangerous when they must agree on **struct layouts** — which is exactly the Assimp case, because
`JoyceCode/builtin/loader/fbx/FbxModel.cs` (1,720 LOC) walks raw `Scene*` / `Node*` / `Material*` /
`Metadata*` pointers.

| Axis | Binding and native must agree on | Native shipped by you? | Skew risk |
|---|---|---|---|
| OpenGL | function signatures, frozen by the Khronos registry | **no** — it's the GPU driver | **structurally zero** |
| OpenAL | function signatures, frozen by OpenAL 1.1 (2005) | yes | very low |
| SDL3 | function signatures + a few small structs; SDL has a hard ABI-stability policy within 3.x | yes | low |
| **Assimp** | **full struct layouts** | yes | **high — this is what bit you** |

### 3c. The architecture makes change cheap

- `JoyceCode` (73,663 LOC) and `nogameCode` (22,399 LOC) contain **zero** Splash or Silk references.
  One dead `using Silk.NET.Maths;` at `JoyceCode/engine/physics/Object.cs:7`.
- `Splash/` (3,123 LOC) is already Silk-free. `Splash/IThreeD.cs` is 21 methods;
  `JoyceCode/engine/IPlatform.cs` is 7 members.
- All coupling lives in `Splash.Silk/` (6,258 LOC), `Wuka/Platforms/Android/` (~700 LOC), and ~150 LOC
  of launcher glue.
- Input already crosses into the engine as **string-keyed events** (`engine/news/Event.cs`), not typed
  platform objects — ~20 translation sites at `Splash.Silk/Platform.cs:254-490`.
- GL already accepts a **foreign context** through `INativeContext`, proven in-repo by
  `Aihao/Aihao/Graphics/AvaloniaNativeContext.cs` and `Splash.Silk/PreviewHelper.cs`.

### 3d. GL coupling is smaller than it looks

- **80 distinct GL entry points**, all vanilla GL 3.3 / ES 3.0. No compute, no tessellation, no
  bindless, no multi-draw-indirect, no DSA beyond one stray `GenerateTextureMipmap`.
- **~10 distinct enum types** at call sites — `TextureTarget` ×27, `EnableCap` ×15,
  `TextureParameterName` ×14, `BufferTargetARB` ×10, `PixelType` ×8, `PixelFormat` ×8,
  `VertexAttribPointerType` ×6, `TextureUnit` ×6 — plus `GLEnum` ×92 as the catch-all.

All of it is described by **`gl.xml`**, the Khronos OpenGL registry: the same machine-readable spec
that Silk.NET, OpenTK, glad and glbinding all generate from.

## 4. Decision

### 4a. Organising principle

> **Depend on specifications and formats, which are regenerable. Do not depend on wrappers, which
> get abandoned.**

| Trusted thing | Durable artifact to depend on | What you own | Size |
|---|---|---|---|
| OpenGL | `gl.xml` (Khronos registry) | generator + generated bindings | ~500 LOC generator |
| OpenAL | `al.h` / `alc.h`, frozen since 2005 | hand-written `DllImport`s | ~250 LOC, 24 entry points |
| ImGui | `ImGui.NET` (already binding-neutral, **not** a Silk package) + the already-vendored controller | inline `ImGuiFontConfig`; drop `IView` | ~20–50 LOC |
| FBX (the format) | the format, parsed **at build time** | a swappable Chushi importer | Phase 4 |
| SDL3 (newly adopted) | vendored single-file binding + self-built native | both, pinned together | vendor as-is |

Assimp is the exception that proves the principle: it is distrusted precisely because the dependency
is on the *library*, not on a spec. Phase 4 demotes it from a shipped runtime dependency to a
replaceable build tool. If Assimp dies in 2030, swap the importer inside Chushi and re-bake; the
shipped game never knows.

**Result: Silk.NET goes from 13 packages to 0.** (13 distinct package IDs across 6 projects:
`Assimp`, `Core`, `Input`, `Input.Sdl`, `OpenAL.Extensions.Enumeration`, `OpenAL.Extensions.Soft`,
`OpenAL.Soft.Native`, `OpenGL`, `OpenGL.Extensions.EXT`, `OpenGL.Extensions.ImGui`, `SDL`,
`Windowing`, `Windowing.Sdl`.)

### 4b. Supporting rules

1. **Vendor what you cannot regenerate.** SDL3-CS is a single generated `.cs`. This is already the
   project's proven habit (`Splash.Silk/ImGui/Controller.cs` is a vendored fork).
2. **Check generated code in; keep the generator small and owned.** Regeneration should be rare and
   deliberate, never a build step.
3. **One backend everywhere.** SDL3 on all platforms, replacing today's GLFW-on-desktop /
   SDL2-on-Android split, which is currently distinguished by *string-matching the `IView` type name*
   (`Splash.Silk/Platform.cs:908-916`).
4. **Build-time over runtime for anything distrusted.**

### 4c. Why "just keep Silk.NET.OpenGL" was rejected

An earlier revision of this document recommended keeping `Silk.NET.OpenGL` on the grounds that it
ships no native and binds a frozen ABI, so it cannot break you. That argument is about
**correctness**, and it is true.

It was rejected because the governing question here is **survival**, not correctness:

- Silk.NET 2.x is maintenance-mode. The 3.0 milestone was ~49% complete as of May 2026 with no ETA,
  and 3.0 is a **full rewrite** (new SilkTouch generator, new windowing model).
- Therefore 2→3 is a *migration*, not an upgrade. Keeping `Silk.NET.OpenGL` is not a resting place;
  it is a **deferred fourth migration on someone else's schedule**.
- Removing it is far cheaper than a naive estimate suggests, *provided you generate rather than
  adopt*. Adopting a foreign API (OpenTK's naming, or raw `uint` constants) churns every one of the
  ~4,100 GL call sites. Generating from `gl.xml` while deliberately emitting the type and member
  names already in use leaves those call sites **essentially untouched**.

## 5. The risk this document is *not* addressing

Stated plainly because it outranks everything above on a 10-year horizon:

**Apple deprecated OpenGL in 2018 and caps macOS at GL 4.1.** It still works in 2026. On a 10-year
view this is the single largest platform threat, and **no binding choice affects it at all** — Silk,
OpenTK and a hand-rolled loader are equally exposed.

What protects against it is `Splash/IThreeD.cs` staying clean enough to grow a Metal / Vulkan /
WebGPU backend when needed. The repo already demonstrates this is the durable asset: `Splash/`
survived the Raylib→Silk migration intact, while `SplashCode/` — the Raylib backend — has been dead
on disk since 2023-03-20. **The abstraction persists; backends are consumable.**

So the highest-value longevity investment is not the binding swap; it is keeping the seam honest,
which is discipline rather than code. Two leaks to close, both cheap:

- **`Splash/Flags.cs` declares `GLAnimBuffers`** (`AnimSSBO`/`AnimUniform`/`AnimUBO`) — a GL-named
  enum in the platform-agnostic project. Verified: referenced *only* from `Splash.Silk/*`, so this
  is a naming leak, not a structural one. Move it into the backend project.
  (`Flags.AnimBatching` is a genuine renderer concept and stays.)
- **`Platform.SetExternalGL(GL)` / `GetGL()`** (`Splash.Silk/Platform.cs:987,1004`) expose a Silk type
  on a public surface that `Aihao/Aihao/Services/EnginePreviewService.cs` consumes directly.
  Re-express as an opaque context handle.

`IThreeD.HasPerInstanceAnimationFrames` is fine as-is — a capability query, not a GL concept.

**Not** recommended: writing a second backend speculatively. That is real work against a
hypothetical. Keep the seam clean and stay ready.

## 6. Options considered

| | LOC to write | Unblocks Play | Silk pkgs left | 10-year posture | Reversible |
|---|---|---|---|---|---|
| **S0** Stay | 0 | ❌ **no** | 6 | deferred forced migration | — |
| **S1** Own natives, keep all Silk bindings | ~0 | ⚠️ partial | 6 | unchanged; **leaves Assimp skew** | yes |
| **S2** Keep Silk for GL only | ~2,100 | ✅ | 1 | defers a 4th migration | yes |
| **S2b** SDL3 windowing + **OpenTK** for GL | **NOT COSTED** — see §11 | ✅ | 0 | ? | yes |
| **S3** Full de-Silk via self-generation ⭐ | **~2,900** | ✅ | **0** | ends the treadmill | yes |
| **S4** Migrate to MonoGame / FNA | ~10,000+, *deletes the renderer* | ✅ | 0 | new framework treadmill | no |

S3 costs roughly **+800 LOC over S2** — the `gl.xml` generator (~500), OpenAL `DllImport`s (~250),
the ImGui detach (~20–50) — because the call sites do not move.

**S1 rejected:** "keep the binding, own the native" is actively wrong for Assimp. Splitting a
struct-layout binding from its native does not remove the skew; it merely relocates responsibility
for it. This was an error in an earlier revision.

**S4 rejected:** MonoGame and FNA are *frameworks*, not bindings. Adopting either means
reimplementing `IThreeD` on `GraphicsDevice` (losing the SSBO instancing path — MonoGame has no
SSBO/compute), rewriting all 703 lines of GLSL under `models/shaders/` as HLSL/`.fx`, and replumbing
Chushi, the resource compiler and the atlas packer around MGCB/`.xnb`. It costs the most, removes the
engine from the owner's control, and — decisively for a 10-year plan — **swaps one framework
treadmill for another**: MonoGame is itself mid-flight on a multi-year Vulkan/DX12 and
shader-compiler rework (3.9).

## 7. Consequences

**Accepted costs.** ~2,900 LOC of new platform code. Ownership of a `gl.xml` generator, ~250 LOC of
OpenAL P/Invoke, and native build recipes for openal-soft and SDL3 across five target triples. A
model-serialisation format and a Chushi bake pass. Loss of Silk's ready-made Android SDL activity.

**Accepted risks.** SDL3 reworked text input (`SDL_StartTextInput` is now per-window), so
`Wuka/Platforms/Android/KarawanInputConnection.cs` (116 LOC) and `GameSurface.cs` (229 LOC) may not
port. This is why Phase 2 is a gate.

**Gains.** Play publishing unblocked. Zero framework vendors in the platform layer. One windowing
backend instead of two. Assimp off-device (−11.2 MB APK). Deletion of `AssimpVersionDetector.cs`,
`AssimpVersion.cs`, the compensation branches, `WukaSilkActivity.cs`, the GLFW/SDL type-name sniffing,
the raw-SDL2 escape hatch, and the duplicated GLES shaders under `Wuka/Platforms/Android/`.

**Untouched:** all of `Splash/`, `JoyceCode` + `nogameCode` (~96k LOC), the 703 lines of GLSL,
`Testbed`/`TestRunner` (one `EasyCreateHeadless` line each), and Aihao's Avalonia-hosted preview.

## 8. Implementation

### CI/CD

**GitHub Actions.** The repo is public, so all runner types — including `macos-15` (arm64) — are free
with unlimited minutes, which removes the usual macOS-CI cost objection. There is no
`.github/workflows` today; this is greenfield.

| Runner | Produces |
|---|---|
| `ubuntu-latest` | `linux-x64`; **android** `arm64-v8a` + `armeabi-v7a` via NDK (cross-builds fine on Linux) |
| `windows-latest` | `win-x64` (MSVC) |
| `macos-15` | `osx-arm64`, `osx-x64` (+ `ios-arm64` later — building needs no signing; only device deployment does) |

**Pinned scripts, not vcpkg.** Working NDK builds already exist in `recipes/` with rationale in
`BUILD_NOTES.md`. vcpkg's Android triplet support is community-grade and per-port flaky — exactly the
hardest target here — and adopting it would re-litigate a solved problem. Reproducibility comes from
three pins: exact upstream **git tags** (not branches, not "latest"); a locked **toolchain** (pinned
NDK revision, pinned runner image label `macos-15` not `macos-latest`, pinned MSVC toolset); and a
**build manifest** emitted into each artifact recording both, so any shipped binary is traceable to
its inputs.

**Distribution.** CI publishes a versioned NuGet with `runtimes/{rid}/native/` plus an Android `.aar`
to GitHub Packages (free for public repos). The game references a pinned version. Developer machines
never build natives.

**CI gates** — these are what make the strategy hold over years:
- `readelf -lW` asserts 16 KB alignment on every Android `.so`; `otool -l` equivalent on macOS.
- Fail the Android build if any `runtimes/linux-*/native/*.so` from a NuGet package reaches the APK.
- Promote `XA0141` (alignment) and `XA4301` (duplicate `.so`) from warning to **error**. Both
  currently pass silently, and XA4301 is precisely how the wrong `libopenal.so` shipped.
- Once Phase 5 lands, assert **zero** `Silk.NET` package references, so the dependency cannot creep
  back.

### Phases

Ordered by risk and by what unblocks shipping. Each is independently valuable.

**Phase 0 — Guardrails.** Add `Directory.Packages.props` for central package management (versions are
currently scattered across 13 csprojs, which is how the `Joyce.csproj:18-19` Assimp split-pin went
unnoticed). Add the CI gates above. Close the two `IThreeD` leaks from §5.

**Phase 1 — Native build pipeline.** Stand up the Actions matrix; generalise
`recipes/build-openal-android.sh` to all targets; add an SDL3 recipe; publish the first pinned native
package. Drop `Silk.NET.OpenAL.Soft.Native` as a native source. **Assimp is deliberately excluded** —
it stays frozen at its current working pin until Phase 4 deletes it, so generalising
`build-assimp-android.sh` would be discarded work.

**Phase 2 — Android SDL3 spike (highest risk; timeboxed; gates Phase 3).** A bare SDL3 Android
activity → GLES 3.0 context → `SDL_GL_GetProcAddress` → clear screen → multi-touch + IME. Resolve: how
`GameActivity` (`Wuka/Platforms/Android/GameActivity.cs:27`, today `: SilkActivity`) rebases onto
SDL3's `org.libsdl.app.SDLActivity`; whether the MAUI shell (`MainActivity.cs:155`) still works;
whether `GameSurface.cs` ports. `docs/main/docs/platforms/ANDROID.md` records *why* SDL2's IME path was
bypassed — re-validate that reasoning against SDL3 rather than porting it forward blindly. Verify
16 KB alignment with `readelf`; do not take it on faith, since that is exactly the assumption that
failed with Silk.

Binding: **flibitijibibo/SDL3-CS** — a single generated `.cs` to vendor, shipping **no natives**.
`ppy/SDL3-CS` and `edwardgushchin/SDL3-CS` both bundle natives, reintroducing the coupling being
removed.

**Phase 3 — Windowing / input / audio (~2,100 LOC).** Rewrite the Silk-bound parts of
`Splash.Silk/Platform.cs` (1,079 LOC total; ~600 are `IView`/`IInputContext`-bound — input handlers
`:254-490`, cursor/keyboard `:492-560`, window callbacks `:560-840`, loop `:838-885`) over SDL3, and
collapse `Wuka/Platforms/Android/` onto the same path. Fold OpenAL in here (~250 LOC of `DllImport`s),
since it touches the same launcher registration lines.

Reuse rather than redesign: `engine.IPlatform` and the `EasyCreatePlatform` / `EasyCreate` /
`EasyCreateHeadless` factories (`Platform.cs:1034/1050/1067`) keep their shape — but **change
`EasyCreate` to stop taking a `Silk.NET.Windowing.IView`**, which is the sole reason all five launchers
import Silk. The string-keyed `engine.news.EventQueue` translation is the contract; only its
left-hand side changes. The hand-rolled loop at `:839-887` already avoids `iView.Run()` and drives
events/update/render itself, mapping directly onto `SDL_PollEvent`.

**Phase 4 — Bake FBX out of the runtime.** 25 `.fbx` files currently ship as runtime resources
(`models/nogame.resources.json`), so Assimp runs on-device and `libassimp.so` is 11.2 MB unstripped —
the largest native in the APK. The `ac-{hash}` bake covers animation *matrices* only; mesh and
skeleton still parse from FBX at runtime.

Much of the infrastructure exists already:
- **Format decided**: `ModelAnimationCollection` is already `[MessagePackObject(AllowPrivate = true)]`
  (`ModelAnimationCollection.cs:18`).
- **Load-or-compute fallback proven twice**: `Model.BakeAnimations` (`Model.cs:207-237`) and
  `engine.tale.bake.ScenarioLibrary.TryGet`.
- **Chushi already has the harness**: `AnimationCompiler` (`Chushi/ConsoleMain.cs:171-182`), including
  staleness skipping.
- **Direct precedent**: the 2026-08-03 TALE storylet fix established that `Directory`-based loading is
  build-tool-only and runtime loading goes through `engine.Assets`.

Work: MessagePack-annotate the `Model` graph (`Model`, `ModelNodeTree`/`ModelNode`, `Skeleton`,
`engine.joyce.Mesh`, `Material`, `InstanceDesc` — the bulk of the effort; `Texture` refs are by URL so
they serialise as strings); add a `ModelCompiler` emitting `mo-{hash}`; add the baked-first load path;
re-declare resources and drop the 25 `.fbx` (**two-build gotcha**: `Wuka.csproj` `<Import>`s the
manifest at project-evaluation time); strip Assimp from `Wuka.csproj`. `FbxModel.cs` (1,720 LOC) is
reused as-is, just running inside Chushi.

Verify rather than assume: (a) **bone ordering must stay stable between the model bake and the `ac-*`
bake** — `AllBakedMatrices` is indexed `frame * Skeleton.NBones + boneIndex`, so any disagreement
renders every animation as a foreign pose; extend `ValidateBakedLayout`'s rigour to bone identity and
lean on `tests/JoyceCode.Tests/engine/joyce/BakedAnimationLayoutTests.cs`. (b) whether
`libc++_shared.so` (8.8 MB) can also go, or whether SkiaSharp / openal-soft still need it — the APK
saving is 11.2 MB if not, ~20 MB if so.

**Phase 5 — Self-generated GL bindings; last Silk package removed.** Write a `gl.xml` → C# generator
emitting **the same type and member names already in use**, so the ~4,100 LOC of call sites need no
edits. Scope is bounded and known (§3d). Include a `GLCheck`-compatible error path
(`Splash.Silk/GLCheck.cs` exists) and keep the `INativeContext`-equivalent seam so Aihao's
Avalonia-hosted context still works. Detach the ImGui backend by inlining `ImGuiFontConfig`;
`ImGui.NET` itself is not a Silk dependency and stays. Then rename `Splash.Silk` → `Splash.GL`.

Deliberately last: least urgent (GL is spec-frozen, so nothing rots while waiting) and most
mechanical, so it must not sit in front of the Play blocker.

### Verification

**Phase 0/1** — `dotnet build Karawan.sln` clean on all TFMs; `dotnet test
tests/JoyceCode.Tests/JoyceCode.Tests.csproj`; `./run_tests_parallel.sh all` then `./run_tests.sh all`
before commit per `PROCESS.md`. Unpack the APK and assert the `.so` inventory against
`Wuka/ANDROID_NATIVE_LIBS.md`: no `libSDL2-2.0.so`; `libopenal.so` is the `libOpenSLES`-linked Android
build; `readelf -lW` shows 16 KB alignment throughout. Visually confirm walk/idle animation on macOS
**and** Windows — the regression the Assimp pin exists to prevent.

**Phase 2 (gate)** — clear screen + touch + IME on a physical device; `readelf` proof of alignment;
Play Console bundle upload shows no "Memory page size" warning.

**Phase 3** — run `Karawan` on all three desktop OSes: keyboard, mouse (including the raw-mouse and
Teamviewer quirks documented at `Platform.cs:337-341`), gamepad, fullscreen toggle, resize,
Retina/HiDPI framebuffer scaling. Same for `examples/Launcher`. Android: multi-touch, soft keyboard,
rotation, resume-from-background. Aihao preview still renders. `Testbed`/`TestRunner` still start
headless. ImGui renders and accepts input everywhere — including the Linux + MacBook Fn-key crash that
motivated the vendored controller (commit `6cf05f78`).

**Phase 4** — byte-compare a `mo-{hash}` bake against the runtime-FBX path on the same model before
switching over (copy the TALE compiler-determinism assertion). Confirm all 25 models load from `mo-*`
with no `.fbx` present, on desktop and in the staged APK. `BakedAnimationLayoutTests.cs` must pass.
Visually verify walk/idle/death on all characters. Confirm `libassimp.so` is gone.

**Phase 5** — pixel-compare frames against the Silk build, before and after, on all three desktop OSes
plus Android; every ImGui panel; the Aihao preview. The `GlStateSaver` / `SilkRenderState` paths are
the subtle ones — a wrong enum value there fails silently rather than loudly.

---

## 9. How to attack this document

Load-bearing claims, with evidence class. **Measured** = counted in this repo. **Estimated** =
extrapolated. **Assumed** = taken on external authority and not verified here.

| # | Claim | Class | Outcome (2026-08-14) |
|---|---|---|---|
| 1 | Zero Silk breakages trace to `Silk.NET.OpenGL` | **measured** (381 commits, `Splash.Silk`) | ✅ **held.** Nothing in five phases contradicted it. The GL binding was replaced for reasons unrelated to defects — see 11b, where the operative reason turned out to be something this document never considered. |
| 2 | ~80 GL entry points, ~10 enum types | **measured** | 🟡 **entry points right, enum types 3× low.** WP-5.1 resolved the surface by Roslyn: 339 call sites, 81 distinct entry points; the generator emits **85** native entry points + 14 hand-written conveniences, and **30** enum types, not ~10. Cost estimate survived anyway. |
| 3 | A generator emitting matching names leaves ~4,100 call-site LOC untouched | **estimated** — *"the biggest single soft spot"* | ✅ **CONFIRMED EXACTLY.** AC-5.0 measured **0 changed lines** on the call-site sample. The document's most-doubted estimate was its most precisely correct one. |
| 4 | `gl.xml` is sufficient to generate what's needed | **assumed** | ✅ **held.** 114 enum values verified against the registry, 0 unverifiable. The one real defect found (`glTexParameterI[u]iv` taking its third parameter by value) came from **our** Roslyn probe flattening `in int`, not from the registry. |
| 5 | Silk 2.x is maintenance-mode; 3.0 is a rewrite with no ETA | **assumed** | ⬜ **never tested.** The programme neither confirmed nor refuted it. It remains the load-bearing assumption under the whole longevity argument, and it is still unverified. |
| 6 | The SDL2 AAR cannot be fixed locally | **assumed** | ❌ **FALSIFIED** by WP-0.0 — the repack works. Escalated per plan §5c; the owner chose to continue, on the grounds that repackability buys *time*, not maintenance attention. So this was **schedule relief, not a change of direction**. |
| 7 | SDL3's Android build is 16 KB-aligned | **assumed** | ✅ **confirmed**, and then validated by the store rather than by our own checker: GATE-B passed twice, including a second deliberate pass for CoreCLR's native set. |
| 8 | `KarawanInputConnection.cs` / `GameSurface.cs` port to SDL3 | **assumed — weakest point** | ✅ **confirmed on hardware**, and **far smaller than feared**. Both files were already SDL3-aware; nothing needed porting. The actual defect was that *nothing raised the keyboard at all* (KI-10). The claim called "the single most likely point of failure in this whole plan" cost one seam and two null guards. |
| 9 | Assimp binding/native skew is a struct-layout problem | **measured** | ⬜ **moot, not verified.** Phase 4 removed Assimp from the runtime, so the skew can no longer occur, but the mechanism was never independently retested. §11's "lower stakes" note was right that the pin had already neutralised it — the real payoff was size and load time. |
| 10 | Phase 4 is tractable because the infra exists | **estimated** | ✅ **confirmed, and the recommended spike was the right call.** The `Mesh`+`Skeleton` spike answered the open question immediately: every shared/cyclic edge in the `Model` graph is *derivable*, so no DTO layer was needed. The plan called WP-4.1 "the bulk of the effort"; it was not. |
| 11 | GitHub Actions is free for this repo on all runners | **measured** | ⬜ **untested — no CI was ever stood up.** WP-1.1 added one workflow for native builds; nothing else. This is KI-17 and the largest structural gap left. |
| 12 | The abstraction is the durable asset | **measured** | ✅ **confirmed emphatically.** Windowing, input, audio, the GL binding and model import were *all* replaced underneath `Splash/`, across five phases, without the renderer design changing. This is the claim the whole document rests on and it is the one the work most clearly validated. |

**Questions a reviewer should press on:**

- Is S1 ("own natives, keep bindings") really dead? It is by far the cheapest thing that unblocks
  Play *if* claim 6 is false. This document's answer is that it leaves the Assimp skew and the
  GLFW/SDL2 split in place — but that is a longevity argument, not an urgency one.
- Is Phase 5 worth doing at all, or is it engineer's satisfaction dressed as strategy? The honest
  answer is that it buys **no** correctness and **no** capability. It buys only independence from a
  vendor's release schedule. If that is not worth ~800 LOC, stop after Phase 4 — and note that
  nothing rots if you do, because GL is frozen.
- Is the ordering right? Phase 5 is last because it is least urgent. But it is also the phase most
  likely to be skipped once the pain is gone — which may be the correct outcome, or may be how the
  next decade's version of this problem starts.
- Does §5 (macOS/OpenGL) deserve to outrank the whole rest of the document? A reviewer could
  reasonably argue the effort here is better spent on a second `IThreeD` backend.

## 10. Revision history

This document changed its central recommendation twice under challenge. Recorded because the
reasoning matters more than the conclusion.

- **Rev 1** — "Own the natives, keep all Silk managed bindings." Rejected: wrong for Assimp, where
  binding and native must agree on struct layout, so splitting them relocates the risk instead of
  removing it.
- **Rev 2** — "Keep Silk for GL only." Argued `Silk.NET.OpenGL` cannot break you because it ships no
  native and binds a frozen ABI. True, but a *correctness* argument answering a *survival* question.
  Also mis-costed removal at ~4,100 LOC by assuming adoption of a foreign API rather than
  self-generation.
- **Rev 3** — Full de-Silk via self-generation, with the ABI-shape principle (§3b) as the
  organising idea and the macOS/OpenGL risk (§5) named as the thing that actually outranks it.
- **Rev 3.1** — Reviewed by an independent Fable instance. Corrected: "6 packages" → 13
  (6 was the *project* count); `Joyce.csproj:18` → `:18-19`. Added §11 recording four unresolved
  objections, and `S2b` to §6 as **not costed**. Phase 5 is no longer treated as settled.
- **Rev 4 (this) — ACCEPTED 2026-08-14.** Phases 0–5 implemented and merged over 10 days against an
  estimate of "months". §9 now carries an outcome per claim and §11 a resolution per challenge,
  including the two that were **not** achieved: no CI (claim 11, KI-17) and no ANGLE evaluation
  (11d), the latter still outranked only by itself as the largest open risk. One claim was
  falsified (#6, the SDL2 AAR *can* be repacked) and escalated as the plan required; the owner
  continued deliberately, treating it as schedule relief rather than a change of direction. The
  decision this document argued for was carried out; the *reasons* it gave were not always the
  reasons that turned out to matter — see 11b.

---

## 11. Open challenges (raised in review 2026-08-04 — **resolved 2026-08-14**)

> **Outcome summary.** Three of the four were answered by doing the work; one (11d, ANGLE) was
> never addressed and remains open. The reviewer's "lower stakes" aside turned out to be the most
> predictive paragraph in the document — see the note at the end of this section.

An independent review by a Fable instance (which would also be this plan's orchestrator) raised four
objections that were **not resolved** by this document as drafted. They were recorded rather than
answered, because answering them changes the plan and is the owner's call. Phases 0–2 are unaffected.

**11a. `S2b` — SDL3 windowing + OpenTK for GL — was never costed, and §1/N4 of the implementation
plan forbids workers from raising it.** That is a process failure: an option was foreclosed without
evaluation. The case for it: OpenTK's GL bindings work without its windowing
(`GLLoader.LoadBindings` over `SDL_GL_GetProcAddress`), it has 15+ years of continuous maintenance,
and the churn objection in §4c is unusually weak *in this specific context* — a one-time mechanical
rename across ~4,100 call sites, verified by the pixel-compare gate (GATE-F) being built anyway, is
close to an ideal agent task. **N4 has been relaxed accordingly. Cost S2b honestly before Phase 5.**

> ✅ **RESOLVED — S2b was costed (WP-5.0b) and lost on numbers, not on preference.** OpenTK 5
> would have changed **37% of code lines** (~83 of 225 sites on the sample), because `GL` is static
> where Silk's is an instance — so all 225 sites change receiver even where nothing else moves.
> `pre.16` also shipped **net10.0 only**, which would have dropped our then-net9.0 targets. Against
> that, self-generation measured **0 changed lines** (claim 3). The process failure the reviewer
> named was real and the fix — relax N4, cost it — produced a decision instead of an assumption.

**11b. §4c's rejection of "keep `Silk.NET.OpenGL`" may be rhetorical rather than technical.** It
conflates the *vendor's* survival with the *artifact's*. A pinned, pure-managed binding of an ABI
frozen since ~2010 needs nothing further from Silk-the-project: nuget.org does not delete packages,
and .NET's P/Invoke surface is among the most stable in the ecosystem. There may be no "deferred
fourth migration" for `Silk.NET.OpenGL` specifically — the migration pressure lives entirely in
windowing/input/native packaging, which **S2 already removes**. Counter-argument to weigh: a
name-compatible generator maintained by one person may be a *worse* 10-year bet than a frozen DLL,
since the DLL cannot rot but the generator must be re-understood by future-you on every change.

> 🟡 **RESOLVED, but the winning argument is one neither side made.** The reviewer is right that
> §4c's survival argument was rhetorical: a pinned pure-managed binding of a frozen ABI does not
> rot. What actually forced the swap was **traceability**. A delegate-based binding *cannot be
> traced*: .NET returns the ORIGINAL delegate from `GetDelegateForFunctionPointer` when the pointer
> came from `GetFunctionPointerForDelegate`, so an interposer's cast to its own delegate type
> throws. GATE-F's tracer interposes exactly there — it worked against Silk (which dispatches
> through function pointers) and broke the moment the renderer moved to our binding, which is what
> drove the switch to `delegate* unmanaged<>` dispatch. That instrument is how the
> `glTexParameterI[u]iv` defect was found *before* it could segfault, and how "0 `glGetError` calls
> in a live frame" was verified. **A frozen DLL cannot rot, but it also cannot be instrumented.**
>
> On the "re-understood by future-you" worry: partly mitigated, not eliminated. `gen.py` is ~1,000
> lines and both generated files **regenerate byte-identically** from a registry pinned by sha256,
> so the artifact is reproducible rather than hand-maintained. But KI-17 records that nothing
> *enforces* that — it is a command someone remembers to run, and it has already caught two real
> drifts (#89/#90, and the WP-5.4 rename). The reviewer's concern is answered by discipline, which
> is exactly the weaker form of answer.

**11c. Claim 3 (§9) is understated, and the ~500 LOC generator estimate is probably 2–4× optimistic.**
Matching "the same type and member names" is not the hard part. `SilkThreeD.cs` call sites depend on
Silk's *overload expansion policy* — unsafe-pointer/`Span`/`out`/`ref` variants, the dual
typed-enum/`GLEnum` surface (92 `GLEnum` uses per §3d), string marshalling, and `GL.GetApi` /
`INativeContext` plumbing. That is reimplementing a compatible slice of SilkTouch's emission
behaviour. Compounding this, claim 4 is a known-real hazard: `gl.xml` `group` attributes are
historically incomplete, and other generator projects supplement them by hand.

> 🟡 **HALF RIGHT, and the halves are instructive.** The **size** estimate was indeed optimistic:
> `gen.py` emits ~1,000 lines and needs `gen-trace.py`, `shapecheck.py`, the Roslyn `surface`
> probe, the `differ` and the `verify` tool alongside it — comfortably 2× the ~500 LOC estimate
> once the verification apparatus is counted, and that apparatus is not optional.
>
> But the **premise** was wrong: matching Silk's overload-expansion policy did not have to be
> reimplemented. WP-5.1 resolved the *actual* surface this codebase binds — 339 call sites, 81
> entry points — and generated only that, the "narrow form of S2a" the owner chose. Reproducing a
> compatible slice of SilkTouch's emission behaviour was never necessary; reproducing **our own**
> usage of it was, and that is a much smaller thing. 14 hand-written conveniences cover what the
> registry cannot express.
>
> Claim 4's hazard did not materialise: 114 enum values verified against the registry, 0
> unverifiable.

**11d. ANGLE is absent, and §5 says macOS GL deprecation outranks everything in this document.**
ANGLE is the industry-standard GL-on-Metal escape hatch, it composes with SDL3, and a working ANGLE
path would validate the `IThreeD` seam with running code rather than discipline — which is §5's own
stated priority. The alternative allocation (do S2, spend the saved months on ANGLE-on-Metal or a
thin second backend) is not in §6's options table and should be.

> 🔴 **NOT RESOLVED. This challenge stands unanswered.** ANGLE was never evaluated, no second
> `IThreeD` backend exists, and the macOS/OpenGL deprecation risk §5 calls the thing that
> *outranks this entire document* is exactly where it was on 2026-08-04.
>
> Two things sharpen it now. First, the reviewer's proposed trade — "do S2, spend the saved months
> on ANGLE" — turned out to be affordable in a way nobody predicted: the whole programme took **10
> days**, not months, so the months were never spent and are still available. Second, `IThreeD` has
> now been validated by *discipline* over five phases (claim 12), which is precisely the weaker
> form of validation the reviewer objected to; a running ANGLE path would still be the stronger
> one. **This is the most significant open item the ADR leaves behind**, and it should be read as
> the reviewer's, not the author's, priority.

**Also noted, lower stakes:** Phase 4's justification leans on ABI skew that the pin has *already*
neutralised; its real payoff is APK size (11–20 MB) and load time, and Assimp does not leave the
project — Chushi still runs it on every dev machine and in CI. And §3a's own table records **zero**
desktop windowing failures in 3.5 years, yet Phase 3 rewrites desktop input on three OSes, including
the hard-won raw-mouse/Teamviewer quirks at `Platform.cs:337-341`, in service of "one backend
everywhere." That is regression risk purchased against no recorded failure.

**Reviewer's net position:** Phases 0–2 unconditionally yes; Phase 3 on Android yes, desktop only if
WP-0.0 shows the AAR cannot be repacked; Phase 4 defensible but re-justify on size/load-time;
**Phase 5 burden of proof not met** — run WP-5.0 early and properly specified, and cost S2b first.

> ### The reviewer's "lower stakes" paragraph was the most predictive text in this document
>
> Both of its claims came true, and the second one cost real defects:
>
> **"Assimp does not leave the project — Chushi still runs it."** Exactly right. Phase 4 moved the
> importer to `JoyceFbx`, which the build tools reference and no shipping project does. Assimp left
> the *APK*, not the repository, and the real payoff was the one the reviewer named: 25 fbx assets
> and `libassimp.so` out of the package.
>
> **"Phase 3 rewrites desktop input on three OSes … regression risk purchased against no recorded
> failure."** Borne out precisely. Desktop input regressions introduced by that rewrite: gamepad
> stick inverted (#61), the mouse cursor never configured (#61), **KI-14 — desktop text entry
> silently dead**, which survived undetected because WASD and every scancode binding kept working,
> and was found only when someone typed into a field, and **KI-19 — mouse-look capped at the window
> borders**, because the port kept differencing the absolute pointer position after moving to a
> backend that, unlike Silk's `CursorMode.Raw`, reports that position clamped. Note the shape it
> shares with KI-14: the code compiled, ran, and produced *plausible* input, so nothing failed —
> the camera merely stopped turning, which reads as a design decision. Four regressions, against
> the zero desktop windowing failures §3a recorded in 3.5 years — and the paragraph the reviewer
> flagged names raw mouse specifically. The reviewer's arithmetic was right.
>
> **What the reviewer got wrong:** the Phase 5 burden of proof *was* met, but by a mechanism they
> did not anticipate and neither did this document — traceability, not survival (see 11b). And the
> cost that made Phase 5 defensible was the one they doubted most: claim 3's 0-changed-lines
> estimate held exactly.
>
> **The standing lesson**, worth more than any single verdict here: *the objections that proved
> most valuable were the quantitative asides, not the strategic positions.* "Regression risk
> against no recorded failure" is a checkable statement. "Longevity" is not.

## Sources

- [Silk.NET 3.0 milestone](https://github.com/dotnet/Silk.NET/milestone/9)
- [Google Play 16 KB page-size requirement](https://android-developers.googleblog.com/2025/05/prepare-play-apps-for-devices-with-16kb-page-size.html)
- [Android 16 KB support guide](https://developer.android.com/guide/practices/page-sizes)
- [flibitijibibo/SDL3-CS](https://github.com/flibitijibibo/SDL3-CS)
- [Khronos OpenGL registry (`gl.xml`)](https://github.com/KhronosGroup/OpenGL-Registry)
- [OpenTK](https://opentk.net/faq.html) · [MonoGame roadmap](https://docs.monogame.net/roadmap/)
- In-repo: `Wuka/ANDROID_NATIVE_LIBS.md`, `docs/ANIMATION_ASSIMP_VERSION_COMPENSATION.md`,
  `recipes/BUILD_NOTES.md`, `docs/main/docs/platforms/ANDROID.md`
