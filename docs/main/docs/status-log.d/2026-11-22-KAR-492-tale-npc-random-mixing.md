### 2026-11-22 — KAR-492: city role mixes follow the role weights

- **Found** because the owner never saw drifters. The start city (Yelukhdidru, 279 NPCs) had worker 73, hustler 69, reveler 45, authority 41, nightworker 38, socialite 10, merchant 2, drifter 1. `PickRole`'s weights there give roughly 27/20/13/13/11/7/7/2 % (worker/merchant/socialite/authority/hustler/nightworker/reveler/drifter).
- **Cause:** each NPC's draws came from `new RandomSource(clusterSeed + "-npc-" + i)`. `RandomSource` hashes a string as a linear sum of char × position and draws by one multiplicative step, so the first draws of such similar seeds lie on a lattice. Deciles over 2000 indices: Yelukhdidru 299 327 330 301 192 83 60 65 120 223. Every city's cast was skewed, each one differently.
- **Fix:**
  - New `RandomSource.Mixed(string)`: FNV-1a 64 → SplitMix64 → an odd 31-bit seed, then the usual draws.
  - The ordinary constructor is unchanged: it seeds the world and its streets.
  - Used for the population generator, the populate offset, storylet duration, time jitter and street pick (`TaleManager`), the character model pick (`TaleSpawnOperator`) and group names.
- **After:**
  - Ufyo (4095) is within 3 points of every weight.
  - Over 200 cluster keys the first draw's χ² (10 bins, 279 draws) has mean 8.9 and p95 16.8; a uniform draw gives 9 and 16.9.
  - Yelukhdidru's key is the largest of the 200 (33). The start city is now a fair but unlucky draw: worker 101, merchant 47, socialite 36, authority 24, hustler 24, reveler 23, nightworker 22, drifter 2 (weight ~6).
  - Exact proportions were not used: they would make an NPC's role depend on the others', which the seed hierarchy forbids.
- **Effect on saves:** every city's cast changes (roles, homes, workplaces, models, group names), so an existing save meets different NPCs at the same indices. Deviated NPCs are saved and keep their state.
- **Not changed:**
  - The scenario bake: it uses `System.Random`; all 13 `sc-*` re-baked byte-identical.
  - TestRunner's synthetic populations.
- **Tests:** `TalePopulationDistributionTests` (+3: role shares for both cities, first-draw uniformity). xUnit 2308 pass; the 26 failures are the known KAR-461 drift. TALE 133/133. TestRunner entity tests pass.
