### 2026-11-20 — KAR-475: a HelloFishmonger chase survives a save

- **Defect:** a save made during the chase loaded without the quest, and with an invisible, untouchable shell of the car.
  - The quest is a `QuestFactory` entity and was never saved.
  - The car carried a `Creator(CreatorId_Hardcoded)`, so `EntitySaver` stored it by its persistable components alone: no model, no body, no collider.
  - That `Creator` was left over from the pre-February `IQuest` module (`62086e8c`), which saved the car and found it again by name on load. The migration dropped the second half.
- **Owner decision (2026-11-20):** resume the chase, as Taxi does, rather than only drop the shell.
- **Change:**
  - `HelloFishmongerQuestCreator` (`ICreator`) sits on the quest entity. Its JSON (`HelloFishmongerSaveState`) holds the cluster index and the junction the car is driving away from, by position, never by id. A car mid-crash answers for the behaviour it will hand back to (`IPersistsAs`).
  - On load the creator waits, off the logical thread and without blocking `EntitySaver`, until the fragment under that junction is loaded. It then builds the car there and attaches the trail strategy. A save with no car on a street ends the quest instead.
  - `FishmongerCar` is the one place the car is built, for a new chase and a resumed one. The car carries no `Creator`.
  - `HelloFishmongerModule` registers the creator. It depends on `TaxiNpcSpawnerModule`, because creator ids are handed out in registration order and stored in saves, so Taxi must keep its id.
  - On load the module also deletes the shell cars older saves contain.
- **Tests:**
  - `HelloFishmongerPersistenceTests` (9): the save-state round trip and the incomplete or absent cases; source scans for no `Creator` on the car, the creator on the quest, the creator writing what it reads, registration after Taxi, and the shell removal. Putting the `Creator` back on the car fails a test.
  - xUnit 2243 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
- **Not run:** the game itself (no display on the build host). In-game check pending: save mid-chase, reload; the car reappears at the junction it last left, drives on, and is followed again; an older save loads without the invisible car.
