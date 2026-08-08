# Platform Backend — state ledger

Required by [`IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md`](IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md) §2.2b.
**The orchestrator must update this on every dispatch and every result.** Without it, a fresh
orchestrator session reconstructs state by git archaeology and gets the "max 3 iterations"
count wrong.

**Last updated:** 2026-08-07

---

## ▶ Programme status: RESUMED — direction unchanged after re-plan

**Escalation raised 2026-08-05:** plan §5c, *"Any ADR §9 'assumed' claim falsified"*.
**WP-0.0 falsified claim #6** — Silk's SDL2 AAR **can** be fixed locally.

**Human decision (2026-08-05): continue as originally planned.** Rationale, in the owner's
words: *"the exchangability of the SDL2 with a 16k page version gives us more time, but does
not solve the underlying problem of lack of maintenance attention for SILK2."*

The falsification is therefore **schedule relief, not a change of direction** — exactly the
distinction drawn in `WP-0.0-FINDINGS.md` §1: claim #6 was load-bearing for *urgency*, while the
case for the migration rests on ADR §4c (Silk 2.x maintenance-mode, 3.0 a rewrite), which this
spike did not test and which still stands.

Consequences carried forward:

- Phases 2–3 proceed, but **without shipping pressure** — a GATE-A/GATE-B failure is no longer
  release-blocking, so a real re-plan stays available if IME on SDL3 proves intractable.
- The **repacked-AAR route stays in the back pocket** as a Play unblock if Phase 2 slips.
- Phase 1's scope may still grow to include SDL2-for-Android; decide when WP-1.1 is dispatched.

---

## Work package status

