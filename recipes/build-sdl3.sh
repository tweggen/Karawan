#!/usr/bin/env bash
#
# Build SDL3 for one target.
#
# WP-1.3. Same shape as build-openal.sh, same six targets, same pinning discipline:
#
#   linux-x64  win-x64  osx-arm64  osx-x64  android-arm64-v8a  android-armeabi-v7a
#
# Usage:
#   recipes/build-sdl3.sh --target <target> [--out <dir>] [--src <dir>]
#
# Why Karawan builds this itself, rather than taking SDL3's official Android AAR:
#   The AAR is 16 KB-aligned and would do (WP-0.0-FINDINGS.md §4), but it uses the
#   prefab layout, ships a fixed NDK's output, and puts SDL's release cadence in the
#   critical path. Building from a pinned tag keeps SDL3 on the same footing as
#   openal-soft: one recipe, one NDK revision, one place to change a version. It also
#   keeps the C++ runtime consistent with libopenal.so and libassimp.so, which matters
#   because only one libc++_shared.so ships in the APK.
#
# On Windows this expects a Developer Command Prompt environment (the workflow calls
# vcvarsall first and bash inherits it).

set -euo pipefail

. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib/common.sh"

SDL3_REPO="https://github.com/libsdl-org/SDL.git"

TARGET=""
OUT_DIR=""
SRC_DIR=""

while [ $# -gt 0 ]; do
    case "$1" in
        --target) TARGET="$2"; shift 2 ;;
        --out)    OUT_DIR="$2"; shift 2 ;;
        --src)    SRC_DIR="$2"; shift 2 ;;
        -h|--help) sed -n '2,22p' "$0"; exit 0 ;;
        *) die "unknown argument '$1'" ;;
    esac
done

[ -n "${TARGET}" ] || die "--target is required (one of: ${ALL_TARGETS})"
KIND="$(target_kind "${TARGET}")"
OUT_DIR="${OUT_DIR:-${KARAWAN_ROOT}/artifacts/sdl3/${TARGET}}"
SRC_DIR="${SRC_DIR:-${TMPDIR:-/tmp}/karawan-natives/SDL}"
BUILD_DIR="${SRC_DIR}-build-${TARGET}"

info "SDL3 ${SDL3_TAG} -> ${TARGET}"

fetch_pinned "${SDL3_REPO}" "${SDL3_TAG}" "${SDL3_COMMIT}" "${SRC_DIR}"

rm -rf "${BUILD_DIR}"
mkdir -p "${OUT_DIR}"

# Shared library only. No tests, no examples, no install step.
COMMON_ARGS=(
    -DSDL_SHARED=ON
    -DSDL_STATIC=OFF
    -DSDL_TEST_LIBRARY=OFF
    -DSDL_EXAMPLES=OFF
    -DSDL_INSTALL=OFF
)

case "${KIND}" in
    android)
        ABI="$(android_abi "${TARGET}")"
        NDK="$(ndk_root)"
        info "NDK ${NDK}, ABI ${ABI}, minSdk ${ANDROID_MIN_API}"
        # See build-openal.sh for why all three STL knobs plus an explicit -lc++_shared
        # are needed: the legacy toolchain file drops ANDROID_STL from the link line, and
        # CMake's built-in Android support silently defaults to a STATIC libc++. Only one
        # libc++_shared.so ships in the APK, so every native must agree on using it.
        cmake_build "${SRC_DIR}" "${BUILD_DIR}" "${KIND}" \
            -DCMAKE_TOOLCHAIN_FILE="${NDK}/build/cmake/android.toolchain.cmake" \
            -DANDROID_USE_LEGACY_TOOLCHAIN_FILE=OFF \
            -DANDROID_ABI="${ABI}" \
            -DANDROID_PLATFORM="android-${ANDROID_MIN_API}" \
            -DANDROID_STL=c++_shared \
            -DCMAKE_ANDROID_STL_TYPE=c++_shared \
            -DCMAKE_SHARED_LINKER_FLAGS="-Wl,-z,max-page-size=16384 -Wl,-z,common-page-size=16384 -lc++_shared" \
            "${COMMON_ARGS[@]}"
        ;;
    macos)
        ARCH="$(macos_arch "${TARGET}")"
        cmake_build "${SRC_DIR}" "${BUILD_DIR}" "${KIND}" \
            -DCMAKE_OSX_ARCHITECTURES="${ARCH}" \
            "${COMMON_ARGS[@]}"
        ;;
    linux|windows)
        cmake_build "${SRC_DIR}" "${BUILD_DIR}" "${KIND}" "${COMMON_ARGS[@]}"
        ;;
esac

# --- collect ---------------------------------------------------------------

case "${KIND}" in
    android|linux)
        SO="$(find "${BUILD_DIR}" -maxdepth 1 -name 'libSDL3.so*' -type f | head -1)"
        [ -n "${SO}" ] || die "no libSDL3.so produced under ${BUILD_DIR}"
        cp "${SO}" "${OUT_DIR}/libSDL3.so"
        if [ "${KIND}" = "android" ]; then
            "$(ndk_bin)/llvm-strip" "${OUT_DIR}/libSDL3.so"
            verify_android_alignment "${OUT_DIR}/libSDL3.so" "$(android_abi "${TARGET}")"
            # Assert the shared runtime, exactly as build-openal.sh does. A static libc++
            # would link and run fine alone and only break next to the other natives.
            if ! "$(ndk_bin)/llvm-readelf" -d -W "${OUT_DIR}/libSDL3.so" | grep -q 'libc++_shared\.so'; then
                die "${OUT_DIR}/libSDL3.so does not link libc++_shared.so - the STL setting did not take effect"
            fi
            info "OK: libSDL3.so links the shared C++ runtime"
        else
            strip --strip-unneeded "${OUT_DIR}/libSDL3.so" 2>/dev/null || true
        fi
        describe_artifact "${OUT_DIR}/libSDL3.so"
        ;;
    macos)
        DYLIB="$(find "${BUILD_DIR}" -maxdepth 1 -name 'libSDL3*.dylib' -type f | head -1)"
        [ -n "${DYLIB}" ] || die "no libSDL3 dylib produced under ${BUILD_DIR}"
        cp "${DYLIB}" "${OUT_DIR}/libSDL3.dylib"
        strip -x "${OUT_DIR}/libSDL3.dylib" 2>/dev/null || true
        describe_artifact "${OUT_DIR}/libSDL3.dylib"
        ;;
    windows)
        DLL="$(find "${BUILD_DIR}" -name 'SDL3.dll' -type f | head -1)"
        [ -n "${DLL}" ] || die "no SDL3.dll produced under ${BUILD_DIR}"
        cp "${DLL}" "${OUT_DIR}/SDL3.dll"
        describe_artifact "${OUT_DIR}/SDL3.dll"
        ;;
esac

info "SDL3 ${TARGET} -> ${OUT_DIR}"
ls -l "${OUT_DIR}"
