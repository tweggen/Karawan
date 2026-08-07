#!/usr/bin/env bash
#
# Build openal-soft for one target.
#
# Generalised from the Android-only build-openal-android.sh (WP-1.2). One code path
# now covers all six targets:
#
#   linux-x64  win-x64  osx-arm64  osx-x64  android-arm64-v8a  android-armeabi-v7a
#
# Usage:
#   recipes/build-openal.sh --target <target> [--out <dir>] [--src <dir>]
#
# Why Karawan builds this itself:
#   Silk.NET.OpenAL.Soft.Native ships desktop binaries only, and its linux-arm64 build
#   links against glibc (libc.so.6), which does not exist on Android. Android needs an
#   NDK build against libOpenSLES.so and Bionic. See recipes/BUILD_NOTES.md.
#
# On Windows this expects to be run from a Developer Command Prompt environment (the
# CI workflow calls vcvarsall first, and the environment is inherited by bash).

set -euo pipefail

. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib/common.sh"

OPENAL_REPO="https://github.com/kcat/openal-soft.git"

TARGET=""
OUT_DIR=""
SRC_DIR=""

while [ $# -gt 0 ]; do
    case "$1" in
        --target) TARGET="$2"; shift 2 ;;
        --out)    OUT_DIR="$2"; shift 2 ;;
        --src)    SRC_DIR="$2"; shift 2 ;;
        -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
        *) die "unknown argument '$1'" ;;
    esac
done

[ -n "${TARGET}" ] || die "--target is required (one of: ${ALL_TARGETS})"
KIND="$(target_kind "${TARGET}")"
OUT_DIR="${OUT_DIR:-${KARAWAN_ROOT}/artifacts/openal/${TARGET}}"
SRC_DIR="${SRC_DIR:-${TMPDIR:-/tmp}/karawan-natives/openal-soft}"
BUILD_DIR="${SRC_DIR}-build-${TARGET}"

info "openal-soft ${OPENAL_TAG} -> ${TARGET}"

fetch_pinned "${OPENAL_REPO}" "${OPENAL_TAG}" "${OPENAL_COMMIT}" "${SRC_DIR}"

rm -rf "${BUILD_DIR}"
mkdir -p "${OUT_DIR}"

# Options common to every target: we want the library only, no utilities, no examples,
# and no install step scribbling outside the build directory.
COMMON_ARGS=(
    -DALSOFT_UTILS=OFF
    -DALSOFT_EXAMPLES=OFF
    -DALSOFT_INSTALL=OFF
    -DALSOFT_TESTS=OFF
)

case "${KIND}" in
    android)
        ABI="$(android_abi "${TARGET}")"
        NDK="$(ndk_root)"
        info "NDK ${NDK}, ABI ${ABI}, minSdk ${ANDROID_MIN_API}"
        # ANDROID_USE_LEGACY_TOOLCHAIN_FILE=OFF is load-bearing, not tidying: with the
        # legacy toolchain file ANDROID_STL is not propagated to the link line, and
        # openal-soft (C++ throughout) fails with hundreds of undefined std::__ndk1::
        # and __cxa_guard_* symbols.
        # Both STL variables are set on purpose. The legacy toolchain file reads
        # ANDROID_STL; CMake's built-in Android support (which is what
        # ANDROID_USE_LEGACY_TOOLCHAIN_FILE=OFF selects) reads CMAKE_ANDROID_STL_TYPE and
        # silently ignores ANDROID_STL - defaulting to a STATIC libc++. That default is
        # wrong here: libassimp.so uses the shared runtime, and two copies of libc++ in
        # one process is a real hazard (duplicate globals, mismatched typeinfo). See
        # recipes/BUILD_NOTES.md.
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

collect() {
    local found=0 f
    for f in "$@"; do
        if [ -f "${f}" ]; then
            cp "${f}" "${OUT_DIR}/"
            found=1
        fi
    done
    [ "${found}" -eq 1 ]
}

