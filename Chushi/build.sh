#!/bin/bash
#
# TFM is READ FROM THE CSPROJ, never hardcoded - see Tooling/Cmdline/build.sh for the two
# failures this prevents: NETSDK1005 (looks like a restore or SDK problem, is neither), and
# the silent exit-1-with-no-output on macOS if the extraction uses GNU-only `\?` instead of
# a POSIX ERE under `sed -E`.
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

# `|| TFM=""` stops a failed pipeline from tripping `set -e` here, so the guard gets to report it.
TFM="$(sed -nE 's:.*<TargetFrameworks?>([^<]*).*:\1:p' Chushi.csproj \
       | tr ';' '\n' | grep -E '^net[0-9]' | head -1)" || TFM=""
[ -n "$TFM" ] || { echo "could not read a net* TargetFramework from Chushi.csproj" >&2; exit 1; }
echo "building Chushi for $TFM"

dotnet build   -c Debug -f "$TFM" --sc
dotnet publish -c Debug -f "$TFM" --sc
