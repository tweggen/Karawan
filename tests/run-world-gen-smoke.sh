#!/bin/bash
#
# Test: world generation pipeline smoke test
#
# Verifies the end-to-end world gen chain:
#   Engine boot -> Mix config -> MetaGen init -> cluster creation ->
#   fragment loading -> street generation -> cluster operators complete
#
# Requires: game builds, display (creates a window), ~3 minutes max
#
# Usage:
#   ./tests/run-world-gen-smoke.sh
#

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_DIR"

RWPATH=$(mktemp -d)
echo "Test rwpath: $RWPATH"

cleanup() {
    rm -rf "$RWPATH"
}
trap cleanup EXIT

COMMON_ARGS="-- --rwpath $RWPATH --setting debug.option.autoLogin=local"

echo ""
echo "=== World Generation Smoke Test ==="
JOYCE_TEST_SCRIPT=tests/world-gen-smoke.json \
    dotnet run --no-build --project Karawan/Karawan.csproj $COMMON_ARGS
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
    echo ""
    echo "FAIL: World generation smoke test failed with exit code $EXIT_CODE"
    echo "  The world generation pipeline did not complete successfully."
    exit 1
fi

echo ""
echo "PASS: World generation smoke test passed."
echo "  Engine booted, clusters generated, streets built, operators completed."
exit 0
