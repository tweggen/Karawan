# Plan: Platform Backend — Silk.NET exit

**Status:** 📋 Ready for Implementation (pending falsification of WP-0.0)
**Created:** 2026-08-04
**Estimated Effort:** large — 6 phases, ~2,900 LOC, spread over months
**Complexity:** Medium (mechanical) with two High-risk gates
**Design doc:** [`docs/ARCHITECTURE/PLATFORM_BACKEND.md`](../../ARCHITECTURE/PLATFORM_BACKEND.md) — **read it first**

---

## 0. Read this first (orchestrator and workers)

You are implementing a decision that has already been argued through. **The reasoning is in the ADR
and must not be re-derived.** Read `docs/ARCHITECTURE/PLATFORM_BACKEND.md` in full before dispatching
or accepting any work package. In particular read §6 (options rejected), §9 (which claims are
unverified), and §10 (two earlier recommendations that were already tried and rejected).

**One-paragraph summary.** Karawan depends on Silk.NET for windowing, input, GL bindings, audio
bindings and model import. Every recorded Silk breakage in 3.5 years is a *native-packaging* failure,
not a binding bug; and Silk's SDL2 AAR is not 16 KB page-aligned, which blocks Google Play publishing
today with no local fix. The decision is to remove Silk.NET entirely — SDL3 for windowing/input,
hand-written P/Invoke for OpenAL, self-generated bindings from the Khronos `gl.xml` registry for GL,
and FBX import moved from runtime to build time (Chushi) so Assimp leaves the shipped app. The
governing principle is *depend on specifications and formats, which are regenerable; not on wrappers,
which get abandoned.*

**Why this ordering.** Phases are ordered by risk and by what unblocks shipping, not by dependency.
WP-0.0 comes first because it can cheaply invalidate the plan's urgency. Phase 5 comes last because
GL is spec-frozen — nothing rots while it waits.

---

## 1. Non-negotiables

These are settled. A worker that proposes any of them has misunderstood the assignment; the
orchestrator must reject the result and re-dispatch with this section quoted.

| # | Rule | Why (ADR ref) |
|---|---|---|
| N1 | **Do not** propose "own the natives but keep Silk's managed bindings." | Rev 1, rejected. Wrong for Assimp: binding and native must agree on *struct layout*, so splitting them relocates the risk instead of removing it. §3b, §10 |
| N2 | **Do not** propose keeping `Silk.NET.OpenGL` "because it's harmless." | Rev 2, rejected. True on correctness, wrong on survival: Silk 2.x is maintenance-mode and 3.0 is a rewrite, so keeping it is a deferred migration. §4c, §10 |
| N3 | **Do not** propose MonoGame, FNA, Godot, Unity, or any framework. | §6. Deletes the renderer and swaps one treadmill for another. Also the owner's explicit red line. |
| N4 | ~~Do not adopt OpenTK.~~ **RELAXED 2026-08-04.** Phase 5's approach is **open**: self-generation from `gl.xml` *or* OpenTK-for-GL-only (`S2b`) are both live. Neither has been costed against the other. **Do not start Phase 5 until S2b is costed** (ADR §11a). Phases 0–4 are unaffected. | The original rule foreclosed an option that was never evaluated. See ADR §11a/§11b/§11c. |
| N5 | **Do not** touch Assimp during Phases 0–3. Leave `Silk.NET.Assimp` at 2.22.0 and do not generalise `recipes/build-assimp-android.sh`. | Phase 4 deletes it; that work would be discarded. |
| N6 | **Do not** modify `JoyceCode/` or `nogameCode/` except where a work package explicitly says so. | They contain zero Splash/Silk references today (§3c). Keeping that true is the invariant that makes this cheap. Machine-checkable — see AC-GLOBAL-2. |
| N7 | **Do not** modify `models/shaders/*.glsl|vert|frag`. | Staying on GL means zero shader work. A shader diff means something went wrong. |
| N8 | **Do not** "fix" a problem by bumping a Silk.NET version. | That is the mechanism that caused the Assimp corruption. |
| N9 | **Never** claim a device- or OS-specific behaviour was verified if you did not run it on that device or OS. Mark it `BLOCKED-ON-HUMAN`. | See §3. This is the most likely way this assignment fails. |

---

## 2. Orchestration protocol

For the coordinating instance. Workers get one work package at a time.

### 2.1 Git model

- One branch per work package: `platform/wp-<id>` off `master` (e.g. `platform/wp-0.1`).
- Worker commits to that branch and opens a **PR against `master`**. Workers **never merge.**
- PR body must include: the WP id, the acceptance-criteria table with pass/fail per row, and an
  explicit `BLOCKED-ON-HUMAN` list if any.
- The orchestrator does not dispatch a dependent WP until its predecessor's PR is **merged by the
  human**. Independent WPs may run in parallel.
- Commit messages end with the project's standard `Co-Authored-By` trailer per `PROCESS.md`.

### 2.2 The iterate-until-met loop

```
dispatch(WP) →
  worker implements →
  worker self-evaluates every AC row, recording the exact command and its actual output →
  if all agent-checkable rows PASS:
       open PR, report result, mark human-gate rows BLOCKED-ON-HUMAN
  else:
       worker fixes and re-evaluates          (max 3 self-iterations)
       if still failing after 3: STOP and report the failing rows verbatim.
                                 Do NOT weaken the criterion. Do NOT skip it.
orchestrator verifies the PR independently by re-running the AC commands →
  disagreement between worker's claim and orchestrator's re-run = automatic re-dispatch
```

