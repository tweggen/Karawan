# Wuka Android Native Library Analysis

**Date:** 2026-03-08
**Target:** `net9.0-android35.0` (min API 26)
**Build warnings:** XA0141 (16KB page size), XA4301 (duplicate .so in APK)

---

## 1. What Actually Ends Up in the APK

Inspecting `de.nassau_records.silicondesert2.apk` (Debug, arm64-v8a):

| Library | Size | Source | Purpose |
|---------|------|--------|---------|
| `libSDL2.so` | 1.4 MB | Silk.NET.Windowing.Sdl 2.22.0 AAR | SDL2 for Android (used by engine) |
| `libSDL2-2.0.so` | 1.8 MB | Ultz.Native.SDL 2.30.8 NuGet (`linux-arm64`) | **SUPERFLUOUS** — Linux SDL2 build, see section 10 |
| `libmain.so` | 5.7 KB | Silk.NET.Windowing.Sdl 2.22.0 AAR | SDL2 JNI entry point |
| `libopenal.so` | 428 KB | Local: `android/arm64-v8a/` | OpenAL Soft audio |
| `libassimp.so` | 11.2 MB | Local: `android/arm64-v8a/` | Assimp model loader |
| `libc++_shared.so` | 8.8 MB | Local: `android/arm64-v8a/` | C++ stdlib (for assimp) |
| `liblua54.so` | 282 KB | (from engine build) | Lua scripting |
| `libSkiaSharp.so` | 8.6 MB | SkiaSharp NuGet | MAUI rendering |
| `libmonosgen-2.0.so` | 3.0 MB | .NET runtime | Mono runtime |
| `libmonodroid.so` | 491 KB | .NET runtime | Android bridge |
| `libxamarin-app.so` | 3.4 MB | Build-generated | App native image |
| `libxamarin-debug-app-helper.so` | 66 KB | .NET runtime (Debug only) | Debugger |
| `libmono-component-debugger.so` | 194 KB | .NET runtime (Debug only) | Debugger |
| `libmono-component-hot_reload.so` | 63 KB | .NET runtime (Debug only) | Hot reload |
| `libmono-component-marshal-ilgen.so` | 35 KB | .NET runtime | IL marshalling |
| `libarc.bin.so` | 18 KB | .NET runtime | Archive |
| `libSystem.Native.so` | 98 KB | .NET runtime | System native |
| `libSystem.Globalization.Native.so` | 70 KB | .NET runtime | Globalization |
| `libSystem.IO.Compression.Native.so` | 742 KB | .NET runtime | Compression |
| `libSystem.Security.Cryptography.Native.Android.so` | 161 KB | .NET runtime | Crypto |

---

## 2. The Three Sources of Native Libraries

### Source A: Silk.NET.Windowing.Sdl 2.22.0 — AAR (automatic)

The NuGet package ships `app-release.aar` (and a duplicate `Silk.NET.Windowing.Sdl.aar`) containing SDL2 for Android:

```
jni/arm64-v8a/libSDL2.so    (1.4 MB)
jni/arm64-v8a/libmain.so    (5.7 KB)
jni/armeabi-v7a/libSDL2.so  (1.0 MB)
jni/armeabi-v7a/libmain.so  (13.6 KB)
jni/x86/libSDL2.so          (1.6 MB)
jni/x86/libmain.so          (5.4 KB)
jni/x86_64/libSDL2.so       (1.6 MB)
jni/x86_64/libmain.so       (6.0 KB)
```

These are included automatically by the Android build system when it processes the AAR. No csproj entry needed. **This is the SDL2 the engine actually uses.**

### Source B: Ultz.Native.SDL 2.30.8 — NuGet runtime (automatic, UNWANTED)

Provides `runtimes/linux-arm64/native/libSDL2-2.0.so` (1.8 MB). This is a **Linux** ARM64 binary (not Android-specific), but .NET for Android picks it up because `linux-arm64` maps to the same architecture. It ends up in the APK as `lib/arm64-v8a/libSDL2-2.0.so` alongside the real Android `libSDL2.so`.

**This is superfluous.** The engine links against `libSDL2.so` from the AAR, not `libSDL2-2.0.so`.

