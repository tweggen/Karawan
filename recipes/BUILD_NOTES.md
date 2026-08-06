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
| `build-assimp-android.sh` | `libassimp.so` + `libc++_shared.so` | [assimp/assimp](https://github.com/assimp/assimp) | android only — **do not touch**, see below |

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
