#!/bin/bash

# TALE Regression Test Runner — parallel edition.
# Runs the same tests as run_tests.sh, but fans TestRunner processes out
# across all CPU cores. Each test is an independent process, so no engine
# changes are required for isolation.
#
# Usage: ./run_tests_parallel.sh [tier|phase|script]      (same filters as run_tests.sh)
#
# Environment knobs:
#   JOYCE_TEST_JOBS           number of concurrent tests (default: hw.ncpu)
#   TEST_TIMEOUT              per-test timeout in seconds passed to TestRunner
#                             as JOYCE_TEST_TIMEOUT (default: 60)
#   JOYCE_TEST_TIMEOUT_SCALE  stretch factor for in-script expect timeouts on
#                             saturated machines (default: 1.0)
#   JOYCE_TEST_SLEEP_SCALE    multiplier for in-script "sleep" steps. The DES
#                             sim runs at CPU speed, so these sleeps only delay
#                             reading buffered events (default here: 0.1;
#                             set 1.0 for original real-time pacing)
#   TALE_SIM_DAYS             simulation days (overridden by tier selection)

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TEST_BASE="models/tests/tale"
TESTRUNNER_DLL="./TestRunner/bin/Release/net9.0/TestRunner.dll"
WORKER="$SCRIPT_DIR/tools/run_one_tale_test.sh"

if [ ! -f "$TESTRUNNER_DLL" ]; then
    echo -e "${RED}Error: TestRunner not found at $TESTRUNNER_DLL${NC}"
    echo "Please run: dotnet build TestRunner/TestRunner.csproj -c Release"
    exit 1
fi

if [ ! -x "$WORKER" ]; then
    echo -e "${RED}Error: worker script not found or not executable: $WORKER${NC}"
    exit 1
fi

FILTER="${1:-all}"
TALE_SIM_DAYS="${TALE_SIM_DAYS:-60}"
TEST_TIMEOUT="${TEST_TIMEOUT:-60}"
JOYCE_TEST_JOBS="${JOYCE_TEST_JOBS:-$(sysctl -n hw.ncpu 2>/dev/null || nproc)}"
export JOYCE_TEST_TIMEOUT_SCALE="${JOYCE_TEST_TIMEOUT_SCALE:-1.0}"
export JOYCE_TEST_SLEEP_SCALE="${JOYCE_TEST_SLEEP_SCALE:-0.1}"

ALL_PHASES=("phase0-des" "phase1-storylets" "phase2-strategies" "phase3-interactions" "phase4-player" "phase5-escalation" "phase6-population" "phaseC1-infrastructure" "phaseC2-storylet" "phaseC3-tone" "phaseC4-trust")

PHASES=()
TESTS=()

if [ "$FILTER" = "smoke" ]; then
    SMOKE_MANIFEST="$TEST_BASE/.smoke-tests"
    if [ ! -f "$SMOKE_MANIFEST" ]; then
        echo -e "${RED}Error: Smoke test manifest not found at $SMOKE_MANIFEST${NC}"
        exit 1
    fi
    while IFS= read -r test_path; do
        [ -z "$test_path" ] && continue
        if [ -f "$TEST_BASE/$test_path" ]; then
            TESTS+=("$TEST_BASE/$test_path")
        else
            echo -e "${RED}  Test not found: $test_path${NC}"
        fi
    done < "$SMOKE_MANIFEST"
    TALE_SIM_DAYS=10
elif [ "$FILTER" = "standard" ] || [ "$FILTER" = "all" ]; then
    PHASES=("${ALL_PHASES[@]}")
    TALE_SIM_DAYS=60
elif [ "$FILTER" = "full" ]; then
    PHASES=("${ALL_PHASES[@]}")
    TALE_SIM_DAYS=120
elif [ "$FILTER" = "bugfix" ]; then
    # Entity-level behavior bugfix tests (single process, not parallelizable)
    echo -e "${YELLOW}=== Entity Behavior Bugfix Tests ===${NC}"
    ( JOYCE_TEST_SCRIPT="entity-behavior-tests" dotnet "$TESTRUNNER_DLL" 2>&1 ) | tee /tmp/test_output.log
    if grep -q "\[TEST\].*PASS" /tmp/test_output.log; then
        echo -e "${GREEN}All bugfix tests passed!${NC}"
        exit 0
    else
        echo -e "${RED}Bugfix tests failed!${NC}"
        exit 1
    fi
