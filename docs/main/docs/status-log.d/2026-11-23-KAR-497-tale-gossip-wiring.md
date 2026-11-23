### 2026-11-23 — KAR-497 (TALE S2-b): gossip runs at the three encounter sites

- `PlayerGossip.Exchange` now runs at all three encounter sites:
  - **`DesSimulation.ProcessEncounter`**, after the pair's trust change, with the symmetric pair trust (`RelationshipTracker`) for both directions, drawing from `_rng`.
  - **`TaleManager.TickLiveSocial`'s drain** (social-tick encounters), with each side's own `NpcSchedule.Trust` after the tick's change. Its source is seeded from the pair key and the encounter time.
  - **`TaleManager._runArrival`**, with the existing per-(NPC, step) source, after `CheckEncounters`' draws, so gossip does not change who meets.
- All live calls run under the cluster lock and `_loSocial`.
- `DesSimulation.Initialize` takes an optional `Random`.
- **Measured** in the DES with the start city's real population (279 NPCs, shipped storylets). One punched victim, with its 33 home and work mates trusted at 0.7:
  - after 23 h, 11 NPCs heard, all generation 1;
  - after 3 days, 28 heard (generation 1: 18, generation 2: 10);
  - nobody besides the victim came to know the player.

  That is the owner's target: one generation within the first day.
- **The bake is unchanged:** all 13 `sc-*` re-baked byte-identical; a DES run without any player entry gives nobody hearsay.
- **Ufyo (`LiveSocialPerfTests`, no player entries, so gossip only takes its early exit):**
  - TickEncounters p99 is 2.24 ms with the rules and 1.78 ms without the encounter rules; the all-off baseline measured 2.87 ms in the same run, which is noise.
  - The open-request pool is larger than at KAR-488 (mean 962 against 759) because KAR-492 changed the cast.
- **Tests:**
  - `GossipWiringTests` (2: DES by generation and day; no player, no hearsay).
  - `LiveRequestClaimTests` (+1: social tick and arrival both pass a punched victim's opinion on; hearing is not knowing).
  - xUnit 2322 pass; the 26 failures are the known KAR-461 drift. TALE 133/133; TestRunner entity tests pass.
- **Not yet:** nothing reads hearsay in the game; greetings, prejudice and the panel come with S2-c (KAR-498).
