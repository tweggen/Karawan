# Platform Backend — state ledger

Required by [`IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md`](IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md) §2.2b.
**The orchestrator must update this on every dispatch and every result.** Without it, a fresh
orchestrator session reconstructs state by git archaeology and gets the "max 3 iterations"
count wrong.

**Last updated:** 2026-08-05

---

## ⛔ Programme status: HALTED — awaiting human decision

**Trigger:** plan §5c — *"Any ADR §9 'assumed' claim falsified"*.
**WP-0.0 falsified claim #6.** Silk's SDL2 AAR **can** be fixed locally.

Per plan §4, this changes the plan's shape: *"Play unblocks without touching windowing.
Phases 2–3 lose their urgency and become longevity-only work the owner can schedule at
leisure. Re-plan with the human."*

**No Phase 1+ work package may be dispatched until the human has decided.** See
[`WP-0.0-FINDINGS.md`](WP-0.0-FINDINGS.md) §6.

---

## Work package status

| WP | Status | Branch | PR | Iter | AC results | Gates | Notes |
|---|---|---|---|---|---|---|---|
| **WP-0.0** | **PR-OPEN** | `platform/wp-0.0` | — | 1 | AC-0.0.1 ✅ · 0.0.2 ✅ · 0.0.3 ✅ · 0.0.4 ✅ | none apply | **Claim #6 FALSIFIED, claim #7 confirmed.** Repack demonstrated working; artifact never executed. |
| WP-0.1 | NOT-STARTED | — | — | 0 | — | GATE-D | CPM across 13 csprojs. ⚠ Capture `dotnet list package` baseline on **master before branching** or AC-0.1.3 is unrunnable. Conflicts with WP-0.3 (`Wuka.csproj`). |
| WP-0.2 | NOT-STARTED | — | — | 0 | — | GATE-D adj. | `IThreeD` seam leaks. Independent, safe to run now. |
| WP-0.3 | NOT-STARTED | — | — | 0 | — | — | Inventory XA0141/XA4301 only. Conflicts with WP-0.1 (`Wuka.csproj`). |
| WP-1.1 – 1.6 | **BLOCKED** | — | — | 0 | — | — | Blocked on the WP-0.0 re-plan decision. Scope likely **grows** to include SDL2-for-Android. Also blocked on `gh` being installed. |
| WP-2.1 – 2.3 | **BLOCKED** | — | — | 0 | — | GATE-A, GATE-B | Urgency removed by WP-0.0. Do not dispatch. ⚠ AC-2.2's AAR path is wrong — SDL3 uses prefab layout. |
| WP-3.1 – 3.5 | BLOCKED | — | — | 0 | — | GATE-C, GATE-E | Blocked on GATE-A + GATE-B per plan. |
| WP-4.1 – 4.4 | NOT-STARTED | — | — | 0 | — | GATE-D | Independent of Phases 2–3; may start once Phase 0 lands. |
| WP-5.0 | NOT-STARTED | — | — | 0 | — | — | Cheap, independent, plan says run EARLY. Not blocked by the WP-0.0 outcome. |
| WP-5.0b | NOT-STARTED | — | — | 0 | — | — | Cost S2b. Plan §5: **neither WP-5.1 nor any Phase 5 work starts until 5.0 and 5.0b are both reported and the human has chosen.** |
| WP-5.1 – 5.4 | BLOCKED | — | — | 0 | — | GATE-E, GATE-F | Approach still open (N4 relaxed). |

Status vocabulary: `NOT-STARTED / IN-PROGRESS / PR-OPEN / BLOCKED-ON-HUMAN / MERGED / ABANDONED`.

---

## Gate ledger

| Gate | What | Status |
|---|---|---|
| GATE-A | SDL3 spike on physical Android device (multi-touch, **IME**, rotation, resume) | not reached |
| GATE-B | Play Console upload, no "Memory page size" warning | not reached — **now reachable much earlier via the WP-0.0 repack route** |
| GATE-C | Windows + Linux desktop | not reached |
| GATE-D | Animation correct on macOS + Windows | not reached |
| GATE-E | ImGui renders + takes input (incl. Linux Fn-key case) | not reached |
| GATE-F | Pixel-compare before/after GL swap | not reached. ⚠ **Baseline must be captured before WP-5.2 merges** or it is unrunnable forever |

---

## Budget counters (plan §5c off-the-rails thresholds)

| Counter | Limit | Current |
|---|---|---|
| Phase 2 worker dispatches | 10 | 0 |
| Programme-wide re-dispatches | 25 | 0 |
| Calendar: Phases 0–2 complete | 3 months from 2026-08-04 | day 1 |
| ADR §9 "assumed" claims falsified | any → escalate | **1 (claim #6)** ⛔ |

---

## Environment blockers

Tracked here because they gate whole phases. Full detail in `WP-0.0-FINDINGS.md` §5.

| Blocker | Impact | Status |
|---|---|---|
| `gh` not installed | **blocks §2.1 entirely** — every WP must open a PR — and AC-1.1 | ⛔ open |
| Plan §5b describes macOS; work is on Windows 11 | misleads every future worker | ✅ fixed in this PR |
| `ninja` not installed | needed for any native build recipe | workaround: standalone `ninja.exe`; should be pinned in CI (WP-1.1) |
| `java` not installed | only matters if the SDL Java side must be rebuilt | open, not currently blocking |
| No physical Android device / Windows / Linux / Play Console access **for the agent** | GATE-A/B/C/D/E/F | inherent — human-only by design |
