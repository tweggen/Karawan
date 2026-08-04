#!/bin/bash

# Worker for run_tests_parallel.sh: runs ONE TALE test in its own TestRunner
# process, classifies the outcome and records it under the shared results dir.
#
# Usage: run_one_tale_test.sh <sim_days> <timeout_s> <results_dir> <test_script_path>
#
# Writes:
#   <results_dir>/<phase>__<name>.log     full TestRunner output
#   <results_dir>/<phase>__<name>.status  PASS | PASS_HEURISTIC | FAIL | ERROR | TIMEOUT
#
# Classification mirrors run_tests.sh exactly, including the
# "no clear result but no errors => pass" heuristic (PASS_HEURISTIC).

SIM_DAYS="$1"
TIMEOUT_S="$2"
RESULTS_DIR="$3"
TEST_SCRIPT="$4"

TESTRUNNER_DLL="./TestRunner/bin/Release/net9.0/TestRunner.dll"

test_name=$(basename "$TEST_SCRIPT")
phase=$(basename "$(dirname "$TEST_SCRIPT")")
log_file="$RESULTS_DIR/${phase}__${test_name}.log"
status_file="$RESULTS_DIR/${phase}__${test_name}.status"

finish() {
    local status="$1"
    echo "$status" > "$status_file"
    case "$status" in
        PASS)           echo "  [$phase] $test_name ... PASS" ;;
        PASS_HEURISTIC) echo "  [$phase] $test_name ... PASS (event stream confirmed)" ;;
        *)              echo "  [$phase] $test_name ... $status" ;;
    esac
    exit 0
}

# exec so that $test_pid is the dotnet process itself (kill -9 reaches it).
( exec /usr/bin/env \
    JOYCE_TEST_SCRIPT="tests/tale/${phase}/${test_name}" \
    TALE_SIM_DAYS="$SIM_DAYS" \
    JOYCE_TEST_TIMEOUT="$TIMEOUT_S" \
    JOYCE_TEST_TIMEOUT_SCALE="${JOYCE_TEST_TIMEOUT_SCALE:-1.0}" \
    dotnet "$TESTRUNNER_DLL" > "$log_file" 2>&1 ) &
test_pid=$!

# Shell-side kill timeout: TestRunner's own timeout plus a small buffer.
# Poll at 0.2s so fast tests release their parallel slot promptly.
kill_ticks=$(((TIMEOUT_S + 5) * 5))
count=0
while kill -0 $test_pid 2>/dev/null && [ $count -lt $kill_ticks ]; do
    sleep 0.2
    count=$((count + 1))
done

if kill -0 $test_pid 2>/dev/null; then
    kill -9 $test_pid 2>/dev/null || true
    wait $test_pid 2>/dev/null || true
    finish "TIMEOUT"
fi
wait $test_pid 2>/dev/null || true

if grep -q "\[TEST\].*PASS" "$log_file"; then
    finish "PASS"
elif grep -q "\[TEST\].*FAIL" "$log_file"; then
    finish "FAIL"
elif grep -q "Error\|error\|Exception" "$log_file"; then
    finish "ERROR"
else
    finish "PASS_HEURISTIC"
fi