### Source C: Local `AndroidNativeLibrary` entries (manual)

Three libraries manually placed in **two duplicate directories**:

| Library | `android/` dir | `libs/` dir | Identical? |
|---------|---------------|-------------|------------|
| `libassimp.so` (arm64) | 11.2 MB, NDK r29-beta1 | 11.2 MB, NDK r29-beta1 | **YES** (same MD5) |
| `libc++_shared.so` (arm64) | 8.8 MB, NDK r27-beta1 | 8.8 MB, NDK r27-beta1 | **YES** (same MD5) |
| `libopenal.so` (arm64) | 428 KB, stripped | 1.4 MB, GNU/Linux, not stripped | **NO** (different builds!) |

Same pattern for armeabi-v7a (all identical between `android/` and `libs/`).

The csproj declares **both** directories as `AndroidNativeLibrary`:
- Lines 101-112: `android/{arch}/libc++_shared.so` and `libassimp.so`
- Lines 113-124: `libs/{arch}/libc++_shared.so` and `libassimp.so`
- Lines 137-145: `android/arm64-v8a/libopenal.so`, `libs/arm64-v8a/libopenal.so`, `libs/armeabi-v7a/libopenal.so`

**The `android/` directory wins** (declared first in csproj). The `libs/` duplicates produce XA4301 warnings and are ignored.

---

## 3. The libopenal.so Situation

Two different builds exist:

| Location | Size | `file` output | Notes |
|----------|------|---------------|-------|
| `android/arm64-v8a/` | 428 KB | `ARM aarch64, SYSV, stripped` | **In the APK.** No NDK tag. Origin unknown. |
| `libs/arm64-v8a/` | 1.4 MB | `ARM aarch64, GNU/Linux, not stripped` | Ignored (duplicate). This is a Linux build, likely wrong platform. |

The NuGet `Silk.NET.OpenAL.Soft.Native 1.21.1.2` does **not** provide an Android `.so` — only linux-x64, osx-x64, win-x64, win-x86. Both local copies were manually sourced.

The `android/` version (428 KB, stripped) is the one that ships. It works, but has no NDK provenance tag — likely an older OpenAL Soft Android build.

---

## 4. Duplicate/Superfluous Files

### Entirely superfluous

| File | Why |
|------|-----|
| `Ultz.Native.SDL` NuGet package reference | Provides `libSDL2-2.0.so` which is unused — engine uses `libSDL2.so` from Silk.NET AAR |
| `android/x86_64/libopenal.so` | Only file in x86_64 dir, not referenced in csproj, incomplete arch support |

### Duplicate (one copy is ignored at build time)

| File | Winner (in APK) | Loser (ignored) |
|------|-----------------|-----------------|
| `libassimp.so` arm64 | `android/arm64-v8a/` | `libs/arm64-v8a/` (identical) |
| `libc++_shared.so` arm64 | `android/arm64-v8a/` | `libs/arm64-v8a/` (identical) |
| `libopenal.so` arm64 | `android/arm64-v8a/` (428 KB) | `libs/arm64-v8a/` (1.4 MB, different!) |
| `libassimp.so` armv7 | `android/armeabi-v7a/` | `libs/armeabi-v7a/` (identical) |
| `libc++_shared.so` armv7 | `android/armeabi-v7a/` | `libs/armeabi-v7a/` (identical) |
| `libopenal.so` armv7 | (not declared for android/) | `libs/armeabi-v7a/` |
| `libSDL2.so` | Silk.NET.Windowing.Sdl AAR | Also in AAR duplicate (Silk.NET.Windowing.Sdl.aar = app-release.aar) |
| `libmain.so` | Silk.NET.Windowing.Sdl AAR | Also in AAR duplicate |

### The AAR duplication

The NuGet package `Silk.NET.Windowing.Sdl 2.22.0` ships **two identical AARs**:
- `app-release.aar`
- `Silk.NET.Windowing.Sdl.aar`

Both contain the same `libSDL2.so` + `libmain.so`. The build processes both, producing XA4301 warnings for the second copy.

---

## 5. Dead csproj Configuration

