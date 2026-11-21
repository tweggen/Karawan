### 2026-11-21 — KAR-488 (TALE S2b-c): live encounters run the shared rules; the social frame purges and resolves

- **Encounters:**
  - Every encounter `RuntimeSocialEvolver.TickEncounters` lets happen now goes to a callback (`TaleModule` → `TaleManager.OnLiveEncounter`). The callback queues it.
  - `TaleManager.TickLiveSocial`, called every social frame, runs four queued encounters per cluster, in order and at the encounter's time: claims in both directions, conditional postconditions (escalation: `NextForcedStorylet`), and the interrupts they bring.
- **Housekeeping:** the same call purges expired requests every frame. At midnight it resolves the day as the DES does, clears the arrival model's pair dedup, and drops presence older than 2 h. That replaces KAR-487's interim daily housekeeping on arrival.
- **Claims cost less:** `TaleSocialRules.ClaimRequests` caches, per role, the request types it can claim at all, returns at once when there are none, and walks the pool without copying it into a list. It visits requests in the same order with the same filters, so the outcome is identical: same claim counts in the measurement, and all 13 shipped scenarios re-baked byte-identical.
- **Measured** (`LiveSocialPerfTests`, Perf; Ufyo, 4095 NPCs, shipped storylets, one game day: background tick at 60 fps over 30 real minutes, social tick every 10 game minutes):

  | | TickEncounters p99 / p99.9 | background slice mean / p99.9 |
  |---|---|---|
  | no live rules (baseline) | 1.46 / 1.88 ms | 0.066 / 1.25 ms |
  | arrival claims only (KAR-487) | 1.68 / 1.99 ms | 0.167 / 1.54 ms |
  | + encounter rules, inline (first try) | 4.87 / 5.67 ms | 0.166 / 1.36 ms |
  | + encounter rules, queued (shipped) | 1.56 / 2.09 ms | 0.166 / 1.40 ms |

  - **TickEncounters** meets its 2 ms p99 budget only because encounters are queued. Run inline, each claim walked ~750 open requests (at most 2571): ~32 µs per encounter, ~50 encounters per tick. An index by request type would claim a different request first than the DES and change every bake, so the work is sliced instead.
  - **The slice's ≤ 0.2 ms p99.9 target is not met, and was not met before S2b either:** the baseline p99.9 with the shipped storylets is 1.25 ms. S0 W5's 0.07 ms matches this run's *mean* (0.066 ms). The live rules add ~0.1 ms to the mean (the arrival claims) and leave the tail within noise.
  - **Claim rate:** live 87 % of requests claimed (12648/14554) against the DES's 89 % (22055/24889) for the same NPCs and day, so claims are not starved. The live game emits fewer requests than the DES.
- **Tests:**
  - `LiveRequestClaimTests` (+4): a social-tick encounter claims and sets the interrupt; a refused demand escalates to the threat and nests into the partner; queued encounters run a few per tick in order; the social frame resolves the day and purges at the timeout.
  - xUnit 2299 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
