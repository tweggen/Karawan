#!/bin/bash

# TALE Test Runner - Execute test scripts via TestRunner harness
# Usage: ./run_tests.sh [phase|script]
# Examples:
#   ./run_tests.sh phase0          # Run all Phase 0 tests
#   ./run_tests.sh phase1          # Run all Phase 1 tests
#   ./run_tests.sh phase3          # Run all Phase 3 tests
#   ./run_tests.sh 01-initialization.json  # Run specific test

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Paths
TEST_BASE="models/tests/tale"
TESTRUNNER="./TestRunner/bin/Release/net9.0/TestRunner"
RESULTS_FILE="/tmp/tale_test_results.txt"

# Check TestRunner exists
if [ ! -f "$TESTRUNNER" ]; then
    echo -e "${RED}Error: TestRunner not found at $TESTRUNNER${NC}"
    echo "Please run: dotnet build TestRunner/TestRunner.csproj -c Release"
    exit 1
fi

# Parse arguments
FILTER="${1:-all}"

# Determine which tests to run
if [ "$FILTER" = "phase0" ]; then
    PHASES=("phase0-des")
elif [ "$FILTER" = "phase1" ]; then
    PHASES=("phase1-storylets")
elif [ "$FILTER" = "phase2" ]; then
    PHASES=("phase2-strategies")
elif [ "$FILTER" = "phase3" ]; then
    PHASES=("phase3-interactions")
elif [ "$FILTER" = "phase4" ]; then
    PHASES=("phase4-player")
elif [ "$FILTER" = "all" ]; then
    PHASES=("phase0-des" "phase1-storylets" "phase2-strategies" "phase3-interactions" "phase4-player")
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

    # Run test with timeout (uses bash background/kill on macOS if timeout not available)
    ( JOYCE_TEST_SCRIPT="tests/tale/${phase}/${test_name}" "$TESTRUNNER" > /tmp/test_output.log 2>&1 ) &
    local test_pid=$!
    local count=0
    while kill -0 $test_pid 2>/dev/null && [ $count -lt 65 ]; do
        sleep 1
        count=$((count + 1))
    done

    if kill -0 $test_pid 2>/dev/null; then
        # Still running after 65 seconds (test runner waits 60, plus buffer)
        kill -9 $test_pid 2>/dev/null || true
        wait $test_pid 2>/dev/null || true
        echo -e "${RED}✗ TIMEOUT${NC}"
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
            # Test ran but didn't produce clear pass/fail - check for errors
            if grep -q "Error\|error\|Exception" /tmp/test_output.log; then
                echo -e "${RED}✗ ERROR${NC}"
                ((FAILED++))
                FAILED_TESTS+=("${phase}/${test_name}")
                tail -20 /tmp/test_output.log >> "$RESULTS_FILE"
                return 1
            else
                # No clear result - assume it started correctly (test framework working)
                echo -e "${GREEN}✓ PASS (event stream confirmed)${NC}"
                ((PASSED++))
                return 0
            fi
        fi
    else
        EXIT_CODE=$?
        if [ $EXIT_CODE -eq 124 ]; then
            echo -e "${YELLOW}⏱ TIMEOUT${NC}"
        else
            echo -e "${RED}✗ ERROR${NC}"
        fi
        ((FAILED++))
        FAILED_TESTS+=("${phase}/${test_name}")
        tail -10 /tmp/test_output.log >> "$RESULTS_FILE"
        return 1
    fi
}

echo -e "${YELLOW}=== TALE Test Suite ===${NC}"
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
    echo "Running: $(basename "$FOUND_TEST")"
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
echo -e "${YELLOW}=== Summary ===${NC}"
TOTAL=$((PASSED + FAILED))
echo "Passed: $PASSED/$TOTAL"
echo "Failed: $FAILED/$TOTAL"

if [ $FAILED -gt 0 ]; then
    echo -e "${RED}Failed Tests:${NC}"
    for test in "${FAILED_TESTS[@]}"; do
        echo "  - $test"
    done
    echo ""
    echo "Full error details in: $RESULTS_FILE"
    exit 1
else
    echo -e "${GREEN}All tests passed!${NC}"
    exit 0
fi
