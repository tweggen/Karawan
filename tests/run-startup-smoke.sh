#!/bin/bash
#
# Test: startup smoke test
#
# Verifies the critical startup path (models/tests/startup-smoke.json):
#   root scene kicks off -> main narration starts -> first narration node reached
#
# Requires: game builds, display (creates a window), ~1 minute
#
# Usage:
#   ./tests/run-startup-smoke.sh
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

# Auto-login locally (bypass the login menu) and use an isolated save dir
COMMON_ARGS="-- --rwpath $RWPATH --setting debug.option.autoLogin=local"

echo ""
echo "=== Startup Smoke Test ==="
JOYCE_TEST_SCRIPT=tests/startup-smoke.json \
    dotnet run --no-build --project Karawan/Karawan.csproj $COMMON_ARGS
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
    echo ""
    echo "FAIL: startup smoke test failed with exit code $EXIT_CODE"
    exit 1
fi

echo ""
echo "PASS: startup smoke test passed."
echo "  Root scene kicked off, narration started and reached its first node."
exit 0
