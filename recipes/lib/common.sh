# Shared helpers for the native build recipes. Sourced, not executed.
#
# One script per library, one target per invocation, all six targets driven by the
# same code path - see recipes/BUILD_NOTES.md.

# shellcheck shell=bash

RECIPES_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
KARAWAN_ROOT="$(dirname "${RECIPES_DIR}")"

# shellcheck source=/dev/null
. "${RECIPES_DIR}/versions.env"

die() { echo "ERROR: $*" >&2; exit 1; }
info() { echo "==> $*"; }

ALL_TARGETS="linux-x64 win-x64 osx-arm64 osx-x64 android-arm64-v8a android-armeabi-v7a"

# Android settings. The NDK revision itself comes from the environment (CI) or from a
# local ANDROID_NDK_HOME, because it is a toolchain, not a source pin.
ANDROID_MIN_API="${ANDROID_MIN_API:-26}"

target_kind() {
    case "$1" in
        linux-x64)            echo linux ;;
        win-x64)              echo windows ;;
        osx-arm64|osx-x64)    echo macos ;;
        android-*)            echo android ;;
        *) die "unknown target '$1' (expected one of: ${ALL_TARGETS})" ;;
    esac
}

android_abi() {
    case "$1" in
        android-arm64-v8a)   echo arm64-v8a ;;
        android-armeabi-v7a) echo armeabi-v7a ;;
        *) die "not an android target: $1" ;;
    esac
}

macos_arch() {
    case "$1" in
        osx-arm64) echo arm64 ;;
        osx-x64)   echo x86_64 ;;
        *) die "not a macos target: $1" ;;
    esac
}

# Locate the NDK. CI exports ANDROID_NDK_HOME; locally either that or ANDROID_NDK_ROOT.
ndk_root() {
    local ndk="${ANDROID_NDK_HOME:-${ANDROID_NDK_ROOT:-}}"
    [ -n "${ndk}" ] || die "set ANDROID_NDK_HOME to your NDK (r27+ required for 16 KB alignment)"
    [ -f "${ndk}/build/cmake/android.toolchain.cmake" ] || die "no NDK toolchain file under ${ndk}"
    echo "${ndk}"
}

ndk_bin() {
    local ndk host
    ndk="$(ndk_root)"
    case "$(uname -s)" in
        Linux*)  host=linux-x86_64 ;;
        Darwin*) host=darwin-x86_64 ;;
        *)       host=windows-x86_64 ;;
    esac
    echo "${ndk}/toolchains/llvm/prebuilt/${host}/bin"
}

# Check out an upstream repo at an exact tag and REFUSE to continue if the resulting
# commit is not the pinned SHA. A tag alone is not a pin: upstream can move it.
fetch_pinned() {
    local repo="$1" tag="$2" commit="$3" dest="$4"

    if [ ! -d "${dest}/.git" ]; then
        info "cloning ${repo} @ ${tag}"
        rm -rf "${dest}"
        git clone --quiet --depth 1 --branch "${tag}" "${repo}" "${dest}"
    else
        info "reusing existing checkout at ${dest}"
        git -C "${dest}" fetch --quiet --depth 1 origin "refs/tags/${tag}:refs/tags/${tag}" || true
        git -C "${dest}" checkout --quiet "${tag}"
    fi

    local actual
    actual="$(git -C "${dest}" rev-parse HEAD)"
    if [ "${actual}" != "${commit}" ]; then
        die "pin mismatch for ${repo} ${tag}:
  expected ${commit}
  actual   ${actual}
The tag has moved upstream, or the pin in recipes/versions.env is wrong.
Do NOT 'fix' this by updating the SHA without understanding why it changed."
    fi
    info "verified ${repo} @ ${tag} = ${commit}"
}

# Configure + build with CMake. Generator choice is per-platform: Ninja where it is
# reliably present, and the Visual Studio generator on Windows so the recipe does not
# depend on ninja being on PATH inside a Developer Command Prompt.
cmake_build() {
    local src="$1" build="$2" kind="$3"
    shift 3

    local -a gen=()
    if [ "${kind}" = "windows" ]; then
        gen=(-G "Visual Studio 17 2022" -A x64)
    elif command -v ninja >/dev/null 2>&1; then
        gen=(-G Ninja)
    fi

    cmake -S "${src}" -B "${build}" "${gen[@]}" \
        -DCMAKE_BUILD_TYPE=Release \
        "$@"

    cmake --build "${build}" --config Release --parallel
}

# Print the LOAD alignment of an ELF and fail if a 64-bit Android library is not
# 16 KB aligned. 32-bit ABIs are exempt: devices with 16 KB pages are 64-bit only
# (docs/roadmap/proposed/WP-0.0-FINDINGS.md).
verify_android_alignment() {
    local so="$1" abi="$2"
    local readelf
    readelf="$(ndk_bin)/llvm-readelf"

    echo "--- $(basename "${so}") program headers ---"
    "${readelf}" -lW "${so}" | grep LOAD || true

    if [ "${abi}" = "armeabi-v7a" ]; then
        info "SKIP 16 KB check: ${abi} is 32-bit, not covered by the Play requirement"
        return 0
    fi

    local bad
    bad="$("${readelf}" -lW "${so}" | awk '/LOAD/ && $NF != "0x4000"' | wc -l)"
    [ "${bad}" -eq 0 ] || die "${so}: ${bad} LOAD segment(s) are not 16 KB aligned"
    info "OK: all LOAD segments of $(basename "${so}") are 16 KB aligned"
}

# Report what a produced binary actually is, so a wrong-ABI artifact is caught in the
# log rather than at load time on a device.
describe_artifact() {
    local f="$1"
    echo "--- $(basename "${f}") ---"
    ls -l "${f}"
    case "$(uname -s)" in
        Darwin*) file "${f}"; lipo -info "${f}" 2>/dev/null || true ;;
        *)       file "${f}" 2>/dev/null || true ;;
    esac
}
