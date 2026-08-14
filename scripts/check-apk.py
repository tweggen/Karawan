#!/usr/bin/env python3
#
# Verify that a built Wuka APK actually CONTAINS what it needs, by reading the APK -
# not by trusting that the build was green.
#
# THE TRAP
#
# An Android build can succeed, sign, install and launch while the APK is missing a
# class or a native library. Nothing warns. This repository has now been bitten twice:
#
#   1. libSDL3.so was absent from every APK for a whole work package. XA0141 and XA4301
#      both stayed clean. Wuka.csproj still carries the comment: "THIS WAS CAUGHT BY
#      LISTING THE APK, NOT BY THE BUILD."
#
#   2. 2026-08-08, Release only: all 49 org/libsdl/app/* classes were missing, while the
#      generated stub crc64e20757511145c75a/GameActivity - which EXTENDS
#      org.libsdl.app.SDLActivity - was present and correctly declared in the manifest.
#      An obj/Release tree from Aug 2 was reused incrementally across the Aug 8 commit
#      that vendored the SDL3 Java glue; _CompileBindingJava produced binding/bin/Wuka.jar
#      with all 49 classes, and d8 never received it. Build: 0 errors, 0 relevant warnings.
#
# WHY THE OBVIOUS CHECKS DO NOT CATCH IT
#
# Case 2 is worth dwelling on, because the runtime error names the WRONG class:
#
#   java.lang.ClassNotFoundException: crc64e20757511145c75a.GameActivity
#
# GameActivity was in classes2.dex the whole time. ART cannot LOAD it, because loading a
# class requires resolving its superclass, and org.libsdl.app.SDLActivity was not in the
# APK - so it reports the subclass as not found. Reading that literally sends you hunting
# for a class that is present, which is why the "dangling superclass" scan below exists:
# it flags the real missing class, generically, without anyone naming it in advance.
#
# Grepping the dex bytes for a class name does NOT work either - a dex contains the name
# string for every class it REFERENCES, not just those it DEFINES. This script parses the
# class_defs table, so it distinguishes the two.
#
# USAGE
#
#   python scripts/check-apk.py Wuka/bin/Release/net9.0-android36.0/android-arm64/de.nassau_records.silicondesert2-Signed.apk
#
# Exit code 0 = every check passed. 1 = something is missing; the APK is broken even if
# the build said otherwise. If it fails right after a dependency or Java-source change,
# suspect stale obj/ first: rm -rf Wuka/obj/<Config> Wuka/bin/<Config> and rebuild.

import struct
import sys
import zipfile

# Natives that must be in the APK for the app to reach its first frame. Each one is here
# because its absence produced a crash or a silent loss of function, not on principle.
REQUIRED_LIBS = [
    "libSDL3.so",      # windowing/input; SDL.loadLibrary("SDL3") dies without it
    "libmain.so",      # SDL's Java glue calls nativeRunMain("libmain.so", "SDL_main")
    "libopenal.so",    # audio
    "libc++_shared.so",  # C++ runtime SDL3 and openal are linked against
]

# Natives that must NOT be in the APK. libassimp.so was required until WP-4.4;
# models are baked into mo-{hash} files at build time now and nothing at runtime
# can read an fbx, so its reappearance means a runtime project picked up an Assimp
# reference again - which is easy to do by accident and invisible otherwise.
FORBIDDEN_LIBS = [
    "libassimp.so",
]

# Classes that must be DEFINED in the dex. The activities are the entry points; SDLActivity
# is GameActivity's superclass and is the one that actually went missing.
REQUIRED_CLASSES = [
    "Lcrc64e20757511145c75a/MainActivity;",
    "Lcrc64e20757511145c75a/GameActivity;",
    "Lorg/libsdl/app/SDLActivity;",
    "Lorg/libsdl/app/SDL;",
]

