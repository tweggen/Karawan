#!/bin/bash
#
# TFM is READ FROM THE CSPROJ, never hardcoded.
#
# It used to say `-f net9.0`. When the tree retargeted to .NET 10 the csproj changed and
# this did not, so restore wrote net10.0 targets while the build asked for net9.0 and the
# SDK reported:
#
#   NETSDK1005: Assets file ... doesn't have a target for 'net9.0'
#
# which reads like a broken restore or a missing SDK - it is neither, and installing a
# newer SDK cannot fix it, because the wrong framework is being requested explicitly.
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

TFM="$(sed -n 's:.*<TargetFrameworks\?>\([^<]*\).*:\1:p' Cmdline.csproj \
       | tr ';' '\n' | grep -E '^net[0-9]' | head -1)"
[ -n "$TFM" ] || { echo "could not read a net* TargetFramework from Cmdline.csproj" >&2; exit 1; }
echo "building Cmdline for $TFM"

dotnet build-server shutdown
dotnet build   -c Debug -f "$TFM"         --sc
dotnet build   -c Debug -f netstandard2.0 --sc
dotnet publish -c Debug -f "$TFM"         --sc
dotnet build-server shutdown
