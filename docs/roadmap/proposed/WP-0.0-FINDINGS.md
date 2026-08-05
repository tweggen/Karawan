# WP-0.0 — Falsification of ADR claims #6 and #7

**Status:** ✅ Complete — **claim #6 is FALSIFIED**, claim #7 is confirmed
**Date:** 2026-08-05
**Branch:** `platform/wp-0.0`
**Plan:** [`IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md`](IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md) §5 / §4
**ADR:** [`docs/ARCHITECTURE/PLATFORM_BACKEND.md`](../../ARCHITECTURE/PLATFORM_BACKEND.md) §9

Spike only. **No product code was changed.** Per plan §4 and §5c, a falsified "assumed"
claim is an escalation trigger — this document is the escalation.

---

## 1. Result summary

| Claim | ADR text | Verdict |
|---|---|---|
| **#6** | Silk's SDL2 AAR cannot be fixed locally | ❌ **FALSIFIED** — it can, in ~15 min of local compute, with tooling already on this machine |
| **#7** | SDL3's Android AAR is 16 KB-aligned | ✅ **CONFIRMED** for the ABIs the Play rule covers (arm64-v8a, x86_64 = `0x4000`) |

**What this changes (plan §4):** "Play unblocks without touching windowing. Phases 2–3 lose
their urgency and become longevity-only work the owner can schedule at leisure. Re-plan with
the human."

**What this does _not_ change:** the ADR's *longevity* argument is untouched. §4c (Silk 2.x is
maintenance-mode, 3.0 is a rewrite) is an independent claim and was not tested here. Claim #6
was load-bearing for **urgency**, not for **direction**. See §6 for what I think this means.

---

## 2. AC-0.0.1 — Silk SDL2 AAR alignment measured

**Package:** `Silk.NET.Windowing.Sdl` **2.23.0** (the version `Wuka.csproj:130` and
`Splash.Silk.csproj` actually reference; the 2.22.0 pin named in plan §5 WP-0.1 is the
*Assimp* pin, a different package).

