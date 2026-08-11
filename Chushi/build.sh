#!/bin/bash
#
# TFM is READ FROM THE CSPROJ, never hardcoded - see Tooling/Cmdline/build.sh for the
# failure this prevents (NETSDK1005, which looks like a restore or SDK problem and is not).
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

TFM="$(sed -n 's:.*<TargetFrameworks\?>\([^<]*\).*:\1:p' Chushi.csproj \
       | tr ';' '\n' | grep -E '^net[0-9]' | head -1)"
[ -n "$TFM" ] || { echo "could not read a net* TargetFramework from Chushi.csproj" >&2; exit 1; }
echo "building Chushi for $TFM"

dotnet build   -c Debug -f "$TFM" --sc
dotnet publish -c Debug -f "$TFM" --sc
