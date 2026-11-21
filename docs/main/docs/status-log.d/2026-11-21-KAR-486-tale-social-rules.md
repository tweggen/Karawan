### 2026-11-21 — KAR-486 (TALE S2b-a): the DES's social rules become a shared component

- **First step of S2b** (plan `docs/tale/plans/proposed/TALE-S2B-LIVE-ESCALATION.md`, reviewed GREEN): `DesSimulation`'s rules move unchanged into `engine.tale.TaleSocialRules`:
  - the selection order (forced → pending interrupt → resumed arc → routine);
  - request emission;
  - claims (a claim starts its storylet as an interrupt, KAR-477);
  - an encounter's conditional postconditions and the interrupts they bring;
  - day resolution.
- The live game will call the same component in S2b-b/c.
- Each driver passes in its request pool, logger, metrics and random source. The DES passes its own `_rng`, including the unused resolver draw at day resolution, so its stream is unchanged. An encounter's interaction type and trust stay with the driver.
- **No behaviour change:**
  - all 13 shipped scenarios, re-baked from scratch with the refactor (`sc-*` deleted, Chushi republished), are byte-identical to the baseline;
  - the TALE suite is 133/133.
- **Tests:**
  - `TaleSocialRulesTests` (5): selection order, Replace pauses nothing, threat → claim → Nest flee, an expired request is not claimed, the DES delegates (scan).
  - xUnit 2292 pass; the 26 failures are the known KAR-461 drift.
