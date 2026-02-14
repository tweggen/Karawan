#!/bin/bash
#
# Test: taxi markers visible
#
# Verifies that taxi NPC markers are spawned after cluster generation.
# Catches regressions where markers fail to appear (e.g., event timing issues).
#
# Requires: game builds, display (creates a window), ~3 minutes max
#
# Usage:
#   ./tests/run-taxi-markers-visible.sh
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
echo "=== Taxi Markers Visible Test ==="
JOYCE_TEST_SCRIPT=tests/taxi-markers-visible.json \
    dotnet run --no-build --project Karawan/Karawan.csproj $COMMON_ARGS
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
    echo ""
    echo "FAIL: Taxi markers visible test failed with exit code $EXIT_CODE"
    echo "  No taxi NPC markers were spawned after cluster generation."
    exit 1
fi

echo ""
echo "PASS: Taxi markers visible test passed."
echo "  Taxi NPC markers appeared after cluster completion."
exit 0
