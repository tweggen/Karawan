### 2026-11-22 — KAR-496 (TALE S2-a): hearsay of the player and the gossip exchange

- **`PlayerHearsay`** (new, `JoyceCode/engine/tale/PlayerHearsay.cs`): what an NPC has heard of the player, kept apart from `Trust[-1]` so it does not make the NPC "met", known or deviated.
  - Fields: regard, weight, sorted fact tags, `HeardAt`, `UpdatedAt`, `Generation`.
  - Immutable, replaced by reference. The weight halves every 3 game days, computed on read (`WeightAt`); below 0.05 the record counts as absent.
  - Carried by `NpcSocialSnapshot` (`SnapshotNpc`/`RestoreNpc`, null when absent), the social-state JSON and `NpcScheduleJson`. Old saves load without it.
- **`PlayerGossip.Exchange`** (new, pure): both directions from the state before the exchange.
  - Every telling carries `Relay` 0.5 of the source; tellings below 0.15 are not told; a listener weighs a telling by `Listen` 0.5 × its trust in the teller; a confirmation raises only the weight.
  - First-hand facts travel as `hurt_someone` / `ran_someone_over` / `helped_someone` / `pays_people`; bribes never travel.
  - It never writes `Trust[-1]` or a `player_fact.*`, and it draws nothing when neither side has something to tell.
- **The owner's answers** (2026-11-22) are in the plan's §6:
  - Reach is measured in generations. Witnesses know at once, the first generation within a day, no further generation within that day, so hearsay is retold only after `RetellAfter` = 1 game day.
  - "Recognised" counts hearsay.
  - Prejudice is ±0.2.
  - Bribes do not travel.
  - Harm two hops, help one.
  - The roadmap's §S2 and §9.4 are updated accordingly.
- **Not wired yet:** the encounter sites (S2-b, KAR-497) and the readers (S2-c, KAR-498). Nothing in the game changes with this PR.
- **Tests:** `PlayerGossipTests` (11):
  - the worked example over days, with generations;
  - help one hop; at most half the source; a weak opinion not told;
  - first-hand untouched and hearsay not "known"; listener trust; confirmation;
  - bribes; decay without writes; no draws without a telling; determinism;
  - snapshot and JSON round-trips.

  xUnit 2319 pass; the 26 failures are the known KAR-461 drift. TALE 133/133; TestRunner entity tests pass.
