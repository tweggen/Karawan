### 2026-11-20 — KAR-475 follow-up: the game starts again (HelloFishmongerModule registered)

- **Regression from #182:** every game start threw `ModuleTracker:ModuleActivate: Error: Unable to instantiate moduleDependency`. The root scene lists `SharedModule<HelloFishmongerModule>`. `ModuleFactory` builds a shared module through `I.Instance.GetInstance(type)`, which knows only types registered in `models/nogame.implementations.json` or by `I.Register<T>`, and the new module was in neither.
- **Fix:** the module's entry in `nogame.implementations.json`, next to `TaxiNpcSpawnerModule`.
- **Test:** `SharedModuleRegistrationTests` checks that every `SharedModule<T>` in `JoyceCode`/`nogameCode` is registered, by full or short name, in the JSON or in code. Without the entry it fails and names exactly this module.
- xUnit 2249 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
