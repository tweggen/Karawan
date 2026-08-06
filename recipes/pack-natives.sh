#!/usr/bin/env bash
#
# Assemble the CI matrix's per-target artifacts into a single NuGet package layout,
# plus a build manifest. WP-1.4.
#
# Usage:
#   recipes/pack-natives.sh --artifacts <dir> --stage <dir> --version <v> [--run <id>] [--commit <sha>]
#
# <artifacts> is the directory actions/download-artifact produced, i.e. one
# subdirectory per matrix target:
#
#   artifacts/natives-linux-x64/out/openal/libopenal.so
#   artifacts/natives-android-arm64-v8a/out/sdl3/libSDL3.so
#   ...
#
# Two consumption paths come out of this, because .NET needs both:
#
#   runtimes/<rid>/native/<lib>        desktop RID-specific natives, resolved by the
#                                      .NET host at run time
#   lib/<android-tfm>/*.aar            Android, where .NET Android extracts jni/<abi>/
#                                      out of an AAR into the APK
#
# The AAR is assembled here as a plain zip. An AAR is a zip - it needs no Java and no
# Gradle to build, only the right entries. This is the same structure Silk.NET.Windowing.Sdl
# ships (verified while measuring it in WP-0.0).

set -euo pipefail

. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib/common.sh"

ARTIFACTS=""
STAGE=""
VERSION=""
RUN_ID="local"
COMMIT="unknown"

while [ $# -gt 0 ]; do
    case "$1" in
        --artifacts) ARTIFACTS="$2"; shift 2 ;;
        --stage)     STAGE="$2"; shift 2 ;;
        --version)   VERSION="$2"; shift 2 ;;
        --run)       RUN_ID="$2"; shift 2 ;;
        --commit)    COMMIT="$2"; shift 2 ;;
        *) die "unknown argument '$1'" ;;
    esac
done

[ -n "${ARTIFACTS}" ] || die "--artifacts is required"
[ -n "${STAGE}" ]     || die "--stage is required"
[ -n "${VERSION}" ]   || die "--version is required"
[ -d "${ARTIFACTS}" ] || die "no such artifacts directory: ${ARTIFACTS}"

rm -rf "${STAGE}"
mkdir -p "${STAGE}/runtimes" "${STAGE}/aar/jni" "${STAGE}/manifest"

# Desktop target -> .NET RID. Same string in both directions today, but keep the
# mapping explicit so a rename upstream does not silently produce an empty runtimes/.
rid_for() {
    case "$1" in
        linux-x64) echo linux-x64 ;;
        win-x64)   echo win-x64 ;;
        osx-arm64) echo osx-arm64 ;;
        osx-x64)   echo osx-x64 ;;
        *) return 1 ;;
    esac
}

sha256_of() {
    if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1
    else shasum -a 256 "$1" | cut -d' ' -f1; fi
}

MANIFEST="${STAGE}/manifest/build-manifest.json"
{
    echo '{'
    echo "  \"package\": \"Karawan.Natives\","
    echo "  \"version\": \"${VERSION}\","
    echo "  \"builtFromCommit\": \"${COMMIT}\","
    echo "  \"workflowRun\": \"${RUN_ID}\","
    echo '  "upstream": {'
    echo "    \"openal-soft\": { \"tag\": \"${OPENAL_TAG}\", \"commit\": \"${OPENAL_COMMIT}\" },"
    echo "    \"SDL\":         { \"tag\": \"${SDL3_TAG}\", \"commit\": \"${SDL3_COMMIT}\" }"
    echo '  },'
    echo '  "targets": ['
} > "${MANIFEST}"

FOUND_ANY=0
FIRST_TARGET=1

