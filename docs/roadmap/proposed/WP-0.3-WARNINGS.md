# WP-0.3 — Inventory of the silent Android warnings

**Status:** ✅ Complete — inventory only, **nothing promoted, nothing suppressed**
**Date:** 2026-08-06
**Branch:** `platform/wp-0.3`
**Plan:** [`IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md`](IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md) §5, WP-0.3

Promotion of `XA0141` / `XA4301` to errors is **deferred to WP-1.6** by design: `Wuka` is in
`Karawan.sln`, so promoting them now would make AC-GLOBAL-1 fail on master for every subsequent
work package until the natives are fixed in Phase 1.

> ## ⏩ Follow-up (2026-08-07, WP-1.6) — what became of this inventory
>
> - **`XA4301` is now promoted to an error** in `Wuka.csproj`. Still zero occurrences, so it is a
>   regression guard exactly as §2 predicted.
> - **`XA0141` is NOT promoted**, and cannot be within Phase 1. Re-measured on `platform/wp-1.6`:
>   **8 warnings / 2 distinct libraries**, down from the 10 / 3 below — `libcimgui.so` is gone,
>   because §4.2's suggested fix was applied (`ExcludeAssets="native"` on `ImGui.NET`, with the
>   caveat that `PrivateAssets="all"` must **not** be added or the managed assembly disappears too).
>   **§4.1 and §4.2 are therefore resolved.** What remains is `libSDL2.so` / `libmain.so` from
>   `Silk.NET.Windowing.Sdl`, which clears only when Silk's SDL2 leaves the APK — Phase 2/3.
> - Full reasoning and the three options: `PLATFORM-BACKEND-STATUS.md` §AC-1.7.
>
> §3's "re-confirm against a Release/AAB build before GATE-B" is **still open**.

Build: `dotnet build Wuka/Wuka.csproj` (Debug, `net9.0-android36.0`) at `1b432fba`.
Result: **exit 0, 974 warnings, 0 errors.** `Wuka.csproj` contains no `NoWarn`,
`WarningsAsErrors` or `TreatWarningsAsErrors` — these are simply at default severity and lost
among the other ~970.

---

## 1. `XA0141` — 16 KB page size

10 occurrences, **3 distinct libraries**, from **2 packages**:

| Library | Package | Source path |
|---|---|---|
| `libSDL2.so` | `Silk.NET.Windowing.Sdl` **2.23.0** | `…/Silk.NET.Windowing.Sdl.aar` |
| `libmain.so` | `Silk.NET.Windowing.Sdl` **2.23.0** | `…/Silk.NET.Windowing.Sdl.aar` |
| `libcimgui.so` | **`ImGui.NET` 1.91.6.1** | `runtimes/`**`linux-x64`**`/native/libcimgui.so` |

Verbatim (paths shortened):

```
warning XA0141: Android 16 will require 16 KB page sizes, shared library 'libSDL2.so' does not
  have a 16 KB page size. Please inform the authors of the NuGet package
  'Silk.NET.Windowing.Sdl' version '2.23.0' …
warning XA0141: … shared library 'libmain.so' … 'Silk.NET.Windowing.Sdl' version '2.23.0' …
warning XA0141: … shared library 'libcimgui.so' … the NuGet package 'ImGui.NET' version
  '1.91.6.1' which contains 'runtimes/linux-x64/native/libcimgui.so'.
```

## 2. `XA4301` — duplicate native library

**Zero occurrences.** The plan (§5, WP-0.3) describes XA4301 as "precisely how the wrong
`libopenal.so` shipped", which implies it was firing at some point. It is not firing today —
`Wuka.csproj` already carries `ExcludeAssets="native" PrivateAssets="all"` on
`Silk.NET.OpenAL.Soft.Native` and `Ultz.Native.SDL`, which is presumably what fixed it.

**Do not treat XA4301 as a live problem.** Promoting it in WP-1.6 is still worth doing as a
regression guard, but it is guarding, not fixing.

---

## 3. Full 16 KB audit of the built APK

The warnings name libraries; they don't say what actually ships. Every `.so` was extracted from
`de.nassau_records.silicondesert2-Signed.apk` and measured with the NDK r27c `llvm-readelf`
(`-lW`, `LOAD` segment alignment). **36 libraries, 2 ABIs.**

**Exactly 5 are not 16 KB aligned — and they are the only ones:**

