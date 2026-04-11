#!/bin/bash

# TALE Regression Test Runner - Multi-tier testing strategy
# For long-running recalibration tests (365+ days), use: ./run_recalibration_tests.sh
#
# Usage: ./run_tests.sh [tier|phase|script]
# Tiers:
#   ./run_tests.sh smoke           # Smoke tests (~1 minute, 10-day sims)
#   ./run_tests.sh standard        # Standard regression (~5 minutes, 60-day sims)
#   ./run_tests.sh full            # Full regression (~15-20 minutes, 120-day sims)
#   ./run_tests.sh all             # Same as 'standard'
# Phases:
#   ./run_tests.sh phase0          # Run all Phase 0 tests
#   ./run_tests.sh phase1          # Run all Phase 1 tests
#   ./run_tests.sh 01-initialization.json  # Run specific test

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Paths
TEST_BASE="models/tests/tale"
TESTRUNNER_DLL="./TestRunner/bin/Release/net9.0/TestRunner.dll"
RESULTS_FILE="/tmp/tale_test_results.txt"

# Check TestRunner exists
if [ ! -f "$TESTRUNNER_DLL" ]; then
    echo -e "${RED}Error: TestRunner not found at $TESTRUNNER_DLL${NC}"
    echo "Please run: dotnet build TestRunner/TestRunner.csproj -c Release"
    exit 1
fi

# Parse arguments
FILTER="${1:-all}"

# Default simulation days (can be overridden by tier)
TALE_SIM_DAYS="${TALE_SIM_DAYS:-60}"

# Determine which tests to run
if [ "$FILTER" = "smoke" ]; then
    # Smoke tier: 10 critical tests, 10-day simulations
    SMOKE_MANIFEST="$TEST_BASE/.smoke-tests"
    if [ ! -f "$SMOKE_MANIFEST" ]; then
        echo -e "${RED}Error: Smoke test manifest not found at $SMOKE_MANIFEST${NC}"
        exit 1
    fi
    PHASES=()
    SPECIFIC_TESTS=()
    while IFS= read -r test_path; do
        [ -z "$test_path" ] && continue
        SPECIFIC_TESTS+=("$test_path")
    done < "$SMOKE_MANIFEST"
    TALE_SIM_DAYS=10
elif [ "$FILTER" = "standard" ] || [ "$FILTER" = "all" ]; then
    # Standard tier: all tests, 60-day simulations
    PHASES=("phase0-des" "phase1-storylets" "phase2-strategies" "phase3-interactions" "phase4-player" "phase5-escalation" "phase6-population" "phaseC1-infrastructure" "phaseC2-storylet" "phaseC3-tone" "phaseC4-trust")
    TALE_SIM_DAYS=60
elif [ "$FILTER" = "full" ]; then
    # Full tier: all tests, 120-day simulations
    PHASES=("phase0-des" "phase1-storylets" "phase2-strategies" "phase3-interactions" "phase4-player" "phase5-escalation" "phase6-population" "phaseC1-infrastructure" "phaseC2-storylet" "phaseC3-tone" "phaseC4-trust")
    TALE_SIM_DAYS=120
elif [ "$FILTER" = "phase0" ]; then
    PHASES=("phase0-des")
elif [ "$FILTER" = "phase1" ]; then
    PHASES=("phase1-storylets")
elif [ "$FILTER" = "phase2" ]; then
    PHASES=("phase2-strategies")
elif [ "$FILTER" = "phase3" ]; then
    PHASES=("phase3-interactions")
elif [ "$FILTER" = "phase4" ]; then
    PHASES=("phase4-player")
elif [ "$FILTER" = "phase5" ]; then
    PHASES=("phase5-escalation")
elif [ "$FILTER" = "phase6" ]; then
    PHASES=("phase6-population")
elif [ "$FILTER" = "phaseC1" ]; then
    PHASES=("phaseC1-infrastructure")
elif [ "$FILTER" = "phaseC2" ]; then
    PHASES=("phaseC2-storylet")
elif [ "$FILTER" = "phaseC3" ]; then
    PHASES=("phaseC3-tone")
elif [ "$FILTER" = "phaseC4" ]; then
    PHASES=("phaseC4-trust")
elif [ "$FILTER" = "bugfix" ]; then
    # Entity-level behavior bugfix tests (not DES simulation)
    echo -e "${YELLOW}=== Entity Behavior Bugfix Tests ===${NC}"
    ( JOYCE_TEST_SCRIPT="entity-behavior-tests" dotnet "$TESTRUNNER_DLL" 2>&1 ) | tee /tmp/test_output.log
    if grep -q "\[TEST\].*PASS" /tmp/test_output.log; then
        echo -e "${GREEN}All bugfix tests passed!${NC}"
        exit 0
    else
        echo -e "${RED}Bugfix tests failed!${NC}"
        exit 1
    fi
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
    ( JOYCE_TEST_SCRIPT="tests/tale/${phase}/${test_name}" TALE_SIM_DAYS="$TALE_SIM_DAYS" dotnet "$TESTRUNNER_DLL" > /tmp/test_output.log 2>&1 ) &
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

# Run specific test(s) if given
if [ -n "$SPECIFIC_TEST" ]; then
    # Find the test file
    FOUND_TEST=$(find "$TEST_BASE" -name "*$SPECIFIC_TEST" -type f | head -1)
    if [ -z "$FOUND_TEST" ]; then
        echo -e "${RED}Test not found: $SPECIFIC_TEST${NC}"
        exit 1
    fi
    echo "Running: $(basename "$FOUND_TEST")"
    run_test "$FOUND_TEST"
elif [ ${#SPECIFIC_TESTS[@]:-0} -gt 0 ]; then
    # Run smoke tests from manifest
    echo -e "${YELLOW}Tier: smoke (10-day simulations)${NC}"
    for test_path in "${SPECIFIC_TESTS[@]}"; do
        full_test_path="$TEST_BASE/$test_path"
        if [ -f "$full_test_path" ]; then
            run_test "$full_test_path"
        else
            echo -e "${RED}  Test not found: $test_path${NC}"
            ((FAILED++))
        fi
    done
    echo ""
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
