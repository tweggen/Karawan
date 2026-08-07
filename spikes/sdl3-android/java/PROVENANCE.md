# Vendored: SDL3's Android Java glue

**Do not edit these files.** They are copied verbatim from SDL and must stay in lockstep with the
`libSDL3.so` we build — see below for why that is not merely tidiness.

## Provenance

| | |
|---|---|
| Upstream | https://github.com/libsdl-org/SDL |
| Tag | `release-3.4.14` |
| Commit | `147a8ee32dbf9ac02f3794964490687b6bbda1bc` |
| Path | `android-project/app/src/main/java/org/libsdl/app/*.java` |
| Files | 12, ~232 KB |
| License | zlib |

The commit is the **same one** `recipes/versions.env` pins for the native build
(`SDL3_COMMIT`), verified by cloning the tag and comparing `git rev-parse HEAD`.

## Why the Java and the native must match exactly

They are two halves of one interface, and neither half is checked at build time.

SDL3 does **not** export `Java_org_libsdl_app_*` symbols the way SDL2 did — it registers its JNI
methods **dynamically**, from `JNI_OnLoad` via `RegisterNatives`
(`src/core/android/SDL_android.c`). The method names and signatures are string literals inside
`libSDL3.so`, matched at runtime against the Java classes in the APK.

Consequences worth knowing before touching either side:

- A mismatched pair produces **no compile error and no link error**. It fails when the activity
  starts, as a `NoSuchMethodError` or a silent hang.
- `llvm-readelf --dyn-syms libSDL3.so | grep Java_org_libsdl` returns **zero** on a perfectly good
  SDL3 build. That is expected, not evidence of a broken build — it *would* have been evidence for
  SDL2, where the same command returns 52 symbols for Silk's `libSDL2.so`.

The check that does mean something is that the class names appear in the binary at all:

```
$ llvm-strings libSDL3.so | grep org/libsdl/app
org/libsdl/app/HIDDeviceManager
org/libsdl/app/SDLActivity
org/libsdl/app/SDLAudioManager
org/libsdl/app/SDLControllerManager
org/libsdl/app/SDLInputConnection
```

## Why these live here and not in `Karawan.Natives`

They should eventually live in the package — that is the only way to guarantee the two halves ship
from one pin. `Karawan.Natives 0.1.0` carries **only `.so` files**; Silk's AAR by contrast carried a
`classes.jar` with 47 compiled classes, which is what made its Android support self-contained.

Folding the Java into our AAR needs a JDK step in `recipes/pack-natives.sh` and a package
republish, which is human-gated (plan §2.5). WP-2.1 is a timeboxed spike, so it vendors instead and
records the follow-up. See the WP-2.1 PR.

## Binding notes

`Transforms/Metadata.xml` removes four fields from the generated C# binding. Java permits several
top-level classes per file but only one public one, and the binding generator emits the
`protected static` fields whose types are the package-private helpers without emitting the helpers
themselves — producing generated C# that does not compile. If SDL is updated and new package-private
helpers appear, that file is where the resulting `CS0234` gets fixed.

## Updating

1. Change `SDL3_TAG`/`SDL3_COMMIT` in `recipes/versions.env`.
2. Re-copy these 12 files from the same commit.
3. Rebuild the natives so `libSDL3.so` comes from that commit too. **Never update one without the
   other.**
