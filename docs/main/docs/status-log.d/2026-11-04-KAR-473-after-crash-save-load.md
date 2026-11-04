### KAR-473: a save holding a crashed quest car loads again (2026-11-04)

**Symptom.** Loading an existing save threw `InvalidOperationException: Each parameter in
the deserialization constructor on type 'nogame.characters.car3.AfterCrashBehavior' must
bind to an object property or field` from `InterfacePointerConverter<IBehavior>.Read`, and
the whole `EntitySaver.LoadAll` stopped there.

**Cause.** The HelloFishmonger quest car carries a `Creator`, so it is saved. When it crashes,
`car3.Behavior` replaces itself with `AfterCrashBehavior(engine, entity)`, and that class had
no constructor System.Text.Json could call. This bug predates KAR-471/472. It shows up only
when a game is saved during the few seconds a quest car is a wreck.

**Fix.**
- New `engine.behave.IPersistsAs`. A behaviour that exists only for a moment is saved as
  the behaviour it stands in for. `InterfacePointerConverter.Write` follows the chain, so
  `AfterCrashBehavior` is saved as the driving behaviour it hands back to.
- `AfterCrashBehavior` gets a `[JsonConstructor]` parameterless constructor (and resolves
  `_engine` lazily), so saves that already hold it still load. With nothing to hand back
  to, the car stays a wreck.
- `InterfacePointerConverter.Read` accepts the null `implementation` that `Write` emits
  without a type. Before this, every saved null failed to load with "found implementation
  without previously defined type".

**Tests.** `TransientBehaviorSaveTests` (3). xUnit 2224 pass / 26 known `engine.streets`
drift failures (KAR-461); TALE 133/133.
