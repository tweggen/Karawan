### 2026-11-21 — KAR-487 (TALE S2b-b): requests and claims run in the live game

- Each populated cluster gets live social state in `TaleManager` (`LiveSocial`): a request pool, an arrival `EncounterResolver` and a `TaleSocialRules` over them. It is created after the populate warm-up, dropped on depopulate, and never saved.
- `_advanceNpc` in a populated cluster:
  - selects through `TaleSocialRules.SelectNext` (forced → pending interrupt → paused arc → routine);
  - emits the new storylet's request;
  - registers the NPC's presence and runs arrival encounters with the DES's model, with claims in both directions. A bystander already standing where a threat is issued claims it at once and gets `flee` as a Nest interrupt.
- The warm-up (`runSocialRules: false`) emits nothing and registers no presence.
- **Locks:** the cluster's `Lock` first, then `_loSocial` (every `ArcStack` write, the partner's included). Nothing takes them in the other order.
- **Interim housekeeping:** once a game day, on the first arrival, expired requests are purged, the pair dedup is cleared and presence older than 2 h is dropped. S2b-c moves this into the social frame.
- **Not yet live:** encounter-time conditional postconditions (escalation), claims in `TickEncounters`, day resolution (S2b-c), and materialised NPCs obeying interrupts (S2b-d).
- **Docs:** `ARCHITECTURE.md` §4/§5 and the `CONCEPT.md` status table are updated.
- **Tests:**
  - `LiveRequestClaimTests` (3): the warm-up emits no request; threat → claim on arrival → Nest flee → resume; depopulate drops the state.
  - xUnit 2295 pass; the 26 failures are the known KAR-461 drift. TALE 133/133. The DES and the bake are untouched.
