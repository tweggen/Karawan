# Wuka Android Native Library Analysis

**Date:** 2026-03-08 (post-cleanup revision)
**Target:** `net9.0-android35.0` (min API 26)
**Silk.NET:** 2.23.0 | **OpenAL.Soft.Native:** 1.23.1

---

## 1. What Actually Ends Up in the APK

Inspecting `de.nassau_records.silicondesert2.apk` (Debug, arm64-v8a) after cleanup:

| Library | Size | Source | Status |
|---------|------|--------|--------|
| `libSDL2.so` | 1.4 MB | Silk.NET.Windowing.Sdl 2.23.0 AAR | OK — Android SDL2 |
| `libSDL2-2.0.so` | 1.7 MB | Ultz.Native.SDL 2.32.10 (transitive) | **SUPERFLUOUS** — Linux SDL2, see section 4 |
| `libmain.so` | 5.7 KB | Silk.NET.Windowing.Sdl 2.23.0 AAR | OK — SDL2 JNI entry |
| `libopenal.so` | 1.0 MB | Silk.NET.OpenAL.Soft.Native 1.23.1 NuGet | **WRONG BINARY** — Linux build, see section 3 |
| `libassimp.so` | 11.2 MB | Local: `android/arm64-v8a/` | OK — NDK r29-beta1 |
| `libc++_shared.so` | 8.8 MB | Local: `android/arm64-v8a/` | OK — NDK r27-beta1 |
| `liblua54.so` | 282 KB | Engine build | OK |
| `libSkiaSharp.so` | 8.6 MB | SkiaSharp NuGet | OK |
| `libmonosgen-2.0.so` | 3.0 MB | .NET runtime | OK |
| `libmonodroid.so` | 491 KB | .NET runtime | OK |
| `libxamarin-app.so` | 3.4 MB | Build-generated | OK |
| `libxamarin-debug-app-helper.so` | 66 KB | .NET runtime (Debug only) | OK |
| `libmono-component-debugger.so` | 194 KB | .NET runtime (Debug only) | OK |
| `libmono-component-hot_reload.so` | 63 KB | .NET runtime (Debug only) | OK |
| `libmono-component-marshal-ilgen.so` | 35 KB | .NET runtime | OK |
| `libarc.bin.so` | 18 KB | .NET runtime | OK |
| `libSystem.Native.so` | 98 KB | .NET runtime | OK |
| `libSystem.Globalization.Native.so` | 70 KB | .NET runtime | OK |
| `libSystem.IO.Compression.Native.so` | 742 KB | .NET runtime | OK |
| `libSystem.Security.Cryptography.Native.Android.so` | 161 KB | .NET runtime | OK |

---

## 2. Sources of Native Libraries

### Source A: Silk.NET.Windowing.Sdl 2.23.0 — AAR (automatic)

Ships `Silk.NET.Windowing.Sdl.aar` containing the proper Android SDL2:

```
jni/arm64-v8a/libSDL2.so    (1.4 MB)  — Android build, 1506 exported symbols
jni/arm64-v8a/libmain.so    (5.7 KB)  — JNI entry point
jni/armeabi-v7a/libSDL2.so  (1.0 MB)
jni/armeabi-v7a/libmain.so  (13.6 KB)
jni/x86/...  jni/x86_64/...
```

**Note:** The AAR binary is byte-identical to the 2.22.0 version (same MD5). The 2.23.0 upgrade fixed the duplicate AAR issue (no more `app-release.aar` alongside `Silk.NET.Windowing.Sdl.aar`), but did not update the SDL2 native libraries — they're still not 16KB page-aligned.

### Source B: Ultz.Native.SDL 2.32.10 — transitive dependency (UNWANTED)

`Silk.NET.SDL 2.23.0` declares a hard dependency on `Ultz.Native.SDL 2.32.10` for **all TFMs** (no Android-specific override). This pulls in `runtimes/linux-arm64/native/libSDL2-2.0.so` (1.7 MB) — a Linux desktop SDL2 binary that .NET for Android erroneously includes because `linux-arm64` arch-matches `android-arm64`.

