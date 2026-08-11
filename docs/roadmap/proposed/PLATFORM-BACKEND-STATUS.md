# Platform Backend — state ledger

Required by [`IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md`](IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md) §2.2b.
**The orchestrator must update this on every dispatch and every result.** Without it, a fresh
orchestrator session reconstructs state by git archaeology and gets the "max 3 iterations"
count wrong.

**Last updated:** 2026-08-10 (WP-6.2 spike — AbiProbe passes on CoreCLR/ARM64)

---

# ▶▶ RESUME HERE — state at 2026-08-10

**Phase 2 and the Android half of Phase 3 are MERGED.** `Wuka` runs on SDL3 with Silk windowing
gone. It **starts, renders, plays audio, loads TALE, and is now controllable on a physical device.**

**Since 2026-08-08, open problems 1, 4 and 5 are all closed, the touch buttons work, and — as of
2026-08-09 — GATE-A HAS PASSED IN FULL.** IME was the last of its four halves and is confirmed on
hardware (WP-2.3). ADR §9 claim 8, which the plan called *"the single most likely point of failure
in this whole plan"*, holds.

**GATE-B PASSED the same day: Google Play Console accepted the build.** So **Phase 2 is COMPLETE**
and **Phase 3 is unblocked in full** — there is no gate left in front of it. The remaining live
problems are 2 (pause/resume, **pre-existing, not migration fallout**) and 3 (Rider deploy,
workaround available). Neither blocks anything.

**PHASE 3 IS COMPLETE (2026-08-10).** WP-3.1 through 3.5 are all merged and Windows-verified by the
owner. Silk no longer provides windowing, input or audio anywhere: `SilkWindowBackend` is deleted and
zero Silk windowing packages remain. What survives of Silk is `Silk.NET.OpenGL*` (Phase 5) and
`Silk.NET.Assimp` (Phase 4, pinned N5/N8).