# Superclass namespaces that legitimately live outside the APK - the Android framework and
# the Java/XML runtime the platform provides. A superclass under any other prefix must be
# defined inside the APK, or the subclass cannot load.
EXTERNAL_PREFIXES = (
    "Landroid/", "Ljava/", "Ljavax/", "Ldalvik/",
    "Lorg/xml/", "Lorg/w3c/", "Lorg/json/", "Lorg/apache/http/",
)


def _uleb128(buf, offset):
    result = 0
    shift = 0
    while True:
        byte = buf[offset]
        offset += 1
        result |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return result, offset
        shift += 7


def class_defs(buf):
    """Yield (class_descriptor, superclass_descriptor) for every class DEFINED in a dex."""
    string_ids_off = struct.unpack_from("<I", buf, 60)[0]
    type_ids_off = struct.unpack_from("<I", buf, 68)[0]
    class_defs_size, class_defs_off = struct.unpack_from("<II", buf, 96)

    def string_at(index):
        off = struct.unpack_from("<I", buf, string_ids_off + 4 * index)[0]
        _, off = _uleb128(buf, off)  # length in UTF-16 code units, unused
        return buf[off:buf.index(b"\x00", off)].decode("utf-8", "replace")

    def type_at(index):
        return string_at(struct.unpack_from("<I", buf, type_ids_off + 4 * index)[0])

    for i in range(class_defs_size):
        base = class_defs_off + 32 * i
        type_idx = struct.unpack_from("<I", buf, base)[0]
        super_idx = struct.unpack_from("<i", buf, base + 8)[0]
        yield type_at(type_idx), (type_at(super_idx) if super_idx != -1 else None)


def main(apk_path):
    apk = zipfile.ZipFile(apk_path)
    names = apk.namelist()

    defined = {}
    for entry in names:
        if entry.endswith(".dex"):
            for descriptor, superclass in class_defs(apk.read(entry)):
                defined[descriptor] = (entry, superclass)

    libs = sorted(n for n in names if n.startswith("lib/"))
    print(f"{apk_path}")
    print(f"  {len(libs)} native libraries, {len(defined)} classes defined, "
          f"{len([n for n in names if n.startswith('assets/')])} assets")

    failures = []

    for lib in REQUIRED_LIBS:
        if not any(n.endswith("/" + lib) for n in libs):
            failures.append(f"native library missing from the APK: {lib}")

    for lib in FORBIDDEN_LIBS:
        present = [n for n in libs if n.endswith("/" + lib)]
        if present:
            failures.append(
                f"native library must NOT be in the APK: {lib} ({', '.join(present)}). "
                f"Since WP-4.4 fbx import is build-time only; a runtime project has "
                f"re-acquired an Assimp reference.")

    for descriptor in REQUIRED_CLASSES:
        if descriptor not in defined:
            failures.append(f"class not defined in any dex: {descriptor}")

    # The generic check: a class whose superclass is neither in the APK nor supplied by the
    # platform cannot be loaded, and the error will name the SUBCLASS.
    for descriptor, (dex, superclass) in sorted(defined.items()):
        if superclass is None or superclass in defined:
            continue
        if superclass.startswith(EXTERNAL_PREFIXES):
            continue
        failures.append(
            f"dangling superclass: {descriptor} (in {dex}) extends {superclass}, "
            f"which is not defined in the APK - loading {descriptor} will raise "
            f"ClassNotFoundException naming {descriptor}, not {superclass}")

    if failures:
        print(f"\nFAILED - {len(failures)} problem(s):")
        for failure in failures:
            print(f"  - {failure}")
        print("\nIf this appeared right after a dependency or Java-source change, suspect a "
              "stale obj tree:\n  rm -rf Wuka/obj/<Config> Wuka/bin/<Config> && dotnet build "
              "Wuka/Wuka.csproj -c <Config>")
        return 1

    print("\nOK - all required libraries and classes present, no dangling superclasses.")
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__ or "usage: check-apk.py <path-to-apk>", file=sys.stderr)
        print("usage: check-apk.py <path-to-apk>", file=sys.stderr)
        sys.exit(2)
    sys.exit(main(sys.argv[1]))
