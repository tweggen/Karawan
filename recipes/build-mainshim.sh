#!/usr/bin/env bash
#
# libmain.so - the SDL entry-point shim for Android.
#
# WHY THIS EXISTS
#
# SDL's Java glue (org.libsdl.app.SDLActivity) starts a thread that calls
# SDLActivity.nativeRunMain(library, function, args). Both defaults come from
# overridable methods:
#
#   getLibraries()        -> { "SDL3", "main" }   => library = "libmain.so"
#   getMainFunction()     -> "SDL_main"
#
# So SDL expects a native library exporting SDL_main. libSDL3.so does NOT export
# one - it is a library, not an application. In a normal C game that symbol comes
# from the game itself. Our "game" is .NET code that is already running in the
# Android runtime by the time SDLActivity starts, so we need a tiny bridge:
# managed code registers a callback, and SDL_main invokes it on SDL's own thread.
#
# That is exactly what Silk.NET shipped as libmain.so inside
# Silk.NET.Windowing.Sdl. Removing Silk removes that shim too, so we build our own.
# The C below is WP-0.0's reconstruction, recovered by disassembling Silk's binary
# (docs/roadmap/proposed/wp-0.0/main-shim/main.c) - it is 3 symbols and ~20 lines,
# and matching its ABI exactly is what lets the managed side stay unchanged.
#
# ALTERNATIVE, deliberately not taken here: SDLActivity.main() is protected and
# could be overridden in C# to run the managed loop directly, removing this native
# entirely. That is cleaner but unproven against .NET Android's binding of a
# source-compiled Java class. WP-2.1 is a spike whose job is to answer whether SDL3
# works on a device, so it uses the route Silk already proved. Revisit in WP-2.2.
#
# 16 KB PAGE SIZE: mandatory, this is the whole point of the migration. Asserted
# below rather than trusted - a wrongly-linked shim would be a silent regression
# that only shows up as a Play Console warning much later.
#
# WHY spikes/sdl3-android/natives/ HOLDS CHECKED-IN OUTPUT OF THIS SCRIPT
#
# GATE-A is a human running the spike on a physical device, and requiring an NDK
# install just to get an APK onto a phone would put a toolchain between the plan and
# its answer. The two .so files are 4-6 KB each and this script regenerates them
# byte-for-byte from source that is also in the repo.
#
# That is a SPIKE affordance, not the destination. When the shim moves into
# Karawan.Natives (see the WP-2.1 PR follow-up), this script gets called from
# .github/workflows/natives.yml like every other recipe and the checked-in copies go
# away with the rest of spikes/.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/common.sh
. "${HERE}/lib/common.sh"

: "${ANDROID_NDK_HOME:?ANDROID_NDK_HOME must point at the pinned NDK}"

OUT_DIR="${1:?usage: build-mainshim.sh <output-dir>}"
SRC="${HERE}/../docs/roadmap/proposed/wp-0.0/main-shim/main.c"

[ -f "${SRC}" ] || die "main shim source not found: ${SRC}"

host_tag() {
    case "$(uname -s)" in
        Linux*)   echo "linux-x86_64" ;;
        Darwin*)  echo "darwin-x86_64" ;;
        MINGW*|MSYS*|CYGWIN*) echo "windows-x86_64" ;;
        *) die "unsupported host: $(uname -s)" ;;
    esac
}

TOOLCHAIN="${ANDROID_NDK_HOME}/toolchains/llvm/prebuilt/$(host_tag)/bin"
READELF="${TOOLCHAIN}/llvm-readelf"

# API 26 matches Wuka.csproj's SupportedOSPlatformVersion for android.
API=26

build_abi() {
    local abi="$1" triple="$2"
    local cc="${TOOLCHAIN}/${triple}${API}-clang"
    [ -x "${cc}" ] || cc="${cc}.cmd"
    [ -x "${cc}" ] || die "no clang for ${abi} at ${TOOLCHAIN}/${triple}${API}-clang"

    local dest="${OUT_DIR}/${abi}"
    mkdir -p "${dest}"

    info "building libmain.so for ${abi}"
    "${cc}" \
        -shared -fPIC -O2 \
        -Wl,-z,max-page-size=16384 \
        -Wl,-soname,libmain.so \
        -o "${dest}/libmain.so" \
        "${SRC}"

    assert_16k "${dest}/libmain.so" "${abi}"
    assert_exports "${dest}/libmain.so" "${abi}"
}

# ---- assertions -------------------------------------------------------------
# Both of these are the difference between "the build ran" and "the build is
# correct". WP-1.2 shipped a Linux openal with no audio backend and CI called it
# success; that is the failure mode these exist to prevent.

assert_16k() {
    local lib="$1" abi="$2"
    local bad
    bad="$("${READELF}" -lW "${lib}" | awk '$1=="LOAD" && $NF!="0x4000"' || true)"
    if [ -n "${bad}" ]; then
        echo "${bad}" >&2
        die "${abi}: libmain.so has a LOAD segment that is not 16 KB aligned"
    fi
    info "OK: ${abi} libmain.so is 16 KB page aligned"
}

assert_exports() {
    local lib="$1" abi="$2"
    local syms
    syms="$("${READELF}" --dyn-syms -W "${lib}" | awk '{print $8}' | sed 's/@@.*//')"
    for want in SDL_main sdSetMain CurrentMain; do
        echo "${syms}" | grep -qx "${want}" \
            || die "${abi}: libmain.so does not export ${want} - the SDL/managed bridge would fail at runtime"
    done
    info "OK: ${abi} libmain.so exports SDL_main, sdSetMain, CurrentMain"
}

build_abi arm64-v8a   aarch64-linux-android
build_abi armeabi-v7a armv7a-linux-androideabi

info "libmain.so written to ${OUT_DIR}/{arm64-v8a,armeabi-v7a}"
