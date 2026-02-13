#!/bin/bash
#
# Test: taxi quest smoke test
#
# Verifies the taxi quest can be auto-triggered via the quest.autoTrigger
# setting and that it emits a quest.triggered event.
#
# Requires: game builds, display (creates a window), ~3 minutes max
#
# Usage:
#   ./tests/run-taxi-quest-smoke.sh
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

COMMON_ARGS="-- --rwpath $RWPATH --setting debug.option.autoLogin=local --setting quest.autoTrigger=nogame.quests.Taxi.Quest"

echo ""
echo "=== Taxi Quest Smoke Test ==="
JOYCE_TEST_SCRIPT=tests/taxi-quest-smoke.json \
    dotnet run --no-build --project Karawan/Karawan.csproj $COMMON_ARGS
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
    echo ""
    echo "FAIL: Taxi quest smoke test failed with exit code $EXIT_CODE"
    echo "  The taxi quest was not triggered successfully."
    exit 1
fi

echo ""
echo "PASS: Taxi quest smoke test passed."
echo "  Quest auto-triggered, quest.triggered event observed."
exit 0