for dir in "${ARTIFACTS}"/natives-*; do
    [ -d "${dir}" ] || continue
    target="$(basename "${dir}")"
    target="${target#natives-}"

    # Every produced library for this target, wherever the recipe put it.
    mapfile -t libs < <(find "${dir}" -type f \( -name '*.so' -o -name '*.dylib' -o -name '*.dll' \) | sort)
    [ "${#libs[@]}" -gt 0 ] || die "target ${target} produced no libraries - refusing to package a hole"

    if [ "${FIRST_TARGET}" -eq 0 ]; then echo "    ," >> "${MANIFEST}"; fi
    FIRST_TARGET=0
    {
        echo "    {"
        echo "      \"target\": \"${target}\","
        echo "      \"toolchain\": ["
    } >> "${MANIFEST}"

    if [ -f "${dir}/toolchain.txt" ]; then
        first_line=1
        while IFS= read -r line; do
            [ -n "${line}" ] || continue
            if [ "${first_line}" -eq 0 ]; then echo "        ," >> "${MANIFEST}"; fi
            first_line=0
            printf '        %s\n' "\"$(printf '%s' "${line}" | sed 's/\\/\\\\/g; s/"/\\"/g')\"" >> "${MANIFEST}"
        done < "${dir}/toolchain.txt"
    fi
    { echo "      ],"; echo "      \"files\": ["; } >> "${MANIFEST}"

    if rid="$(rid_for "${target}")"; then
        mkdir -p "${STAGE}/runtimes/${rid}/native"
        first_file=1
        for lib in "${libs[@]}"; do
            cp "${lib}" "${STAGE}/runtimes/${rid}/native/"
            if [ "${first_file}" -eq 0 ]; then echo "        ," >> "${MANIFEST}"; fi
            first_file=0
            echo "        { \"path\": \"runtimes/${rid}/native/$(basename "${lib}")\", \"sha256\": \"$(sha256_of "${lib}")\" }" >> "${MANIFEST}"
        done
        info "${target} -> runtimes/${rid}/native (${#libs[@]} files)"
        FOUND_ANY=1
    else
        # Android: everything goes into jni/<abi>/ inside the AAR.
        abi="${target#android-}"
        mkdir -p "${STAGE}/aar/jni/${abi}"
        first_file=1
        for lib in "${libs[@]}"; do
            cp "${lib}" "${STAGE}/aar/jni/${abi}/"
            if [ "${first_file}" -eq 0 ]; then echo "        ," >> "${MANIFEST}"; fi
            first_file=0
            echo "        { \"path\": \"aar:jni/${abi}/$(basename "${lib}")\", \"sha256\": \"$(sha256_of "${lib}")\" }" >> "${MANIFEST}"
        done
        info "${target} -> aar jni/${abi} (${#libs[@]} files)"
        FOUND_ANY=1
    fi

    { echo "      ]"; echo "    }"; } >> "${MANIFEST}"
done

[ "${FOUND_ANY}" -eq 1 ] || die "no natives-* artifacts found under ${ARTIFACTS}"

{ echo '  ]'; echo '}'; } >> "${MANIFEST}"

# --- AAR --------------------------------------------------------------------

if [ -n "$(ls -A "${STAGE}/aar/jni" 2>/dev/null)" ]; then
    cat > "${STAGE}/aar/AndroidManifest.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<!-- Generated by recipes/pack-natives.sh. An AAR carrying only jni/ payload still
     needs a manifest to be a valid AAR. -->
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
    package="de.nassau_records.karawan.natives">
    <uses-sdk android:minSdkVersion="${ANDROID_MIN_API}" />
</manifest>
EOF
    mkdir -p "${STAGE}/aar-out"
    # Built with python rather than zip(1) deliberately. AC-1.5 wants byte-identical
    # artifacts across reruns, and zip(1) stamps each entry with its mtime, so two
    # identical inputs produce two different archives. Fixing the timestamp and sorting
    # the entries makes the AAR a pure function of its contents.
    python3 - "${STAGE}/aar" "${STAGE}/aar-out/Karawan.Natives.Android.aar" <<'PY'
import os, sys, zipfile

src, dest = sys.argv[1], sys.argv[2]
entries = []
for root, dirs, files in os.walk(src):
    dirs.sort()
    for f in sorted(files):
        full = os.path.join(root, f)
        entries.append((os.path.relpath(full, src).replace(os.sep, '/'), full))
entries.sort()

# Fixed DOS epoch; any constant works, it just must not vary between runs.
FIXED = (1980, 1, 1, 0, 0, 0)
with zipfile.ZipFile(dest, 'w', zipfile.ZIP_DEFLATED) as z:
    for arcname, full in entries:
        zi = zipfile.ZipInfo(arcname, date_time=FIXED)
        zi.external_attr = 0o644 << 16
        zi.compress_type = zipfile.ZIP_DEFLATED
        with open(full, 'rb') as fh:
            z.writestr(zi, fh.read())
print(f"AAR: {dest} ({os.path.getsize(dest)} bytes, {len(entries)} entries)")
for arcname, _ in entries:
    print(f"  {arcname}")
PY
fi

echo ""
info "staged package layout:"
find "${STAGE}/runtimes" "${STAGE}/aar-out" "${STAGE}/manifest" -type f 2>/dev/null | sort
echo ""
info "build manifest:"
cat "${MANIFEST}"