**The orchestrator must re-run the acceptance commands itself.** A worker reporting PASS is a claim,
not evidence. This is the main defence against a plausible-sounding but false completion.

### 2.5 Exemptions from independent re-run

Not every criterion is a cheap one-liner. For the following, the orchestrator accepts **committed
evidence** (tool output, hashes, transcripts in the PR or findings doc) instead of re-running:

| Exempt | Why |
|---|---|
| AC-0.0.2 (SDL2 AAR repack) | re-verification means redoing an NDK build |
| AC-1.5 (reproducibility) | a full CI matrix re-run, tens of runner-minutes |
| WP-1.4 publish to GitHub Packages | **stateful and non-idempotent** — a NuGet version cannot be re-published |
| AC-2.5 / GATE-B (Play upload) | consumes a Play Console upload; human-only anyway |
| All `[HUMAN]` rows | by definition |

Everything else **is** re-run. Note that AC-GLOBAL-1 (full solution build, including nogame's
texture-pack → Chushi → resource-compile pipeline) and AC-GLOBAL-5 (TALE suite) take minutes each —
budget for that, and use the "applies to" column to skip them where they are meaningless.

### 2.2b State ledger (required — this runs for months across many sessions)

The orchestrator **must** maintain `docs/roadmap/proposed/PLATFORM-BACKEND-STATUS.md`, committed,
updated on every dispatch and every result. Without it each fresh orchestrator session reconstructs
state by git archaeology and will get the "max 3 iterations" count wrong.

One row per WP: `WP id | status (NOT-STARTED / IN-PROGRESS / PR-OPEN / BLOCKED-ON-HUMAN / MERGED /
ABANDONED) | branch | PR # | iteration count | AC results with actual command output | gate
outcomes | notes`.

### 2.2c When the human rejects a PR

Requested changes → **re-dispatch to a fresh worker** with the review comments quoted verbatim; this
does **not** reset the iteration counter. If master has moved, rebase the WP branch before
re-dispatch. A WP that reaches 3 iterations *including* human-requested changes escalates per §2.3.

### 2.2d Parallelism and conflicts

`⟂` marks **cross-phase** independence only. Within a phase, assume serial unless stated. Known
conflict sets that must never run concurrently:

- WP-0.1 and WP-0.3 both edit `Wuka.csproj`
- WP-3.1 / 3.2 / 3.3 / 3.5 all edit `Splash.Silk/Platform.cs`
- Any two WPs whose AC set includes AC-GLOBAL-5 (the TALE suite cannot run twice at once)

### 2.2e Human gates are pre-merge

A PR with an outstanding `BLOCKED-ON-HUMAN` row **must not be merged** until that gate passes.
Otherwise master silently carries unverified Windows/Android behaviour for weeks. The human may
override case by case; the orchestrator may not.

### 2.3 Escalation — stop and ask the human

Stop immediately, do not work around, when:

- Any `BLOCKED-ON-HUMAN` gate is reached (§3).
- An acceptance criterion cannot be met after 3 iterations.
- A worker believes a non-negotiable (§1) is wrong. *This is allowed* — the ADR was twice revised
  under challenge — but it is a conversation with the human, never a unilateral deviation.
- Any ADR §9 claim marked **assumed** turns out to be false. Several of these change the plan's
  shape; see §4.

### 2.4 Worker dispatch template

```
Read docs/ARCHITECTURE/PLATFORM_BACKEND.md (the why) and
docs/roadmap/proposed/IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md §1, §2, §3 (the rules).

Implement work package <WP-ID> from §5 of the implementation plan, and only that package.

Branch: platform/wp-<id> off master. Commit there, open a PR against master, do not merge.

When done, fill in the acceptance-criteria table for <WP-ID>: for every row give the exact
command you ran and its actual output. If a row is marked [HUMAN], do not attempt it — list it
as BLOCKED-ON-HUMAN in the PR body.

If you cannot satisfy a criterion after 3 attempts, stop and report the failing rows verbatim.
Do not weaken or reinterpret a criterion to make it pass.

Do not modify JoyceCode/ or nogameCode/ unless this package explicitly says to.
```

---

## 3. Human gates — the agent cannot do these

An agent running on the owner's Mac **cannot** verify Android device behaviour, Windows or Linux
behaviour, GPU rendering correctness, or Play Console state. These gates are hard stops.

| Gate | What only a human can do | Blocks |
|---|---|---|
| **GATE-A** | Run the SDL3 spike on a **physical Android device**: multi-touch, soft keyboard/IME, rotation, resume-from-background | all of Phase 3 |
| **GATE-B** | Upload a bundle to **Play Console** and confirm no "Memory page size" warning | declaring Phase 2 done |
| **GATE-C** | Run the desktop build on **Windows** and on **Linux** — keyboard, mouse, gamepad, fullscreen, resize, HiDPI | Phase 3 sign-off |
| **GATE-D** | **Visually** confirm character animation (walk / idle / death) is correct on macOS *and* Windows | Phase 1 and Phase 4 sign-off |
| **GATE-E** | **Visually** confirm ImGui panels render and take input, incl. the Linux + MacBook Fn-key case (commit `6cf05f78`) | Phase 3 and Phase 5 sign-off |
| **GATE-F** | Pixel-compare rendered frames before/after the GL binding swap | Phase 5 sign-off |

An agent may *prepare* for a gate — build the APK, write the comparison script, produce the artifact —
and must then stop.

---

## 4. Falsification first

ADR §9 lists twelve load-bearing claims by evidence class. Three are **assumed** and cheap to test,
and two of them can change the plan. Test before building.