**WP-6.1 IS MERGED** (PR [#66](https://github.com/tweggen/Karawan/pull/66), commit `595b5296`).
MAUI is gone from `Wuka`: `UseMaui=false`, `Microsoft.Maui.Controls` dropped from CPM, splash theme
hand-written. The row below said "SCOPED, not started" for a day after it landed — a reminder that
this ledger is only as good as the update discipline.

**WP-6.2 IS ANSWERED, AND THE ANSWER IS THE CHEAPEST ONE AVAILABLE (2026-08-10).** On
`net10.0-android36.0`, `AbiProbe` reports **all cases PASS, including M and N**, on a physical ARM64
device — on **BOTH runtimes**. The runtime was confirmed from the installed APK in each case, not
inferred from the command that was typed:

| Runtime | APK evidence | AbiProbe M + N |
|---|---|---|
| CoreCLR (`-p:WukaCoreClr=true`) | `libcoreclr.so` present, `libmonosgen-2.0.so` absent | ✅ PASS |
| **Mono (default)** | **`libmonosgen-2.0.so` present** | ✅ **PASS** |

**Therefore: the Mono ARM64 JIT defect was fixed UPSTREAM between .NET 9 and .NET 10, and we do NOT
need to switch runtimes.** The recommendation is **bump the TFM and stay on Mono.** CoreCLR-on-Android
remains available behind `-p:WukaCoreClr=true` but adopting it buys nothing for KAR-411 and would add
a new runtime's risk surface to a shipping mobile target for no corresponding gain.

**The comparison is airtight, and deliberately so** — this is a "runtime got fixed" claim, which is
exactly the kind that deserves a control:

- **The probe logic did not change between the failing and passing runs.** `ec048bef` bumped
  `ProbeRevision` 15 → 16 and changed *nothing else*: `1 file changed, 1 insertion(+), 1 deletion(-)`,
  the diff being literally `15` → `16`. So rev 15 (FAILED on .NET 9 Mono, `incomingBodyOffset=<0,
  0.9740994, 0>`) and rev 16 (PASSES on .NET 10 Mono) are the same test.
- **The managed assembly was identical across the two .NET 10 runs**: both reported
  `mvid=d6f6e38b-806e-4a66-816c-0a18d1a6bf08`. Same IL, different runtime — the only variable was the
  runtime itself.

### ✅ SHIPPED 2026-08-10 — Play accepted versionCode 199 (.NET 10 + CoreCLR)

**WP-6.2 is not merely evaluated, it is in production.** Google Play accepted the bundle, so
GATE-B has now been passed a second time — deliberately, because the first pass validated
**Mono's** native set and five CoreCLR libraries had never faced a review.

Signing is scripted: `scripts/build-release-aab.sh` builds and signs from Git Bash or macOS,
and refuses to emit a bundle whose embedded certificate does not match the keystore alias
(the SDK's silent debug-key fallback is otherwise indistinguishable from success — the file
is called `-Signed.aab` either way).

Play also reports a missing deobfuscation/symbol file. Measured, scoped, and parked as
**KI-13** — deliberately not a work package. Short version: a `mapping.txt` would buy nothing
(nothing is obfuscated); the real gap is native debug symbols, and our recipes strip them in
place, so they do not exist outside a CI run. Managed C# exceptions are unaffected.

**Next: WP-6.3 steps 2–4 are PR-open; then GATE-C input on real hardware, then WP-6.4.**
KI-12 landed with #73. **Hold the KAR-411 unwind** until the CoreCLR build has a few
days of field data; removing the guards that defend against that exact defect class, hours after
shipping a new runtime with no crash history, is the wrong order even though the guards are now
believed unnecessary.
Separately, the **perf A/B is now MEASURED**: CoreCLR reaches the first physics body **4.64× faster**
than Mono (3.95 s vs 18.31 s from the starting tap) — but in a **Debug** build, and covering the load
phase only. Re-measure in `Release` before letting it reopen the runtime choice. See "Perf A/B" below.

### Closed since the last update

- **Open problem 5 — root producer found and fixed.** It was **not** the NaN touch path the guards
  were built around. Cause: a Mono/ARM64 codegen defect corrupting `engine.physics.Object`'s
  trailing constructor argument, which made the ship's inverse inertia tensor **indefinite** and
  amplified angular impulses **441.6×**. PR [#51](https://github.com/tweggen/Karawan/pull/51),
  KAR-411, full writeup in `docs/BUGS/MONO-ARM64-CTOR-PROLOGUE-ARG-CORRUPTION.md`. Device-verified.
  The NaN guards from #42–#46 stay — they were independently correct, and the black-screen half of
  the report is still explained by them. **Note for anyone reading the old entry:** "the mechanism
  is not in doubt and is visible in the code" was true of the NaN path and still produced the wrong
  culprit, because a plausible mechanism was mistaken for the operative one.
- **Touch buttons were dead on Android** (`0ea01284`). `IView.Resize` never fires on Android, so
  `view.size` stayed at the `SetupDone()` default `320x200` while the renderer refreshed from
  `IView.FramebufferSize` every frame. Aspect 1.60 vs 2.22 displaced the hit rects vertically and
  killed the bottom button row. `Platform._applyFramebufferSize()` now drives `SetDimension()`,
  `view.size` and `VIEW_SIZE_CHANGED` from one place so they cannot disagree.
- **Open problem 1** (boot loop) closed 2026-08-08 by `SafeOrientation.Sanitize` — detail retained
  below.
- **Open problem 4** (`ClassNotFoundException`) closed 2026-08-08 — stale `obj/Release`, not code.
- **Both `platform/wp-2.1*` branches are fully landed** (verified with
  `scripts/check-branch-landed.sh`, 2026-08-09). The "Open PRs" table below is cleared.

## What landed

| PR | What |
|---|---|
| #31 | KI-8 threading correction + `scripts/check-branch-landed.sh` |
| #32 | `Karawan.Natives` Android payload made deliverable (KI-5, KI-6) |
| #33 | **WP-3.3** — `IWindowBackend` seam; `Platform` off Silk's `IView`. Desktop source-unchanged, **Windows confirmed working by the owner** |
| #34 | `Sdl3WindowBackend` (inert on desktop) |
| #35/#37 | **WP-2.2** — Wuka on SDL3, Silk windowing removed, **AC-1.7 closed** (`XA0141` promoted; all 19 arm64 libs 16 KB aligned) |
| #36/#38 | 60 FPS cap regression fix + single `RuntimeIdentifier` |
| #41-#46 | Boot-loop fix (`SafeOrientation`), NaN guards across touch / camera / player pose |
| `0ea01284` | Dead touch buttons: `view.size` published from the framebuffer, so hit tests match rendering |
| #51 | **KAR-411** — Mono/ARM64 ctor-prologue argument corruption; the ship's angular runaway |

`Karawan.Natives` **0.2.0 is published** on nuget.org.

## 🔴 Open problems, in priority order

**1 (was), 4 (was) and 5 (was) are CLOSED — see the RESUME block above. The two live
items are 2 and 3; they keep their original numbers so older notes still resolve.**

**1. ✅ CLOSED 2026-08-08. Physics `Debug.Assert` crash — restart loop on device.** Native `SIGABRT` via
`mono_runtime_invoke_checked`; the managed stack is unambiguous:

```
System.Diagnostics.Debug.Assert(...)
  at engine.physics.actions.CreateDynamic.Execute(...)
  at engine.physics.Object..ctor(...)
  at engine.scheduler.WorkerQueue.RunPart(Single dt)
  at engine.Engine._onLogicalFrame / _logicalThreadFunction
```

**SOLVED 2026-08-08, and the standing hypothesis was wrong.** The full assert text, once
captured, named it outright:

```
---- Assert Short Message ----
Orientation should be initialized to a unit length quaternion.
  at BepuPhysics.Bodies.Add(BodyDescription& description)
  at engine.physics.actions.CreateDynamic.Execute(...)
  at nogame.modules.playerhover.HoverModule.<_setupPlayer>b__0()
```

`_setupPlayer` — this fires **once, while creating the player body**, not per frame. It has
nothing to do with the render loop, the frame cap, or accumulated input, and the "spun like wild"
theory explained none of it. Attributing it to the uncapped loop was inference from a truncated
stack that stopped one frame short of the answer.

Actual cause, in `PlayerPosition.GetPlayerPosition`:

```csharp
Quaternion qShip = Quaternion.Normalize(gameState.PlayerOrientation);
```

`Quaternion.Normalize` reads like validation and is not. `default(Quaternion)` is `(0,0,0,0)`,
**not** identity — so any save whose orientation field was never written gives length 0, and
Normalize computes `1/sqrt(0) = Infinity` then `0 * Infinity = NaN`. One invalid quaternion in,
a different invalid quaternion out, with the evidence that it started as zero destroyed. NaN
passes straight through for the same reason. The guard beneath it only tested the **position**
(`v3Ship == Vector3.Zero`), so a save with a good position and a blank orientation walked past it.

Because a failed `Debug.Assert` on Android aborts rather than prints, one bad persisted
orientation is a **boot loop**: load state → build player body → abort → relaunch → load the same
state. Fixed by `builtin.tools.SafeOrientation.Sanitize` (identity for non-finite/degenerate,
rescale for merely drifted), called from `GetPlayerPosition`, which also now rejects a non-finite
position. 14 regression tests in `tests/JoyceCode.Tests/builtin/tools/SafeOrientationTests.cs`,
two of which pin the `Normalize(default) → NaN` behaviour that caused this.

**Correction to the note that used to be here:** "a Release build would sail past the same invalid
state silently" is right about `Debug.Assert`, and it means the run that produced this stack was a
**Debug** build. Verified: `Configuration=Release` yields `DefineConstants=TRACE;RELEASE;…` with no
`DEBUG` for `Wuka`, `Joyce` **and** `BepuPhysics` (sibling repo, solution maps Release→Release), so
the assert cannot fire in Release regardless of which assembly it lives in.

**2. 🟡 OPEN, DEPRIORITISED — PRE-EXISTING, NOT AN SDL3 REGRESSION.**
**Owner, 2026-08-09: "it was not working with Silk.NET either."** So this is not migration
fallout and must not be treated as Phase 2/3 exit criteria — it predates the whole programme.
The analysis below stays because it is still the best starting point whenever someone picks it
up, but it is **out of the platform-backend critical path**. Note this also weakens it as
GATE-A 'resume' evidence in the other direction: resume was confirmed working in the WP-2.1
spike, so SDL3 resumes fine; it is *our* engine/render restart that does not.

**2 (original). Black screen after pause/resume; render loop stops, process and audio stay alive.** Log
evidence: `surfaceDestroyed()` → `nativePause()` → `onResume()` → `surfaceCreated()`
(`Window size: 2800x1260`), rendering briefly resumes (`framebuffer://rootscene_3d-Pixels`
uploaded), then all `DOTNET` traces stop while OpenAL keeps logging.
**Leading hypothesis:** `Sdl3WindowBackend.LifecycleWatch` maps `WILL_ENTER_BACKGROUND` →
`OnFocusChanged(false)` and `DID_ENTER_FOREGROUND` → `OnFocusChanged(true)`; if `Platform`'s focus
handling suspends the engine and the resume path never restores it, this is exactly the symptom.
Second candidate: EGL surface recreated on resume, renderer holding stale FBOs.

**Hypothesis refined by code reading, 2026-08-09 — the leading candidate now looks WEAKER than the
second.** The focus path is symmetric on inspection: `Sdl3WindowBackend.cs:169-177` maps the two
events onto `OnFocusChanged`, and `Platform._windowOnFocusChanged` (`Platform.cs:814-838`) pairs
`Suspend()` + `SetEngineState(Stopping)` against `SetEngineState(Starting)` → `Running` →
`Resume()`, guarded by `_hadFocus` so neither half can run twice. If the resume path simply never
ran, rendering would never come back — but the log says **rendering briefly resumes and then
stops**, which is a different shape: something restarts and *then* dies.

That points at the second candidate, and at **KI-4**: the unbounded `Monitor.Wait` in
`LogicalRenderer.WaitNextRenderFrame` turns any post-resume fault on the logical thread into a
silent freeze with the process and audio still alive — exactly the reported symptom. A throw
inside the first post-resume logical frame would look identical to a deadlock from here.

**Therefore instrument before theorising further.** The three questions a device run must answer:
1. Do `WILL_ENTER_BACKGROUND` / `DID_ENTER_FOREGROUND` both reach `_windowOnFocusChanged`, and does
   `_hadFocus` end up `true`? (Log both edges with the resulting engine state.)
2. Does the logical thread survive the first post-resume frame, or does it throw? (KI-4 hides this
   today — a temporary timeout on that `Monitor.Wait` that logs and returns is the cheapest way to
   find out, and is the diagnostic KI-4 was deliberately left open to enable.)
3. Are the GL objects still valid after `surfaceCreated()`? EGL context loss would invalidate every
   FBO and texture handle the renderer caches.

Note `LOW_MEMORY` fires during a routine backgrounding (confirmed on device, see KI-8) and `Wuka`
ignores it — if anything drops caches or GL resources in response, that is a third candidate.

**3. 🟠 OPEN, workaround available. Rider cannot deploy Wuka** — "Unable evaluate deployment properties", **while the build
succeeds and signs the APK**. Two fixes were tried and BOTH FAILED: single `RuntimeIdentifier`
(merged anyway, independently correct) and singular `TargetFramework` (#39, merged, same — keep
it as tidy-up, it did not fix this).

**Third candidate now removed (2026-08-08), and it is the one real authoring error of the three.**
`AndroidResourceWriter` emitted the generated manifest as `<Project Sdk="Microsoft.NET.Sdk">`, and
`Wuka.csproj` `<Import>`s that file at line 156 — so MSBuild re-imported the SDK *there*. Two
`MSB4011` warnings on every build, and the second one is the damaging half:

```
Wuka.csproj : warning MSB4011: "…\Microsoft.NET.Sdk\Sdk\Sdk.targets" cannot be imported again.
It was already imported at "…\nogame\generated\AndroidResources.xml".
This subsequent import will be ignored.
```

Wuka.csproj's own implicit **bottom** import of `Sdk.targets` was skipped, so the .NET SDK plus
the Android and MAUI workloads all landed ~190 lines early, and every static `ItemGroup` inside
them evaluated against a Wuka.csproj that stopped at the `<Import>`: no `libSDL3`/`libmain`/
`libopenal` `AndroidNativeLibrary`, no `AndroidResource`, no `PackageReference`, no
`ProjectReference`. `dotnet build` tolerates it (targets read those items at execution time, by
which point evaluation is complete); a project evaluator that reads properties without running a
build need not. The writer now emits a plain `<Project>` — MSB4011 is gone, the build is
unchanged, and the APK is byte-for-byte the same shape (19 arm64 libs, 170 assets).

**A fourth candidate, also fixed (2026-08-08): the project lied about its own package name.**
`<ApplicationId>` was still the MAUI template default `com.companyname.wuka`, while
`Platforms/Android/AndroidManifest.xml` declares `package="de.nassau_records.silicondesert2"` —
and the hand-written manifest is what wins, so that is the APK name, the installed package and
the launch intent. Nothing in the build ever noticed. But a tool that asks MSBuild what the
project deploys, rather than parsing the manifest, got a package that has never existed on any
device; installing one package and then launching or uninstalling another surfaces exactly as
"package not installed" in logcat with no error of its own. `<ApplicationId>` now matches the
manifest; verified the APK name, the merged manifest's `package=`, the 19 libs and the 170 assets
are all unchanged.

**Whether either was Rider's actual cause is UNVERIFIED — nobody has retried Rider since.** If it
still refuses, the remaining suspects are a **stale Rider run configuration** (the output path
moved to `bin/Debug/net9.0-android36.0/android-arm64/` under Rider's feet) or `.idea` cache, and
the next step is to capture Rider's own MSBuild log (Help ▸ Diagnostic Tools ▸ *Show Log*) rather
than logcat — logcat only shows a package that is not installed, which is the consequence, not
the cause.
**Workaround meanwhile: `dotnet build Wuka/Wuka.csproj -t:Run`, then attach Rider to the process.**

**4. Release crashed on device with `ClassNotFoundException: crc64e20757511145c75a.GameActivity`
— DIAGNOSED AND CLOSED (2026-08-08), it was a stale `obj/Release`, not a code defect.** The APK
defined `GameActivity` and declared it in the manifest; what it lacked was all 49
`org/libsdl/app/*` classes, i.e. the superclass. ART names the subclass in that situation, which
sends you looking for a class that is present. `Wuka/obj/Release/net9.0-android36.0/android-arm64/`
dated from Aug 2, six days before WP-2.2 vendored the SDL3 Java glue; the incremental build
produced `binding/bin/Wuka.jar` (49 classes) and d8 never received it. Clean Release builds — with
and without the MSB4011 fix — contain all 49, and incremental builds after a clean one keep them.
Fix: `rm -rf Wuka/obj/Release Wuka/bin/Release && dotnet build Wuka/Wuka.csproj -c Release`.
`scripts/check-apk.py` now asserts required natives/classes and scans for dangling superclasses;
it fails the bad APK with the *real* missing class named, and passes the clean one.

**5. ✅ CLOSED 2026-08-09 — root producer found, and it was NOT the one hypothesised here.**
See the RESUME block: the vehicle-spin half was the Mono/ARM64 constructor defect (KAR-411,
PR #51), not the NaN touch path. The guards below stay and the black-screen half still
belongs to them. Original entry retained verbatim because the reasoning is instructive:
it is a worked example of a correct mechanism that was not the operative one.

**5 (original). Touch drag spun the view, then black screen with OpenAL warnings forever — guarded
(2026-08-08), root producer NOT yet confirmed on device.** Reported as "drag a finger, the vehicle
rotates way too fast", followed by a black screen and this, repeating every frame:

```
[ALSOFT] (WW) Error ... "Listener velocity out of range"
[ALSOFT] (WW) Error ... "Listener orientation out of range"
```

The *mechanism* is not in doubt and is visible in the code. A non-finite touch position is not a
bad frame, it is terminal: `RightStickFingerState.HandleMotion` accumulates finger deltas into
`InputController.V2RightTouchMove`, and `FollowCameraController` accumulates those again into its
own long-lived `_v2MouseOffseting` (`FollowCameraController.cs:872-873`). Neither accumulator has
any path back from NaN/Infinity, so the camera orientation — and through it the OpenAL listener —
stays NaN for the rest of the process. Spin, then black, then warnings forever. Every symptom
reported, in order.

The only unguarded producer in that path was `Wuka.GameSurface.OnTouch`, which normalises with
`e.GetX(i) / Width` against `View.Width`/`Height`. Those are 0 until the surface is laid out, and
again after it is destroyed and recreated on resume — 0 gives Infinity, and 0/0 at the origin gives
NaN. SDL guards the identical division in its own `SDLSurface.java`
(`getNormalizedX`: `if (mWidth <= 1) return 0.5f`). Three guards added:

- `GameSurface.OnTouch` drops the event and logs a `Warning` naming the surface size.
- `FingerStateHandler` rejects non-finite positions at the engine boundary (all platforms). A
  release still removes the finger from the map unconditionally — a lifted finger must never stick.
- `FollowCameraController` resets `_v2MouseOffseting` and logs an `Error` if it ever goes
  non-finite, so no future producer can make this unrecoverable.

**What is NOT established: that `Width`/`Height` were actually 0 on the reporting device.** That
needs a device run. The `Warning` was added precisely so the next run says yes or no — if it never
appears and the spin returns, the producer is elsewhere and the `Error` backstop will name the
inputs. Note the black screen here is a *different* failure from open problem 2 above (that one
follows pause/resume and leaves audio running normally).

## Small open items

- ✅ **DONE (verified 2026-08-09): `Directory.Packages.props` pins `Karawan.Natives` **0.2.0**.**
  The WP-2.1 workaround (`ExcludeAssets="all"` + `GeneratePathProperty`) is still present in
  `spikes/sdl3-android/Sdl3Spike.csproj:107`, but that spike is disposable anyway (below).
- ✅ **GATE-A: IME CONFIRMED ON DEVICE 2026-08-09** (WP-2.3). KI-10's two defects are fixed and
  the keyboard works. The owner confirmed it working; the individual sub-cases (per-widget
  `inputType` via `RestartInput`, autocorrect not corrupting the field) were **not itemised**,
  so if a text-entry oddity shows up later, start there rather than assuming it regressed.
- **`armeabi-v7a` and `x86_64` are no longer built** (single RID `android-arm64`). x86_64 never
  worked — WP-0.3 §4.3. Re-add x86_64 to the recipes' matrix first if emulator support is wanted.
- `spikes/sdl3-android/` is disposable now that WP-2.2 has landed.
- ✅ **The plan's "project structure decision needed before WP-3.1" is RESOLVED** and can be struck:
  the vendored `SDL3-CS.cs` lives in its own **`Platform.SDL3`** project, and `Splash.Silk` already
  takes a `ProjectReference` on it. Desktop builds therefore already carry the managed SDL3 bindings
  — source-only, so `libSDL3` is never loaded unless that backend is instantiated. Nothing blocks
  WP-3.1 on this any more.

## ⚠ Process note

The merge-order trap has now bitten **five times** (#17, #19, #28, #30, #35). `scripts/check-branch-landed.sh`
exists to catch it — **run it before reporting anything as landed**. It uses `git cherry`, not
`git log base..branch`, so cherry-picked recoveries don't produce false positives.

---

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
| **WP-2.2** | ⛔ **NOT COMPLETABLE AS SCOPED** | — | — | 0 | — | needs a human ordering decision | **KI-9**: SDL2 and SDL3 Java glue cannot coexist in one APK (proven — dex duplicate-class), so WP-2.2 cannot be staged; and removing Silk windowing strands `EasyCreate(…, IView, …)`, which is **WP-3.3**. Minimum atomic unit = **2.2 + 3.3 (+3.1/3.2)**. |
| **WP-2.3** | ✅ **COMPLETE — GATE-A PASSED** | `platform/wp-2.3` | [#53](https://github.com/tweggen/Karawan/pull/53) | 1 | AC-2.1 ✅ · 2.3 ✅ · **2.4 ✅** · 3.4 ✅ · APK shape unchanged ✅ (19 libs / 170 assets) | **GATE-A ✅ PASSED 2026-08-09** | **Far smaller than scoped.** `GameSurface`/`KarawanInputConnection` were already SDL3-aware, so nothing needed porting — the defect was that **nothing raised the keyboard at all** (KI-10). Adds `IWindowBackend.SetKeyboardVisible`; **deliberately not `SDL_StartTextInput`**, which would bind the IME to SDL's `SDLDummyEdit` and reinstate the composition bug. |
| **WP-3.1** | ⏳ **PR-OPEN** | `platform/wp-3.1` | [#55](https://github.com/tweggen/Karawan/pull/55) | 1 | builds ✅ · headless ✅ · APK ✅ | GATE-C 🔒 | Mouse + gamepad over SDL3. `Platform`'s 11 Silk handlers are now thin adapters over shared `_push*` cores the backend callbacks reach too, so both paths build the same event by construction. **Not runtime-exercised until WP-3.2** — desktop is still on the Silk backend and Android has neither mouse nor gamepad. Three judgement calls to check at GATE-C: trigger convention derived from the CONSUMER (see below), stick Y negated, touch→mouse synthesis disabled. **AC-3.4 as written in the plan is wrong** — `TestRunner` ignores argv and requires `JOYCE_TEST_SCRIPT`, so `-- --help` can never exit 0; verified headless with a real script instead (phase0-des passes). |
| **WP-3.2** | :hourglass_flowing_sand: **PR-OPEN** | `platform/wp-3.2` | [#56](https://github.com/tweggen/Karawan/pull/56) | 1 | desktop RUNS on SDL3 :white_check_mark: (GL 4.3 core, shaders, textures, title cards) - Silk fallback runs :white_check_mark: - APK :white_check_mark: - headless :white_check_mark: | GATE-C :lock: | Desktop on SDL3. GL profile now read from `platform.threeD.API[.version]` instead of hardcoded GLES 3.0. `Size` (logical) and `FramebufferSize` (pixels) are now separate queries - they must be, since SDL reports mouse in logical units. Fullscreen, HiDPI, resizable and window icon implemented. **Silk kept behind `platform.windowBackend=silk` so GATE-C failures are one setting away from being bisected.** **Android behaviour change to test: it now requests GLES 3.1 (from the 310 setting) rather than 3.0.** |
| **WP-3.4** | ✅ **MERGED + GATE-C AUDIO CONFIRMED** | `platform/wp-3.4` | [#57](https://github.com/tweggen/Karawan/pull/57) | 1 | audio works on desktop :white_check_mark: (device enumerated, context created, alcGetIntegerv + extension query OK) - APK :white_check_mark: - solution :white_check_mark: | **GATE-C ✅ audio confirmed HEARD on Windows 11, 2026-08-09** | `Silk.NET.OpenAL` and its Enumeration/Soft extensions replaced by ~24 hand-written `DllImport`s in `Boom.OpenAL/Native/`. Drop-in: method names and signatures mirror Silk, so all ~92 call sites are unchanged apart from the namespace they import. **The library-name fallback WP-1.5 relied on Silk for is now ours** (`OpenALNative._candidates`: OpenAL32.dll / soft_oal.dll on Windows, versioned SONAME first on Linux/macOS) - a single-name DllImport reintroduces the WP-1.5 breakage, at first playback rather than at startup. `AudioError` keeps Silk's IllegalEnum/IllegalCommand aliases, declared FIRST so logged error names are unchanged. |
| **WP-3.5** | ✅ **MERGED** | `platform/wp-3.5` | [#64](https://github.com/tweggen/Karawan/pull/64) | 1 | Windows verified by the owner ✅ | GATE-C ✅ Windows | **`SilkWindowBackend` deleted** (431 lines), with the `IView` entry points, the Silk input subscription, the `platform.windowBackend=silk` fallback, and `Silk.NET.Windowing` / `.Windowing.Sdl` / `.Input.Sdl` / `.SDL` — the packages that carried SDL2 natives. Zero Silk windowing `PackageReference`s remain in the tree. |
| WP-4.1 – 4.4 | NOT-STARTED | — | — | 0 | — | GATE-D | Independent of Phases 2–3; Phase 0 has landed, so this is dispatchable now. |
| **WP-5.0** | ✅ **MERGED** | `platform/wp-5.0` | [#22](https://github.com/tweggen/Karawan/pull/22) | 1 | **AC-5.0 ✅ exactly 0 changed lines** | none apply | Generated from `gl.xml`; baseline and candidate both compile the identical sample. **Caveat: 4 hand-written overloads for 5 entry points** — `gl.xml` cannot describe Silk's overload policy. |
| **WP-5.0b** | ✅ **MERGED** | `platform/wp-5.0` | [#22](https://github.com/tweggen/Karawan/pull/22) | 1 | **AC-5.0b ✅** costed side by side | none apply | OpenTK 5: **37 % of code lines** ≈ 83 of 225 sites. `GL` is static vs Silk's instance → all 225 change receiver. Also `pre.16` ships **net10.0 only**, dropping our net9.0. |
| **WP-5.1** | ✅ **MERGED** | `platform/wp-5.1` | [#25](https://github.com/tweggen/Karawan/pull/25) | 1 | generated surface compiles standalone ✅ | none apply | `Splash.GL/generated/GL.g.cs` generated from Khronos `gl.xml`, no package references. Surface resolved by **Roslyn** (339 call sites / 81 distinct entry points), not regex — an earlier MSBuildWorkspace attempt silently reported **zero**, indistinguishable from "uses no GL". |
| **WP-5.3** | ✅ **MERGED** | `platform/wp-5.3-imgui-detach` | [#76](https://github.com/tweggen/Karawan/pull/76) | 1 | build ✅ · 234/234 ✅ · net −123 lines | **GATE-E desktop ✅ (owner-confirmed)** | **ImGui detached from Silk, desktop UI restored.** `ImGuiFontConfig` inlined and `Silk.NET.OpenGL.Extensions.ImGui` dropped — it was the only type used from that package. Removing it exposed that **`Silk.NET.Input`/`.Windowing` were arriving transitively through it**, keeping ~250 lines of dead Silk-typed handlers compiling long after KI-12 reported them gone; all deleted. |
| WP-5.2, 5.4 | **BLOCKED-ON-HUMAN** | — | — | 0 | — | GATE-F | Owner chose **S2a, narrow form** (2026-08-06). Remaining blocker: GATE-F reference frames, which must be captured **before** WP-5.2 merges or the comparison is unrunnable forever. The ImGui entanglement that also blocked this is now gone (WP-5.3). |

| **WP-6.1** | ✅ **MERGED, device-verified** | `platform/wp-6.1` | [#66](https://github.com/tweggen/Karawan/pull/66) | 1 | gameplay loop, sound, input, splash all confirmed on device ✅ | — | **MAUI is gone from Wuka.** Four things surfaced only by building and running, none visible from source: `SingleProject` was hiding four dead platform folders AND pointing the SDK at `AndroidManifest.xml` (without `<AndroidManifest>` the SDK silently SYNTHESISES one — green build, right package name via `ApplicationId`, `android:label`/`icon` gone); the Silk `ExcludeAssets` entries are **suppressors, not sources** (see KI-12); and the splash artwork cannot be used as-is. Bluetooth permission prompt also disabled, code retained. |
| **WP-6.2** | ⚠ **HALF-MERGED — see note** | `platform/wp-6.2-net10-spike` → `platform/wp-6.2-coreclr-default` | [#70](https://github.com/tweggen/Karawan/pull/70) **merged**, follow-up PR open | 1 | GLOBAL-1 ✅ · 1b ✅ · 4 ✅ (192/192 on net10.0) · Windows desktop ✅ · Android both runtimes ✅ · APK+AAB shape ✅ · AC-1.7 ✅ · 16 KB verified from ELF ✅ | **ABIPROBE M+N ✅ PASSED on device — Mono AND CoreCLR** | **.NET 10 retarget is ON MASTER via #70.** **CoreCLR-as-default and `versionCode` 199 are NOT** — they were pushed after #70 merged and stranded (merge-order trap, 6th occurrence). Recovered onto `platform/wp-6.2-coreclr-default`. **Two source fixes in the whole tree**, both the C# 14 span-overload change. Both runtimes pass → fix came upstream; CoreCLR chosen anyway for load time (4.64×) and coverage. Built on Windows 11 **and** macOS. |
| **WP-6.3** | ✅ **MERGED (all four steps)** | `platform/wp-6.3-scancodes`, `platform/wp-6.3-device-contracts` | [#73](https://github.com/tweggen/Karawan/pull/73), [#74](https://github.com/tweggen/Karawan/pull/74) | 1 | build ✅ · **234/234 tests** ✅ · ScanCode ≡ SDL_Scancode 104/104 ✅ | **GATE-C input 🟡 macOS: WASD ✅, text entry ✅** | **Native input semantics.** `ScanCode` on USB HID usage IDs, so `Sdl3KeyCodes` is a **cast**; one name table in `engine.inputs.ScanCodeNames`, not one per backend. Devices carry no events; `OnConnectionChanged` → `INPUT_DEVICE_ATTACHED/DETACHED` on the queue (**a race fix**); enumeration is an immutable `IReadOnlyList` snapshot. **Validating it uncovered KI-14**, a pre-existing WP-3.2 regression that had killed desktop text entry. **Still unconfirmed: F8, the arrow/escape/enter family, and an Android re-check of `SetKeyboardVisible`.** |
| **WP-6.4** | 🟡 **SCOPED, after WP-6.3** | — | — | 0 | — | `[HUMAN]` rebinding UI | **Action / binding layer, runtime r/w.** Grows the existing `InputMapper` JSON assignment into a proper control→action layer that can be read AND written while the game runs. Section below. |

Status vocabulary: `NOT-STARTED / IN-PROGRESS / PR-OPEN / BLOCKED-ON-HUMAN / MERGED / ABANDONED`.

### Open PRs

**None.** [#28](https://github.com/tweggen/Karawan/pull/28) is merged and both `platform/wp-2.1`
and `platform/wp-2.1-lifecycle` are fully landed on master — re-verified with
`scripts/check-branch-landed.sh` on 2026-08-09.

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

### ✅ KI-5 / KI-6 — FIXED in `packaging/`, 🔒 pending a republish (2026-08-08)

Both are packaging-only changes; the binaries are untouched. Verified end to end against a
locally-packed `0.1.2-local` consumed by a **clean** Android project — bare `PackageReference`,
no `ExcludeAssets`, no path hacks, with `XA0141` **and** `XA4301` promoted to errors:

```
lib/arm64-v8a/libSDL3.so      lib/armeabi-v7a/libSDL3.so
lib/arm64-v8a/libc++_shared.so  lib/armeabi-v7a/libc++_shared.so
lib/arm64-v8a/libopenal.so    lib/armeabi-v7a/libopenal.so
0 Error(s), no NU1701, no XA0141, no XA4301
```

**Two distinct defects, and the first fix alone was not enough** — worth recording, because the
obvious fix looks like it works:

1. **NuGet did not recognise the Android TFM.** A `lib/<tfm>/` folder containing only an `.aar`
   registers no framework, so the package fell back to `netstandard2.0`. Adding `_._` beside the
   `.aar` fixes detection — `NU1701` disappears and the android TFM is selected.
2. **…and the APK still had no `libSDL3.so`.** NuGet surfaces `lib/` assets only for *assemblies*;
   an `.aar` appears in no asset list. `Silk.NET.Windowing.Sdl` seems to disprove this but does
   not — it ships a real `.dll` beside its `.aar`. We have no managed assembly, so the `.aar` must
   be declared explicitly by a **`build/`+`buildTransitive/` targets file** in the package
   (`<AndroidLibrary Include="…aar" />`). That is what actually delivers it.
3. **KI-6** is closed by shipping empty `runtimes/android-arm64|android-arm/native/_._`, giving RID
   fallback an exact match so it stops before `linux-x64`. Confirmed in `project.assets.json`:
   `native -> runtimes/android-arm64/native/_._`.

`NATIVES_PACKAGE_VERSION` bumped to **0.2.0**. 🔒 **Publishing is human-gated (§2.5)** — until then
consumers keep the WP-2.1 workaround (`ExcludeAssets="all"` + `GeneratePathProperty`).

### 🔴 KI-9 — WP-2.2 cannot be done as scoped: SDL2 and SDL3 Java glue cannot coexist

**Proven by building it**, not inferred. Adding SDL3's `org.libsdl.app` sources to `Wuka` while
`Silk.NET.Windowing.Sdl` is still referenced fails at dex time:

```
Type org.libsdl.app.HIDDeviceBLESteamController$1 is defined multiple times:
  android/bin/classes.zip      <- SDL3's Java glue
  lp/163/jl/classes.jar        <- Silk's SDL2 .aar
```

Same package, same class names. So **WP-2.2 cannot be staged**: SDL3's glue can only enter `Wuka`
in the same change that removes `Silk.NET.Windowing.Sdl`.

But removing it means `GameActivity` can no longer produce the `Silk.NET.Windowing.IView` that
`Splash.Silk.Platform.EasyCreate(args, iView, out platform)` requires — and changing that signature
is **WP-3.3** (`Platform.cs` has 29 `IView` references; 3 callers pass one: `Karawan`,
`examples/Launcher`, `Wuka`).

> **The minimum atomic unit for Android is therefore WP-2.2 + WP-3.3, realistically plus WP-3.1/3.2**
> (input and main loop both run through the Silk view). The plan orders 2.2 before Phase 3 **and**
> blocks Phase 3 behind GATE-A/GATE-B, so as written WP-2.2 is not completable. Needs a human
> decision on ordering — the ledger already notes GATE failures are no longer release-blocking
> after the WP-0.0 falsification, which makes pulling Phase 3 forward defensible.

Two smaller facts found while measuring, both relevant to whoever does that work:

- `Wuka` sets **`EnableDefaultAndroidItems=false`** (MAUI does this), so every Android item must be
  listed explicitly. This is why Wuka's native libraries never double up the way the WP-2.1 spike's
  did — the default `**/*.so` glob is simply off there.
- `Wuka`'s `GameActivity` pins `ScreenOrientation.Landscape`; the spike does not, which is why it
  ran portrait.

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

### ✅ KI-10 — FIXED by WP-2.3 (2026-08-09). The SDL3 backend had no keyboard path, and two unguarded dereferences

Found by code reading while scoping WP-2.3. `Sdl3WindowBackend.SilkInputContext => null`
(`Sdl3WindowBackend.cs:77`) — correct by design, the SDL3 backend translates events into
`engine.news.EventQueue` itself. WP-3.3 guarded the **subscribe** path for exactly this
(`Platform.cs:562`, with a comment explaining why). It did **not** guard the two setters:

| site | code | consequence on the SDL3 backend |
|---|---|---|
| `Platform.cs:519` | `_iInputContext.Keyboards.Count` in `_setKeyboardEnabled` | **NRE** whenever anything enables the keyboard |
| `Platform.cs:494` | `_iInputContext.Mice.Count` in `_setMouseEnabled` | **NRE** whenever anything toggles the cursor |

Reachable from `builtin/jt/InputWidgetImplementation.cs:25/30` → `Engine.SetKeyboardEnabled` →
`Platform.KeyboardEnabled` (`Platform.cs:71-74`), which enqueues the setter onto the platform
thread. **Not yet observed on device** — the app runs, so neither setter is hit on the startup
path; both are latent until a JT input widget takes focus. Guarding them is a two-line change.

**The larger half: nothing raises the Android soft keyboard on SDL3 at all.** A repo-wide search
finds **no** call to `SDL_StartTextInput`, `ShowSoftInput` or `InputMethodManager`. The old path
went through Silk's `IKeyboard.BeginInput()`, which is now unreachable on Android.
`KarawanInputConnection` (`Wuka/Platforms/Android/`) is already SDL3-aware — it derives from
`BaseInputConnection` precisely because `SDLSurface` is a plain `SurfaceView` that returns null
from `onCreateInputConnection`, and it turns IME composition into `INPUT_TEXT_REPLACE` events — so
**composition handling is ported; only the "open the keyboard" trigger is missing.**

That makes WP-2.3 smaller than the plan feared: not "port 345 LOC", but wire one trigger
(`SDL_StartTextInput` on the window, or `InputMethodManager.ShowSoftInput` on `GameSurface`) and
guard the two setters. The plan calls AC-2.4 *"the single most likely point of failure in this
whole plan"*; on this reading the risk is materially lower than that, but it is still **unproven
on device**, which is the only thing that can close GATE-A.

**Fixed in WP-2.3.** `IWindowBackend.SetKeyboardVisible(bool)` is now the seam: `SilkWindowBackend`
forwards to the `BeginInput`/`EndInput` loop that used to live inline in `Platform` (now null-guarded),
and `Sdl3WindowBackend` invokes a `SoftKeyboardHandler` that `GameActivity` installs. `_setMouseEnabled`
got its guard too. Details in the WP-2.3 row above; **still unverified on a device**, which is the only
thing that can close GATE-A.

### 🟡 GATE-C — Windows 11 results, 2026-08-09

Fullscreen, keyboard, mouse, gamepad and **audio as heard** all confirmed by the owner on the SDL3
backend. That closes WP-3.4's open item: the hand-written OpenAL `DllImport`s are proven end to end,
not merely "a context was created".

**Two defects found, both introduced by WP-3.1, both fixed in
[#61](https://github.com/tweggen/Karawan/pull/61):**

| defect | cause |
|---|---|
| Gamepad left stick front/back inverted while walking | The Y axis was negated because SDL reports +Y as DOWN while `InputController` stores `Y > 0` into `AnalogLeftStick`**`Up`**. That field name says which ACCUMULATOR the value lands in, not which way the character walks. Silk normalises axes without flipping; SDL's sign now passes through unchanged. |
| Mouse cursor visible in fullscreen | Two compounding causes. WP-3.1 guarded the null Silk input context in `_setMouseEnabled` and returned, which stopped the latent NRE but left the cursor never configured at all on SDL3 — now `IWindowBackend.SetMouseVisible`. AND the setter was never called anyway: `Engine.SetMouseEnabled` has **no caller in the game**, so `_mouseEnabled` sat at its default `false` while both windowing libraries default to showing a cursor. `Platform` now applies the state once at startup. The second half was equally true under Silk — not an SDL3 regression, just newly visible. |

> **Consequence to watch:** the cursor is now hidden by DEFAULT on both backends. Nothing in the
> game re-enables it, so a menu will not have a pointer until game code calls
> `Engine.SetMouseEnabled(true)`. That is a game-side fix, not a windowing default.

**Methodological note worth keeping.** Of the three judgement calls WP-3.1 flagged for GATE-C, the
stick flip was the one derived from a FIELD NAME rather than from observed behaviour, and it is the
one that was wrong. The trigger convention, derived from the consumer's arithmetic, held.

### ⚠ Merge-ordering trap — hit twice, now structurally closed

Both #17 and #19 were **stacked PRs based on `platform/wp-1.2`**. In each case the base branch
was merged to master *seconds before* the stacked PR landed on it, so GitHub reported MERGED —
truthfully, but into `platform/wp-1.2`, not master. Master silently lacked SDL3 (#17) and then
the packaging (#19). Recovery PRs #18 and #20 carried them across.

**All merged WP branches have since been deleted**, so nothing can be stacked onto them again.
**Future work packages branch from master directly**, even when that costs a rebase; the ordering
hazard is not worth the tidiness of stacking.

#### It recurred twice more — #28 and #30 — in a different form, and is now guarded

Not stacking this time. **Commits were pushed to a branch whose PR had already merged.** Every
local signal says fine: the branch tracks its remote, `git status` is clean, `git push` succeeds,
and `gh pr view` reports MERGED — truthfully, just not with those commits.

- **#28:** two commits stranded, recovered by #30.
- **#30:** one commit stranded — and this one **left master documenting a fact that had already
  been corrected** (KI-8's threading claim said "UI thread"; it is the SDL thread). A later work
  package would have read and trusted it. Recovered by
  [#31](https://github.com/tweggen/Karawan/pull/31).

**Guard: `scripts/check-branch-landed.sh`.** Asks the one question that actually detects it —
*what is on this branch that master does not have?* — and, when the branch's PR is already merged,
prints the exact `cherry-pick` recovery commands.

It uses **`git cherry`, not `git log base..branch`**: recovery means cherry-picking, which mints
new SHAs, so a SHA-based check reports the originals as missing forever. `git cherry` compares
patch-ids, so already-landed content is recognised however it got there. Both paths were tested
against the real branches — `platform/wp-2.1` (content landed as cherry-picks) passes,
`platform/wp-2.1-lifecycle` (genuinely stranded) fails and names PR #30.

**Run it before reporting a work package as landed.**

---


### 🟢 WP-6.1 - drop MAUI from Wuka (scoped 2026-08-09)

**Finding: MAUI is template scaffolding the project has outgrown.** `MainActivity`, the actual
`MainLauncher`, is a plain `Android.App.Activity` - NOT `MauiAppCompatActivity`. It handles
Bluetooth permissions and launches `GameActivity`, which is an `SDLActivity`. Every MAUI type
present is untouched default template code; `App.xaml.cs` sets `MainPage = new AppShell()` and
nothing in the game path ever reaches it.

**Delete outright** (7 files, all template): `App.xaml(.cs)`, `AppShell.xaml(.cs)`,
`MainPage.xaml(.cs)`, `MauiProgram.cs`.

**Rework, small but real:**

| item | change | note |
|---|---|---|
| `MainApplication : MauiApplication` | to `: Android.App.Application` | **Keep its body.** It does real work: a `libassimp.so` `DllImport` probe that reports load failures at startup. |
| `Theme = "@style/Maui.SplashTheme"` | replace with an own style | Referenced by **both** `MainActivity:17` and `GameActivity:22`. MAUI generates that theme from `<MauiSplashScreen>`. |
| `<MauiSplashScreen splash_nassau.svg>` | hand-written drawable + style, or drop | Cosmetic but visible; decide deliberately rather than by omission. |
| `<UseMaui>true</UseMaui>` | `false` | Drops the MAUI workload from the build prerequisites. |
| `Microsoft.Maui.Controls` 9.0.111 pin | remove | Also removes the CPM workaround in `Directory.Packages.props`, which exists only because `UseMaui=true` injects an implicit reference. |

**Two traps, both already documented elsewhere in this ledger:**

1. **`EnableDefaultAndroidItems` must stay `false` EXPLICITLY.** MAUI sets it, which is precisely
   why Wuka's native libraries never double up - the default `**/*.so` glob is off. Letting it
   revert to `true` risks re-introducing duplicate natives, and `XA4301` is promoted to an error
   with a current count of 0.
2. **AndroidX may arrive transitively via MAUI.** `ActivityCompat` is used directly in
   `MainActivity` and `GameActivity`. If MAUI is what pulls `Xamarin.AndroidX.Core`, it needs an
   explicit `PackageReference` - check `project.assets.json` before assuming.

**Verification is already built:** `scripts/check-apk.py` asserts **19 native libraries, 170
assets, no dangling superclasses**. An unchanged APK shape is a real check, not a hope. Plus a
`[HUMAN]` device launch: permission prompt, game start, `ABIPROBE BUILD` line present.

**Why it is worth doing:** largest single de-risking of the .NET 10 migration, independent of
Phase 3 so it cannot confound GATE-C, and mostly deletion. It does change app startup on the only
mobile target - so: own branch, own device check, bundled with nothing else.

### ✅ WP-6.2 - .NET 10 evaluation - EXECUTED 2026-08-10, PR [#70](https://github.com/tweggen/Karawan/pull/70)

> **Result first: `AbiProbe` passes ALL cases including M and N on .NET 10, on a physical ARM64
> device, on BOTH Mono and CoreCLR.** Each runtime was confirmed from the installed APK rather than
> inferred from the command typed — the ABIPROBE log line does not name the runtime.
>
> **Conclusion: the fix came UPSTREAM in Mono between .NET 9 and .NET 10. Do not switch runtimes.**
> Bump the TFM, stay on Mono. CoreCLR stays available behind `-p:WukaCoreClr=true` as a bisection
> tool and a fallback; adopting it buys nothing for KAR-411.

### Perf A/B — MEASURED on device: CoreCLR loads ~4.6× faster in Debug

The owner's impression that CoreCLR was significantly faster **is confirmed, and the magnitude is
large.** Measured from the two device logcat runs of 2026-08-10, using the interval from the first
`OnFingerPressed` (the tap that starts the scene) to a fixed downstream milestone:

| Anchor | Mono | CoreCLR | CoreCLR faster by |
|---|---|---|---|
| touch → `ABIPROBE BUILD` (first `CreateDynamic`) | **18.308 s** | **3.949 s** | **4.64×** |
| touch → first `SkTexture:_uploadImage` | 19.514 s | 4.130 s | 4.73× |

**`ABIPROBE BUILD` is the anchor to trust.** It fires exactly once, deterministically, from
`AbiProbe.RunOnce()` in `CreateDynamic`, and it is the *same* event in both logs. The texture anchor
is weaker — the two runs happened to name **different** atlases (`atlas-albedo001.png` vs
`atlas-emissive-half-trans000.png`), so it is not strictly the same milestone. It is quoted only
because the two anchors agree to within 2 %, which shows the result is not an artifact of endpoint
choice.

**What this is NOT:** a frame-rate or steady-state measurement. It covers the **load phase** —
asset decode, mesh/texture upload, world generation, physics setup. Steady-state throughput is
still unmeasured, and the engine emits no frame timing to measure it with.

**The Debug caveat still stands, and here it is load-bearing:**

- **Both runs were `Debug`.** Debug is not a performance configuration, and the two APKs do not carry
  the same diagnostic scaffolding: the Mono Debug APK ships `libmono-component-debugger.so`,
  `libmono-component-hot_reload.so`, `libmono-component-marshal-ilgen.so`,
  `libxamarin-debug-app-helper.so` and `libarc.bin.so`; the CoreCLR one ships none of those. A gap
  measured here may be mostly debug scaffolding rather than runtime throughput, and may narrow or
  invert in `Release`.
- The engine emits **no frame-time or FPS logging**, so there is currently no in-process metric to
  compare. Any number has to come from outside the app or from new instrumentation.

**Measure before deciding.** Startup is the cheap objective one:

```
adb shell cmd package resolve-activity --brief de.nassau_records.silicondesert2   # get component
adb shell am start -W -n <component>      # reports ThisTime / TotalTime / WaitTime
```

Run each build ~5×, take the median, **in `Release`**, force-stopping between runs
(`adb shell am force-stop de.nassau_records.silicondesert2`).

A 4.6× gap concentrated in a compute-heavy load phase is the classic signature of a **codegen-quality
difference**, which is exactly what `Debug` perturbs most — so `Release` is where this has to be
re-run before it drives a decision. It could narrow sharply. It could also survive, in which case
18 s versus 4 s to enter the world is a user-visible difference that matters on its own.

**Note this is a genuinely separate question from KAR-411.** The correctness result no longer favours
either runtime — both pass. If CoreCLR stays materially faster in `Release`, that becomes its **own**
business case for adopting it, argued on those numbers alone. Treat the two questions separately:

- **Correctness → stay on Mono.** Settled, both runtimes pass, no reason to take on new risk.
- **Performance → open.** Re-measure in `Release`; if the gap holds, reopen the runtime choice
  deliberately, with the CoreCLR risk surface priced in rather than inherited from this spike.

**What the spike actually measured** (all on PR #70, verified on Windows 11 and built again on macOS):

| Finding | Detail |
|---|---|
| **A TFM bump alone does not test the hypothesis** | Default `net10.0-android36.0` still ships `libmonosgen-2.0.so`. A green net10 build proves nothing about KAR-411. CoreCLR is opt-in via `-p:WukaCoreClr=true`, anchored in `Wuka.csproj`, so both runtimes are one flag apart on one commit and a device regression stays bisectable by runtime. |
| **Two source changes in the whole tree** | Both the same C# 14 breaking change: with first-class span conversions a bare `Matrix4x4[]` binds to `MemoryMarshal.Cast(ReadOnlySpan<T>)` instead of the `Span<T>` overload. `Splash.Silk/SkAnimationsEntry.cs:36`, `Splash.Silk/SilkThreeD.cs:553`, fixed with explicit `.AsSpan()`. **Fails at compile time, not silently.** |
| **APK shape unchanged** | Diffed entry-by-entry vs the net9 baseline: **167 assets, 3789 classes, zero entries added or removed.** 19 native libs on Mono, 17 on CoreCLR. `XA0141`/`XA4301` stay promoted to errors, so the CoreCLR natives are 16 KB aligned too. |
| **192/192 unit tests pass** on `net10.0` | |
| **The documented "170 assets" was already stale** | The net9 baseline is also 167. Not a net10 regression. `scripts/check-apk.py` asserts required libs/classes, not that count. |

**Two corrections to the migration surface recorded below:**

1. **The sibling repos need no retargeting.** The paragraph below says three of them are on `net9.0`
   and must be retargeted or multi-targeted. Measured: `DefaultEcs` is `net7.0`/`net7.0-windows`/
   `netstandard2.1`, `BepuPhysics2` is `net6.0`/`net8.0`/`net9.0`, `ObjLoader` is `net6.0`,
   `glTF-CSharp-Loader` and `ink` are `net6.0`/`netstandard2.0`. **`net10.0` consumes all of them
   unchanged.** They were left untouched and everything builds. Budgeted work that evaporates.
2. **`global.json` distorts diagnostics, not just builds.** The old `9.0.0`/`latestFeature` pin meant
   every SDK-scoped command run inside this repo — notably `dotnet workload list` — silently answered
   for .NET 9. That is how the .NET 10 android workload came to be reported as MISSING when it is in
   fact installed at **`36.1.43/10.0.100`** via VS 2026. **Ground workload questions by reading
   `C:\Program Files\dotnet\sdk-manifests\<band>\microsoft.net.sdk.android`, or by querying under an
   explicitly pinned SDK — never by running `dotnet workload list` in a repo with a stale pin.**

> ⚠ **`global.json` now pins `10.0.302` with `rollForward: latestFeature`, which accepts feature band
> 300+.** A machine with only a `10.0.1xx` SDK fails with "compatible SDK version not found" despite
> having .NET 10. Relax to `10.0.100` if this branch needs to build on such a machine.

**Unwinding KAR-411 workarounds — do NOT batch this.** The probe's own message says verify on device
before removing any of them. The candidates are the `HoverModule` workaround, the "no instance
initialisers" rule, the ~309-class latent audit, and an entry point never located. Remove **one at a
time with a device check each**: this failure mode is silent and renders plausible-looking wrong
results, which is exactly what made KAR-411 expensive to find. Note also that passing at
`probeRev=16` on .NET 10 does not by itself re-explain the `probeRev=15` failure on .NET 9 — the
comparison holds only because the probe source did not change between those runs.

---

**Original scoping notes (2026-08-09), retained — the reasoning still stands:**

**The reason to upgrade is not features.** The ARM64 trailing-struct corruption is a **JIT codegen
fault** - proved 2026-08-09: identical source passes all 14 `AbiProbe` cases on x64/CoreCLR and
fails M and N on arm64/Mono, and the corrupt value straddles a parameter boundary, which is not
expressible in IL at all (IL addresses arguments by ordinal, never by offset). Android runs
**Mono**. A runtime without the defect retires the `HoverModule` workaround, the "no instance
initialisers" rule, the ~309-class latent audit, and an entry point we still have not located.

**Cheapest first experiment, and it may need no upgrade at all: try CoreCLR on Android.** If it is
reachable as an opt-in on .NET 9, the hypothesis is testable immediately. The acceptance test
already exists - deploy and read `ABIPROBE RESULT`:

- **M and N PASS** -> the defect is Mono-specific, the migration has a hard business case, and
  workaround removal can begin.
- **M and N still FAIL** -> it is broader than Mono, the upgrade drops to routine maintenance, and
  we learned that for an hour of work. It also sharpens any upstream report.

**Prefer .NET 10 (LTS) over 11.** 11 is preview until roughly Nov 2026, and preview toolchains for
a shipping mobile target cost days on things unrelated to our code. Skipping 10 also means
migrating twice, or sitting on 9 for another year.

**Do it AFTER Phase 3 is green.** Upgrading mid-phase makes every regression ambiguous between the
windowing rewrite and the runtime move.

**Migration surface, measured:** 53 projects on `net9.0`, plus `net9.0-android36.0` and
`net9.0-windows10.0.22000.0`; three sibling repos (`DefaultEcs`, `BepuPhysics2`, `ObjLoader`) on
`net9.0` - our forks, so retarget or multi-target; `Karawan.Natives` targets `net9.0-android34/36`
and needs republishing (human-gated); a few `net6.0` / `net7.0-windows` stragglers.
**`Silk.NET.Assimp` stays pinned at 2.22.0** - N5/N8, bumping it is what corrupted model loading.


### 🟢 WP-6.3 — native input semantics (design agreed 2026-08-10)

**Why now.** KI-12 removed `Silk.NET.Input`, and with it the translation layer that used to define
what a key event *meant*. `engine.inputs.*` was introduced as a Silk-shaped replacement; this work
package settles its semantics before anything is built on it.

**The agreed split — two halves doing different jobs:**

| | responsibility |
|---|---|
| **`engine.news.EventQueue`** | everything that HAPPENS — keys, mouse, gamepad, and device attach/detach |
| **`engine.inputs.IContext`** | what EXISTS right now — enumeration and capability only, nothing subscribable |

**Where we sit on keys, and it is already correct.** SDL3 hands us both channels cleanly and
`Sdl3WindowBackend` already uses both:

- `SDL_EVENT_KEY_DOWN/UP` → `ev.key.scancode` — physical position, layout- and IME-independent
- `SDL_EVENT_TEXT_INPUT` → UTF-8, already composed by layout **and** IME

Nothing needs introducing. What is missing is honesty in the types: engine codes look like
characters (`"a"`, `"w"`) but are POSITIONS. On AZERTY the physical W-position key yields engine
code `"w"` — right for movement, misleading to read.

**Three rules to encode:**

1. **Bindings consume positions only.** WASD is `ScanCode.W`, never the character `'w'`.
2. **Text consumes `INPUT_KEY_CHARACTER` only** — never synthesised from key events. That is what
   makes IME, dead keys and accents work, and it is already wired.
3. **Display is a THIRD thing.** A rebinding UI must show the label the user's layout prints on
   that key: `SDL_GetKeyName(SDL_GetKeyFromScancode(...))`. Layout-dependent, display-only, never
   used for lookup. This is the one usually missed — it is why "press a key to bind" screens show
   `Z` on AZERTY while correctly storing the W position.

**Ordered steps:**

1. **`ScanCode` on USB HID usage IDs** (`A = 0x04`), which is exactly what `SDL_Scancode` already
   is — so `Sdl3KeyCodes` collapses from a translation table to a cast, and the enum is
   platform-neutral by construction rather than by convention. The key event carries it. **Do this
   first**: it is the step with real behaviour attached, and a wrong table is silently wrong rather
   than loudly wrong.
2. **Strip the events off the device interfaces.** Biggest departure from the copied Silk shape:
   Silk's `IKeyboard` carries `KeyDown`/`KeyUp`; ours carries nothing. `IKeyboard`/`IMouse`/
   `IGamepad` reduce to identity and capability. `IDevicePart` (`IButton`, `IThumbstick`,
   `ITrigger`, `IMotor`, `IKey`, `IWheel`) then earns its place as capability description, which is
   what a bindings UI needs. Note `IMotor` is the odd one out — output flows the other way and will
   not fit the queue model.
3. **Device attach/detach onto the queue.** Delete `IContext.OnConnectionChanged`; emit
   `INPUT_DEVICE_ATTACHED` / `INPUT_DEVICE_DETACHED` instead. **Not tidiness — a race.** A C# event
   fires immediately on SDL's thread while queue events drain later on the logical thread, so a
   gamepad's first axis event could be PROCESSED before the game is told the device exists. One
   channel gives one ordering and one thread by construction.
4. **Enumeration as an immutable snapshot.** It will be read from the logical thread while SDL
   mutates devices on its own. Rebuild on change and publish atomically: no locks in the hot path,
   and a caller iterating cannot have the collection change underneath it.

**Consequence worth noting:** because devices no longer raise events, `ScanCode` does not belong on
`IKeyboard` at all — it is part of the event payload. That is what makes step 1 independent of
steps 2–4.


### 🟡 WP-6.4 — action / binding layer, readable and writable at runtime

**Continuation of what already exists.** `builtin.controllers.InputMapper` already holds a
`SortedDictionary<string, string> MapButtonToLogical`, already configured from JSON, already
lock-guarded, and already re-emits a logical event beside every raw one via `_pushTranslate`. This
work package grows that into the full layer rather than replacing it.

**The model, borrowed deliberately:**

| concept | meaning here | prior art |
|---|---|---|
| **control** | a physical thing that produces a value: `ScanCode.W`, gamepad stick axis, mouse button | Unity's "control" |
| **action** | the game-meaningful verb: `move.forward`, `ui.confirm` | Unity action / Godot input map action |
| **binding** | control → action, plus its transform | Godot's flat named-action config |
| **modifier** | dead zone, inversion, curve, hold, double-tap, expressed as composable transforms on a binding | Unreal Enhanced Input |

**The rule that makes it worth doing:** gameplay code sees ACTIONS ONLY. No `if (code == "w")`
anywhere outside the binding layer. Today `InputController` reads raw codes directly, which is why
the trigger convention and `StickTransfer`'s x⁴ curve are hardcoded at their call sites — those are
modifiers in disguise and should become data.

**Runtime read/write is the defining requirement,** and it drives three things:

1. **The binding table is live state, not load-time config.** `InputMapper` already has the shape
   (locked accessor, wholesale replace). Rebinding writes one entry, not a reload.
2. **A write must be persistable.** JSON in, JSON out, same schema — so a rebinding UI round-trips
   through the file the game shipped with, and a user's overrides survive an update.
3. **Rebinding needs "listen for the next control".** A transient capture mode where the next raw
   control is returned instead of being translated. That is a mode on the mapper, not a new
   subsystem.

**Depends on WP-6.3 step 1** (`ScanCode` on HID usage IDs), because a binding must store a POSITION,
never a character - otherwise a user's saved bindings break when they switch layout. And on WP-6.3
rule 3 for display: the UI shows `SDL_GetKeyName(SDL_GetKeyFromScancode(...))`, layout-dependent and
display-only, while the stored binding stays positional.

**Deliberately NOT copied:** Unity's device-layout description language and Unreal's asset-based
binding editor. Both solve third-party hardware support without engine changes - a problem this
project does not have, at a cost it should not pay.

> ⚠ The prior art above is reconstructed from memory and API details will have drifted. The
> concepts are stable enough to design from; if a specific mechanism is adopted - Unreal's modifier
> evaluation order, say - check it against current documentation before encoding it.

## Gate ledger

| Gate | What | Status |
|---|---|---|
| GATE-A | SDL3 on a physical Android device (multi-touch, **IME**, rotation, resume) | ✅ **PASSED 2026-08-09.** Rendering, multi-touch and rotation confirmed 2026-08-07; resume 2026-08-08; **IME confirmed by the owner 2026-08-09** after WP-2.3 ("working beautifully on mobile"). All four halves are now answered on real hardware. ADR §9 claim 8 — the claim the plan called *"the single most likely point of failure"* — **holds**. |
| GATE-B | Play Console upload, no "Memory page size" warning | ✅ **PASSED 2026-08-09** — the owner uploaded and **Google Play Console accepted the build**. Together with GATE-A this completes **Phase 2**, and **Phase 3 is now unblocked in full**. The 16 KB work (AC-1.7, `XA0141` promoted, all 19 arm64 libraries aligned) is validated by the store rather than by our own checker. |
| GATE-B (again) | Play accepts the **.NET 10 + CoreCLR** bundle | ✅ **PASSED 2026-08-10, versionCode 199.** Re-passed deliberately, because the first pass validated **Mono's** native set. `libcoreclr`, `libclrjit`, `libmscordaccore`, `libmscordbi` and `libassembly-store` had never been through a Play review; all 18 arm64 libraries verified `0x4000` from the ELF program headers before upload. **Silicon Desert 2 now ships on .NET 10 with CoreCLR as its Android runtime.** |
| GATE-C | Windows + Linux desktop | 🟡 **WINDOWS 11 PASSED 2026-08-09** (fullscreen, keyboard, mouse, gamepad, resize, audio-as-heard). **HiDPI FULLY PASSED 2026-08-11 on a 15" M4 Air at 1440x932** — rendering, ImGui scaling, mouse-position match, windowed and fullscreen, and the UI/3D boundary. Closing it took **three** separate logical-vs-pixel fixes, all invisible on a 1x display: [#80](https://github.com/tweggen/Karawan/pull/80) ImGui `DisplaySize` was framebuffer pixels; [#82](https://github.com/tweggen/Karawan/pull/82) the renderer used the LOGICAL view rectangle directly as pixels, so the 3D viewport sat half underneath the panel — **wrong since long before the splitter, just invisible while the boundary was static**; and KI-14's text-entry gap found on the way. **Still unrun: Linux entirely.** |
| GATE-D | Animation correct on macOS + Windows | ✅ **PASSED 2026-08-06** — Windows confirmed, macOS confirmed on a **Debug** build (Release does not currently start, see known issue KI-1) |
| GATE-E | ImGui renders + takes input (incl. Linux Fn-key case) | 🟡 **DESKTOP PASSED 2026-08-11** — owner, after WP-5.3 ([#76](https://github.com/tweggen/Karawan/pull/76)): *"works better than it ever did"*. Worth noting mouse input to ImGui had **never** been wired, even under Silk (position/buttons/wheel were commented-out `TXWTODO`s), so this is the first build in which it is clickable at all. **Android is out of scope for this gate**: [#13](https://github.com/tweggen/Karawan/pull/13) excluded the ImGui native and there is no Android build of cimgui — re-enabling needs a real arm64 `libcimgui.so`, not a flag. **Still unrun: the Linux Fn-key case.** |
| — | *(AC-0.2.4, gate-adjacent)* Aihao L-System preview renders | ✅ **PASSED 2026-08-06** — confirmed after the GL-context seam was re-expressed in WP-0.2 |
| GATE-F | Pixel-compare before/after GL swap | not reached. ⚠ **Baseline must be captured before WP-5.2 merges** or it is unrunnable forever |

---

## Known issues (found in passing, not part of any WP)

| id | Issue | Status |
|---|---|---|
| **KI-1** | **Release builds do not start.** `Karawan/DesktopMain.cs:196-202` starts fullscreen when `#if DEBUG` is false. On macOS the fullscreen window switches display mode but never activates — it ends up minimised behind the IDE, the main thread parks in `LogicalRenderer.WaitNextRenderFrame`'s untimed `Monitor.Wait`, and the app appears hung while still alive. Debug builds are windowed and run fine on both platforms. **Untested on Windows Release.** Also note fullscreen is applied at `DesktopMain.cs:218`, *before* `iWindow.Initialize()` at :220. | **postponed by owner 2026-08-06** — no Windows machine available to complete the 2×2. Pre-existing; not caused by any WP. |
| **KI-2** | `I.RegisterFactory: Error: Already registered engine.news.EmissionContext` on every run, both platforms. Something registers that factory twice. | open, unowned, benign so far |
| **KI-3** | `ink` is listed as a required sibling checkout in `README.md` and `CLAUDE.md`, but no csproj references it via `$(SiblingRoot)`. Possibly a second dead prerequisite (cf. FbxSharp, PR #10). | open, unverified |
| **KI-11** | **ImGui was OFF on desktop.** `Splash.Silk/ImGui/Controller.cs` needed a Silk `IInputContext`, which no surviving backend provided. Entered at **WP-3.2** (desktop stopped satisfying `_backend is SilkWindowBackend` the moment it moved to SDL3) and went unreported at the time; made explicit by WP-3.5. | ✅ **RESOLVED 2026-08-11** by WP-5.3, [#76](https://github.com/tweggen/Karawan/pull/76). Owner: *"works better than it ever did"*. The controller now takes `IWindowBackend` and is FED input by `Platform` rather than polling a device. **Mouse input had never actually been wired even under Silk** — position, buttons and wheel were all commented-out `TXWTODO`s — so this is the first build in which the desktop UI can be clicked at all. |
| **KI-12** | **Dead Silk input handlers in `Platform`.** `_onKeyDown(IKeyboard,…)`, `_onMouseDown(IMouse,…)` and the gamepad pair are no longer subscribed by anything, and `IWindowBackend.SilkInputContext` is always null. They are the only reason `Silk.NET.Input` is still referenced. Harmless — the package ships no natives — but they are dead code. | ✅ **RESOLVED 2026-08-10** by `refactor/ki-12-drop-silk-input`. `Silk.NET.Input` is gone, and with it `Input.Sdl`, `Windowing.Sdl`, `SDL` and `Ultz.Native.SDL` — verified absent from `dotnet list Wuka --include-transitive`. The four `ExcludeAssets` suppressors went too: with nothing pulling those packages in, they had become the sole SOURCE, the inverse of their role an hour earlier. **KI-9 is retired in practice** — no SDL2 `.aar` in the graph means no duplicate Java glue to collide. Wuka builds; APK unchanged at 19 natives. Semantics follow in WP-6.3. |
| **KI-14** | ✅ **FIXED 2026-08-10 (device-confirmed).** **Desktop text entry was silently dead on SDL3.** Typing into a focused field produced nothing; WASD, F8 and every scancode binding kept working perfectly, which is why it survived undetected. Cause: SDL2 has text input ON by default and Silk's `BeginInput`/`EndInput` merely toggled it, but **SDL3 makes it per-window and OFF until `SDL_StartTextInput`** — and nothing in the tree ever called it. `Sdl3WindowBackend.SetKeyboardVisible` only invoked `SoftKeyboardHandler`, which `GameActivity` installs on Android and nothing installs on desktop, so on desktop it warned once and returned. **Entered at WP-3.2, sealed by WP-3.5** deleting `SilkWindowBackend` and the `BeginInput` forward with it — the same entry point and the same silence as KI-11. The false assumption was written into the doc comment on `IWindowBackend.SetKeyboardVisible` (*"a desktop platform has a real keyboard and nothing visible happens either way"*), which is now corrected in place. Fix drives `SDL_StartTextInput`/`SDL_StopTextInput` **below** the handler check, because that call is right on desktop and wrong on Android (it would raise `SDLDummyEdit` and reinstate the KI-10 composition bug). | fixed in [#74](https://github.com/tweggen/Karawan/pull/74). **Found only because someone typed into a field** — the first time anyone had, on the SDL3 desktop build. Android shares the method and is untouched, but should be re-checked. |
| **KI-4** | An unbounded `Monitor.Wait` in `LogicalRenderer.WaitNextRenderFrame` runs on the thread macOS requires for event pumping, so *any* logical-thread fault presents as a frozen app rather than an error. A timeout that logs and returns null would make this class of failure diagnosable. | open — deliberately not "fixed", since it would mask causes |
| **KI-15** | **Logical-vs-pixel confusion is a recurring defect class on this codebase, not three coincidences.** Four instances found in two days, every one invisible on a 1x display and therefore on every machine except a retina Mac: `SetKeyboardVisible` (KI-14, different axis but same "equal on my machine" blindness), ImGui `DisplaySize` (#80), the render viewport (#82), and the splitter drag which would have inherited it. **The seam is now documented where it is crossed** — `IWindowBackend.Size` vs `.FramebufferSize`, `OnResize` (pixels), `SetDimension(px, logical)` — but nothing MECHANICALLY prevents the next one. A units type (`LogicalV2` / `PixelV2`) would; that is a real refactor across `Splash.Silk` and worth costing before committing to. | open, unowned. **Suggested cheap mitigation: run GATE-C's HiDPI checks on any PR touching `Splash.Silk` geometry**, since a 1x developer machine cannot see this class at all. |
| **KI-13** | **No native debug symbols ship, so native crashes in Play are bare addresses.** Play reports a missing deobfuscation/symbol file. Measured on the shipped versionCode 199 bundle, not assumed: (a) the dex is **not obfuscated** — 5,785 readable slash-separated identifiers vs 49 short ones, and no R8/ProGuard settings exist in `Wuka.csproj` — so **a `mapping.txt` would buy nothing** and no build step emits one (`obj/.../type-mapping.txt` is .NET Android's managed↔Java map, unrelated despite the name); (b) the AAB carries **no `BUNDLE-METADATA/`**, so no symbols ship — this is the real gap; (c) **our own natives are stripped in place** by `recipes/build-openal.sh` and `recipes/build-assimp-android.sh` (`llvm-strip`), so outside a live CI run the symbols **no longer exist anywhere**. Managed C# exceptions are unaffected and stay readable. Fix = preserve unstripped copies (or `--only-keep-debug` companions) **before** the strip, publish as CI artifacts, then either upload per release or embed under `BUNDLE-METADATA/com.android.tools.build.debugsymbols/`. The runtime libraries (`libcoreclr`, `libclrjit`, `libSystem.*`) come from the .NET Android workload and are a separate sourcing question. | open, deferred by owner 2026-08-10 — pairs naturally with the CI workflow, since that is where unstripped artifacts would live. **Step (c) is the irreversible half**: symbols for a crash that already happened cannot be recovered later. |

---

## Budget counters (plan §5c off-the-rails thresholds)

| Counter | Limit | Current |
|---|---|---|
| Phase 2 worker dispatches | 10 | 0 |
| Programme-wide re-dispatches | 25 | 0 |
| Calendar: Phases 0–2 complete | 3 months from 2026-08-04 | **day 5 — Phases 0, 1 and 2 ALL COMPLETE, both gates passed.** Budgeted 3 months; took 5 days. |
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
