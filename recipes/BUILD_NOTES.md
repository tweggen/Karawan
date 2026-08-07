# Native Library Build Recipes

Build scripts for the native shared libraries Karawan ships itself, rather than taking
from a NuGet package.

Since WP-1.2 these run in CI for **every** target, not just Android:
[`.github/workflows/natives.yml`](../.github/workflows/natives.yml).

## Targets

```
linux-x64   win-x64   osx-arm64   osx-x64   android-arm64-v8a   android-armeabi-v7a
```

## Recipes

| Script | Library | Upstream | Targets |
|--------|---------|----------|---------|
| `build-openal.sh` | `libopenal.so` / `.dylib` / `OpenAL32.dll` | [kcat/openal-soft](https://github.com/kcat/openal-soft) | all six |
| `build-sdl3.sh` | `libSDL3.so` / `.dylib` / `SDL3.dll` | [libsdl-org/SDL](https://github.com/libsdl-org/SDL) | all six |
| `build-assimp-android.sh` | `libassimp.so` + `libc++_shared.so` | [assimp/assimp](https://github.com/assimp/assimp) | android only — **do not touch**, see below |

### Why SDL3 is built rather than taken from the official AAR

SDL ships an Android AAR and it *is* 16 KB-aligned ([`WP-0.0-FINDINGS.md`](../docs/roadmap/proposed/WP-0.0-FINDINGS.md) §4),
so it would work. Building from a pinned tag instead keeps SDL3 on the same footing as
openal-soft: one recipe, one NDK revision, one file to change a version in. It also keeps
the C++ runtime consistent across every native, which matters because only one
`libc++_shared.so` ships in the APK. The AAR additionally uses the **prefab** layout, which
is a different consumption path from the `jni/`-layout AAR the project uses today.

`lib/common.sh` holds the shared logic (target parsing, pinned checkout, CMake driver,
alignment verification). It is sourced, not executed.

## Usage

```bash
recipes/build-openal.sh --target android-arm64-v8a
recipes/build-openal.sh --target linux-x64 --out /tmp/openal-linux
```

Output goes to `artifacts/openal/<target>/` by default. **The scripts no longer write
into `Wuka/Platforms/Android/android/<abi>/`** — the checked-in natives stay checked in
until WP-1.5 switches the app over to consuming the CI-built package.

On Windows, run from a Developer Command Prompt environment (the workflow calls
`vcvarsall` first and bash inherits the environment).

## Version pinning

All upstream versions live in [`versions.env`](versions.env) — one place, sourced by the
scripts and exported into CI.

Each library is pinned by **both tag and commit SHA**. The tag is what a human reads; the
SHA is what actually guarantees reproducibility, because upstream can move or re-cut a
tag. `fetch_pinned` checks out the tag and then **fails the build** if `HEAD` is not the
recorded commit:

```
ERROR: pin mismatch for https://github.com/kcat/openal-soft.git 1.24.3:
  expected dc7d705...
  actual   abc1234...
```

If you see that, find out *why* the tag moved before changing the SHA.

## Distribution: `Karawan.Natives`

CI packages the matrix output as a versioned NuGet — `runtimes/<rid>/native/` for desktop,
an `.aar` for Android, and `build-manifest.json` recording the upstream tag, commit,
toolchain and a sha256 per binary.

It is published to **two** feeds, and the reason is not redundancy:

| feed | anonymous restore | role |
|---|---|---|
| GitHub Packages | ❌ **no — 401** | artifact of record, tied to the run that built it |
| nuget.org | ✅ yes | what the build actually consumes |

**GitHub Packages NuGet requires authentication even for a public package.** Verified: an
anonymous `dotnet restore` against `https://nuget.pkg.github.com/tweggen/index.json` returns
`401 Unauthorized`. Consuming from there would mean every contributor — and every fork's CI —
needed a PAT with `read:packages` simply to build Karawan, when today the build needs no
credentials at all. That is why the package also goes to nuget.org.

Publishing is manual and opt-in, via `workflow_dispatch` on `natives.yml`:

| input | effect |
|---|---|
| `publish` | push to GitHub Packages (uses the run's own `GITHUB_TOKEN`) |
| `publish_nuget_org` | push to nuget.org via **Trusted Publishing** |
| `version` | the version to publish, e.g. `0.1.0` |

Neither ever runs on a push or a pull request. A NuGet version **can never be re-published**;
on nuget.org the package ID is additionally claimed globally and permanently, and versions can
be unlisted but never deleted. Non-manual builds get `<version>-ci.<run_number>` so a CI build
can never collide with a release.

### Trusted Publishing, not an API key

nuget.org now discourages API keys for automated publishing. The workflow uses
[`NuGet/login`](https://github.com/NuGet/login) to exchange this run's short-lived GitHub OIDC
token for a short-lived nuget.org key, so **there is no long-lived publish secret** in the
repository to leak, rotate or mis-scope.

Two pieces of one-time setup, neither of them a secret:

1. A **Trusted Publishing policy** on nuget.org naming this repository and workflow —
   see [the NuGet docs](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).
2. A repository **variable** (not a secret) `NUGET_USER`, holding the nuget.org account name.
   Settings → Secrets and variables → Actions → *Variables*.

The job fails fast with these instructions if `NUGET_USER` is missing, rather than failing
obscurely inside `dotnet nuget push`. It also needs `id-token: write`, which is granted on that
job alone rather than workflow-wide.

## Why Karawan builds these itself

- **Silk.NET.OpenAL.Soft.Native** ships desktop binaries only, and its `linux-arm64` build
  links against glibc — it cannot load on Android, which needs `libOpenSLES.so` and Bionic.
- **Silk.NET.Assimp / Ultz.Native.Assimp** ship no Android natives at all.
- Android 16 requires **16 KB ELF page alignment** on 64-bit ABIs.

## 16 KB page alignment

Every Android build passes `-Wl,-z,max-page-size=16384 -Wl,-z,common-page-size=16384`, and
`verify_android_alignment` then **asserts** it on arm64-v8a rather than trusting the flag:

```bash
llvm-readelf -lW libopenal.so | grep LOAD    # alignment column must read 0x4000
```

`armeabi-v7a` is deliberately exempt. Devices with 16 KB pages are 64-bit only, so the Play
requirement does not cover 32-bit ABIs — see
[`WP-0.0-FINDINGS.md`](../docs/roadmap/proposed/WP-0.0-FINDINGS.md) §3.3.

## `libc++_shared.so`

Both assimp and the Android openal build use `ANDROID_STL=c++_shared`, and only one copy of
`libc++_shared.so` ships in the APK. Both must therefore be built with the **same NDK
revision** or the shared C++ runtime is ABI-mismatched. CI pins the NDK to `27.2.12479018`.

## Running the Android recipes locally

`recipes/build-mainshim.sh` (WP-2.1) needs `ANDROID_NDK_HOME`. CI exports it; locally you set it
yourself, and **the NDK is often not where a search suggests**. It is *not* necessarily under
`%LOCALAPPDATA%\Android\Sdk\ndk\` — the standalone Windows NDK installer puts it at:

```bash
export ANDROID_NDK_HOME="/c/Program Files (x86)/Android/AndroidNDK/android-ndk-r27c"
bash recipes/build-mainshim.sh /tmp/mainshim
```

The tools the recipes and the plan's acceptance criteria rely on (`llvm-readelf`, `llvm-strip`,
`llvm-strings`, the `*-clang` wrappers) all live in
`$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/<host-tag>/bin/`.

> **`llvm-readelf` is the right tool for Android `.so` files** — they are ELF, and `otool` is
> Mach-O only. If no NDK is installed at all, `p_align` can still be read straight out of the ELF
> program headers; WP-1.6 did exactly that to verify 16 KB compliance on a machine without one.
> Note that a symbol lookup must **strip the `@@SDL3_0.0.0` version suffix** first, or an exact
> match silently finds nothing and looks like a missing symbol.

## `build-assimp-android.sh` is deliberately untouched

Non-negotiable **N5** of the platform-backend plan: Phase 4 removes Assimp from the runtime
entirely by baking FBX at build time, so generalising this script would be discarded work.
It is left exactly as it was.

## History

`build-openal-android.sh` was the Android-only ancestor of `build-openal.sh`. WP-1.2
generalised it to all six targets and removed it; the equivalent of the old invocation is

```bash
recipes/build-openal.sh --target android-arm64-v8a
recipes/build-openal.sh --target android-armeabi-v7a
```

with the difference that output now lands in `artifacts/` instead of overwriting the
checked-in libraries under `Wuka/`.
