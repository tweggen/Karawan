### 2026-11-23 — KAR-499 (TALE S2-d): close ties hear without meeting

- **Owner's decision (2026-11-23), after the live measurement showed 2–4 NPCs reached in three days:** close ties hear without a chance encounter, "because we could assume the use of cellphones or social media".
- **`PlayerGossip.TellCloseTies`:** an NPC with something to tell (first-hand, or hearsay held for a game day) tells its strongest ties (trust ≥ `CloseTie` 0.5, at most `MaxTiesTold` 4), each pair once per game day, with the same rules as an encounter telling (`PlayerGossip.Tell`).
- **Live:** `TaleManager.TickLiveSocial` runs the pass once per game hour per cluster, sliced at `NetworkScanBudget` (512) NPCs per call, under the cluster lock and `_loSocial`. In one go it cost 1.5 ms median at 4095 NPCs; sliced, 0.053 ms median per slice, p99 0.37 ms.
- **Reach, Ufyo** (`GossipReachPerfTests`, a baked medium scenario, the victim as the only first-hand source):

  | hours | punch: heard (by generation) | would greet | help: heard |
  |---|---|---|---|
  | 6 | 4 (gen 1) | 4 | 4 (gen 1) |
  | 23 | 6 (gen 1) | 4 | 6 (gen 1) |
  | 36 | 16 (gen 1: 6, gen 2: 10) | 14 | 6 |
  | 48 | 25 (gen 1: 7, gen 2: 18) | 23 | 7 |
  | 72 | 28 (gen 1: 7, gen 2: 21) | 27 | 7 |

  - Generation 1 within the first day, generation 2 from the second day and only for harm: the owner's target.
  - Before, the same punch reached 4 NPCs in three days.
  - Standing is "recognised" from the first hours on.
- **DES unchanged:** the bake has no player.
- **Tests:**
  - `PlayerGossipTests` (+2: strongest four, once per day; a hearer tells its ties only a day later);
  - `LiveRequestClaimTests` (+1: a close tie hears within the hour, someone who merely trusts the victim does not);
  - the pass cost in `GossipReachPerfTests` (Perf).
  - xUnit 2333 pass; the 26 failures are the known KAR-461 drift. TALE 133/133; TestRunner entity tests pass.
