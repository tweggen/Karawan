# WP-2.1 — Android SDL3 spike

A bare SDL3 Android app: GLES 3.0 context, GL entry points resolved through
`SDL_GL_GetProcAddress`, a colour-cycling clear loop, and event logging.

**Its deliverable is an answer, not a feature.** The plan is explicit that "a spike that compiles
but was never run on a device answers nothing" — so everything below the line marked 🔒 is
something only a human with a physical device can complete.

## Status

| | |
|---|---|
| AC-2.1 APK builds | ✅ `dotnet build spikes/sdl3-android/Sdl3Spike.csproj` → exit 0 |
| AC-2.2 SDL3 16 KB aligned in the APK | ✅ all four `LOAD` segments `0x4000` — and so is **every other** `.so` in the APK |
| AC-2.3 no Silk | ✅ no Silk assemblies in the APK; no Silk code anywhere in the spike |
| **AC-2.4 GATE-A** | 🔒 **human, physical device** — see below |
| **AC-2.5 GATE-B** | 🔒 **human, Play Console** |

## Running it (GATE-A)

```bash
dotnet build spikes/sdl3-android/Sdl3Spike.csproj -t:Run     # build, install, launch
adb logcat -s SDL3SPIKE:V SDL:V                              # the spike's output
```

Rider and Visual Studio work too — open `Karawan.sln`, then add this project, or open the
`.csproj` directly. The spike is deliberately not in the solution (see the end of this file).

If `-t:Run` picks the wrong device, `adb devices` then
`-p:AdbTarget=-s\ <serial>`.

### What a pass looks like

The screen cycles slowly through magenta/blue/purple, and logcat shows:

```
SDL3SPIKE: SDL_Init(VIDEO|EVENTS)
SDL3SPIKE: GL_VENDOR   = <gpu vendor>
SDL3SPIKE: GL_RENDERER = <gpu>
SDL3SPIKE: GL_VERSION  = OpenGL ES 3.x ...      <- THE answer line
SDL3SPIKE: drawable    = 2400x1080
SDL3SPIKE: first frame presented
```

Then exercise each of AC-2.4's four requirements and watch for the matching line:

| AC-2.4 requirement | What to do | Expected log |
|---|---|---|
| clear screen | look at it | colours cycle |
| multi-touch | two fingers at once | two `FINGER_DOWN` with **different** `id` |
| **IME text entry** | see below | `TEXT_INPUT '<char>'` |
| rotation | rotate the device | `RESIZED <w>x<h>` with swapped dimensions |
| resume | home, then back | `WILL_ENTER_BACKGROUND` → `DID_ENTER_FOREGROUND`, rendering resumes |

### ⚠ About the IME test

**This is the single most likely point of failure in the whole migration** (ADR §9 claim 8), and
it is also the one this spike tests least directly: the soft keyboard is not shown automatically,
because SDL3 made `SDL_StartTextInput` per-window and the spike does not call it.

So a missing `TEXT_INPUT` line here is **not** evidence that IME is broken — it may just mean text
input was never started. To test it properly, either attach a hardware/Bluetooth keyboard (which
produces `KEY_DOWN` and `TEXT_INPUT` without any IME), or add an `SDL_StartTextInput(window)` call
and rebuild.

Getting a real answer on the soft keyboard is **WP-2.3's job** — that work package exists to port
`KarawanInputConnection.cs` and re-validate why SDL2's IME path was bypassed
(`docs/SYSTEMS/PLATFORMS/ANDROID.md`). Do not let a green screen here be read as "IME works".

## How it fits together

```
Android starts SdlSpikeActivity  (extends org.libsdl.app.SDLActivity)
  └─ loadLibraries()            [C# override]
       ├─ base: System.loadLibrary("SDL3"), ("main")
       └─ sdSetMain(&SdlMain)   -> libmain.so
  └─ SDL's Java glue creates the surface, spawns "SDLThread"
       └─ nativeRunMain("libmain.so", "SDL_main", ...)
            └─ libmain.so SDL_main -> CurrentMain() -> C# SdlMain()
                 └─ SpikeRenderer.Run()   <- SDL thread, NOT the UI thread
```

Three pieces had to exist before any of this could run, and only the first was already in place:

| piece | where it came from |
|---|---|
| `libSDL3.so` | `Karawan.Natives` (WP-1.2/1.3) ✅ |
| `org.libsdl.app` Java glue | vendored here — **not** in the package (`java/PROVENANCE.md`) |
| `libmain.so` | built by `recipes/build-mainshim.sh` from WP-0.0's reconstruction |

## Deleting it

The spike is deliberately **not in `Karawan.sln`** and nothing references it. Once GATE-A has given
its answer and WP-2.2 has moved the real integration into `Wuka`, `rm -rf spikes/sdl3-android` is
the whole cleanup. `Platform.SDL3/` and `recipes/build-mainshim.sh` are **not** disposable — those
are real deliverables the rest of Phase 2/3 builds on.