Cannot be removed as a direct package reference since it's a transitive dependency. Must be excluded via MSBuild — see section 5.

### Source C: Silk.NET.OpenAL.Soft.Native 1.23.1 — NuGet runtime (NEW PROBLEM)

Version 1.23.1 now provides `runtimes/linux-arm64/native/libopenal.so` (1.0 MB) — which 1.21.1.2 did not. Same `linux-arm64` → `android-arm64` bleed-through as Ultz.Native.SDL. This NuGet version **wins over** the local `AndroidNativeLibrary` (428 KB), which is silently ignored with XA4301.

### Source D: Local `AndroidNativeLibrary` entries (manual)

After cleanup, only the `android/` directory remains:

| Library | arm64-v8a | armeabi-v7a |
|---------|-----------|-------------|
| `libassimp.so` | 11.2 MB, NDK r29-beta1 | 9.3 MB |
| `libc++_shared.so` | 8.8 MB, NDK r27-beta1 | 6.9 MB |
| `libopenal.so` | 428 KB, Android build | 374 KB |

---

## 3. CRITICAL: libopenal.so — Wrong Binary in APK

The NuGet `Silk.NET.OpenAL.Soft.Native 1.23.1` `linux-arm64` libopenal (1.0 MB) is shipping instead of the local Android build (428 KB). These are fundamentally different binaries:

| | NuGet (linux-arm64) — IN APK | Local (android/) — IGNORED |
|---|---|---|
| Size | 1,044,848 bytes | 428,368 bytes |
| Exported functions | 361 | 284 |
| **Dynamic dependencies** | `libstdc++.so.6` | `libdl.so` |
| | `libm.so.6` | **`libOpenSLES.so`** |
| | `libgcc_s.so.1` | `liblog.so` |
| | `libc.so.6` | `libstdc++.so` |
| | **`ld-linux-aarch64.so.1`** | `libm.so` |
| | | `libc.so` |

**The NuGet version links against Linux glibc** (`libc.so.6`, `libm.so.6`, `ld-linux-aarch64.so.1`). These do not exist on Android. It will **fail to load at runtime**.

