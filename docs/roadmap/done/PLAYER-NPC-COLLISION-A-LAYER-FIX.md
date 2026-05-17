# Plan A: Player Body Collision Layer Fix

**Status:** ✅ Done (2026-05-17)
**Created:** 2026-05-17
**Complexity:** Trivial (1-2 lines of code)
**Risk:** Low
**Related:** Plan B (`PLAYER-NPC-COLLISION-B-HAND-ARM-TOGGLE.md`, proposed), Plan C (`PLAYER-NPC-COLLISION-C-PUSH-ASIDE.md`, proposed)

## Implementation Note (2026-05-17)

`Layers.Player` is also used elsewhere as a `SensitiveLayerMask` (`TaxiNpcSpawnerModule.cs:78`, `GeneratePolytopeOperator.cs:126`, `GenerateCharacterOperator.cs:161`, `ToSomewhere.cs:411`) — those are quest markers and collectables that legitimately want to react to anything player-related (walking or driving). The alias was kept; only the single `SolidLayerMask` line at `WalkModule.cs:126` was changed. Desktop build verified: 0 errors. In-game manual verification still pending (no automated coverage exists for collision events).

---

## Problem

When the player (on foot) bumps an NPC, the NPC collapses with the death animation as if hit by a car.

**Root cause:** `Layers.Player = PlayerCharacter | PlayerVehicle = 0x0003` (`JoyceCode/engine/physics/CollisionProperties.cs:25-27`). The walking player body is created with this combined mask at `nogameCode/nogame/modules/playerhover/WalkModule.cs:126`. NPC `OnCollision` handlers (`WalkBehavior.cs:82`, plus 4 parallel sites listed below) test `other.SolidLayerMask & AnyVehicle` — where `AnyVehicle = PlayerVehicle | NpcVehicle`. The player body's `PlayerVehicle` bit matches → `CrashEvent` is published → `RecoverStrategy` plays the death animation.

The walking body should not advertise itself as a vehicle.

---

## Goal

After this change, an NPC contacted by the walking player body:
- Does **not** receive `CrashEvent` (no collapse / death animation).
- Does **not** receive `HitEvent` (no flee — body is not a weapon).
- Remains in `WalkStrategy`, continuing its route.

The player vehicle (HoverModule, `SolidLayerMask = PlayerVehicle`) is unaffected — vehicles still cause crashes. NPC-vs-NPC, NPC weapons, NPC vehicles unaffected.

---

## Approach

Change one line:

**`nogameCode/nogame/modules/playerhover/WalkModule.cs:126`**
```csharp
SolidLayerMask = CollisionProperties.Layers.Player,
```
to
```csharp
SolidLayerMask = CollisionProperties.Layers.PlayerCharacter,
```

### Decision: leave the `Layers.Player` alias as-is

Grep first for other uses of `Layers.Player` (not `PlayerCharacter`, not `PlayerVehicle`). If the only consumer is `WalkModule.cs:126`, optionally redefine the alias to `Player = PlayerCharacter` and remove `| PlayerVehicle` from `CollisionProperties.cs:27`. Otherwise leave the alias untouched — it's harmless if unused.

**Recommendation:** leave the alias. The single-line site change is unambiguous and reviewable; the alias rename adds a semantic-cleanup commit that isn't needed for the fix.

---

## Files Affected

- `nogameCode/nogame/modules/playerhover/WalkModule.cs` — line 126.
- (Optional cleanup) `JoyceCode/engine/physics/CollisionProperties.cs` — only if grep shows no other `Layers.Player` users.

No documentation files need updating (no public API or design doc references this).

---

## Test Plan

No automated test suite covers physics collision events on the player. Manual:

1. Build & launch desktop (`dotnet run --project Karawan/Karawan.csproj`).
2. Walk pedestrian player into one of the wandering citizens.
3. **Expected:** NPC keeps walking past the player (kinematic, blocks player motion), no death animation.
4. Board hover vehicle, drive into a citizen.
5. **Expected:** NPC still collapses (vehicle case unchanged).
6. Verify regression on TALE NPCs in a populated cluster — same expectations.

If Plan B is not yet merged: the player's right hand will still cause Flee on arm-swing contact. Acceptable — Plan B addresses that separately.

---

## Out of Scope

- Right-hand collider behavior (Plan B).
- Push-aside reaction / `BumpEvent` (Plan C).
- NPC-NPC collision routing (unchanged).
