### 2026-11-21 — KAR-477: claim-triggered storylets run only by claims; groups are typed by what they are

- **Report:** the default start showed 1 social and 7 criminal groups, right at the start. The owner interview: any emergent mix is fine if explainable. KAR-478's half-the-city-in-transit at 03:38 turned out to be the same cause.
- **Cause, measured:** 26 storylets carry a `claim_trigger` (flee_from_threat, respond_to_blackmail, engage_in_argument, investigate_crime, …). They are meant to start when an NPC claims a request.
  - `StoryletSelector` treated them as ordinary candidates: no preconditions, default weight 1.0 against the routine's 0.3–0.5. About 40 % of Yelukhdidru was in one of three such storylets at any hour.
  - A 365-day bake of 200 NPCs: 8 of 12 groups criminal, morality 0.64 → 0.14, wealth 0.46 → 0.28. With them excluded: 0 criminal, morality 0.62.
  - And the claim itself never started the storylet (`DesSimulation.CheckAndClaimRequests` recorded and signalled only), so the escalation chains had run only through the selection bug.
- **Owner decision:** exclude, then tune.
- **Change:**
  - **Selection:** `StoryletSelector.SelectNext` skips `claim_trigger` storylets.
  - **Claims:** a claim sets the claiming storylet as the NPC's interrupt with the trigger's `interrupt_scope` (nest by default), choosing among several matching storylets by weight, seeded by request and claimer. Before, the first in pool order was taken, so `investigate_complaint` never ran.
  - **Content:** `threaten_harder` (escalation.json) now issues the threat it names: a `threat` request, as `initiate_threat` does. Otherwise refused protection never made anyone flee.
  - **Group types:** "trade" was wealth ≥ 0.5 and reputation ≥ 0.5, the bake's population average (32 of 45 groups over four medium bakes after the fix). It now needs ≥ 40 % merchants or hustlers (new rule type `trader_threshold`) and wealth ≥ 0.5: 10 of 45.
  - **Mirrors:** the group-type JSON is written out by hand in Chushi and TestRunner; both updated, and `GroupTypeRegistryTests` fails when a mirror drifts.
  - **Bake inputs:** Chushi now re-bakes when `nogame.group-types.json` changes; it watched only nogame.json, nogame.scenarios.json and the storylets.
- **Shipped bake, all 13 scenarios, before → after:**

  | | before | after |
  |---|---|---|
  | groups | 58 criminal, 32 social | 84 social, 15 trade, 4 criminal, 1 patrol |
  | morality | 0.12 | 0.49 |
  | wealth | 0.26 | 0.61 |
  | anger | 0.16 | 0.24 |
  | fear | 0.10 | 0.16 |

  Drama now comes through real claims and threats.
- **Live game:** claims exist only in the DES (S2b ports them), so claim-triggered storylets no longer happen among live, materialised NPCs until then. The live society is seeded from the corrected bake.
- **Found, not changed:** the role economies run away over a year (workers and merchants toward wealth 1.0, socialites and revelers toward 0), and no storylet raises morality. This is a design question for the owner.
- **Tests:**
  - `ClaimTriggeredStoryletTests` (2; both fail on the old selector).
  - `GroupTypeRegistryTests` +3 (traders, a well-off circle without traders is social, the mirrors match the JSON).
  - The phase-5 escalation scripts now pass through real claims; 18-group-id-assigned is reordered to the order the corrected world produces (leave_gang before profit_sharing).
  - xUnit 2282 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
