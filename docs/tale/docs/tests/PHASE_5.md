# Phase 5 Test Scripts — Branching, Interrupts & Escalation

> **Status:** 15 scripts, each asserting what the fixed TestRunner world really produces ·
> **Rewritten:** 2026-10-20 (KAR-433, TALE S0 W1)

Until KAR-433 these 20 scripts declared their checks in an `"expected"` block that
`ScriptRunner` ignored, so they passed without running. They are now ordinary `steps` scripts
built on the events `TestEventLogger` emits (`TestRunner/README.md` lists types and codes). The
numbers below are from the runner's event tally at seed 42, 60 days.

**Location:** `models/tests/tale/phase5-escalation/` · **Run:** `./run_tests.sh phase5`

Every script first waits for 11 `npc_created` (ten NPCs and the player proxy).

| Script | Asserts |
|---|---|
| 01 interrupt-nest-scope | a `Nest` interrupt fires, then a paused storylet resumes |
| 04 interrupt-priority | both interrupt sources fire: `investigate_complaint` (priority 6) and `demand_protection` (7) |
| 06 conditional-target-effect | an encounter, then `demand_protection` triggers against a named partner |
| 07 conditional-storylet-next | after a matched branch, its `storylet_next` runs (`collect_protection` or `threaten_harder`) |
| 10 protection-comply | after a demand, `collect_protection` runs (target fear ≥ 0.5) |
| 11 protection-refuse | after a demand, `threaten_harder` runs (target fear < 0.5) |
| 12 authority-investigate | a `crime_report` request, then `investigate_crime` |
| 13 authority-patrol | 5 × `patrol`, then `patrol_venue` |
| 14 escalation-chain | `crime_report`, then `investigate_crime`, then `prepare_arrest` (needs the `evidence_found` tag) |
| 15 fear-accumulation | after a demand, a threatened NPC runs `flee_from_threat` |
| 16 in-group-precondition | `intimidate_together` (needs `in_group`) runs |
| 17 group-storylet-unlock | `organize_criminal_operation` (`in_group`, morality ≤ 0.2, reputation ≥ 0.5) runs |
| 18 group-id-assigned | `profit_sharing`, then `leave_gang` (both need `in_group`) |
| 19 interrupt-metrics | 50 interrupts (73 at seed 42) |
| 20 escalation-fraction | day 30, then 3 `demand_protection` escalations |

## Removed, and why

| Script | Reason |
|---|---|
| 02 interrupt-replace-scope | `Replace` needs an interrupting storylet of priority ≥ 8 with conditional postconditions (`DesSimulation.cs`, the encounter step); no shipped storylet has both, so it never happens. |
| 03 interrupt-cancel-scope | Nothing produces `InterruptScope.Cancel` (kept on purpose until S2b, KAR-436). |
| 05 conditional-self-branch | The shipped conditional branches test only target properties (`target_fear`, `target_morality`); there is no self-condition to observe. |
| 08 conditional-no-match | `demand_protection`'s two branches cover every fear value; `investigate_complaint`'s unmatched branch emits no event. |
| 09 gang-formation | No `gang_formed` is ever emitted in this world: DES groups come from detection, and the `form` verb's storylet is never selected. Group membership is asserted through the `in_group` storylets (16–18) instead. |

## What the tally taught

- **Escalations wait for groups.** `demand_protection` needs `in_group`, and the DES detects
  groups every 30 days, so all 21 escalations at seed 42 come after day 30. Script 20 used to
  claim they were spread over the run.
- **Only Nest interrupts.** See 02 above.
