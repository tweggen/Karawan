#!/usr/bin/env bash
#
# Detect commits that are on a branch and pushed, but NOT on master - the failure this
# repository has now hit FOUR times (PRs #17, #19, #28, #30).
#
# THE TRAP
#
# You push to a branch whose PR has already merged. Git is perfectly happy: the branch
# tracks its remote, `git status` is clean, `git push` succeeds, and GitHub shows the PR
# as MERGED. Nothing anywhere says the new commits went nowhere. They sit on a dead
# branch until somebody happens to look.
#
# The first three times this cost a recovery PR. The fourth time was worse: master was
# left documenting a FACT THAT HAD BEEN CORRECTED - the KI-8 threading claim - which is
# exactly the kind of thing a later work package reads and trusts.
#
# WHY THE OBVIOUS CHECKS DO NOT CATCH IT
#
#   git status          clean - the branch matches its remote
#   git push            succeeds - there is nothing wrong with the push
#   gh pr view          MERGED - truthfully, just not with these commits
#
# The only reliable question is the one below: what is on this branch that master does
# not have? Ask it before assuming work has landed.
#
# USAGE
#
#   scripts/check-branch-landed.sh              # current branch vs origin/master
#   scripts/check-branch-landed.sh my-branch    # a specific branch
#
# Exit 0 = everything on the branch is on master. Exit 1 = commits are stranded.

set -euo pipefail

BRANCH="${1:-$(git rev-parse --abbrev-ref HEAD)}"
BASE="${BASE:-origin/master}"

if [ "${BRANCH}" = "master" ] || [ "${BRANCH}" = "HEAD" ]; then
    echo "OK: on ${BRANCH}, nothing to compare."
    exit 0
fi

git fetch -q origin

# `git cherry`, NOT `git log ${BASE}..${BRANCH}`.
#
# Recovering from this trap means cherry-picking, which gives the commits NEW SHAs. A
# SHA-based comparison then keeps reporting the originals as missing forever, even though
# their content landed - and a check that cries wolf is a check people stop reading.
#
# `git cherry` compares patch-ids, so an already-applied change is recognised however it
# got there. It prefixes each commit with '+' (not upstream) or '-' (equivalent patch
# already upstream); we want only the '+' lines.
MISSING="$(git cherry "${BASE}" "${BRANCH}" 2>/dev/null \
           | awk '$1=="+" {print $2}' \
           | while read -r sha; do git log --oneline -1 "${sha}"; done || true)"

if [ -z "${MISSING}" ]; then
    echo "OK: every commit on '${BRANCH}' is already on ${BASE}."
    exit 0
fi

COUNT="$(printf '%s\n' "${MISSING}" | wc -l | tr -d ' ')"

echo "=============================================================================="
echo " ${COUNT} commit(s) on '${BRANCH}' are NOT on ${BASE}:"
echo "=============================================================================="
printf '%s\n' "${MISSING}" | sed 's/^/  /'
echo

# The specific trap: the PR for this branch is already merged, so these will never land.
if command -v gh >/dev/null 2>&1; then
    STATE="$(gh pr list --head "${BRANCH}" --state all --json state,number \
             --jq '.[0] | "\(.state) \(.number)"' 2>/dev/null || true)"
    case "${STATE}" in
        MERGED*)
            PR="${STATE#MERGED }"
            echo "  *** PR #${PR} for this branch is ALREADY MERGED. ***"
            echo "  These commits were pushed after the merge and will never reach"
            echo "  ${BASE} on their own. Open a NEW branch off ${BASE} and"
            echo "  cherry-pick them:"
            echo
            echo "      git checkout -b <new-branch> ${BASE}"
            printf '%s\n' "${MISSING}" | awk '{print $1}' | tac | sed 's/^/      git cherry-pick /'
            echo
            ;;
        OPEN*)
            echo "  PR #${STATE#OPEN } is open - these land when it merges. Nothing to do."
            ;;
        *)
            echo "  No PR found for this branch yet."
            ;;
    esac
fi

exit 1
