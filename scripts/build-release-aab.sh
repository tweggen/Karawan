#!/usr/bin/env bash
#
# Build a SIGNED release AAB for the Play Store, from the command line, on either
# Windows Git Bash or macOS. No Visual Studio, no Rider.
#
# WHY THIS SCRIPT EXISTS
#
# `dotnet build -c Release` already produces a file called
# "de.nassau_records.silicondesert2-Signed.aab". That name is a trap. With no signing
# configuration in the project - and there is none - the .NET Android SDK signs with an
# auto-generated DEBUG keystore:
#
#     Windows: %LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore
#     macOS:   ~/.local/share/Xamarin/Mono for Android/debug.keystore
#
# "-Signed" means "a signature was applied", not "the right one". Play rejects it, and
# nothing in the build output says so.
#
# THE ONE THAT REALLY HURTS
#
# Silicon Desert 2 is already published, so an UPLOAD KEY already exists. Play rejects a
# bundle signed with any other key, and recovering means an upload-key reset request to
# Google with a waiting period. So this script does not just sign - it verifies that the
# certificate inside the finished AAB matches the certificate in the keystore alias you
# named, and prints the SHA-256 fingerprint so you can compare it against
# Play Console -> App integrity -> Upload key certificate.
#
# OTHER TRAPS ENCODED HERE
#
#   * Stale obj/Release once produced a phantom ClassNotFoundException that cost a day
#     (the incremental build never handed d8 the SDL3 Java glue). This script cleans by
#     default; --no-clean if you know what you are doing.
#   * versionCode must increase monotonically or Play rejects the upload. The script
#     prints it prominently before building, and refuses to run if it did not change
#     since the last git tag it can find - see --allow-same-version.
#   * Passwords never appear in argv, where `ps` can read them. They are exported as
#     MSBuild properties via the environment instead, the same mechanism the repo
#     already uses for SiblingRoot in Directory.Build.props.
#   * Git Bash needs Windows-shaped paths for MSBuild; cygpath handles that here.
#   * keytool is looked for in the Android SDK's and Android Studio's bundled JDKs, not
#     only on the PATH. A machine set up for Android development nearly always HAS a
#     JDK and nearly never has it on the PATH - `command -v keytool` alone reports "no
#     JDK" on a box carrying a perfectly good JDK 17, and sends you off installing a
#     second one. Set JAVA_HOME to override the search.
#
# USAGE
#
#   scripts/build-release-aab.sh --keystore ~/keys/nassau-upload.keystore
#   scripts/build-release-aab.sh -k ../keys/upload.jks -a nassau_release --mono
#
set -euo pipefail

ALIAS_DEFAULT="nassau_release"

KEYSTORE=""
ALIAS="$ALIAS_DEFAULT"
DO_CLEAN=1
USE_CORECLR=1
ALLOW_SAME_VERSION=0

_die()  { printf '\nERROR: %s\n' "$*" >&2; exit 1; }
_info() { printf '  %s\n' "$*"; }
_head() { printf '\n=== %s ===\n' "$*"; }

usage() {
    sed -n '2,40p' "$0" | sed 's/^# \{0,1\}//'
    cat <<EOF

OPTIONS
  -k, --keystore PATH    Keystore to sign with. REQUIRED.
  -a, --alias NAME       Key alias inside the keystore. Default: $ALIAS_DEFAULT
      --mono             Build with Mono instead of the default CoreCLR runtime.
      --no-clean         Do not wipe obj/Release and bin/Release first.
      --allow-same-version
                         Proceed even if versionCode looks unchanged since the last tag.
  -h, --help             This text.
EOF
    exit 0
}

while [ $# -gt 0 ]; do
    case "$1" in
        -k|--keystore)          KEYSTORE="${2:-}"; shift 2 ;;
        -a|--alias)             ALIAS="${2:-}"; shift 2 ;;
        --mono)                 USE_CORECLR=0; shift ;;
        --no-clean)             DO_CLEAN=0; shift ;;
        --allow-same-version)   ALLOW_SAME_VERSION=1; shift ;;
        -h|--help)              usage ;;
        *) _die "unknown argument: $1  (try --help)" ;;
    esac