> **Incidental finding:** the AAR is **byte-identical between 2.22.0 and 2.23.0** —
> SHA256 `4D5640ED79CB2B109284F158CA91B50F483F0AF6746A54B5ADE336DC4930B68B` for both.
> Independent support for non-negotiable **N8** ("do not fix a problem by bumping a Silk
> version"): for this artifact, bumping Silk changes nothing at all.

Command (NDK r27c `llvm-readelf`, `readelf` is not on PATH — see §5):

```
llvm-readelf -lW <aar>/jni/<abi>/libSDL2.so | grep LOAD
```

| File | LOAD alignment |
|---|---|
| `jni/arm64-v8a/libSDL2.so` | `0x1000` (4 KB) |
| `jni/arm64-v8a/libmain.so` | `0x1000` (4 KB) |
| `jni/armeabi-v7a/libSDL2.so`, `libmain.so` | `0x1000` |
| `jni/x86_64/libSDL2.so`, `libmain.so` | `0x1000` (4 KB) |
| `jni/x86/libSDL2.so`, `libmain.so` | `0x1000` |

**The ADR's premise is correct:** as shipped, nothing in this AAR is 16 KB-aligned.

### What is actually in the AAR

```
AndroidManifest.xml   classes.jar   R.txt
jni/{arm64-v8a,armeabi-v7a,x86,x86_64}/{libSDL2.so,libmain.so}
META-INF/com/android/build/gradle/aar-metadata.properties
```

- **SDL2 version: 2.30.8.** No revision string is embedded (built from a tarball, so
  `SDL_GetRevision` is empty). Recovered by disassembling `SDL_GetVersion` at `0x2fffc`:
  `mov w8, #0x1e02` → `strh` writes major=`0x02`, minor=`0x1e`=30; `mov w9, #0x8` →
  patch=8. Independently confirmed: the `release-2.30.8` tag declares
  `SDL_MAJOR_VERSION 2 / MINOR 30 / MICRO 8`.
- **Toolchain: clang 9.0.9** — `Android (7019983 based on r365631c3)`, i.e. **NDK r21e**.
  So Silk compiled a late-2024 SDL2 with a 2021 NDK. That is the whole bug: 16 KB
  alignment became the NDK default only in r27. Nothing about SDL2 required this.
- `libmain.so` is **Silk's own shim**, not SDL's — it exports `sdSetMain`, `SDL_main` and
  `CurrentMain` and nothing else. Its source is not shipped in the AAR.

---

## 3. AC-0.0.2 — Repack attempted: **it works**

### 3.1 Why a relink was required (and a repack alone is not enough)

Two separate things are called "16 KB alignment":

1. **ELF segment alignment** (`p_align` in the LOAD headers) — a **link-time** property. It
   cannot be patched in place: `p_vaddr ≡ p_offset (mod p_align)` must hold, so raising
   `p_align` means relaying out the file. **This is the actual blocker**, and it forces a
   rebuild from source.
2. **Zip alignment** inside the APK — a packaging step (`zipalign -P 16`), trivially fixable.

So the test had to be: rebuild SDL2 from source, relinked at 16 KB, and reassemble the AAR.

### 3.2 What was done

Recipe committed at [`wp-0.0/`](wp-0.0/) (`build-sdl2.ps1`, `main-shim/main.c`).

1. `git clone --depth 1 --branch release-2.30.8 https://github.com/libsdl-org/SDL.git`
2. CMake + Ninja + **NDK r27c**, per ABI, with
   `-Wl,-z,max-page-size=16384 -Wl,-z,common-page-size=16384`.
3. Rebuilt Silk's `libmain.so` shim from a disassembly-derived reconstruction
   (`main-shim/main.c` — 3 functions, semantics recovered from the arm64 disassembly and
   documented inline in the file).
4. Swapped the four 64-bit `.so`s into the extracted AAR tree, kept `classes.jar`,
   `AndroidManifest.xml`, `R.txt` and the 32-bit ABIs untouched, re-zipped.

Non-obvious build details that cost time and are needed to reproduce:

- CMake 4.3.3 rejects SDL 2.30.8's `cmake_minimum_required` → needs
  `-DCMAKE_POLICY_VERSION_MINIMUM=3.5`.
- SDL2 links its shared lib with the **C** driver, so hidapi's one C++ TU leaves
  `__gxx_personality_v0` undefined. `-DANDROID_STL=c++_static` does **not** fix it;
  appending `-lc++_static -lc++abi` to `CMAKE_SHARED_LINKER_FLAGS` does. This keeps the
  result free of a `libc++_shared.so` dependency, matching the shipped AAR.

### 3.3 Verification of the repacked AAR

| File | Alignment | Verdict |
|---|---|---|
| `jni/arm64-v8a/libSDL2.so` | `0x4000` | ✅ 16 KB |
| `jni/arm64-v8a/libmain.so` | `0x4000` | ✅ 16 KB |
| `jni/x86_64/libSDL2.so` | `0x4000` | ✅ 16 KB |
| `jni/x86_64/libmain.so` | `0x4000` | ✅ 16 KB |
| `jni/armeabi-v7a/*`, `jni/x86/*` | `0x1000` | left as-is — 32-bit ABIs cannot run on 16 KB-page devices and are not covered by the Play rule. **Worth confirming against GATE-B's actual Play Console output rather than taking my word for it.** |

### 3.4 Compatibility checks (this is where a naive repack would have silently broken)

| Check | Result |
|---|---|
| JNI entry points `Java_org_libsdl_app_*` | **52 shipped → 52 rebuilt, 0 lost.** `classes.jar` was kept unchanged, so this is the check that matters for the Java↔native boundary. |
| Silk's `libmain.so` undefined symbols | needs only `__cxa_finalize`, `__cxa_atexit` (libc). **Zero unresolved `SDL_*`.** |
| Rebuilt `libmain.so` exports | `sdSetMain` (16 bytes, byte-identical size to original), `SDL_main`, `CurrentMain` (8 bytes) — same export set |
| `NEEDED` parity for `libSDL2.so` | identical modulo `libstdc++.so`, which the rebuild drops (an Android stub; harmless) |
| hidapi present | yes in both — the rebuild does not silently drop controller support |

**One real difference, investigated:** the shipped `libSDL2.so` exports **1239**
`SDL_*`/`PLATFORM_*` symbols; the rebuild exports **866**. The 373-symbol delta is entirely
SDL2 **internals** (`SDL_SendKeyboardKey`, `SDL_EGL_*`, `SDL_SYS_*`, `SDL_HIDAPI_Driver*`,
`SDL_Private*`, …). The rebuild exports **nothing** the shipped one didn't — it is a strict
subset. Cause: Silk's build did not apply `-fvisibility=hidden`; the CMake build does. The
CMake result is the *more* correct artifact.

Confirmed not to matter here: `rg` over the repo finds **zero** references to any of those
internal symbols, and the only raw-SDL2 consumer — the `BeforeDoEvent` hatch in
`Wuka/Platforms/Android/GameActivity.cs:89-103` — uses public API only
(`Sdl.GetApi()`, `PeepEvents`, `EventType`).

---

## 4. AC-0.0.3 — SDL3 AAR alignment measured

**SDL3 `release-3.4.14`** (published 2026-08-03), asset `SDL3-devel-3.4.14-android.zip`,
SHA256 `E41691E75433B2A0A75685781BED2160FE4A85F75F3803F7F43D1811E212E3EF`, containing
`SDL3-3.4.14.aar`.

| ABI | LOAD alignment | Verdict |
|---|---|---|
| `android.arm64-v8a` | `0x4000` | ✅ 16 KB |
| `android.x86_64` | `0x4000` | ✅ 16 KB |
| `android.armeabi-v7a` | `0x1000` | 32-bit, not covered |
| `android.x86` | `0x1000` | 32-bit, not covered |

**Claim #7 confirmed.** SDL3 ships correctly aligned out of the box.

> ⚠ **Correction to the plan for Phase 2.** Plan §5 AC-0.0.3 and AC-2.2 both give the path
> `<sdl3-aar>/jni/arm64-v8a/libSDL3.so`. **That path does not exist.** The SDL3 AAR uses the
> **prefab** layout: `prefab/modules/SDL3-shared/libs/android.<abi>/libSDL3.so`, plus
> `prefab/prefab.json`. This is not cosmetic — .NET Android's `@(AndroidLibrary)` consumption
> of a prefab AAR is a different path from the `jni/`-layout AAR Silk ships, and WP-2.1 must
> budget for it. AC-2.2's command needs updating too.

---

## 5. Environment — plan §5b is wrong for this machine

Plan §5b describes the owner's **Mac**. Work is happening on **Windows 11**. Corrected:

| Need | Plan §5b says | Actual (Windows, 2026-08-05) |
|---|---|---|
| ELF tooling | ❌ absent, `brew install llvm` | ✅ **present** — NDK r27c and r23c both ship `llvm-readelf.exe` / `llvm-objdump.exe` under `toolchains/llvm/prebuilt/windows-x86_64/bin`. `readelf` is not on PATH; use the NDK's. |
| Android NDK | ❌ not at `~/Library/Android/sdk/ndk` | ✅ **present** — `C:\Program Files (x86)\Android\AndroidNDK\{android-ndk-r23c, android-ndk-r27c}` |
| `gh` authenticated | ✅ | ❌ **absent** — `gh` is not installed. Blocks AC-1.1 and *all PR operations in §2.1*. Needs installing before any WP can open a PR. |
| cmake / ninja | not listed | cmake 4.3.3 ✅ · ninja ❌ (fetched a standalone `ninja.exe` into scratch for this spike) |
| `java` | not listed | ❌ absent — did not matter here (`classes.jar` was reused, never rebuilt); **would** matter if the Java side ever needs rebuilding |

`dotnet workload`: android, ios, maccatalyst, maui-windows installed.

---

## 6. Recommendation — for the human, not for a worker

Claim #6 is falsified, so per plan §4/§5c the programme **stops here for a decision**. My
reading, stated as opinion:

1. **The acute Play blocker is now cheap to clear.** A 16 KB-aligned SDL2 AAR is ~15 minutes
   of compute from a pinned SDL tag with tooling already installed. That is a **Phase 1**
   shape of work (build natives in pinned CI, consume as a versioned package) — *not* a
   Phase 2/3 shape (rewrite windowing onto SDL3).
2. **Phases 2–3 lose their urgency, exactly as plan §4 predicted.** They do not lose their
   *rationale* — that rests on ADR §4c (Silk 2.x maintenance-mode, 3.0 a rewrite), which this
   spike did not test.
3. **Phase 1 gets bigger, not smaller.** Falsifying #6 means Karawan now owns an SDL2 build in
   addition to openal-soft. That is the same recipe/CI machinery WP-1.2/1.3 already planned;
   the target list changes, the work does not disappear. Note this also *sharpens* WP-1.6:
   with a correct SDL2 AAR, promoting XA0141 to an error becomes achievable much earlier.
4. **This strengthens the "bank-the-wins exit" in plan §5c.** Phases 0–1 alone now plausibly
   deliver the entire acute value.

Concretely, I'd suggest re-scoping Phase 1 to include SDL2-for-Android and deferring the
Phase 2 decision until after GATE-B passes on a repacked-AAR build — at which point the
SDL3 migration can be judged purely on longevity grounds, with no shipping pressure.

---

## 7. What is **not** verified — do not let this be read as more than it is

Per **N9**, none of the following were run and none may be claimed:

- ❌ **The repacked AAR has never been executed.** Not on a device, not on an emulator. It is
  a correctly-structured, correctly-aligned artifact and nothing more. Alignment was proven;
  *function* was not.
- ❌ **Not integrated into `Wuka`.** Plan step 1's final clause — "reference it in place of
  the package's" — was **not** done, because WP-0.0 is scoped "change no product code". No
  APK was built. `BLOCKED-ON-HUMAN` / deferred to a Phase 1 WP.
- ❌ **GATE-B not attempted** (Play Console upload). The 32-bit-ABIs-are-exempt reasoning in
  §3.3 is my understanding of the Play rule, and should be confirmed by GATE-B's real output.
- ❌ **Runtime behaviour of the 373 dropped internal symbols** verified only by static
  analysis of this repo. Sound, but not a runtime proof.
- ❌ **SDL3's prefab-layout AAR was not test-consumed** by a .NET Android project.

## 8. Acceptance criteria

| id | Criterion | Verdict | Evidence |
|---|---|---|---|
| AC-0.0.1 | SDL2 AAR alignment measured | ✅ PASS | §2 — `0x1000` across all 8 `.so` |
| AC-0.0.2 | Repack attempted, outcome recorded | ✅ PASS | §3 — **repack works**; recipe in `wp-0.0/` |
| AC-0.0.3 | SDL3 AAR alignment measured | ✅ PASS | §4 — `0x4000` on both 64-bit ABIs |
| AC-0.0.4 | Report written | ✅ PASS | this file |
| AC-GLOBAL-2 | `JoyceCode/`, `nogameCode/` untouched | ✅ PASS | `git diff --stat master -- JoyceCode/ nogameCode/` → empty |
| AC-GLOBAL-3 | `models/shaders/` untouched | ✅ PASS | `git diff --stat master -- models/shaders/` → empty |
| AC-GLOBAL-1/1b/4/5 | build + tests | n/a | docs-only WP, no C# touched |