| WP | Status | Branch | PR | Iter | AC results | Gates | Notes |
|---|---|---|---|---|---|---|---|
| **WP-0.0** | ✅ **MERGED** | `platform/wp-0.0` | [#8](https://github.com/tweggen/Karawan/pull/8) | 1 | AC-0.0.1 ✅ · 0.0.2 ✅ · 0.0.3 ✅ · 0.0.4 ✅ | none apply | **Claim #6 FALSIFIED, claim #7 confirmed.** Repack demonstrated working; artifact never executed. |
| **WP-0.1** | ✅ **MERGED** | `platform/wp-0.1` | [#9](https://github.com/tweggen/Karawan/pull/9) | 1 | AC-0.1.1 ✅ · 0.1.2 ✅ · 0.1.3 ✅ (59/59 identical) · 0.1.4 ✅ · GLOBAL-1 ✅ · 1b ✅ · 2 ✅ · 3 ✅ · 4 ✅ (168/168) | **GATE-D ✅ passed** (Windows + macOS Debug, 2026-08-06) | CPM across 12 csprojs; `Aihao.old` excluded. Two version conflicts kept exact via `VersionOverride`. **CPM silently disabled MAUI's implicit `Microsoft.Maui.Controls`** — now declared explicitly in `Wuka.csproj`. |
| **WP-0.2** | ✅ **MERGED** | `platform/wp-0.2` | [#14](https://github.com/tweggen/Karawan/pull/14) | 1 | AC-0.2.1 ✅ · 0.2.2 ✅ (source clean) · 0.2.3 ✅ · GLOBAL-1/1b/2/3/4 ✅ | **AC-0.2.4 ✅** Aihao preview confirmed rendering | `GLAnimBuffers` → `Splash.Silk`; GL-version detection moved into `PreviewHelper`, so **Aihao has zero Silk references in source**. `AvaloniaNativeContext` deleted; `SetExternalGL`/`GetGL` now `internal`. |
| **WP-0.3** | ✅ **MERGED** | `platform/wp-0.3` | [#12](https://github.com/tweggen/Karawan/pull/12) | 1 | AC-0.3.1 ✅ · 0.3.2 ✅ · GLOBAL-2/3 ✅ | none apply | Inventory only. **Found a gap in the plan: `libcimgui.so` (ImGui.NET) is also not 16 KB-aligned**, so the Silk exit alone would not achieve compliance. Fixed separately in [#13](https://github.com/tweggen/Karawan/pull/13). |
| **WP-1.1** | ✅ **MERGED** | `platform/wp-1.1` | [#15](https://github.com/tweggen/Karawan/pull/15) | 1 | **AC-1.1 ✅** CI green, 6 targets | none apply | First workflow in the repo. Pinned runner images (all three, not just macOS — flagged deviation), NDK `27.2.12479018`, MSVC toolset `14.44`, all Actions by SHA. No third-party actions. |
| **WP-1.2** | ✅ **MERGED** | `platform/wp-1.2` | [#16](https://github.com/tweggen/Karawan/pull/16) | 1 | AC-1.1 ✅ · 1.6 ✅ · GLOBAL-2/3 ✅ | none apply | openal-soft, all six targets, pinned by tag **and** commit. Caught: static-libc++ clash with assimp, and a Linux build with **no audio backend** that CI had already passed. Both now build-time assertions. |
| **WP-1.3** | ✅ **MERGED** | `platform/wp-1.3` | [#17](https://github.com/tweggen/Karawan/pull/17) → [#18](https://github.com/tweggen/Karawan/pull/18) | 1 | AC-1.1 ✅ · 1.2 ✅ (asserted in-build) · 1.6 ✅ | none apply | SDL3, all six targets. ⚠ Merged into `platform/wp-1.2` rather than master; **#18 was needed to land it** — see §Merge-ordering below. |
| **WP-1.4** | ✅ **MERGED** | `platform/wp-1.4` | [#19](https://github.com/tweggen/Karawan/pull/19) → [#20](https://github.com/tweggen/Karawan/pull/20) | 1 | AC-1.1 ✅ · GLOBAL-2/3 ✅ | **publish 🔒 not done** | `Karawan.Natives` NuGet: `runtimes/<rid>/native/` + Android `.aar` + `build-manifest.json` with per-file sha256. AAR built deterministically (python, fixed timestamps) — byte-identical across runs. ⚠ Same merge-ordering trap as #17; **#20 was needed to land it**. |
| **WP-1.5** | ✅ **MERGED** | `platform/wp-1.5` | [#26](https://github.com/tweggen/Karawan/pull/26) | 1 | AC-GLOBAL-1 ✅ · 2/3 ✅ · 4 ✅ (168/168) · AC-1.6 ✅ | **desktop audio confirmed by owner** (Windows) | `Karawan.Natives` replaces `Silk.NET.OpenAL.Soft.Native`. Windows file name differs (`OpenAL32.dll` vs `soft_oal.dll`); Silk falls back across name candidates — verified by forcing **real** native calls, since `GetApi()` alone binds lazily and proves nothing. **Android deliberately unchanged** (duplicate `.so` + libc++ ABI vs assimp); revisit at Phase 4. |
| **WP-1.6** | ⚠ **PARTIAL — PR-OPEN** | `platform/wp-1.6` | [#27](https://github.com/tweggen/Karawan/pull/27) | 1 | **AC-1.7 ⚠ half** — XA4301 ✅ promoted, **XA0141 ⛔ deferred** | none apply | XA4301: 0 occurrences, promoted, build green. XA0141: promoted → **4 errors, all `Silk.NET.Windowing.Sdl`'s `libSDL2.so`/`libmain.so` @ `0x1000`**. Every native we own is `0x4000` (verified independently of the SDK). **Not satisfiable in Phase 1** — only removing Silk's SDL2 fixes it, which is Phase 2/3. See §AC-1.7 below. |
| **WP-2.1** | ⏳ **PR-OPEN — blocked on GATE-A** | `platform/wp-2.1` | [#28](https://github.com/tweggen/Karawan/pull/28) | 1 | AC-2.1 ✅ · **2.2 ✅** · 2.3 ✅ · GLOBAL-1/2/3/4 ✅ | **GATE-A 🔒 human+device** | Spike builds and packages; **every `.so` in the APK is 16 KB aligned**, a first. Found 3 defects listed below. `Platform.SDL3` + `recipes/build-mainshim.sh` are permanent; `spikes/` is disposable. |
| WP-2.2 – 2.3 | BLOCKED | — | — | 0 | — | GATE-A, GATE-B | Blocked on GATE-A. ⚠ AC-2.2/AC-0.0.3 AAR path warning was **wrong for our own package** — `Karawan.Natives` uses `jni/<abi>/`, not prefab. |
| WP-3.1 – 3.5 | BLOCKED | — | — | 0 | — | GATE-C, GATE-E | Blocked on GATE-A + GATE-B per plan. |
| WP-4.1 – 4.4 | NOT-STARTED | — | — | 0 | — | GATE-D | Independent of Phases 2–3; Phase 0 has landed, so this is dispatchable now. |
| **WP-5.0** | ✅ **MERGED** | `platform/wp-5.0` | [#22](https://github.com/tweggen/Karawan/pull/22) | 1 | **AC-5.0 ✅ exactly 0 changed lines** | none apply | Generated from `gl.xml`; baseline and candidate both compile the identical sample. **Caveat: 4 hand-written overloads for 5 entry points** — `gl.xml` cannot describe Silk's overload policy. |
| **WP-5.0b** | ✅ **MERGED** | `platform/wp-5.0` | [#22](https://github.com/tweggen/Karawan/pull/22) | 1 | **AC-5.0b ✅** costed side by side | none apply | OpenTK 5: **37 % of code lines** ≈ 83 of 225 sites. `GL` is static vs Silk's instance → all 225 change receiver. Also `pre.16` ships **net10.0 only**, dropping our net9.0. |
| **WP-5.1** | ✅ **MERGED** | `platform/wp-5.1` | [#25](https://github.com/tweggen/Karawan/pull/25) | 1 | generated surface compiles standalone ✅ | none apply | `Splash.GL/generated/GL.g.cs` generated from Khronos `gl.xml`, no package references. Surface resolved by **Roslyn** (339 call sites / 81 distinct entry points), not regex — an earlier MSBuildWorkspace attempt silently reported **zero**, indistinguishable from "uses no GL". |
| WP-5.2 – 5.4 | **BLOCKED-ON-HUMAN** | — | — | 0 | — | GATE-E, GATE-F | Owner chose **S2a, narrow form** (2026-08-06). Remaining blocker: GATE-F reference frames, plus `Silk.NET.OpenGL.Extensions.ImGui` entanglement — it takes Silk's `GL` type in its public API, so swapping the GL binding drags ImGui with it. |

Status vocabulary: `NOT-STARTED / IN-PROGRESS / PR-OPEN / BLOCKED-ON-HUMAN / MERGED / ABANDONED`.

### Open PRs

| PR | What | State |
|---|---|---|
| [#28](https://github.com/tweggen/Karawan/pull/28) | WP-2.1 — Android SDL3 spike | open; buildable ACs green, **GATE-A needs a device** |

### 🟡 GATE-A evidence — rendering confirmed on device, 2026-08-07

The WP-2.1 spike ran on a physical Adreno 825 device. Verbatim from `logcat`:

```
SDL/APP   pixel format wanted SDL_PIXELFORMAT_RGBA8888 (1), got SDL_PIXELFORMAT_RGBA8888 (1)
SDL3SPIKE GL_VENDOR   = Qualcomm
SDL3SPIKE GL_RENDERER = Adreno (TM) 825
SDL3SPIKE GL_VERSION  = OpenGL ES 3.2 V@0800.73.1 (GIT@b39bb67739, ...)
SDL3SPIKE drawable    = 1260x2800
SDL3SPIKE first frame presented
```

**What this establishes**, beyond "it renders":

- The **whole managed bridge works**: `SDLActivity` → `libmain.so` → `SDL_main` → managed
  `SdlMain` → `SpikeRenderer.Run`. Nothing else in the process requests an EGL context, and the
  Adreno driver banner (`/vendor/lib64/egl/libGLESv2_adreno.so`, note `lib64` → arm64-v8a) appears
  before the first spike log line.
- **`SDL_GL_GetProcAddress` resolves GL entry points.** All four (`glClearColor`, `glClear`,
  `glGetString`, `glViewport`) loaded; the spike throws if any returns null. This is the same
  mechanism Phase 5's generated bindings use, so **WP-5.2 is de-risked as a side effect**.
- **GLES 3.2, not the 3.0 requested** — a better context than the minimum, and useful input to
  Phase 5's GLES feature baseline.
- Our own 16 KB `libSDL3.so` **loads and runs**, not merely packages.
- **The screen cycles colours** (confirmed visually by the owner). This matters more than the
  `first frame presented` line on its own: it means `SDL_GL_SwapWindow` is presenting *every*
  frame and the loop keeps running, rather than one lucky frame followed by a stall. The colour
  cycle exists in the spike precisely so this is answerable by looking rather than by trusting the
  absence of errors.

**Also confirmed on device (2026-08-07):** **multi-touch** ✅ (distinct `FINGER_DOWN` ids) and
**rotation** ✅ (`RESIZED` with swapped dimensions). **Resume** ✅ **fully confirmed 2026-08-08** — rendering
continues after home-and-back, and all five lifecycle events now log via the event watch (see
KI-8; the original log lines were missing because the spike watched for them the wrong way).

**Still not established:** **IME**, which this spike cannot answer at all. GATE-A stays 🟡.

### 🔴 KI-8 — app-lifecycle events need `SDL_AddEventWatch`; `SDL_PollEvent` never sees them

Found by GATE-A: resume worked and rendering continued, but `WILL_ENTER_BACKGROUND` /
`DID_ENTER_FOREGROUND` never logged. The obvious reading — "the events aren't firing" — is wrong.
`SDL_events.h` says of **all six** app-lifecycle events (`TERMINATING`, `LOW_MEMORY`,
`WILL_`/`DID_ENTER_BACKGROUND`, `WILL_`/`DID_ENTER_FOREGROUND`):

> *This event must be handled in a callback set with `SDL_AddEventWatch()`.*

**This is absolute, not a timing hazard** — confirmed from the SDL source at our pinned commit.
`SDL_SendAppEvent` special-cases these six types and never queues them at all
(`src/events/SDL_events.c`):

```c
case SDL_EVENT_WILL_ENTER_BACKGROUND: ...
    // We won't actually queue this event, it needs to be handled in this call stack by an event watcher
    SDL_CallEventWatchers(&event);
```

The rationale is Android's pause semantics (`SDL_androidevents.c`): *"as soon as the enter
background event has been queued, the app will block. The application should do any life cycle
handling in an event filter while the event was being queued."*

> **⚠ Hard constraint on WP-3.2.** `Wuka`'s `GameActivity.OnStop` saves the game
> (`I.Get<engine.Saver>()?.Save("OnStop")`). Once SDL3 owns the activity that hook **must** hang
> off an event watch. Ported as a polled event it would never run at all — no crash, no error,
> just *"the game quietly stopped saving"*, noticed days later.
>
> **Threading — corrected 2026-08-08.** An earlier revision of this entry said the callback runs
> on the Android UI thread. **That was wrong.** It runs on the **SDL thread**, the same one that
> called `SDL_PollEvent`, because the chain is a single call stack: `SDL_PollEvent` →
> `SDL_PumpEvents` → `Android_PumpEvents` → `Android_OnPause` → `SDL_SendAppEvent` → watchers.
> The UI thread only enqueues a lifecycle token the SDL thread picks up. Consequence for WP-3.2:
> the save hook may touch game state **directly**, with no cross-thread hazard — simpler than the
> original entry implied.

**Confirmed on device 2026-08-08**, home-and-back produced all five in order:
`WILL_ENTER_BACKGROUND` → `DID_ENTER_BACKGROUND` → **`LOW_MEMORY`** → `WILL_ENTER_FOREGROUND` →
`DID_ENTER_FOREGROUND`. `LOW_MEMORY` firing during a routine backgrounding is a free signal to drop
caches that `Wuka` currently ignores.

> The spike ran **portrait** (1260×2800) because it does not pin an orientation; `Wuka`'s
> `GameActivity` sets `ScreenOrientation.Landscape`. Irrelevant to the spike, relevant to WP-2.2.

### 🔴 KI-5 — `Karawan.Natives` 0.1.0's Android payload is not consumable (found by WP-2.1)

NuGet resolves the package to `lib/netstandard2.0/_._` for a `net9.0-android` project and **never
selects** `lib/net9.0-android34.0/Karawan.Natives.Android.aar`:

```
warning NU1701: Package 'Karawan.Natives 0.1.0' was restored using '.NETFramework,Version=v4.6.1,
  ...' instead of the project target framework 'net9.0-android35.0'.
project.assets.json:  compile -> lib/netstandard2.0/_._
                      runtime -> lib/netstandard2.0/_._
```

**Cause**, by diff against a package whose `.aar` delivery works: `Silk.NET.Windowing.Sdl` ships
`lib/net7.0-android33.0/{aar, dll}` — an **assembly beside the aar**. Ours ships the `.aar` alone,
so NuGet does not count `net9.0-android34.0` among the package's supported frameworks.

**Why it went unnoticed:** WP-1.5 excludes the package's Android assets outright for unrelated
reasons (duplicate `.so`, libc++ ABI vs assimp), so the defect sat behind an exclusion nobody had
cause to lift. AC-1.1 (CI green) and AC-1.2 (alignment) both passed — the package *contains* the
right bytes, it just cannot hand them to a consumer.

**Workaround in WP-2.1:** reference the `.aar` by path via `GeneratePathProperty`. **Real fix:**
add a placeholder/assembly to that lib folder in `packaging/Karawan.Natives`, republish. Republish
is human-gated (§2.5), so it is not bundled into WP-2.1.

### 🟠 KI-6 — `Karawan.Natives` leaks **linux-x64** natives into Android APKs

Referencing the package from an Android project without `ExcludeAssets="native"` RID-falls-back
`runtimes/linux-x64/native/{libSDL3.so, libopenal.so}` into the APK — glibc x64 binaries that
cannot load on Bionic, neither 16 KB aligned. Same defect class as `ImGui.NET`'s `libcimgui.so`
in WP-0.3 §4.2, and exactly what plan **AC-1.3** forbids. Caught only because WP-2.1 promotes
`XA0141` to an error. Fix belongs in the package alongside KI-5.

### 🟡 KI-7 — `AndroidSupportedAbis` is silently ineffective on .NET 9

It emits `warning XA0036` and then builds the **default** ABI set. WP-2.1's first APK shipped
`x86_64` while the project asked for `armeabi-v7a`. Use `RuntimeIdentifiers`. Relevant to WP-2.2,
since a wrong ABI set is invisible unless someone lists the APK.

### ⛔ AC-1.7 is only half-satisfiable, and not for the reason the plan expected

The plan (§Phase 1) says of WP-1.6: *"With correct natives in place this should pass; if it does
not, the natives are not actually fixed and Phase 1 is not done."* **That inference does not hold
here**, so it is recorded rather than acted on.

Measured on `platform/wp-1.6`, promoting both codes:

| Code | Occurrences | Verdict |
|---|---|---|
| `XA4301` (duplicate native lib in APK) | 0 | ✅ **promoted**, build stays green |
| `XA0141` (no 16 KB page size) | 4 | ⛔ **deferred** |

All four `XA0141` errors name the same two libraries, from a package we do not build:

```
libSDL2.so, libmain.so   <-  Silk.NET.Windowing.Sdl 2.23.0 (.aar)   p_align = 0x1000
```

Every native this repository *is* responsible for already passes. Checked directly from the ELF
program headers, independently of the Android SDK (no NDK on the verifying machine), and the same
checker was confirmed to flag Silk's two — so this is not a checker that says OK to everything:

```
OK    libassimp.so       elf64  0x4000
OK    libc++_shared.so   elf64  0x4000
OK    libopenal.so       elf64  0x4000
FAIL  libSDL2.so         elf64  0x1000     <- Silk
FAIL  libmain.so         elf64  0x1000     <- Silk
```

So Phase 1 **is** done by its own scope (N5: openal-soft and SDL3 only). `XA0141` cannot be
cleared by fixing our natives; it clears when Silk's SDL2 leaves the APK, which is Phase 2/3.
Promoting it now would red-line master for every work package in between, which is precisely the
situation WP-0.3 deferred promotion to avoid.

Three ways out, for the record:

1. **Wait for Phase 2/3** (default, chosen). Master stays green; the criterion stays visibly open.
2. **Repack Silk's AAR at 16 KB.** Already proven feasible in WP-0.0 (claim #6 falsified). Clears
   the warning without waiting, but adds a build step whose only job is to patch a dependency we
   are in the middle of deleting.
3. **Promote anyway and accept red master.** Honest signal, no practical benefit — the fact is
   already recorded here and in a comment at the promotion site.

**Re-run when Silk.NET.Windowing.Sdl is removed:** add `XA0141` to `MSBuildWarningsAsErrors` in
`Wuka/Wuka.csproj` and rebuild. That is the whole remaining task for AC-1.7.

### ⚠ Merge-ordering trap — hit twice, now structurally closed

Both #17 and #19 were **stacked PRs based on `platform/wp-1.2`**. In each case the base branch
was merged to master *seconds before* the stacked PR landed on it, so GitHub reported MERGED —
truthfully, but into `platform/wp-1.2`, not master. Master silently lacked SDL3 (#17) and then
the packaging (#19). Recovery PRs #18 and #20 carried them across.

**All merged WP branches have since been deleted**, so nothing can be stacked onto them again.
**Future work packages branch from master directly**, even when that costs a rebase; the ordering
hazard is not worth the tidiness of stacking.

---

## Gate ledger

| Gate | What | Status |
|---|---|---|
| GATE-A | SDL3 spike on physical Android device (multi-touch, **IME**, rotation, resume) | 🟡 **RENDERING HALF PASSED 2026-08-07** on an Adreno 825 device — `GL_VERSION = OpenGL ES 3.2`, `first frame presented`. **Interaction half not yet run** (multi-touch, rotation, resume). ⚠ **The IME half cannot be answered by this spike at all** — see §GATE-A evidence below. |
| GATE-B | Play Console upload, no "Memory page size" warning | not reached — **now reachable much earlier via the WP-0.0 repack route** |
| GATE-C | Windows + Linux desktop | not reached |
| GATE-D | Animation correct on macOS + Windows | ✅ **PASSED 2026-08-06** — Windows confirmed, macOS confirmed on a **Debug** build (Release does not currently start, see known issue KI-1) |
| GATE-E | ImGui renders + takes input (incl. Linux Fn-key case) | not reached. **Android is now out of scope for this gate**: [#13](https://github.com/tweggen/Karawan/pull/13) excluded the ImGui native, and there is no Android build of cimgui at all — re-enabling it needs a real arm64 `libcimgui.so` built and shipped, not just flipping `createUI`. Desktop still applies. |
| — | *(AC-0.2.4, gate-adjacent)* Aihao L-System preview renders | ✅ **PASSED 2026-08-06** — confirmed after the GL-context seam was re-expressed in WP-0.2 |
| GATE-F | Pixel-compare before/after GL swap | not reached. ⚠ **Baseline must be captured before WP-5.2 merges** or it is unrunnable forever |

---

## Known issues (found in passing, not part of any WP)

| id | Issue | Status |
|---|---|---|
| **KI-1** | **Release builds do not start.** `Karawan/DesktopMain.cs:196-202` starts fullscreen when `#if DEBUG` is false. On macOS the fullscreen window switches display mode but never activates — it ends up minimised behind the IDE, the main thread parks in `LogicalRenderer.WaitNextRenderFrame`'s untimed `Monitor.Wait`, and the app appears hung while still alive. Debug builds are windowed and run fine on both platforms. **Untested on Windows Release.** Also note fullscreen is applied at `DesktopMain.cs:218`, *before* `iWindow.Initialize()` at :220. | **postponed by owner 2026-08-06** — no Windows machine available to complete the 2×2. Pre-existing; not caused by any WP. |
| **KI-2** | `I.RegisterFactory: Error: Already registered engine.news.EmissionContext` on every run, both platforms. Something registers that factory twice. | open, unowned, benign so far |
| **KI-3** | `ink` is listed as a required sibling checkout in `README.md` and `CLAUDE.md`, but no csproj references it via `$(SiblingRoot)`. Possibly a second dead prerequisite (cf. FbxSharp, PR #10). | open, unverified |
| **KI-4** | An unbounded `Monitor.Wait` in `LogicalRenderer.WaitNextRenderFrame` runs on the thread macOS requires for event pumping, so *any* logical-thread fault presents as a frozen app rather than an error. A timeout that logs and returns null would make this class of failure diagnosable. | open — deliberately not "fixed", since it would mask causes |

---

## Budget counters (plan §5c off-the-rails thresholds)

| Counter | Limit | Current |
|---|---|---|
| Phase 2 worker dispatches | 10 | 0 |
| Programme-wide re-dispatches | 25 | 0 |
| Calendar: Phases 0–2 complete | 3 months from 2026-08-04 | day 3 — **Phase 0 and Phase 1 (except 1.5/1.6) complete** |
| ADR §9 "assumed" claims falsified | any → escalate | **1 (claim #6)** — escalated 2026-08-05, resolved: continue as planned |

Nothing has tripped. Worth noting the programme is well inside every threshold: no work package
has needed a second iteration, and the two recovery PRs (#18, #20) were merge-ordering fixes
carrying already-reviewed commits, not re-dispatches.

---

## Environment blockers

Tracked here because they gate whole phases. Full detail in `WP-0.0-FINDINGS.md` §5.

| Blocker | Impact | Status |
|---|---|---|
| `gh` not installed | **blocked §2.1 entirely** — every WP must open a PR — and AC-1.1 | ✅ **resolved 2026-08-05** — `gh` 2.97.0 installed via winget to `C:\Program Files\GitHub CLI`, authenticated as `tweggen` |
| Plan §5b describes macOS; work is on Windows 11 | misleads every future worker | ✅ fixed 2026-08-05 (#8) |
| PAT lacks the `workflow` scope | GitHub **refuses any push touching `.github/workflows/`**, so no workflow change can be pushed with it | ⚠ **open** — worked around by pushing with the `gh` credential, which does have the scope. Will recur on every workflow change. Fixing the PAT (below) fixes this too. |
| `Karawan.sln` would not restore at worktree depth | `.sln` hardcodes `..\DefaultEcs` etc., which don't resolve under `.claude/worktrees/<name>/` | ✅ **resolved 2026-08-05** — directory junctions for the five sibling repos placed in `.claude/worktrees/`, and that path added to `.git/info/exclude` (local only). The whole solution now builds from a worktree. |
| PAT embedded in the `origin` remote URL | plaintext in `.git/config`; leaks on any `git remote -v` | ⚠ **open — recommend rotating.** Surfaced during WP-0.0. Move to a credential helper. |
| `ninja` not installed | needed for any native build recipe | workaround: standalone `ninja.exe`; should be pinned in CI (WP-1.1) |
| `java` not installed | only matters if the SDL Java side must be rebuilt | open, not currently blocking |
| No physical Android device / Windows / Linux / Play Console access **for the agent** | GATE-A/B/C/D/E/F | inherent — human-only by design |
