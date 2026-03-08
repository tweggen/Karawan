#!/bin/bash
# Build Assimp for Android (arm64-v8a + armeabi-v7a)
#
# Host: Linux x64
# Prerequisites: Android NDK r27+, CMake, git
#
# The resulting libassimp.so + libc++_shared.so files go into:
#   Wuka/Platforms/Android/android/arm64-v8a/
#   Wuka/Platforms/Android/android/armeabi-v7a/
#
# Why we build this ourselves:
#   Silk.NET.Assimp / Ultz.Native.Assimp don't ship Android native libs.
#   We also need 16KB page alignment for Android 16+ and want stripped
#   binaries to keep APK size reasonable.
#
# Note: libc++_shared.so comes from the NDK, not from Assimp itself.
#   It's installed alongside libassimp.so because Assimp links against it
#   and ANDROID_STL=c++_shared means it must be bundled in the APK.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
KARAWAN_ROOT="$(dirname "$SCRIPT_DIR")"
OUTPUT_DIR="${KARAWAN_ROOT}/Wuka/Platforms/Android/android"

# --- Configuration ---
ASSIMP_REPO="https://github.com/assimp/assimp.git"
ASSIMP_TAG="v5.4.3"   # Adjust to desired version
ANDROID_MIN_API=26
BUILD_DIR="/tmp/assimp-android-build"

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
echo "Assimp:  ${ASSIMP_TAG}"
echo ""

# --- Clone / update source ---
if [ -d "${BUILD_DIR}/assimp" ]; then
    echo "=== Updating existing Assimp checkout ==="
    cd "${BUILD_DIR}/assimp"
    git fetch --tags
else
    echo "=== Cloning Assimp ==="
    mkdir -p "${BUILD_DIR}"
    git clone "${ASSIMP_REPO}" "${BUILD_DIR}/assimp"
    cd "${BUILD_DIR}/assimp"
fi
git checkout "${ASSIMP_TAG}"

# --- Build function ---
build_arch() {
    local ABI=$1
    local BUILD="${BUILD_DIR}/build_${ABI}"

    echo ""
    echo "=== Building for ${ABI} ==="

    rm -rf "${BUILD}"
    mkdir -p "${BUILD}"
    cd "${BUILD}"

    cmake "${BUILD_DIR}/assimp" \
        -DCMAKE_TOOLCHAIN_FILE="${TOOLCHAIN}" \
        -DANDROID_ABI="${ABI}" \
        -DANDROID_PLATFORM="${ANDROID_MIN_API}" \
        -DANDROID_STL=c++_shared \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_SHARED_LINKER_FLAGS="-Wl,-z,max-page-size=16384" \
        -DASSIMP_BUILD_TESTS=OFF \
        -DASSIMP_BUILD_ASSIMP_TOOLS=OFF \
        -DASSIMP_BUILD_SAMPLES=OFF \
        -DASSIMP_INSTALL=OFF \
        -DASSIMP_BUILD_ZLIB=ON

    make -j"$(nproc)"

    echo "--- Stripping ${ABI}/libassimp.so ---"
    "${STRIP}" bin/libassimp.so

    local DEST="${OUTPUT_DIR}/${ABI}"
    mkdir -p "${DEST}"
    cp bin/libassimp.so "${DEST}/libassimp.so"
    echo "--- Installed: ${DEST}/libassimp.so ($(stat -c%s "${DEST}/libassimp.so") bytes) ---"

    # Copy libc++_shared.so from NDK (matches the STL used during build)
    local STL_LIB
    if [ "${ABI}" = "arm64-v8a" ]; then
        STL_LIB="${NDK_ROOT}/toolchains/llvm/prebuilt/linux-x86_64/sysroot/usr/lib/aarch64-linux-android/libc++_shared.so"
    elif [ "${ABI}" = "armeabi-v7a" ]; then
        STL_LIB="${NDK_ROOT}/toolchains/llvm/prebuilt/linux-x86_64/sysroot/usr/lib/arm-linux-androideabi/libc++_shared.so"
    fi

    if [ -f "${STL_LIB}" ]; then
        cp "${STL_LIB}" "${DEST}/libc++_shared.so"
        echo "--- Installed: ${DEST}/libc++_shared.so ($(stat -c%s "${DEST}/libc++_shared.so") bytes) ---"
    else
        echo "WARNING: libc++_shared.so not found at ${STL_LIB}"
        echo "         You may need to adjust the path for your NDK version."
    fi
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
    file "${OUTPUT_DIR}/${ABI}/libassimp.so"
    ls -la "${OUTPUT_DIR}/${ABI}/libassimp.so" "${OUTPUT_DIR}/${ABI}/libc++_shared.so"
done

echo ""
echo "Done. Copy complete."
