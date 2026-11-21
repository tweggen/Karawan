### 2026-11-21 — KAR-490 (TALE S2b-e): live escalation accepted; TALE panel shows requests and interruptions

- **Acceptance with the shipped storylets.** A drifter's `demand_protection` meets a target with fear 0.2, so it is refused (+0.2 anger). The refusal forces `threaten_harder`, which issues a `threat`. A bystander meets the drifter, claims the threat and runs `flee_from_threat` as a Nest. When the flight ends, the paused storylet resumes.
  - **Live and abstract:** xUnit `LiveEscalationAcceptanceTests`. Encounters go through `OnLiveEncounter` → `TickLiveSocial`, the flight through the background tick. The panel snapshot shows the request with its claimer and the pending interrupt, then the paused storylet.
  - **Materialised:** TestRunner `./run_tests.sh bugfix`. The bystander is walking; its entity's interrupt event makes it flee, and it resumes afterwards. 9/9 entity tests pass.
- **TALE panel:**
  - "Open requests": type, requester, storylet, location, emitted/timeout, claimer.
  - "Interruptions": NPCs with a pending interrupt or a paused storylet, selectable.
  - The selected NPC shows its pending interrupt (scope, since when) and its paused storylets.
  - Built from `TaleManager.CopyOpenRequests` under the pool's lock.
- **Bug found by the acceptance run (from KAR-489):** if an NPC's storylet ended before an interrupt arrived and the background tick had not reached the NPC yet, the tick stepped at that earlier end time. It took the interrupt before it existed and caught up past the flight. A pending interrupt is now stepped at the time it was set, never earlier.
- **Also:** `StoryletLibrary.GetById(null)` returns null instead of throwing. The shared rules called it for an NPC that had not run a storylet yet.
- **Tests:** all 13 shipped scenarios re-baked byte-identical. xUnit 2305 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
- **Open:** the ticket's in-game check. With TALE running, watch the panel's "Open requests" and "Interruptions" for a threat being claimed and the claimer fleeing.