elif [[ "$FILTER" =~ ^phase ]]; then
    for p in "${ALL_PHASES[@]}"; do
        if [ "$p" = "$FILTER" ] || [[ "$p" == "$FILTER"-* ]]; then
            PHASES+=("$p")
        fi
    done
    if [ ${#PHASES[@]} -eq 0 ]; then
        echo -e "${RED}Unknown phase: $FILTER${NC}"
        exit 1
    fi
else
    # Assume it's a specific test file
    FOUND_TEST=$(find "$TEST_BASE" -name "*$FILTER" -type f | head -1)
    if [ -z "$FOUND_TEST" ]; then
        echo -e "${RED}Test not found: $FILTER${NC}"
        exit 1
    fi
    TESTS+=("$FOUND_TEST")
fi

# Expand phases into a flat test list
for phase in "${PHASES[@]}"; do
    phase_dir="$TEST_BASE/$phase"
    if [ ! -d "$phase_dir" ]; then
        echo -e "${RED}Phase directory not found: $phase_dir${NC}"
        continue
    fi
    for test_script in "$phase_dir"/*.json; do
        [ -f "$test_script" ] && TESTS+=("$test_script")
    done
done

if [ ${#TESTS[@]} -eq 0 ]; then
    echo -e "${RED}No tests selected.${NC}"
    exit 1
fi

RESULTS_DIR=$(mktemp -d /tmp/tale_tests.XXXXXX)

echo -e "${YELLOW}=== TALE Test Suite (parallel) ===${NC}"
echo "Started: $(date)"
echo "Tests: ${#TESTS[@]}  Jobs: $JOYCE_TEST_JOBS  Sim days: $TALE_SIM_DAYS  Timeout: ${TEST_TIMEOUT}s"
echo "Results dir: $RESULTS_DIR"
echo ""

# Fan out. BSD-xargs compatible (macOS: no GNU parallel, bash 3.2: no wait -n).
# Worker exits 0 always; failures are recorded via .status files.
printf '%s\n' "${TESTS[@]}" | \
    xargs -P "$JOYCE_TEST_JOBS" -n 1 \
    "$WORKER" "$TALE_SIM_DAYS" "$TEST_TIMEOUT" "$RESULTS_DIR"

# Aggregate results
PASSED=0
FAILED=0
FAILED_TESTS=()
FAILURES_FILE="$RESULTS_DIR/failures.txt"
> "$FAILURES_FILE"

for test_script in "${TESTS[@]}"; do
    test_name=$(basename "$test_script")
    phase=$(basename "$(dirname "$test_script")")
    status_file="$RESULTS_DIR/${phase}__${test_name}.status"
    log_file="$RESULTS_DIR/${phase}__${test_name}.log"
    status=$(cat "$status_file" 2>/dev/null || echo "MISSING")

    case "$status" in
        PASS|PASS_HEURISTIC)
            PASSED=$((PASSED+1))
            ;;
        *)
            FAILED=$((FAILED+1))
            FAILED_TESTS+=("${phase}/${test_name} [$status]")
            {
                echo "=== ${phase}/${test_name} [$status] ==="
                tail -20 "$log_file" 2>/dev/null || echo "(no log)"
                echo ""
            } >> "$FAILURES_FILE"
            ;;
    esac
done

echo ""
echo -e "${YELLOW}=== Summary ===${NC}"
TOTAL=$((PASSED + FAILED))
echo "Passed: $PASSED/$TOTAL"
echo "Failed: $FAILED/$TOTAL"
echo "Finished: $(date)"

if [ $FAILED -gt 0 ]; then
    echo -e "${RED}Failed Tests:${NC}"
    for test in "${FAILED_TESTS[@]}"; do
        echo "  - $test"
    done
    echo ""
    echo "Full error details in: $FAILURES_FILE"
    echo "Per-test logs in: $RESULTS_DIR"
    exit 1
else
    echo -e "${GREEN}All tests passed!${NC}"
    exit 0
fi