The PropertyGroups at lines 43-69 are conditioned on **`net7.0-android33.0`** but the TargetFramework is **`net9.0-android35.0`**:

```xml
<!-- DEAD — never matches! -->
<PropertyGroup Condition="'$(Configuration)|$(TargetFramework)|$(Platform)'=='Release|net7.0-android33.0|AnyCPU'">
  <ApplicationId>de.nassau_records.silicondesert2</ApplicationId>
  <ApplicationTitle>Silicon Desert 2</ApplicationTitle>
  ...
</PropertyGroup>
```

This means:
- `ApplicationId` falls back to default `com.companyname.wuka` (but AndroidManifest.xml overrides to `de.nassau_records.silicondesert2`, so it still works)
- `ApplicationTitle` falls back to `Wuka` (cosmetic only)
- `AndroidPackageFormat`, `AndroidStoreUncompressedFileExtensions`, etc. use defaults

---

## 6. XA0141 — 16KB Page Size Warnings

Android 16 (API 36) requires ELF segments aligned to 16KB. These libraries fail the check:

| Library | Source | Fix needed |
|---------|--------|------------|
| `libSDL2-2.0.so` | Ultz.Native.SDL 2.30.8 | Remove the package (superfluous anyway) |
| `libSDL2.so` | Silk.NET.Windowing.Sdl 2.22.0 AAR | Upstream: update Silk.NET |
| `libmain.so` | Silk.NET.Windowing.Sdl 2.22.0 AAR | Upstream: update Silk.NET |
| `libopenal.so` | Local `android/arm64-v8a/` | Rebuild with `-Wl,-z,max-page-size=16384` |

Note: `libassimp.so` and `libc++_shared.so` (NDK r29-beta1/r27-beta1) are likely already 16KB-aligned since newer NDK versions default to it. The build did NOT warn about them.

---

## 7. Debug vs Release Build

Both configurations currently use the **same native libraries** — there is no conditional inclusion. The difference is only in .NET runtime components:

| Component | Debug | Release |
|-----------|-------|---------|
| `libxamarin-debug-app-helper.so` | Included | Excluded |
| `libmono-component-debugger.so` | Included | Excluded |
| `libmono-component-hot_reload.so` | Included | Excluded |
| Native .so from csproj | Same | Same |
| NuGet native libs | Same | Same |

Since the PropertyGroup conditions for Release are dead (wrong TFM), there's effectively no difference in the manual native lib configuration between Debug and Release.

---

## 8. Recommended Cleanup

### Quick fixes (no rebuild of native libs needed)

1. **Remove `Ultz.Native.SDL` package reference** (line 173) — `libSDL2-2.0.so` is unused, wastes 1.8 MB in APK, triggers XA0141
2. **Delete `libs/` directory entirely** — it's a complete duplicate of `android/` (except libopenal where the `libs/` version is actually a Linux binary, not Android)
3. **Remove all `libs/` AndroidNativeLibrary entries** from csproj (lines 113-124, 140-145)
4. **Remove `None Remove` lines for `libs/`** (lines 95-96)
5. **Delete `android/x86_64/`** — only contains libopenal, no other libs, not referenced in csproj
6. **Fix PropertyGroup TFM conditions** — change `net7.0-android33.0` to `net9.0-android35.0`

### Needs native lib rebuild

7. **Rebuild `libopenal.so`** with 16KB page alignment (`-Wl,-z,max-page-size=16384`) for Android 16 compatibility
8. Consider stripping debug info from `libassimp.so` (11.2 MB) and `libc++_shared.so` (8.8 MB) to reduce APK size

### Upstream dependency updates (longer term)

9. **Update Silk.NET** to a version that ships 16KB-aligned `libSDL2.so` and `libmain.so` in its AAR
10. Consider whether `Silk.NET.OpenAL.Soft.Native` is needed at all in Wuka (it provides no Android libs)

---

## 9. Clean Minimal csproj Native Lib Section

After cleanup, the native lib section should look like:

```xml
<ItemGroup>
  <None Remove="Platforms\Android\android\arm64-v8a\libopenal.so" />
  <None Remove="Platforms\Android\mapicons.png" />
  <None Remove="Platforms\Android\mipmap\appicon_android.png" />
  <None Remove="Platforms\Android\Prototype.ttf" />
  <None Remove="Resources\Splash\splash_nassau.svg" />

  <!-- arm64-v8a -->
  <AndroidNativeLibrary Include="Platforms\Android\android\arm64-v8a\libc++_shared.so" />
  <AndroidNativeLibrary Include="Platforms\Android\android\arm64-v8a\libassimp.so" />
  <AndroidNativeLibrary Include="Platforms\Android\android\arm64-v8a\libopenal.so" />

  <!-- armeabi-v7a -->
  <AndroidNativeLibrary Include="Platforms\Android\android\armeabi-v7a\libc++_shared.so" />
  <AndroidNativeLibrary Include="Platforms\Android\android\armeabi-v7a\libassimp.so" />
  <AndroidNativeLibrary Include="Platforms\Android\android\armeabi-v7a\libopenal.so" />
</ItemGroup>
```

Note: `CopyToOutputDirectory` is unnecessary for `AndroidNativeLibrary` items — they go into the APK via the Android build pipeline, not via output directory copy.

---

## 10. Symbol Analysis: libSDL2.so vs libSDL2-2.0.so

Detailed ELF dynamic symbol comparison (arm64-v8a):

| | libSDL2.so (AAR / Silk.NET) | libSDL2-2.0.so (Ultz) |
|---|---|---|
| **Exported functions** | **1506** | **841** |
| **Common symbols** | 836 | 836 |
| **Unique symbols** | **670** | **5** |

**The AAR `libSDL2.so` is a strict superset.** All 836 public SDL2 API functions in Ultz are also in the AAR version. The AAR has 670 additional symbols:

### Only in AAR (Android-specific, 670 symbols)

- **JNI bridge** (59 symbols): `Java_org_libsdl_app_SDLActivity_*`, `Java_org_libsdl_app_SDLAudioManager_*`, `Java_org_libsdl_app_SDLControllerManager_*` — the Java-to-native interface required for Android
- **Android platform backend** (100+ symbols): `Android_JNI_*`, `Android_*`, `ANDROIDAUDIO_*` — audio, input, window, clipboard, permissions, screen keyboard
- **EGL/Vulkan Android** (30+ symbols): `SDL_EGL_*`, `Android_Vulkan_*`, `Android_GLES_*` — Android GL context management
- **Android audio** (~10 symbols): `aaudio_*`, `openslES_*` — AAudio and OpenSL ES backends
- **HIDAPI** (25+ symbols): `HIDAPI_*`, `PLATFORM_hid_*`, `_ZN10CHIDDevice*` — USB/Bluetooth gamepad support
- **SDL internals** (400+ symbols): `SDL_Private*`, `SDL_Send*`, `SDL_SYS_*`, `SDL_*Init`, `SDL_*Quit` — subsystem initialization, event dispatch, etc.
- **SDLTest** (~60 symbols): `SDLTest_*` — test framework (compiled in but unused)
- **Android-only APIs**: `SDL_AndroidGetActivity`, `SDL_AndroidGetJNIEnv`, `SDL_GetAndroidSDKVersion`, `SDL_IsAndroidTV`, `SDL_IsChromebook`, etc.

### Only in Ultz (Linux-specific, 5 symbols)

- `SDL_DYNAPI_entry` — SDL dynamic API dispatch (Linux loader mechanism)
- `SDL_LinuxSetThreadPriority` — Linux-only thread priority
- `SDL_LinuxSetThreadPriorityAndPolicy` — Linux-only thread scheduling
- `_init` / `_fini` — ELF constructor/destructor (Linux convention)

### Conclusion

`libSDL2-2.0.so` from Ultz.Native.SDL is a **Linux desktop** build. It was packaged under `runtimes/linux-arm64/` and .NET for Android incorrectly picks it up because ARM64 matches. It provides zero Android-specific functionality. The Silk.NET P/Invoke layer links against `libSDL2` (the AAR one), not `libSDL2-2.0`.

**`Ultz.Native.SDL` should be removed from Wuka's NuGet references.** It adds 1.8 MB of dead weight and triggers XA0141 warnings.