| Claim | Test | If false |
|---|---|---|
| **#6** — Silk's SDL2 AAR cannot be fixed locally | WP-0.0 | **Play unblocks without touching windowing.** Phases 2–3 lose their urgency and become longevity-only work the owner can schedule at leisure. Re-plan with the human. |
| **#7** — SDL3's Android AAR is 16 KB-aligned | WP-0.0 | The acute problem is *not* solved by this plan. Stop and re-plan. |
| **#3** — a name-matching generator leaves call sites untouched | WP-5.0 | Phase 5's cost estimate breaks. Re-cost before continuing. |

---

## 5. Work packages

Legend: `[HUMAN]` = human gate, not agent-checkable. `⟂` = independent, may run in parallel.

### Global acceptance criteria — apply to **every** work package

| id | Criterion | Command | Expected | Applies to |
|---|---|---|---|---|
| AC-GLOBAL-1 | Solution builds | `dotnet build Karawan.sln` | exit 0, 0 errors | all WPs touching C# |
| AC-GLOBAL-1b | TestRunner builds (**not in the .sln**) | `dotnet build TestRunner/TestRunner.csproj` | exit 0 | all WPs touching C# |
| AC-GLOBAL-2 | Engine/game untouched (N6) | `git diff --stat master -- JoyceCode/ nogameCode/` | empty, unless the WP says otherwise | **all** |
| AC-GLOBAL-3 | Shaders untouched (N7) | `git diff --stat master -- models/shaders/` | empty | **all** |
| AC-GLOBAL-4 | Unit tests pass | `dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj` | all pass | WPs touching C# |
| AC-GLOBAL-5 | TALE suite passes | `./run_tests_parallel.sh all` | all pass | WPs touching `JoyceCode/`, `nogameCode/`, or `models/tale/` **only** |
| AC-GLOBAL-6 | Docs updated per `PROCESS.md` §3 | — | ADR + affected subsystem docs updated in the same PR | **all** — orchestrator judgment, not machine-checkable |

> **AC-GLOBAL-5 must be serialised.** Per `CLAUDE.md`, `models/tale/*.json` is a live input to the
> TALE suite and must not be touched while a gate runs. The orchestrator must never run two TALE
> verifications concurrently. Running it for a CI-YAML-only WP is waste — hence the "applies to"
> column.

---

### WP-0.0 — Falsify claims #6 and #7 ⟂ **DO THIS FIRST**

**Goal:** spike only. Produce evidence, change no product code.

