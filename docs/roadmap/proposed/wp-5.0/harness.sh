#!/usr/bin/env bash
#
# WP-5.0 harness. Compiles ONE call-site sample against two bindings and measures the
# difference between the two sources.
#
#   baseline  : Silk.NET.OpenGL 2.23.0 (what ships today)
#   candidate : Karawan.GL, generated from Khronos gl.xml
#
# The sample is shared verbatim. The only thing the harness varies is a single injected
# namespace-import line, so `diff` reports exactly the call-site churn claim 3 is about.

set -euo pipefail
S="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

rm -rf "$S/build"
mkdir -p "$S/build/baseline" "$S/build/candidate"

emit() {          # emit <outdir> <alias-namespace>
    local out="$1" ns="$2"
    { echo "using ${ns};"; cat "$S/sample/CallSites.cs"; } > "${out}/CallSites.cs"
}

emit "$S/build/baseline"  "Silk.NET.OpenGL"
emit "$S/build/candidate" "Karawan.Graphics.OpenGL"

cat > "$S/build/baseline/baseline.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Nullable>disable</Nullable>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Silk.NET.OpenGL" Version="2.23.0" />
  </ItemGroup>
</Project>
EOF

cat > "$S/build/candidate/candidate.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Nullable>disable</Nullable>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
EOF
cp "$S/Generated.cs" "$S/build/candidate/Generated.cs"

echo "=============================================================="
echo "BASELINE: sample + Silk.NET.OpenGL 2.23.0"
echo "=============================================================="
if dotnet build "$S/build/baseline/baseline.csproj" -v q --nologo 2>&1 | grep -E "error|Build succeeded" | head -10; then :; fi

echo
echo "=============================================================="
echo "CANDIDATE: same sample + bindings generated from gl.xml"
echo "=============================================================="
if dotnet build "$S/build/candidate/candidate.csproj" -v q --nologo 2>&1 | grep -E "error|Build succeeded" | head -10; then :; fi

echo
echo "=============================================================="
echo "AC-5.0: call-site churn"
echo "=============================================================="
echo "diff baseline/CallSites.cs candidate/CallSites.cs"
if diff "$S/build/baseline/CallSites.cs" "$S/build/candidate/CallSites.cs" > "$S/build/churn.diff"; then
    echo "  (identical)"
else
    cat "$S/build/churn.diff"
fi
CHANGED=$(diff "$S/build/baseline/CallSites.cs" "$S/build/candidate/CallSites.cs" | grep -cE '^[<>]' || true)
TOTAL=$(wc -l < "$S/build/baseline/CallSites.cs")
echo
echo "  changed lines : ${CHANGED}"
echo "  total lines   : ${TOTAL}"
echo "  call-site lines changed (excluding the injected using): $(( CHANGED - 2 ))"
