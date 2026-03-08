#!/bin/bash
# Build OpenAL Soft for Android (arm64-v8a + armeabi-v7a)
#
# Host: Linux x64
# Prerequisites: Android NDK r27+ (for 16KB page size support), CMake, git
#
# The resulting libopenal.so files go into:
#   Wuka/Platforms/Android/android/arm64-v8a/libopenal.so
#   Wuka/Platforms/Android/android/armeabi-v7a/libopenal.so
#
# Why we build this ourselves:
#   Silk.NET.OpenAL.Soft.Native only ships desktop binaries (linux, osx, win).
#   Its linux-arm64 build links against glibc (libc.so.6, ld-linux-aarch64.so.1)
#   which doesn't exist on Android. We need an NDK build that links against
#   libOpenSLES.so (Android native audio) and Bionic libc.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
KARAWAN_ROOT="$(dirname "$SCRIPT_DIR")"
OUTPUT_DIR="${KARAWAN_ROOT}/Wuka/Platforms/Android/android"

# --- Configuration ---
OPENAL_REPO="https://github.com/kcat/openal-soft.git"
OPENAL_TAG="1.24.3"  # Match or exceed Silk.NET's bundled version
ANDROID_MIN_API=26    # Must match Wuka's SupportedOSPlatformVersion
BUILD_DIR="/tmp/openal-android-build"

# NDK discovery: ANDROID_NDK_HOME > ANDROID_NDK_ROOT > default path
NDK_ROOT="${ANDROID_NDK_HOME:-${ANDROID_NDK_ROOT:-${HOME}/Android/Sdk/ndk/27.2.12479018}}"
TOOLCHAIN="${NDK_ROOT}/build/cmake/android.toolchain.cmake"
STRIP="${NDK_ROOT}/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-strip"

if [ ! -f "$TOOLCHAIN" ]; then
    echo "ERROR: NDK toolchain not found at ${TOOLCHAIN}"
    echo "Set ANDROID_NDK_HOME to your NDK installation directory."
    exit 1
fi

echo "NDK:     ${NDK_ROOT}"
echo "Output:  ${OUTPUT_DIR}"
echo "OpenAL:  ${OPENAL_TAG}"
echo ""

# --- Clone / update source ---
if [ -d "${BUILD_DIR}/openal-soft" ]; then
    echo "=== Updating existing OpenAL Soft checkout ==="
    cd "${BUILD_DIR}/openal-soft"
    git fetch --tags
else
    echo "=== Cloning OpenAL Soft ==="
    mkdir -p "${BUILD_DIR}"
    git clone "${OPENAL_REPO}" "${BUILD_DIR}/openal-soft"
    cd "${BUILD_DIR}/openal-soft"
fi
git checkout "${OPENAL_TAG}"

# --- Build function ---
build_arch() {
    local ABI=$1
    local BUILD="${BUILD_DIR}/build_${ABI}"

    echo ""
    echo "=== Building for ${ABI} ==="

    rm -rf "${BUILD}"
    mkdir -p "${BUILD}"
    cd "${BUILD}"

    cmake "${BUILD_DIR}/openal-soft" \
        -DCMAKE_TOOLCHAIN_FILE="${TOOLCHAIN}" \
        -DANDROID_ABI="${ABI}" \
        -DANDROID_PLATFORM="${ANDROID_MIN_API}" \
        -DANDROID_STL=c++_shared \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_SHARED_LINKER_FLAGS="-Wl,-z,max-page-size=16384" \
        -DALSOFT_UTILS=OFF \
        -DALSOFT_EXAMPLES=OFF \
        -DALSOFT_INSTALL=OFF

    make -j"$(nproc)"

    echo "--- Stripping ${ABI}/libopenal.so ---"
    "${STRIP}" libopenal.so

    local DEST="${OUTPUT_DIR}/${ABI}"
    mkdir -p "${DEST}"
    cp libopenal.so "${DEST}/libopenal.so"

    echo "--- Installed: ${DEST}/libopenal.so ($(stat -c%s "${DEST}/libopenal.so") bytes) ---"
}

# --- Build both architectures ---
build_arch arm64-v8a
build_arch armeabi-v7a

# --- Verify ---
echo ""
echo "=== Verification ==="
for ABI in arm64-v8a armeabi-v7a; do
    echo ""
    echo "--- ${ABI} ---"
    file "${OUTPUT_DIR}/${ABI}/libopenal.so"
    readelf -d "${OUTPUT_DIR}/${ABI}/libopenal.so" 2>/dev/null | grep NEEDED || true
    readelf -l "${OUTPUT_DIR}/${ABI}/libopenal.so" 2>/dev/null | grep -A1 "LOAD" | head -6 || true
done

echo ""
echo "Done. Copy complete."
