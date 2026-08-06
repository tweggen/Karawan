# WP-1.1 — Native build matrix (CI skeleton)

**Status:** ✅ Complete — matrix green on all six targets
**Date:** 2026-08-06
**Branch:** `platform/wp-1.1`
**Workflow:** [`.github/workflows/natives.yml`](../../../.github/workflows/natives.yml)
**Plan:** [`IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md`](IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md) §5, Phase 1

This is the **skeleton**. It establishes and proves the toolchain matrix; it does **not** build
openal-soft (WP-1.2) or SDL3 (WP-1.3). Each target compiles a trivial smoke library instead, so
that when the real recipes land they land on a toolchain already shown to work and to emit
correctly-formatted output.

It is the first workflow in the repository — there was no `.github/` directory before.

---

## 1. The matrix

| Target | Runner | Notes |
|---|---|---|
| `linux-x64` | `ubuntu-24.04` | system `cc` |
| `android-arm64-v8a` | `ubuntu-24.04` | NDK clang, 16 KB alignment **asserted** |
| `android-armeabi-v7a` | `ubuntu-24.04` | NDK clang, 16 KB check skipped (see §3) |
| `win-x64` | `windows-2022` | MSVC via `vswhere` + `vcvarsall` |
| `osx-arm64` | `macos-15` | `cc -arch arm64` |
| `osx-x64` | `macos-15` | `cc -arch x86_64` |

`fail-fast: false` — one broken target must not hide the state of the other five.

## 2. What is pinned, and why

AC-1.5 requires **byte-identical artifacts across reruns**. Floating versions actively prevent
that, so everything that can drift is either pinned or recorded.

| Thing | Pin | Note |
|---|---|---|
| Runner images | `ubuntu-24.04`, `windows-2022`, `macos-15` | see the deviation in §4 |
| Android NDK | `27.2.12479018` | installed via `sdkmanager`, not "whatever the image ships". r27+ is what makes 16 KB alignment the default (`recipes/BUILD_NOTES.md`) |
| Android min API | `26` | must match Wuka's `SupportedOSPlatformVersion` |
| MSVC toolset | `14.44` via `-vcvars_ver` | compiler `19.44.35228`, VS 2022 17.14 |
| `actions/checkout` | `3d3c42e…` (v7.0.1) | full commit SHA, not a tag |
| `actions/upload-artifact` | `043fb46…` (v7.0.1) | full commit SHA |
| `actions/download-artifact` | `3e5f45b…` (v8.0.1) | full commit SHA |

**Not pinned, but recorded** into `toolchain.txt` in every artifact and into the run summary:
CMake version (3.31.6 at time of writing), host `cc` version, NDK clang version, VS install path.
The plan names NDK, image labels and MSVC toolset as the required pins; the rest are recorded so a
future reproducibility failure can be *explained* rather than guessed at.

**No third-party actions.** MSVC is set up with `vswhere` + `vcvarsall` rather than a
`setup-msvc` action: it removes a supply-chain dependency and it is the documented way to select
an exact toolset. That is the same principle the ADR applies to Silk — depend on the interface,
not on someone's wrapper.

## 3. Verification is inside the matrix

A build that emits the wrong ABI, or a 4 KB-aligned Android library, is worse than a failed build,
because it ships. So each target verifies its own output:

- **arm64-v8a** asserts **every** `LOAD` segment is `0x4000`, and fails the job otherwise.
- **armeabi-v7a** explicitly *skips* that check and says so in the log. 32-bit ABIs are not
  covered by Google's 16 KB rule — devices with 16 KB pages are 64-bit only (see
  `WP-0.0-FINDINGS.md` §3.3).
- Linux/macOS/Windows check the produced file is of the expected format and architecture
  (`file`, `readelf -h`, `lipo -info`).

## 4. Deviation from the plan text, flagged

Plan §5 says: *"`ubuntu-latest` → linux-x64 + android …; `windows-latest` → win-x64; `macos-15` →
osx-arm64/x64. Pinned NDK revision, **pinned image labels** (`macos-15`, never `macos-latest`)"*.

Those two halves contradict each other: `ubuntu-latest` and `windows-latest` **are** floating
labels, and GitHub rotates what they point at. Since the stated reason for pinning `macos-15`
applies identically to the other two — and AC-1.5 demands byte-identical reruns — **all three
images are pinned**. If the intent really was to pin only macOS, this is the line to change.

## 5. Acceptance criteria

| id | Criterion | Command | Result |
|---|---|---|---|
| AC-1.1 | CI green | `gh run list --workflow=natives.yml --limit 1 --json conclusion` | ✅ **PASS** — `[{"conclusion":"success"}]` |
| AC-1.6 | Assimp untouched (N5) | `git diff master -- recipes/build-assimp-android.sh` | ✅ PASS — empty |
| AC-GLOBAL-2 | `JoyceCode/`, `nogameCode/` untouched | `git diff --stat master -- JoyceCode/ nogameCode/` | ✅ PASS — empty |
| AC-GLOBAL-3 | `models/shaders/` untouched | `git diff --stat master -- models/shaders/` | ✅ PASS — empty |
| AC-GLOBAL-1/1b/4 | build + tests | — | n/a — no C# or csproj touched |
| AC-GLOBAL-5 | TALE suite | — | n/a — plan says CI-YAML-only WPs skip it as waste |

Per-job results from run `31096966618`:

```
success  linux-x64
success  android-armeabi-v7a
success  osx-x64
success  osx-arm64
success  win-x64
success  android-arm64-v8a
success  manifest
```

AC-1.2 / 1.3 / 1.4 / 1.5 / 1.7 belong to later work packages and are **not** claimed here — this
skeleton builds a smoke library, not openal-soft or SDL3.

## 6. Notes for WP-1.2 / WP-1.3

- The push/PR triggers are scoped to `.github/workflows/natives.yml` and `recipes/**`, so adding a
  recipe under `recipes/` will trigger the matrix automatically.
- `recipes/build-openal-android.sh` currently assumes a Linux host, `make`, and an NDK discovered
  from `ANDROID_NDK_HOME`. The workflow already exports `ANDROID_NDK_HOME` for Android targets, so
  generalising it (WP-1.2) is mostly about the desktop targets and about replacing `make` with
  something available on all three runners.
- The `manifest` job already collects every target's `toolchain.txt`. WP-1.4 needs to extend that
  into a real build manifest recording the upstream **git tag** alongside the toolchain.

## 7. Environment note

Pushing this branch failed initially: the PAT embedded in the `origin` remote URL lacks the
`workflow` scope, so GitHub refuses any push that creates or updates a file under
`.github/workflows/`. The `gh` credential does have `workflow` scope and was used instead.

**This will bite again** on any future workflow change. Either grant the PAT `workflow` scope or —
better, and already recommended after WP-0.0 — drop the embedded PAT entirely and let `gh` supply
credentials.