done

[ -n "$KEYSTORE" ] || _die "--keystore is required. See --help."
[ -f "$KEYSTORE" ] || _die "keystore not found: $KEYSTORE"

# ---------------------------------------------------------------- environment ----

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

CSPROJ="Wuka/Wuka.csproj"
MANIFEST="Wuka/Platforms/Android/AndroidManifest.xml"
[ -f "$CSPROJ" ]   || _die "not in the Karawan repo? missing $CSPROJ"
[ -f "$MANIFEST" ] || _die "missing $MANIFEST"

case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) IS_WINDOWS=1 ;;
    *)                    IS_WINDOWS=0 ;;
esac

# MSBuild on Windows cannot read a /c/Users/... path. Hand it a native one.
if [ "$IS_WINDOWS" -eq 1 ] && command -v cygpath >/dev/null 2>&1; then
    KEYSTORE_ARG="$(cygpath -w "$KEYSTORE")"
else
    KEYSTORE_ARG="$(cd "$(dirname "$KEYSTORE")" && pwd)/$(basename "$KEYSTORE")"
fi

command -v dotnet >/dev/null 2>&1 || _die "dotnet not on PATH."

# Find keytool. Do NOT just test the PATH: on a machine set up for Android development
# there is almost always a JDK present and almost never on the PATH - the Android SDK
# and Android Studio each bundle one. Checking `command -v keytool` alone reports "no
# JDK" on a box that has a perfectly good JDK 17, which is a wrong answer that sends
# someone off to install a second one.
KEYTOOL=""
_try_keytool() { [ -x "$1" ] && KEYTOOL="$1" && return 0; return 1; }

if command -v keytool >/dev/null 2>&1; then
    KEYTOOL="keytool"