| Library | ABI | Align | Owner |
|---|---|---|---|
| `libSDL2.so` | arm64-v8a | `0x1000` | Silk.NET.Windowing.Sdl |
| `libmain.so` | arm64-v8a | `0x1000` | Silk.NET.Windowing.Sdl |
| `libSDL2.so` | x86_64 | `0x1000` | Silk.NET.Windowing.Sdl |
| `libmain.so` | x86_64 | `0x1000` | Silk.NET.Windowing.Sdl |
| **`libcimgui.so`** | **x86_64** | `0x1000` | **ImGui.NET** |

Everything else is already `0x4000`, including all of Microsoft's runtime
(`libmonosgen-2.0`, `libmonodroid`, `libxamarin-app`, the `libSystem.*` set),
`libSkiaSharp.so`, `libassimp.so`, `libopenal.so`, `liblua54.so`, `libc++_shared.so`.

> Measured on a **Debug** APK. Re-confirm against a Release/AAB build before GATE-B, since
> trimming and the linker can change which natives are packaged.

---

## 4. ⚠ Two findings that affect the plan

### 4.1 Removing Silk does **not** achieve 16 KB compliance on its own

The ADR frames the page-size blocker as a Silk problem. It is *mostly* Silk — 4 of 5 offenders —
but **`libcimgui.so` comes from `ImGui.NET`**, and plan §5 WP-5.3 says explicitly:

> *"`ImGui.NET` is **not** a Silk package and stays."*

So completing the entire Silk exit as currently written would still leave one non-compliant
library in the APK. **This needs to be an explicit work item.** It is not covered by
WP-1.2/1.3 (openal-soft and SDL3 only) and it is not covered by Phase 5.

### 4.2 `libcimgui.so` is a **linux-x64** binary shipped inside an Android APK

The warning names its source: `runtimes/linux-x64/native/libcimgui.so`. It lands in
`lib/x86_64/` of the APK (1.7 MB). A glibc-linked Linux binary cannot load on Android's bionic
libc, so this is dead weight at best and a load failure at worst.

This is the same class of defect as plan AC-1.3 ("No Linux natives in the APK; no glibc-linked
`libopenal.so`") — and the mechanism that ships it, RID-fallback picking a `linux-x64` native
for an Android target, is exactly what `ExcludeAssets="native"` on
`Silk.NET.OpenAL.Soft.Native` and `Ultz.Native.SDL` already guards against elsewhere in
`Wuka.csproj`.

**Suggested fix (deliberately NOT applied here — WP-0.3 is inventory only):** give `ImGui.NET`
the same `ExcludeAssets="native"` treatment in `Wuka.csproj`. That removes the library, which
resolves its `XA0141` as a side effect. Verify ImGui still behaves on Android first — see GATE-E.

### 4.3 ABI asymmetry worth understanding before Phase 1

The two ABIs do not ship the same set:

| Library | arm64-v8a | x86_64 |
|---|---|---|
| `libassimp.so` (11.8 MB) | ✅ | ❌ |
| `libc++_shared.so` (9.2 MB) | ✅ | ❌ |
| `libopenal.so` | ✅ | ❌ |
| `libcimgui.so` | ❌ | ✅ |

Whatever the cause, it means **the x86_64 split is not a faithful emulator stand-in for an arm64
device** — an emulator has no OpenAL and no Assimp, so audio and model loading cannot be
exercised there. Relevant to GATE-A: the SDL3 spike must be validated on a **physical arm64
device**, which the plan already requires, and this explains why that matters beyond convenience.

`libc++_shared.so` presence on arm64 only is also direct input to **AC-4.6** (the keep-or-drop
decision for `libc++_shared`).

---

## 5. Acceptance criteria

| id | Criterion | Command | Result |
|---|---|---|---|
| AC-0.3.1 | Warnings inventoried | `dotnet build Wuka/Wuka.csproj 2>&1 \| rg 'XA0141\|XA4301'` | ✅ PASS — 10 hits, 3 distinct libs (§1); XA4301 zero (§2); full audit §3 |
| AC-0.3.2 | No suppressions added | `git diff master -- Wuka/Wuka.csproj` | ✅ PASS — **empty**; `Wuka.csproj` is untouched by this WP |
| AC-GLOBAL-2 | `JoyceCode/`, `nogameCode/` untouched | `git diff --stat master -- JoyceCode/ nogameCode/` | ✅ PASS — empty |
| AC-GLOBAL-3 | `models/shaders/` untouched | `git diff --stat master -- models/shaders/` | ✅ PASS — empty |
| AC-GLOBAL-1/1b/4 | build + tests | — | n/a — docs-only WP, no C# or csproj touched |
| AC-GLOBAL-5 | TALE suite | — | n/a — touches none of `JoyceCode/`, `nogameCode/`, `models/tale/` |
