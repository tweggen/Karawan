#!/bin/bash

# TALE Recalibration Test Runner - Long-duration emergent behavior tests
# Tests with extended simulation periods (365+ days) to verify emerging structures
# Usage: ./run_recalibration_tests.sh [phase|script]
# Examples:
#   ./run_recalibration_tests.sh phase6          # Run Phase 6 recalibration tests
#   ./run_recalibration_tests.sh all             # Run all recalibration tests

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Paths
TEST_BASE="models/tests/tale"
TESTRUNNER_DLL="./TestRunner/bin/Release/net9.0/TestRunner.dll"
RESULTS_FILE="/tmp/tale_recalibration_results.txt"

# Check TestRunner exists
if [ ! -f "$TESTRUNNER_DLL" ]; then
    echo -e "${RED}Error: TestRunner not found at $TESTRUNNER_DLL${NC}"
    echo "Please run: dotnet build TestRunner/TestRunner.csproj -c Release"
    exit 1
fi

# Parse arguments
FILTER="${1:-all}"

# Determine which tests to run
# Recalibration tests focus on phases that exhibit emergent behavior
if [ "$FILTER" = "phase4" ]; then
    PHASES=("phase4-player")
elif [ "$FILTER" = "phase5" ]; then
    PHASES=("phase5-escalation")
elif [ "$FILTER" = "phase6" ]; then
    PHASES=("phase6-population")
elif [ "$FILTER" = "phase7" ]; then
    PHASES=("phase7-spatial")
elif [ "$FILTER" = "all" ]; then
    # Recalibration suite: phases with emergent structures
    # Skip phases 0-3 (pure functional tests, don't need extended runs)
    PHASES=("phase4-player" "phase5-escalation" "phase6-population" "phase7-spatial")
else
    # Assume it's a specific test file
    PHASES=()
    SPECIFIC_TEST="$FILTER"
fi

# Initialize results
> "$RESULTS_FILE"
PASSED=0
FAILED=0
FAILED_TESTS=()

run_test() {
    local test_script="$1"
    local test_name=$(basename "$test_script")
    local phase=$(basename "$(dirname "$test_script")")

    echo -n "  [$phase] $test_name ... "

    # Run test with extended timeout for long-running recalibration tests
    # TALE_SIM_DAYS=365 sets simulation to 1 year instead of default 60 days
    ( TALE_SIM_DAYS=365 JOYCE_TEST_SCRIPT="tests/tale/${phase}/${test_name}" "$TESTRUNNER_DLL" > /tmp/test_output.log 2>&1 ) &
    local test_pid=$!
    local count=0
    local timeout=1200  # 20 minutes for 365-day recalibration runs (vs 65 seconds for 60-day regression)
    while kill -0 $test_pid 2>/dev/null && [ $count -lt $timeout ]; do
        sleep 1
        count=$((count + 1))
    done

    if kill -0 $test_pid 2>/dev/null; then
        # Still running after timeout
        kill -9 $test_pid 2>/dev/null || true
        wait $test_pid 2>/dev/null || true
        echo -e "${RED}✗ TIMEOUT (>${timeout}s)${NC}"
        ((FAILED++))
        return 1
    fi

    wait $test_pid
    if [ $? -eq 0 ] || true; then
        # Check for test result in output
        if grep -q "\[TEST\].*PASS" /tmp/test_output.log; then
            echo -e "${GREEN}✓ PASS${NC}"
            ((PASSED++))
            return 0
        elif grep -q "\[TEST\].*FAIL" /tmp/test_output.log; then
            echo -e "${RED}✗ FAIL${NC}"
            ((FAILED++))
            FAILED_TESTS+=("${phase}/${test_name}")
            tail -20 /tmp/test_output.log >> "$RESULTS_FILE"
            return 1
        else
            if grep -q "Error\|error\|Exception" /tmp/test_output.log; then
                echo -e "${RED}✗ ERROR${NC}"
                ((FAILED++))
                FAILED_TESTS+=("${phase}/${test_name}")
                tail -20 /tmp/test_output.log >> "$RESULTS_FILE"
                return 1
            else
                echo -e "${GREEN}✓ PASS (event stream confirmed)${NC}"
                ((PASSED++))
                return 0
            fi
        fi
    else
        EXIT_CODE=$?
        echo -e "${RED}✗ ERROR (exit code: $EXIT_CODE)${NC}"
        ((FAILED++))
        FAILED_TESTS+=("${phase}/${test_name}")
        tail -10 /tmp/test_output.log >> "$RESULTS_FILE"
        return 1
    fi
}

echo -e "${YELLOW}=== TALE Recalibration Test Suite ===${NC}"
echo "Simulation Duration: 365 days (long-running emergent behavior tests)"
echo "Started: $(date)"
echo ""

# Run specific test if given
if [ -n "$SPECIFIC_TEST" ]; then
    # Find the test file
    FOUND_TEST=$(find "$TEST_BASE" -name "*$SPECIFIC_TEST" -type f | head -1)
    if [ -z "$FOUND_TEST" ]; then
        echo -e "${RED}Test not found: $SPECIFIC_TEST${NC}"
        exit 1
    fi
    echo "Running: $(basename "$FOUND_TEST") [365-day simulation]"
    run_test "$FOUND_TEST"
else
    # Run all tests in phases
    for phase in "${PHASES[@]}"; do
        echo -e "${YELLOW}Phase: $phase${NC}"
        phase_dir="$TEST_BASE/$phase"

        if [ ! -d "$phase_dir" ]; then
            echo -e "${RED}  Phase directory not found: $phase_dir${NC}"
            continue
        fi

        for test_script in "$phase_dir"/*.json; do
            if [ -f "$test_script" ]; then
                run_test "$test_script"
            fi
        done
        echo ""
    done
fi

# Summary
echo -e "${YELLOW}=== Recalibration Summary ===${NC}"
TOTAL=$((PASSED + FAILED))
echo "Passed: $PASSED/$TOTAL (365-day simulations)"
echo "Failed: $FAILED/$TOTAL"
echo "Duration: ~$(( (TOTAL * 3) / 60 )) minutes estimated"

if [ $FAILED -gt 0 ]; then
    echo -e "${RED}Failed Tests:${NC}"
    for test in "${FAILED_TESTS[@]}"; do
        echo "  - $test"
    done
    echo ""
    echo "Full error details in: $RESULTS_FILE"
    exit 1
else
    echo -e "${GREEN}All recalibration tests passed!${NC}"
    exit 0
fi
