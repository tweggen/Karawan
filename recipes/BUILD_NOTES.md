# Native Library Build Recipes

Build scripts for the native shared libraries bundled in the Android APK
(`Wuka/Platforms/Android/android/{arch}/`).

## Prerequisites

- Linux x64 host
- Android NDK r27+ (for 16KB page alignment support)
- CMake 3.21+
- git

Set `ANDROID_NDK_HOME` to point to your NDK installation, e.g.:
```bash
export ANDROID_NDK_HOME=$HOME/Android/Sdk/ndk/27.2.12479018
```

## Recipes

| Script | Library | Source | Current version in repo |
|--------|---------|--------|------------------------|
| `build-openal-android.sh` | `libopenal.so` | [kcat/openal-soft](https://github.com/kcat/openal-soft) | Unknown (428 KB stripped, no NDK tag) |
| `build-assimp-android.sh` | `libassimp.so` + `libc++_shared.so` | [assimp/assimp](https://github.com/assimp/assimp) | NDK r29-beta1, not stripped (11.2 MB!) |

## Usage

```bash
cd recipes
./build-openal-android.sh     # builds + installs libopenal.so for both ABIs
./build-assimp-android.sh     # builds + installs libassimp.so + libc++_shared.so
```

Output goes directly into `Wuka/Platforms/Android/android/{arm64-v8a,armeabi-v7a}/`.

## Why we build these ourselves

- **Silk.NET.OpenAL.Soft.Native** only ships desktop binaries. Its `linux-arm64`
  build links against glibc — will crash on Android. We need an NDK build that
  uses `libOpenSLES.so` (Android audio) and Bionic libc.

- **Silk.NET.Assimp / Ultz.Native.Assimp** don't ship Android native libs at all.

- Both need **16KB ELF page alignment** (`-Wl,-z,max-page-size=16384`) for
  Android 16 (API 36) compatibility.

- Stripping saves significant APK size (libassimp: 11.2 MB unstripped vs ~4 MB stripped).

## 16KB Page Alignment

All scripts pass `-Wl,-z,max-page-size=16384` to the linker. To verify after building:

```bash
readelf -l libopenal.so | grep -A1 LOAD
# The alignment (last column) should show 0x4000 (16384)
```

## libc++_shared.so

Both libassimp and libopenal use `ANDROID_STL=c++_shared`. Only one copy of
`libc++_shared.so` ships in the APK. The assimp build script copies it from the
NDK. Both libs must be built with the **same NDK version** to ensure ABI
compatibility of the shared C++ runtime.