case "${KIND}" in
    android|linux)
        collect "${BUILD_DIR}/libopenal.so" \
            || die "no libopenal.so produced under ${BUILD_DIR}"
        SO="${OUT_DIR}/libopenal.so"
        # Strip: unstripped openal-soft is several times larger, and this ships in an APK.
        if [ "${KIND}" = "android" ]; then
            "$(ndk_bin)/llvm-strip" "${SO}"
            verify_android_alignment "${SO}" "$(android_abi "${TARGET}")"

            # Assert we got the SHARED C++ runtime. A static libc++ links fine and runs
            # fine in isolation, which is exactly why this needs asserting rather than
            # trusting: the damage only shows up alongside libassimp.so.
            if ! "$(ndk_bin)/llvm-readelf" -d -W "${SO}" | grep -q 'libc++_shared\.so'; then
                die "${SO} does not link libc++_shared.so - the STL setting did not take effect"
            fi
            info "OK: libopenal.so links the shared C++ runtime"

            # AC-1.4, expressed as assertions rather than as a one-off gate command.
            #
            # The plan originally asked for libOpenSLES.so in NEEDED. No correct build
            # produces that: openal-soft dlopen()s OpenSLES, so it appears as a string
            # and a symbol reference, never as a NEEDED entry. The criterion's intent -
            # this is a real Android build and it can actually make sound - is right, so
            # it is tested here in the two ways that are actually true of a good build.
            if "$(ndk_bin)/llvm-readelf" -d -W "${SO}" | grep -qE 'libc\.so\.6|ld-linux'; then
                die "${SO} links glibc - this is a Linux binary, not an Android one.
Android needs Bionic (libc.so) and would fail to load this at runtime."
            fi
            info "OK: libopenal.so links Bionic, not glibc"

            if ! grep -q 'libOpenSLES.so' "${SO}" || ! grep -q 'slCreateEngine' "${SO}"; then
                die "${SO} has no OpenSL ES backend - it would be silent on Android.
openal dlopen()s OpenSLES, so both the library name and slCreateEngine must appear
in the binary. Check that alc/backends/opensl.cpp was compiled in."
            fi
            info "OK: libopenal.so carries the OpenSL ES audio backend"

            # Ship the matching runtime from the SAME NDK. Mixing NDK revisions between
            # libassimp.so and libopenal.so breaks the shared runtime's ABI.
            ABI_DIR="$(ndk_root)/toolchains/llvm/prebuilt/$(basename "$(dirname "$(ndk_bin)")")/sysroot/usr/lib"
            case "$(android_abi "${TARGET}")" in
                arm64-v8a)   TRIPLE_DIR=aarch64-linux-android ;;
                armeabi-v7a) TRIPLE_DIR=arm-linux-androideabi ;;
            esac
            CXX_SHARED="${ABI_DIR}/${TRIPLE_DIR}/libc++_shared.so"
            if [ -f "${CXX_SHARED}" ]; then
                cp "${CXX_SHARED}" "${OUT_DIR}/libc++_shared.so"
                "$(ndk_bin)/llvm-strip" "${OUT_DIR}/libc++_shared.so"
                verify_android_alignment "${OUT_DIR}/libc++_shared.so" "$(android_abi "${TARGET}")"
                describe_artifact "${OUT_DIR}/libc++_shared.so"
            else
                die "libc++_shared.so not found at ${CXX_SHARED}"
            fi
        else
            strip --strip-unneeded "${SO}" 2>/dev/null || true

            # openal-soft compiles only the backends whose headers exist at configure
            # time, and reports success either way. Without ALSA/Pulse/PipeWire you get
            # a library with OSS/WaveFile/Null only: it links, it loads, and it is
            # silent on any modern desktop. Assert a real backend is present rather
            # than reading the configure summary and hoping.
            #
            # openal dlopen()s these, so they appear as strings, not as NEEDED entries.
            if ! grep -qE 'libasound\.so|libpulse\.so|libpipewire' "${SO}"; then
                die "${SO} has no ALSA/PulseAudio/PipeWire backend - it would be silent.
Install the audio development headers (libasound2-dev libpulse-dev libpipewire-0.3-dev)
and rebuild."
            fi
            info "OK: libopenal.so carries a real Linux audio backend"
        fi
        describe_artifact "${SO}"
        ;;
    macos)
        # openal-soft emits a versioned dylib plus symlinks; take the real file.
        DYLIB="$(find "${BUILD_DIR}" -maxdepth 1 -name 'libopenal*.dylib' -type f | head -1)"
        [ -n "${DYLIB}" ] || die "no libopenal dylib produced under ${BUILD_DIR}"
        cp "${DYLIB}" "${OUT_DIR}/libopenal.dylib"
        strip -x "${OUT_DIR}/libopenal.dylib" 2>/dev/null || true
        describe_artifact "${OUT_DIR}/libopenal.dylib"
        ;;
    windows)
        collect "${BUILD_DIR}/Release/OpenAL32.dll" "${BUILD_DIR}/Release/soft_oal.dll" \
            || die "no OpenAL dll produced under ${BUILD_DIR}/Release"
        for f in "${OUT_DIR}"/*.dll; do describe_artifact "${f}"; done
        ;;
esac

info "openal-soft ${TARGET} -> ${OUT_DIR}"
ls -l "${OUT_DIR}"