elif [ -n "${JAVA_HOME:-}" ] && _try_keytool "$JAVA_HOME/bin/keytool"; then :
elif [ -n "${JAVA_HOME:-}" ] && _try_keytool "$JAVA_HOME/bin/keytool.exe"; then :
else
    # Newest first, so a modern JDK wins over an ancient one.
    for _c in \
        "/c/Program Files (x86)/Android/openjdk"/*/bin/keytool.exe \
        "/c/Program Files/Android/openjdk"/*/bin/keytool.exe \
        "/c/Program Files/Android/Android Studio/jbr/bin/keytool.exe" \
        "/c/Program Files/Microsoft"/jdk*/bin/keytool.exe \
        "/c/Program Files/Java"/*/bin/keytool.exe \
        "/Applications/Android Studio.app/Contents/jbr/Contents/Home/bin/keytool" \
        "$HOME/Library/Java/JavaVirtualMachines"/*/Contents/Home/bin/keytool \
        "/Library/Java/JavaVirtualMachines"/*/Contents/Home/bin/keytool \
        "/opt/homebrew/opt/openjdk/bin/keytool" \
        "/usr/local/opt/openjdk/bin/keytool"
    do
        _try_keytool "$_c" && break
    done
    # macOS ships a resolver; ask it last, since it may point at a JRE-only install.
    if [ -z "$KEYTOOL" ] && [ -x /usr/libexec/java_home ]; then
        _jh="$(/usr/libexec/java_home 2>/dev/null || true)"
        [ -n "$_jh" ] && _try_keytool "$_jh/bin/keytool"
    fi
fi

[ -n "$KEYTOOL" ] || _die \
"no keytool found - a JDK is required to sign and to verify the result.
   Looked on PATH, in \$JAVA_HOME, and in the usual Android SDK / Android Studio
   bundled-JDK locations.
   macOS:   brew install --cask temurin
   Windows: the Android SDK usually ships one, e.g.
            C:\\Program Files (x86)\\Android\\openjdk\\jdk-17.x.x
            Set JAVA_HOME to it, or put its bin/ on PATH."

# TFM is read, not hardcoded, so a future retarget does not silently break this script.
TFM="$(sed -n 's:.*<TargetFramework>\(net[^<]*android[^<]*\)</TargetFramework>.*:\1:p' "$CSPROJ" | head -1)"
[ -n "$TFM" ] || _die "could not read the android TargetFramework from $CSPROJ"

VERSION_CODE="$(sed -n 's/.*android:versionCode="\([0-9]*\)".*/\1/p' "$MANIFEST" | head -1)"
VERSION_NAME="$(sed -n 's/.*android:versionName="\([^"]*\)".*/\1/p' "$MANIFEST" | head -1)"

_head "What is about to be built"
_info "repo          $REPO_ROOT"
_info "target        $TFM"
_info "runtime       $([ "$USE_CORECLR" -eq 1 ] && echo 'CoreCLR (default)' || echo 'Mono (--mono)')"
_info "versionCode   $VERSION_CODE"
_info "versionName   $VERSION_NAME"
_info "keystore      $KEYSTORE_ARG"
_info "alias         $ALIAS"

# versionCode must increase or Play rejects the upload. We cannot know what was last
# published, but a versionCode identical to the one at the last tag is a strong smell.
if [ "$ALLOW_SAME_VERSION" -eq 0 ] && git rev-parse --git-dir >/dev/null 2>&1; then
    LAST_TAG="$(git describe --tags --abbrev=0 2>/dev/null || true)"
    if [ -n "$LAST_TAG" ]; then
        PREV="$(git show "$LAST_TAG:$MANIFEST" 2>/dev/null \
                | sed -n 's/.*android:versionCode="\([0-9]*\)".*/\1/p' | head -1 || true)"
        if [ -n "$PREV" ] && [ "$PREV" -ge "$VERSION_CODE" ] 2>/dev/null; then
            _die "versionCode $VERSION_CODE is not greater than $PREV (at tag $LAST_TAG).
       Play requires versionCode to increase monotonically. Bump it in
       $MANIFEST, or pass --allow-same-version if you know this is fine."
        fi
    fi
fi

# ------------------------------------------------------------------ passwords ----

_head "Keystore credentials"
printf '  Store password for %s: ' "$(basename "$KEYSTORE")"
stty -echo 2>/dev/null || true
read -r STORE_PASS
stty echo 2>/dev/null || true
printf '\n'
[ -n "$STORE_PASS" ] || _die "empty store password."

printf '  Key password for alias "%s" (blank = same as store): ' "$ALIAS"
stty -echo 2>/dev/null || true
read -r KEY_PASS
stty echo 2>/dev/null || true
printf '\n'
[ -n "$KEY_PASS" ] || KEY_PASS="$STORE_PASS"

# Fail before a five-minute build rather than after it.
_head "Verifying the keystore and alias"
if ! KEYSTORE_CERT="$("$KEYTOOL" -list -v -keystore "$KEYSTORE" -alias "$ALIAS" \
                        -storepass "$STORE_PASS" 2>&1)"; then
    printf '%s\n' "$KEYSTORE_CERT" | head -5 >&2
    _die "could not read alias '$ALIAS' from the keystore (wrong password, or wrong alias).
       List what is actually in there with:
           \"$KEYTOOL\" -list -keystore \"$KEYSTORE\""
fi

EXPECTED_SHA="$(printf '%s\n' "$KEYSTORE_CERT" \
                | grep -i 'SHA256:' | head -1 | sed 's/.*SHA256: *//' | tr -d '[:space:]')"
[ -n "$EXPECTED_SHA" ] || _die "could not extract a SHA-256 fingerprint from the keystore."
_info "alias '$ALIAS' found"
_info "SHA-256  $EXPECTED_SHA"
printf '\n  Compare that against Play Console -> App integrity -> Upload key certificate.\n'
printf '  If it does not match, STOP: signing with the wrong key means an upload-key\n'
printf '  reset request to Google, not a rebuild.\n'

# --------------------------------------------------------------------- build ----

if [ "$DO_CLEAN" -eq 1 ]; then
    _head "Cleaning Release output"
    _info "rm -rf Wuka/obj/Release Wuka/bin/Release"
    rm -rf Wuka/obj/Release Wuka/bin/Release
fi

# Passwords go through the environment, NOT argv: MSBuild seeds properties from
# environment variables, so these arrive as $(AndroidSigningStorePass) etc. without
# ever being visible to `ps`.
export AndroidSigningStorePass="$STORE_PASS"
export AndroidSigningKeyPass="$KEY_PASS"

CORECLR_ARG="true"
[ "$USE_CORECLR" -eq 1 ] || CORECLR_ARG="false"

_head "Building signed release AAB"
set +e
dotnet publish "$CSPROJ" -c Release -f "$TFM" \
    -p:AndroidPackageFormats=aab \
    -p:AndroidKeyStore=true \
    -p:AndroidSigningKeyStore="$KEYSTORE_ARG" \
    -p:AndroidSigningKeyAlias="$ALIAS" \
    -p:WukaCoreClr="$CORECLR_ARG"
BUILD_RC=$?
set -e
unset AndroidSigningStorePass AndroidSigningKeyPass
[ $BUILD_RC -eq 0 ] || _die "build failed (exit $BUILD_RC)."

# -------------------------------------------------------------------- verify ----

AAB="$(find "Wuka/bin/Release/$TFM" -name '*-Signed.aab' 2>/dev/null | head -1)"
[ -n "$AAB" ] || AAB="$(find "Wuka/bin/Release/$TFM" -name '*.aab' 2>/dev/null | head -1)"
[ -n "$AAB" ] || _die "build succeeded but no .aab was produced under Wuka/bin/Release/$TFM"

_head "Verifying the finished AAB"

# The decisive check: is the bundle signed with the key we asked for, or with the
# debug key the SDK silently falls back to?
if ! AAB_CERT="$("$KEYTOOL" -printcert -jarfile "$AAB" 2>&1)"; then
    printf '%s\n' "$AAB_CERT" | head -5 >&2
    _die "could not read a certificate from $AAB - is it signed at all?"
fi
ACTUAL_SHA="$(printf '%s\n' "$AAB_CERT" \
              | grep -i 'SHA256:' | head -1 | sed 's/.*SHA256: *//' | tr -d '[:space:]')"

if [ "$ACTUAL_SHA" = "$EXPECTED_SHA" ]; then
    _info "signature OK - AAB certificate matches keystore alias '$ALIAS'"
else
    printf '\n' >&2
    printf '  expected (keystore) %s\n' "$EXPECTED_SHA" >&2
    printf '  actual   (aab)      %s\n' "$ACTUAL_SHA" >&2
    _die "the AAB is NOT signed with the key you specified.
       The usual cause is the SDK falling back to the debug keystore. Do NOT upload this."
fi

if command -v python >/dev/null 2>&1 && [ -f scripts/check-apk.py ]; then
    APK="$(find "Wuka/bin/Release/$TFM" -name '*-Signed.apk' 2>/dev/null | head -1)"
    if [ -n "$APK" ]; then
        _head "APK shape assertions"
        python scripts/check-apk.py "$APK" || _die "check-apk.py failed."
    fi
fi

AAB_SIZE="$(ls -l "$AAB" | awk '{print $5}')"
_head "Done"
_info "bundle        $AAB"
_info "size          $AAB_SIZE bytes"
_info "versionCode   $VERSION_CODE  ($VERSION_NAME)"
_info "signed with   $ALIAS  [$EXPECTED_SHA]"
printf '\n  Upload that file to Play Console. Nothing here has verified that the game RUNS -\n'
printf '  build shape and signature only. Launch it on a device first.\n\n'