1. Extract `Silk.NET.Windowing.Sdl.aar`; attempt to rebuild SDL2 (matching the AAR's version) with
   `-Wl,-z,max-page-size=16384`; repack the AAR; reference it in place of the package's.
2. Independently, download the SDL3 Android AAR and inspect its alignment.

| id | Criterion | Command | Expected |
|---|---|---|---|
| AC-0.0.1 | SDL2 AAR alignment measured | `readelf -lW <aar>/jni/arm64-v8a/libSDL2.so \| grep LOAD` | alignment recorded in the report |
| AC-0.0.2 | Repack attempted, outcome recorded | — | written finding: repack works / does not work, with the blocker if not |
| AC-0.0.3 | SDL3 AAR alignment measured | `llvm-readelf -lW <sdl3-aar>/prefab/modules/SDL3-shared/libs/android.arm64-v8a/libSDL3.so \| grep LOAD` | `0x4000` (16 KB) or the actual value |
| AC-0.0.4 | Report written | — | `docs/roadmap/proposed/WP-0.0-FINDINGS.md` |

> **Path corrected 2026-08-05.** The SDL3 AAR uses the **prefab** layout, not `jni/<abi>/`.
> The originally-specified `<sdl3-aar>/jni/arm64-v8a/libSDL3.so` does not exist. This is not
> cosmetic — consuming a prefab AAR from .NET Android differs from consuming Silk's
> `jni/`-layout AAR, and **WP-2.1 must budget for it**.

**Output:** a findings doc. **Escalate to the human before dispatching any WP in Phase 1 or later.**
WP-0.1/0.2/0.3 are guardrails that are correct under either outcome and **may run concurrently with
WP-0.0** (subject to the §2.2d conflict list).

> ### ✅ WP-0.0 COMPLETE — 2026-08-05 — **claim #6 FALSIFIED**
>
> See [`WP-0.0-FINDINGS.md`](WP-0.0-FINDINGS.md). Silk's SDL2 AAR **can** be rebuilt 16 KB-aligned
> and repacked locally (SDL 2.30.8 + NDK r27c, ~15 min); SDL3's AAR **is** 16 KB-aligned on both
> 64-bit ABIs. Per §4 and §5c the programme is **halted pending a human re-plan** — do not dispatch
> any Phase 1+ work package. State ledger: [`PLATFORM-BACKEND-STATUS.md`](PLATFORM-BACKEND-STATUS.md).

---

### Phase 0 — Guardrails

#### WP-0.1 — Central package management ⟂

Add `Directory.Packages.props`; move every `Version=` out of **13 csprojs** — verified list:
`Aihao/Aihao`, `Boom.OpenAL`, `examples/Launcher`, `Joyce`, `Karawan`, `Mazu`, `nogame`,
`Splash.Silk`, `Splash`, `tests/JoyceCode.Tests`, `Tooling/Cmdline`, `Wuka`, and `Aihao.old`.
**Exclude `Aihao.old` (dead code) — exclude it from the glob rather than migrating it.** Preserve
today's resolved versions exactly, including the deliberate `Silk.NET.Assimp` 2.22.0 pin (N5).

**Baseline capture:** run `dotnet list package > /tmp/pkg-baseline.txt` on **master before
branching** — AC-0.1.3 is unrunnable afterwards otherwise.

| id | Criterion | Command | Expected |
|---|---|---|---|
| AC-0.1.1 | CPM active | `grep ManagePackageVersionsCentrally Directory.Packages.props` | `true` |
| AC-0.1.2 | No stray versions | `rg 'PackageReference.*Version=' --glob '*.csproj' --glob '!Aihao.old/**'` | no matches |
| AC-0.1.3 | Resolved set unchanged | `dotnet list package \| diff /tmp/pkg-baseline.txt -` | no differences, incl. Assimp 2.22.0 |
| AC-0.1.4 | Wuka still builds | `dotnet build Wuka/Wuka.csproj` | exit 0 |
| AC-0.1.5 | `[HUMAN]` **GATE-D** | — | animation correct on macOS **and** Windows |

> GATE-D lives here, not in Phase 1: WP-0.1 is the only Phase 0/1 change that could shift Assimp
> package resolution, and Assimp resolution is what GATE-D guards.

#### WP-0.2 — Close the two `IThreeD` seam leaks ⟂

Per ADR §5. (a) Move `GLAnimBuffers` out of `Splash/Flags.cs` into `Splash.Silk` — it is referenced
only from `Splash.Silk/*`, so this is a rename, not a redesign; leave `Flags.AnimBatching` where it
is. (b) Re-express `Platform.SetExternalGL(GL)` / `GetGL()` (`Splash.Silk/Platform.cs:987,1004`) as an
opaque context handle so `Aihao/.../EnginePreviewService.cs` no longer names a Silk type.

| id | Criterion | Command | Expected |
|---|---|---|---|
| AC-0.2.1 | No GL naming in `Splash/` | `rg -i 'GLAnim\|Silk\|OpenGL' Splash/` | no matches outside comments |
| AC-0.2.2 | Aihao free of Silk GL types | `rg 'Silk\.NET\.OpenGL' Aihao/` | no matches |
| AC-0.2.3 | Aihao builds | `dotnet build Aihao/Aihao/Aihao.csproj` | exit 0 |
| AC-0.2.4 | `[HUMAN]` Aihao preview still renders | GATE-D adjacent | visual confirmation |

#### WP-0.3 — Inventory the silent Android warnings ⟂ (does **not** promote them yet)

`XA0141` (16 KB alignment) and `XA4301` (duplicate `.so`) currently pass silently in `Wuka.csproj`.
XA4301 is precisely how the wrong `libopenal.so` shipped.

**Promotion to errors is deferred to WP-1.6.** `Wuka` is in `Karawan.sln`, so promoting now would
make AC-GLOBAL-1 fail on master for every subsequent work package until the natives are fixed in
Phase 1 — through no fault of those packages. This WP only records the current state.

| id | Criterion | Command | Expected |
|---|---|---|---|
| AC-0.3.1 | Warnings inventoried | `dotnet build Wuka/Wuka.csproj 2>&1 \| rg 'XA0141\|XA4301'` | full list committed to `docs/roadmap/proposed/WP-0.3-WARNINGS.md` |
| AC-0.3.2 | No suppressions added | `git diff master -- Wuka/Wuka.csproj` | empty or comment-only |

---

### Phase 1 — Native build pipeline

Depends on: WP-0.0 findings. **N5 applies: openal-soft and SDL3 only. Not Assimp.**

- **WP-1.1** — GitHub Actions matrix skeleton (`ubuntu-latest` → linux-x64 + android arm64-v8a/armeabi-v7a;
  `windows-latest` → win-x64; `macos-15` → osx-arm64/x64). Pinned NDK revision, pinned image labels
  (`macos-15`, never `macos-latest`), pinned MSVC toolset.
- **WP-1.2** — openal-soft recipe generalised from `recipes/build-openal-android.sh` to all targets,
  pinned to an exact upstream **git tag**.
- **WP-1.3** — SDL3 recipe, same treatment.
- **WP-1.4** — Package as a versioned NuGet (`runtimes/{rid}/native/` + Android `.aar`), publish to
  GitHub Packages; emit a build manifest recording tag + toolchain into each artifact.
- **WP-1.5** — Consume the package; drop `Silk.NET.OpenAL.Soft.Native` as a native source.
- **WP-1.6** — **Now** promote `XA0141` and `XA4301` to errors in `Wuka.csproj` (deferred from
  WP-0.3). With correct natives in place this should pass; if it does not, the natives are not
  actually fixed and Phase 1 is not done.

**Internal ordering (not parallel):** 1.1 → (1.2 ⟂ 1.3) → 1.4 → 1.5 → 1.6.

| id | Criterion | Command | Expected |
|---|---|---|---|
| AC-1.1 | CI green | `gh run list --workflow=natives.yml --limit 1 --json conclusion` | `"success"` |
| AC-1.2 | Every Android `.so` 16 KB-aligned | `llvm-readelf -lW <artifact>/*.so \| grep LOAD` | `0x4000` throughout |
| AC-1.3 | No Linux natives in the APK | unzip APK; list `lib/arm64-v8a/` | no `libSDL2-2.0.so`; no glibc-linked `libopenal.so` |
| AC-1.4 | Correct OpenAL | asserted in `recipes/build-openal.sh` on every Android build; look for the four `OK:` lines in the job log | Bionic not glibc · OpenSL ES backend present · shared C++ runtime · 16 KB aligned |
| AC-1.5 | Reproducible | re-run workflow; `sha256sum` both artifact sets | hashes identical (**no "or documented reason" escape** — if they differ, the WP fails) |
| AC-1.6 | Assimp untouched (N5) | `git diff master -- recipes/build-assimp-android.sh` | empty |
| AC-1.7 | WP-1.6 promotion holds | `dotnet build Wuka/Wuka.csproj` | exit 0 with XA0141/XA4301 as errors |

> **AC-1.2/1.4 need ELF tooling** — Android `.so`s are ELF; `otool` is Mach-O only and will not
> work. Use the NDK's bundled `llvm-readelf` (present on every machine that has the NDK). The
> recipes already do this, so a local build needs nothing extra installed.
>
> **AC-1.5 is expensive** (a full CI matrix re-run). It is on the §2.5 evidence-based exemption list.

> ### AC-1.4 was rewritten 2026-08-06, and why it matters beyond this one row
>
> It originally read: `llvm-readelf -d libopenal.so | grep NEEDED` → `libOpenSLES.so`, **not**
> `libc.so.6`.
>
> **No correct build satisfies that.** openal-soft `dlopen()`s OpenSL ES, so it appears in the
> binary as a string and a symbol reference and *never* as a `NEEDED` entry — confirmed against both
> the library shipped in the APK today and a fresh build (WP-1.2). As written, the criterion failed
> good builds and would have pushed someone toward "fixing" a non-problem.
>
> The **intent** was right — *this is a real Android build and it can actually make sound* — so it is
> now tested the two ways that are true of a good build (no glibc; OpenSL ES backend present),
> alongside the shared-C++-runtime and 16 KB checks.
>
> The change worth generalising is *where* it lives. **A criterion that can be a build-time assertion
> should be one.** A gate checked once at Phase-1 sign-off protects nothing in month six; an
> assertion in the recipe fails the build the day it regresses. Two defects in Phase 1 were caught
> exactly this way and neither would have failed a build otherwise:
>
> - `libopenal.so` linked libc++ **statically** while `libassimp.so` used the shared runtime — links
>   and runs fine alone, breaks only in combination.
> - The Linux `libopenal.so` shipped with **no real audio backend** (OSS/WaveFile/Null only) and was
>   silent. CI reported success.
>
> Prefer assertions in `recipes/` over commands in this table wherever the check can be made from the
> build's own output.

---

### Phase 2 — Android SDL3 spike 🔴 **HIGH RISK — GATES PHASE 3**

Timeboxed. The deliverable is **an answer**, not a feature. A spike that compiles but was never run
on a device answers nothing.

- **WP-2.1** — bare SDL3 Android activity → GLES 3.0 context → `SDL_GL_GetProcAddress` → clear screen.
  Vendor **flibitijibibo/SDL3-CS** (single generated `.cs`, ships no natives — `ppy/SDL3-CS` and
  `edwardgushchin/SDL3-CS` bundle natives and reintroduce the coupling being removed).
- **WP-2.2** — rebase `GameActivity` (`Wuka/Platforms/Android/GameActivity.cs:27`, today `: SilkActivity`)
  onto SDL3's `org.libsdl.app.SDLActivity`; confirm the MAUI shell (`MainActivity.cs:155`) still
  launches it.
- **WP-2.3** — port or replace `GameSurface.cs` (229 LOC) and `KarawanInputConnection.cs` (116 LOC).
  **SDL3 reworked text input** (`SDL_StartTextInput` is now per-window). `docs/SYSTEMS/PLATFORMS/ANDROID.md`
  records *why* SDL2's IME path was bypassed — re-validate that reasoning against SDL3 rather than
  porting the workaround forward blindly.

| id | Criterion | Command | Expected |
|---|---|---|---|
| AC-2.1 | APK builds | `dotnet build Wuka/Wuka.csproj -f net9.0-android36.0` | exit 0 |
| AC-2.2 | SDL3 alignment verified in the APK | `readelf -lW lib/arm64-v8a/libSDL3.so` | 16 KB |
| AC-2.3 | Silk windowing gone | `rg 'Silk\.NET\.(Windowing\|Input\|SDL)' Wuka/` | no matches |
| AC-2.4 | `[HUMAN]` **GATE-A** | physical device | clear screen; multi-touch; **IME text entry**; rotation; resume |
| AC-2.5 | `[HUMAN]` **GATE-B** | Play Console | no "Memory page size" warning |

> **AC-2.4 is the single most likely point of failure in this whole plan** (ADR §9 claim 8). If IME
> cannot be made to work on SDL3, stop and re-plan with the human. Do not proceed to Phase 3.

---

### Phase 3 — Windowing / input / audio (~2,100 LOC)

**Blocked until GATE-A and GATE-B pass.**

- **WP-3.1** — rewrite input handling in `Splash.Silk/Platform.cs` (`:254-490`) over SDL3 events.
  The string-keyed `engine.news.EventQueue` translation is the **contract** — only the left-hand side
  changes.
- **WP-3.2** — window lifecycle + main loop (`:560-885`). The existing hand-rolled loop already avoids
  `iView.Run()` and drives events/update/render itself, so it maps directly onto `SDL_PollEvent`.
- **WP-3.3** — change `EasyCreate` to stop taking a `Silk.NET.Windowing.IView` (`Platform.cs:1050`).
  That parameter is the sole reason all five launchers import Silk. Update `Karawan`,
  `examples/Launcher`, `Wuka`, `Testbed`, `TestRunner`.
- **WP-3.4** — replace `Silk.NET.OpenAL` with ~250 LOC of hand-written `DllImport`s (24 entry points,
  ~92 call sites in `Boom.OpenAL/`).
- **WP-3.5** — deletions: GLFW-vs-SDL type-name sniffing (`Platform.cs:908-916`), the raw-SDL2
  `BeforeDoEvent` hatch (`GameActivity.cs:83-153`), `WukaSilkActivity.cs`, duplicated GLES shaders
  under `Wuka/Platforms/Android/`.

**Internal ordering (not parallel):** 3.1 → 3.2 → 3.3 → 3.5, then 3.4 ⟂. WP-3.1/3.2/3.3/3.5 all edit
`Splash.Silk/Platform.cs` and **must be serialised** — dispatching them concurrently guarantees
conflicts.

**Project structure decision needed before WP-3.1:** the vendored `SDL3-CS.cs` is introduced by
WP-2.1 in an Android context, but desktop needs it too. Decide where it lives (new `Platform.SDL3`
project vs. inside `Splash.Silk`) and record it in the WP-2.1 PR — this is currently unassigned.

| id | Criterion | Command | Expected |
|---|---|---|---|
| AC-3.1 | Silk windowing/input/audio gone | `rg 'Silk\.NET\.(Windowing\|Input\|SDL\|OpenAL)' --glob '*.csproj'` | no matches. **Assimp, OpenGL, OpenGL.Extensions.\*, Core legitimately remain** until Phases 4/5 |
| AC-3.2 | No launcher imports Silk | `rg 'using Silk' Karawan/ Wuka/ examples/ Testbed/ TestRunner/` | no matches |
| AC-3.3 | Event contract unchanged | `git diff master -- JoyceCode/engine/news/Event.cs` | empty |
| AC-3.4 | Headless still works | `dotnet run --project TestRunner/TestRunner.csproj -- --help` | exit 0 |
| AC-3.5 | macOS desktop launches | `dotnet run --project Karawan/Karawan.csproj` | process stays alive >10s, no exception in log |
| AC-3.6 | `[HUMAN]` macOS **interactive** | — | input actually responds (not agent-checkable) |
| AC-3.7 | `[HUMAN]` **GATE-C** | Windows + Linux | keyboard, mouse, gamepad, fullscreen, resize, HiDPI |
| AC-3.8 | `[HUMAN]` **GATE-E** | all platforms | ImGui renders and takes input |

---

### Phase 4 — Bake FBX out of the runtime

Independent of Phases 2–3; may run in parallel once Phase 0 lands.

- **WP-4.1** — MessagePack-annotate the `Model` graph (`Model`, `ModelNodeTree`/`ModelNode`,
  `Skeleton`, `engine.joyce.Mesh`, `Material`, `InstanceDesc`). **This is the bulk of the effort and
  the main unknown** — spike `Mesh` + `Skeleton` first and report before continuing.
  *Exception to N6: this WP modifies `JoyceCode/engine/joyce/`.*
- **WP-4.2** — `ModelCompiler` in Chushi emitting `mo-{hash}`, mirroring `AnimationCompiler`
  (`Chushi/ConsoleMain.cs:171-182`) including staleness skipping.
- **WP-4.3** — baked-first load path on `Model`, mirroring `BakeAnimations` (`Model.cs:207-237`).
- **WP-4.4** — re-declare resources; drop the 25 `.fbx`; strip Assimp from `Wuka.csproj`.

| id | Criterion | Command | Expected |
|---|---|---|---|
| AC-4.1 | Bake is deterministic | run `ModelCompiler` twice; `sha256sum mo-*` | byte-identical |
| AC-4.2 | Baked == runtime path | **a new xUnit test in `tests/JoyceCode.Tests/`** loading both paths and comparing | vertex/index/bone arrays equal within `1e-6` per float; bone **names and order** exactly equal |
| AC-4.3 | **Bone order stable** | `dotnet test --filter BakedAnimationLayoutTests` | pass |
| AC-4.4 | All 25 models load without FBX | move `*.fbx` aside; run TestRunner; grep the log | 25 `mo-*` load lines, **zero** `Fbx.LoadModelInstance` calls, zero errors |
| AC-4.5 | Assimp gone from APK | unzip APK | no `libassimp.so` |
| AC-4.6 | `libc++_shared.so` decision recorded | check remaining consumers | documented keep-or-drop |
| AC-4.7 | `[HUMAN]` GATE-D | — | walk/idle/death correct on every character |

> **AC-4.3 is the trap.** `AllBakedMatrices` is indexed `frame * Skeleton.NBones + boneIndex`. If the
> model bake and the `ac-*` animation bake disagree on bone ordering, **every animation renders a
> foreign pose** — and it will look plausible, not crash. This exact failure mode already happened
> once (July 2026). Two builds are needed before a new asset stages (`Wuka.csproj` `<Import>`s the
> manifest at project-evaluation time).

---

### Phase 5 — Self-generated GL bindings

Last, deliberately: least urgent (GL is spec-frozen) and most mechanical.

> **⚠ Phase 5's approach is OPEN.** N4 was relaxed after review. Self-generation from `gl.xml` and
> OpenTK-for-GL-only (`S2b`) are both live, and **S2b has never been costed** (ADR §11a). Claim 3 is
> also understated (ADR §11c): matching Silk's *overload expansion policy* — unsafe/`Span`/`out`/`ref`
> variants, the dual typed-enum/`GLEnum` surface, string marshalling, `GL.GetApi` plumbing — is the
> real work, not name matching. The ~500 LOC estimate is likely 2–4× optimistic. **Do not start
> WP-5.1 until WP-5.0 and WP-5.0b are both reported and the human has chosen.**

- **WP-5.0** — **prototype first, and run it EARLY** (it is cheap, independent, and can run any time
  after Phase 0 — do not leave it to month N). Extract a representative call-site sample from
  `SilkThreeD.cs` into a scratch project; generate 5 entry points + 2 enum types from `gl.xml` into a
  **distinct namespace**; compile the sample against it via `extern alias` or a namespace swap.
  *Note the naive version does not work*: 5 generated entry points cannot make `SilkThreeD.cs`
  (1,168 LOC) compile, and emitting identical names while the Silk package is still referenced is a
  namespace collision. Designing this harness **is part of the WP**.
- **WP-5.0b** — **cost S2b honestly**: OpenTK GL-only via `GLLoader.LoadBindings` over
  `SDL_GL_GetProcAddress`. Measure the actual rename churn on the same call-site sample. Report
  both options side by side with real numbers.
- **WP-5.1** — full generator (~500 LOC) covering 80 entry points and ~10 enum types, emitting the
  names already in use.
- **WP-5.2** — swap `Splash.Silk` onto the generated bindings; keep a `GLCheck`-compatible error path
  (`Splash.Silk/GLCheck.cs`) and the `INativeContext`-equivalent seam Aihao needs.
- **WP-5.3** — detach the ImGui backend by inlining `ImGuiFontConfig`. `ImGui.NET` is **not** a Silk
  package and stays.
- **WP-5.4** — rename `Splash.Silk` → `Splash.GL`.

| id | Criterion | Command | Expected |
|---|---|---|---|
| AC-5.0 | Call-site churn measured (claim 3) | `git diff --numstat` on the WP-5.0 sample | **exactly 0** changed lines, or a number reported honestly — no "~0" |
| AC-5.0b | S2b costed | WP-5.0b report | churn numbers for both options, side by side |
| AC-5.1 | Zero Silk in **code** | `rg 'Silk\.NET' --glob '*.cs' --glob '*.csproj'` | **no matches**. Docs (this file, the ADR, `CLAUDE.md`, `ANDROID_NATIVE_LIBS.md`) legitimately still mention Silk historically — do **not** scrub them |
| AC-5.2 | Generator rerunnable | run it twice; `sha256sum` output | identical |
| AC-5.3 | Generated code committed | `git status` | generated files tracked, not gitignored |
| AC-5.4 | `[HUMAN]` **GATE-F** | pixel compare | frames identical pre/post on all platforms |
| AC-5.5 | `[HUMAN]` GATE-E | — | ImGui incl. Linux Fn-key case |

> **GATE-F baseline must be captured BEFORE WP-5.2 merges.** Reference frames from the current Silk
> build, on every platform, stored somewhere named. Afterwards the comparison is unrunnable.

> `GlStateSaver` and `SilkRenderState` are the subtle ones: a wrong enum value there **fails silently**
> rather than loudly. Pixel comparison is the only reliable check.

---

## 5b. Execution environment (verify before claiming any PASS)

> **Corrected 2026-08-05 (WP-0.0).** The table below originally described the owner's **Mac**.
> Work is actually happening on **Windows 11**, where the situation is close to inverted: the
> ELF tooling and NDK *are* present, and `gh` — which §2.1 needs for every single PR — is *not*.
> Re-verify on any new machine rather than trusting this table.

| Need | Status (Windows 11, verified 2026-08-05) | Used by |
|---|---|---|
| `readelf` / `llvm-readelf` (ELF, for Android `.so`) | ✅ **present** via the NDK: `C:\Program Files (x86)\Android\AndroidNDK\android-ndk-r27c\toolchains\llvm\prebuilt\windows-x86_64\bin\llvm-readelf.exe` (also `llvm-objdump.exe`). Not on `PATH` — invoke by full path. | AC-0.0.1, 0.0.3, 1.2, 1.4, 2.2 |
| Android NDK | ✅ **present** — `C:\Program Files (x86)\Android\AndroidNDK\{android-ndk-r23c, android-ndk-r27c}` | WP-0.0, 1.2, 1.3 |
| `gh` authenticated | ❌ **ABSENT** — not installed. **Blocks §2.1 entirely** (every WP must open a PR) and AC-1.1. Install before dispatching any WP. | AC-1.1, all PR operations |
| `cmake` | ✅ 4.3.3 — note it rejects `cmake_minimum_required` < 3.5; needs `-DCMAKE_POLICY_VERSION_MINIMUM=3.5` for older sources | WP-1.2, 1.3 |
| `ninja` | ❌ absent — standalone `ninja.exe` works; pin it in CI (WP-1.1) | WP-1.2, 1.3 |
| `java` | ❌ absent — only needed if an AAR's Java side must be rebuilt | WP-1.3, 2.x |
| Physical Android device | human | GATE-A |
| Windows machine | ✅ this **is** the Windows machine — GATE-C/D still need a human to *look* at it | GATE-C, GATE-D |
| Linux machine | human | GATE-C, GATE-E |
| Play Console access | human | GATE-B |

A worker that cannot run a command **must report `TOOL-MISSING`**, never infer the result.

## 5c. Off-the-rails thresholds

The orchestrator halts the whole programme and escalates when any of these trips:

- **Phase 2 timebox exceeded:** more than **10 worker dispatches** across WP-2.1–2.3 without GATE-A passing.
- **Total re-dispatch budget:** more than **25** re-dispatches programme-wide.
- **Any ADR §9 "assumed" claim falsified** (§4) — several change the plan's shape.
- **Calendar:** if Phases 0–2 are not complete within 3 months, re-evaluate against S1/S2.

**The bank-the-wins exit.** Phases 0–2 deliver the entire acute value (Play unblocked, guardrails,
reproducible natives). ADR §9 concedes Phase 5 buys *no correctness and no capability*, and §11 records
unresolved objections to it. **Stopping after Phase 2 — or after Phase 3-on-Android — is a legitimate
successful outcome, not a failure.** The orchestrator should say so explicitly when a threshold trips.

## 6. Definition of done (whole plan)

**Status as of 2026-08-14: Phases 0–5 complete and merged.** Marked up against what was actually
achieved — see `PLATFORM-BACKEND-STATUS.md` for the evidence behind each line.

- [x] ~~`rg 'Silk\.NET' --glob '*.cs' --glob '*.csproj'` returns nothing~~ — **met in substance,
      NOT literally satisfiable, and it should not be.** Zero Silk.NET in any *shipping* project.
      What remains: `Silk.NET.Assimp` in `JoyceFbx` (build-time fbx import, which Phase 4 chose
      deliberately) and `Silk.NET.OpenGL` in the WP-5.0/5.1 comparison tools, whose entire job is
      to diff our binding *against* Silk. The criterion behind the criterion is met.
- [x] APK publishes to Play with no page-size warning — GATE-B, passed twice (Mono, then .NET 10 +
      CoreCLR, versionCode 199)
- [x] One windowing backend (SDL3) on all platforms — Phase 3; `SilkWindowBackend` deleted
- [x] `libassimp.so` absent from the APK — verified from the built APK, and `scripts/check-apk.py`
      now **fails** if it returns. ⚠ `AssimpVersionDetector.cs` / `AssimpVersion.cs` were NOT
      deleted, deliberately: `FbxModel` uses the detected version to compensate bone offset
      matrices at load time, so deleting them would silently change the geometry the bake
      produces. Both moved into `JoyceFbx` instead.
- [x] All natives built by pinned CI, distributed as a versioned package — `Karawan.Natives` 0.2.0
- [x] `Splash/`, `JoyceCode/`, `nogameCode/`, `models/shaders/` show no functional diff except
      WP-4.1's `engine/joyce` serialisation attributes — shaders untouched throughout; the
      `JoyceCode` changes beyond WP-4.1 are the Phase 4 baked-load path and the WP-5.4 rename,
      each granted in its own work package
- [ ] **CI asserts zero Silk references so it cannot creep back** — ✗ **NOT DONE. This repo still
      has no CI at all**, which is also KI-17 (nothing enforces that the generated GL files are
      reproducible). The single largest remaining structural gap in the programme.
- [x] ADR status changed from *Proposal* to *Accepted*, with outcomes recorded against each §9
      claim — done 2026-08-14, plus a resolution per §11 challenge. Two are recorded as **not**
      achieved: no CI (claim 11), and no ANGLE evaluation (11d), which the ADR now names as the
      largest open risk it leaves behind.

---

## 7. How this fails

Ranked by likelihood, for the orchestrator to watch for.

1. **An agent declares a device gate passed** from something that merely compiled. N9 and §3 exist for
   this. Re-run every claimed command yourself.
2. **An agent re-derives rev 1 or rev 2** because "keep the bindings, own the natives" sounds
   reasonable in isolation. N1/N2. Quote §10.
3. **Phase 4 bone ordering silently breaks animations.** AC-4.3. Looks plausible, doesn't crash.
4. **Scope creep into `JoyceCode`.** AC-GLOBAL-2 catches it mechanically.
5. **An agent weakens a criterion to make it pass.** The loop in §2.2 forbids this explicitly; the
   orchestrator's independent re-run is the backstop.
6. **WP-0.0 is skipped** because it produces no code. It is the cheapest thing here that could change
   the whole plan.

---

## Document History

| Date | Change |
|---|---|
| 2026-08-04 | Created from `docs/ARCHITECTURE/PLATFORM_BACKEND.md` rev 3 |
| 2026-08-06 | **AC-1.4 rewritten.** It required `libOpenSLES.so` in `NEEDED`, which no correct build produces — openal `dlopen()`s it. Replaced by four assertions that live in `recipes/build-openal.sh` and run on every Android build. Establishes the general preference: a criterion that can be a build-time assertion should be one. Also corrected the §8 note about ELF tooling, which assumed a Mac. |
| 2026-08-05 | **WP-0.0 executed — ADR claim #6 falsified, claim #7 confirmed.** Programme halted for re-plan per §4/§5c. Added `PLATFORM-BACKEND-STATUS.md` (the §2.2b ledger, previously missing). Corrected §5b (described macOS; work is on Windows 11 — ELF tooling and NDK are present, `gh` is **not**). Corrected AC-0.0.3's SDL3 AAR path (prefab layout, not `jni/`). |
| 2026-08-04 | Revised after orchestrator review. Fixed: WP-0.3 vs AC-GLOBAL-1 contradiction (promotion deferred to WP-1.6); WP-0.1 scope 6→13 csprojs; AC-3.1 and AC-5.1 expected values (were unachievable); AC-1.1/1.5/4.2/4.4/5.0 made measurable; GATE-D moved to WP-0.1; TestRunner added (not in `.sln`). Added: §2.2b state ledger, §2.2c PR-rejected path, §2.2d conflict sets, §2.2e gates-are-pre-merge, §2.5 re-run exemptions, §5b environment, §5c off-the-rails thresholds and the bank-the-wins exit. **N4 relaxed** — Phase 5's approach is now open pending S2b costing (WP-5.0b). |
