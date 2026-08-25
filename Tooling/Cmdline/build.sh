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
# The extraction MUST use `sed -E`. `\?` is a GNU extension to basic regular expressions
# and the BSD sed macOS ships does not have it - there it matches a literal `?`, so
# `<TargetFrameworks?>` never matches `<TargetFramework>`. sed then prints nothing, grep
# exits 1, and under `set -e -o pipefail` the ASSIGNMENT dies before the guard below can
# say why. Symptom on macOS: no output at all, exit 1, as if the script never ran.
# `-E` (POSIX ERE) is understood by both BSD sed and GNU sed.
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

# `|| TFM=""` stops a failed pipeline from tripping `set -e` here, so the guard gets to report it.
TFM="$(sed -nE 's:.*<TargetFrameworks?>([^<]*).*:\1:p' Cmdline.csproj \
       | tr ';' '\n' | grep -E '^net[0-9]' | head -1)" || TFM=""
[ -n "$TFM" ] || { echo "could not read a net* TargetFramework from Cmdline.csproj" >&2; exit 1; }
echo "building Cmdline for $TFM"

dotnet build-server shutdown
dotnet build   -c Debug -f "$TFM"         --sc
dotnet build   -c Debug -f netstandard2.0 --sc
dotnet publish -c Debug -f "$TFM"         --sc
dotnet build-server shutdown
