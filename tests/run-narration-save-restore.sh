#!/bin/bash
#
# Test: narration state survives save/load cycle
#
# Reproduces the bug where narration restarts from "intro" after loading
# a save, instead of resuming at the saved node. This causes duplicate
# quest triggers and broken navigation.
#
# Phase 1: Play through intro narration, choose ramen, save at ramen1
# Phase 2: Restart with same save data, verify narration resumes at ramen1
#
# Requires: game builds, display (creates a window), ~2 minutes total
#
# Usage:
#   ./tests/run-narration-save-restore.sh
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

# Ensure the rwpath is clean (mktemp -d creates empty, but be explicit)
rm -rf "$RWPATH"/*

# Common args: auto-login locally (bypass login menu) and use isolated save dir
COMMON_ARGS="-- --rwpath $RWPATH --setting debug.option.autoLogin=local"

echo ""
echo "=== Phase 1: Advance narration to ramen1 and save ==="
JOYCE_TEST_SCRIPT=tests/narration-save.json \
    dotnet run --no-build --project Karawan/Karawan.csproj $COMMON_ARGS
PHASE1_EXIT=$?

if [ $PHASE1_EXIT -ne 0 ]; then
    echo "FAIL: Phase 1 (save) failed with exit code $PHASE1_EXIT"
    echo "  Could not advance narration and save. Check game startup."
    exit 1
fi

echo "Phase 1 passed. Save data written to: $RWPATH"

# Verify save file was actually created
if [ ! -f "$RWPATH/gamestate.db" ]; then
    echo "FAIL: No gamestate.db found in $RWPATH after phase 1"
    echo "  Contents of $RWPATH:"
    ls -la "$RWPATH"
    exit 1
fi
echo "  gamestate.db exists: $(ls -la "$RWPATH/gamestate.db")"

echo ""
echo "=== Phase 2: Restart and verify narration restores at ramen1 ==="
JOYCE_TEST_SCRIPT=tests/narration-restore-verify.json \
    dotnet run --no-build --project Karawan/Karawan.csproj $COMMON_ARGS
PHASE2_EXIT=$?

if [ $PHASE2_EXIT -ne 0 ]; then
    echo ""
    echo "FAIL: Phase 2 (restore verify) failed with exit code $PHASE2_EXIT"
    echo "  Narration did NOT resume at ramen1 after loading save."
    echo "  This confirms the bug: narration restarted from intro."
    exit 1
fi

echo ""
echo "PASS: Narration save/restore works correctly."
echo "  Narration resumed at ramen1 after save/load cycle."
exit 0
