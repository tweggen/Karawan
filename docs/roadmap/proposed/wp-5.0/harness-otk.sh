#!/usr/bin/env bash
# WP-5.0b harness: compile the OpenTK port of the sample, then measure churn against the
# Silk baseline. Same sample, same call sites, different binding.
set -euo pipefail
S="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

rm -rf "$S/build-otk" "$S/build-otk-diff"
mkdir -p "$S/build-otk" "$S/build-otk-diff"
cp "$S/sample/CallSites.OpenTK.cs" "$S/build-otk/CallSites.cs"

cat > "$S/build-otk/otk.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Nullable>disable</Nullable>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="OpenTK.Graphics" Version="5.0.0-pre.15" />
  </ItemGroup>
</Project>
EOF

echo "=============================================================="
echo "OPENTK: same call sites, ported"
echo "=============================================================="
dotnet build "$S/build-otk/otk.csproj" -v q --nologo 2>&1 | grep -E "error|Build succeeded" | head -10

echo
echo "=============================================================="
echo "AC-5.0b: churn, Silk baseline -> OpenTK"
echo "=============================================================="
# Compare the bodies, ignoring the leading comment blocks of each file.
sed -n '/^using System;/,$p' "$S/sample/CallSites.cs"        > "$S/build-otk-diff/a.cs"
sed -n '/^using System;/,$p' "$S/sample/CallSites.OpenTK.cs" > "$S/build-otk-diff/b.cs"
diff -u "$S/build-otk-diff/a.cs" "$S/build-otk-diff/b.cs" > "$S/build-otk-diff/churn.diff" || true
cat "$S/build-otk-diff/churn.diff"

ADDED=$(grep -cE '^\+[^+]' "$S/build-otk-diff/churn.diff" || true)
REMOVED=$(grep -cE '^-[^-]' "$S/build-otk-diff/churn.diff" || true)
TOTAL=$(wc -l < "$S/build-otk-diff/a.cs")
echo
echo "  lines removed : ${REMOVED}"
echo "  lines added   : ${ADDED}"
echo "  sample size   : ${TOTAL} lines"