The local version links against `libOpenSLES.so` (Android's native audio API) and `liblog.so` (Android logging) — it's the correct Android NDK build.

**This is a regression from upgrading OpenAL.Soft.Native 1.21.1.2 → 1.23.1.** Version 1.21.1.2 didn't provide `linux-arm64`, so the local copy was used.

---

## 4. libSDL2-2.0.so — Still Superfluous (Transitive)

Even though `Ultz.Native.SDL` was removed as a direct package reference, it's back as version 2.32.10 via `Silk.NET.SDL 2.23.0 → Ultz.Native.SDL 2.32.10` (declared for all TFMs, no Android exception).

The symbol analysis from the previous revision still applies — `libSDL2.so` (AAR) is a strict superset of `libSDL2-2.0.so` (Ultz) with 670 additional Android-specific symbols (JNI, EGL, Android audio backends, HIDAPI, etc.). The Ultz version has only 5 Linux-specific symbols not in the AAR.

---

## 5. Remaining Build Warnings

### XA0141 — 16KB page size (Android 16/API 36)

| Library | Source | Fix |
|---------|--------|-----|
| `libSDL2-2.0.so` | Ultz.Native.SDL 2.32.10 (transitive) | Exclude from APK |
| `libSDL2.so` | Silk.NET.Windowing.Sdl 2.23.0 AAR | Upstream (same binary as 2.22.0) |
| `libmain.so` | Silk.NET.Windowing.Sdl 2.23.0 AAR | Upstream (same binary as 2.22.0) |
| `libopenal.so` | Silk.NET.OpenAL.Soft.Native 1.23.1 NuGet | Exclude NuGet version, use local |

### XA4301 — Duplicate .so

| Library | Winner | Loser |
|---------|--------|-------|
| `libopenal.so` | NuGet 1.23.1 (wrong!) | Local AndroidNativeLibrary (correct!) |

---

## 6. Recommended Fixes

### Fix 1: Exclude NuGet native libs that bleed into Android (CRITICAL)

Add to `Wuka.csproj` to prevent `linux-arm64` NuGet runtimes from overriding local Android libs:

```xml
<!-- Exclude Linux native libs from NuGet packages that bleed into Android builds.
     Silk.NET.SDL depends on Ultz.Native.SDL which provides libSDL2-2.0.so for linux-arm64.
     Silk.NET.OpenAL.Soft.Native provides libopenal.so for linux-arm64.
     Both are Linux desktop binaries, not Android. The AAR provides the real Android SDL2,
     and our local AndroidNativeLibrary provides the correct Android OpenAL. -->
<PackageReference Include="Ultz.Native.SDL" Version="2.32.10" ExcludeAssets="native" PrivateAssets="all" />
<PackageReference Include="Silk.NET.OpenAL.Soft.Native" Version="1.23.1" ExcludeAssets="native" PrivateAssets="all" />
```

This keeps the managed code from these packages but prevents their `runtimes/linux-arm64/native/*.so` files from being included in the APK.

### Fix 2: Delete orphaned x86_64 directory

`Platforms/Android/android/x86_64/` contains only `libopenal.so` — incomplete, not referenced in csproj. Delete it.

### Fix 3: Remove stale None Remove entries

Lines 94-96 reference `libs/` paths and a root-level `libopenal.so` that no longer exist:

```xml
<!-- Remove these lines: -->
<None Remove="Platforms\Android\libopenal.so" />
<None Remove="Platforms\Android\libs\arm64-v8a\libopenal.so" />
<None Remove="Platforms\Android\libs\armeabi-v7a\libopenal.so" />
```

### Fix 4 (cosmetic): Remove CopyToOutputDirectory

`CopyToOutputDirectory` is unnecessary for `AndroidNativeLibrary` items. They go into the APK via the Android build pipeline, not via output copy.

### Fix 5 (longer term): 16KB page alignment

For Android 16 (API 36) compatibility:
- `libopenal.so` (local): Rebuild with `-Wl,-z,max-page-size=16384`
- `libSDL2.so` + `libmain.so` (AAR): Wait for Silk.NET update with 16KB-aligned SDL2
- Consider stripping `libassimp.so` (11.2 MB, not stripped) and `libc++_shared.so` (8.8 MB, with debug_info) to reduce APK size

---

## 7. What Changed from the Previous Analysis

| Item | Before (Silk.NET 2.22.0) | After (Silk.NET 2.23.0) |
|------|-------------------------|------------------------|
| `Ultz.Native.SDL` | Direct reference, v2.30.8 | Removed direct ref, but v2.32.10 back as transitive dep |
| `libSDL2-2.0.so` in APK | Yes (1.8 MB) | Still yes (1.7 MB) — from transitive dep |
| `libs/` directory | Existed, full duplicate | Deleted |
| Dead PropertyGroups | Active (wrong TFM) | Commented out |
| AAR duplication | Two AARs (`app-release.aar` + `Silk.NET.Windowing.Sdl.aar`) | One AAR only — **fixed in 2.23.0** |
| `libopenal.so` in APK | Local 428 KB (correct Android build) | **NuGet 1.0 MB (WRONG — Linux build!)** |
| `Silk.NET.OpenAL.Soft.Native` | v1.21.1.2 (no linux-arm64) | v1.23.1 (has linux-arm64 — bleeds into APK) |
| XA4301 warnings | `libc++_shared`, `libassimp`, `libopenal`, `libmain`, `libSDL2` | Only `libopenal` (local ignored by NuGet) |
| XA0141 warnings | 4 libraries | Same 4 libraries (AAR unchanged) |

---

## 8. Clean csproj Native Lib Section (Target State)

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

<!-- ... -->

<ItemGroup>
  <!-- Exclude Linux native libs from transitive NuGet deps that bleed into Android -->
  <PackageReference Include="Ultz.Native.SDL" Version="2.32.10" ExcludeAssets="native" PrivateAssets="all" />
  <PackageReference Include="Silk.NET.OpenAL.Soft.Native" Version="1.23.1" ExcludeAssets="native" PrivateAssets="all" />
</ItemGroup>
```
