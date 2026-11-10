### 2026-11-10 — KAR-474: a wreck loaded from a save no longer throws every frame

- **Report:** with KAR-473, the old save holding the HelloFishmonger quest car mid-crash loaded, but `AfterCrashBehavior.Behave` then threw `IndexOutOfRangeException` from `Entity.Get<Body>()` on every frame.
- **Cause:** `EntitySaver` rebuilds a `Creator_Hardcoded` entity from its persistable components only, so the loaded car has no physics `Body`. The driving behaviour (`SimpleNavigationBehavior`) never reads the body, so saves written since KAR-473 were unaffected.
- **Fix:** without a body, `Behave` hands back to the old behaviour if there is one; otherwise it does nothing.
- **Tests:**
  - `TransientBehaviorSaveTests` source scan: the body check comes before the first body read.
  - xUnit 2224 pass; the 26 failures are the known KAR-461 drift.
  - TALE 133/133.
- **Not addressed:** a loaded quest car is a body-less shell (no model, no collider). That is how every saved quest car loads, not only wrecks.
