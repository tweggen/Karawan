# Plan B: Arm Right-Hand Collider Only During Punch

> **Status:** implemented - **Implemented as:** `nogameCode/nogame/characters/EntityCreator.cs:61,298`, `nogameCode/nogame/modules/playerhover/WalkBehavior.cs:37,45,113`, `WalkController.cs:62,133,339,451,846`, `WalkModule.cs:92,146,155` - **Verified:** 2026-07-21


**Status:** ✅ Done (2026-05-17)
**Created:** 2026-05-17
**Complexity:** Small (single file change + plumbing)
**Risk:** Low–Medium (timing of layer toggle relative to swing arc)
**Related:** Plan A (`PLAYER-NPC-COLLISION-A-LAYER-FIX.md`, done), Plan C (`PLAYER-NPC-COLLISION-C-PUSH-ASIDE.md`, proposed)

## Implementation Note (2026-05-17)

Implemented with one deviation from the plan as drafted. `BehaviorManager._onComponentAdded` (`JoyceCode/engine/behave/BehaviorManager.cs:70`) calls `IBehavior.OnAttach` synchronously when the Behavior component is added — and inside `EntityCreator._createLogical`, the Behavior is set (line 226) *before* the right-hand block runs (line 237+). So `WalkController` is constructed before the hand exists. Plumbing the hand via `WalkController` constructor / init-only property does not work.

**Resolution:** the hand is plumbed via a settable-property forwarder. `WalkBehavior.RightHandEntity` (playerhover) is a settable property whose setter writes through to `_controllerWalkController.RightHandEntity` if the controller exists. After `creator.CreateLogical()` returns, `WalkModule._setupPlayer` reads `creator.RightHandEntity` and assigns it to `createdBehavior.RightHandEntity` (captured by closure from the `BehaviorFactory` lambda). The forwarder then pushes it into the live controller. No reorder of `_createLogical` was needed.

Files actually touched:
- `nogameCode/nogame/characters/EntityCreator.cs` — `public Entity RightHandEntity { get; private set; }` output, assigned at end of the `CreateRightHand` block. Initial `SolidLayerMask` for the hand changed from `PlayerMelee` → `0`.
- `nogameCode/nogame/modules/playerhover/WalkBehavior.cs` — settable `RightHandEntity` property that forwards to the controller. `OnAttach` initializes the controller's `RightHandEntity` from the current value (handles the case where the field is set before `OnAttach`, though in practice it's set after).
- `nogameCode/nogame/modules/playerhover/WalkController.cs` — `RightHandEntity` property, `_setHandArmed(bool)` helper, calls at the two `_attackState` transitions plus `OnModuleActivate` (where `_attackState` is also defensively reset to `Peaceful`).
- `nogameCode/nogame/modules/playerhover/WalkModule.cs` — closure-captured `createdBehavior`; after `CreateLogical()` returns, hand is forwarded to the behavior. Also assigns the previously-dead `_eRightHand` field.

Desktop build verified: 0 errors. In-game manual verification still pending.

---

## Problem

The player's right-hand collider (`nogameCode/nogame/characters/EntityCreator.cs:264`) is permanently `SolidLayerMask = PlayerMelee`. NPC `OnCollision` tests `other.SolidLayerMask & AnyWeapon` (which includes `PlayerMelee`) → `HitEvent` → `FleeStrategy`. The hand follows the skeleton animation, so its sphere collider swings through the walk/idle arm motion and can touch NPCs without the player ever pressing fire. NPCs flee from peaceful proximity.

`WalkController` already tracks attack state — `_attackState ∈ {Peaceful, Attacking}`, set on `<fire>` button press (line 426), cleared after 23 frames (line 313-317) — but never propagates it to the hand collider.

---

## Goal

The right hand acts as a weapon **only** while a punch is in flight:
- `_attackState == Peaceful` → hand `SolidLayerMask = 0` (no solid-layer presence; NPC `OnCollision` ignores it).
- `_attackState == Attacking` → hand `SolidLayerMask = PlayerMelee` (matches `AnyWeapon`, triggers `HitEvent` → flee).

The 23-frame attack window already exists; reusing it gives a tunable hit window without new state.

---

## Approach

### 1. Plumb the right-hand entity from `EntityCreator` to `WalkController`

`EntityCreator._eRightHand` is currently private (`EntityCreator.cs:40`). `WalkModule._eRightHand` is declared (`WalkModule.cs:36`) but never assigned. Two options:

**Option B1 (recommended):** Expose the hand entity on `EntityCreator` as a public output.
- Add `public Entity RightHandEntity { get; private set; }` to `EntityCreator`, assigned inside the `if (CreateRightHand)` block (line 237).
- `WalkModule._eRightHand = creator.RightHandEntity;` after `creator.CreateLogical()` (around `WalkModule.cs:140`).
- `WalkController` gets a new `public DefaultEcs.Entity RightHandEntity { get; set; }`, set from `WalkModule` when `WalkController` is constructed.

**Option B2:** Have `WalkController` find the hand by walking child hierarchy on `_eTarget`. Brittle (depends on name) — skip.

### 2. Initial state: inert

In `EntityCreator.cs:264`, change initial mask:
```csharp
SolidLayerMask = CollisionProperties.Layers.PlayerMelee,
```
to
```csharp
SolidLayerMask = 0,
```

### 3. Toggle on attack-state transitions in `WalkController`

The two transition points already exist in `WalkController.OnLogicalFrame`:
- **Peaceful → Attacking** at line 425-426 (after `_isFireTriggered` is consumed).
- **Attacking → Peaceful** at line 313-317 (after 23-frame window expires).

Add a small helper:
```csharp
private void _setHandArmed(bool armed)
{
    if (RightHandEntity == default || !RightHandEntity.IsAlive) return;
    if (!RightHandEntity.Has<engine.physics.components.Body>()) return;
    var po = RightHandEntity.Get<engine.physics.components.Body>().PhysicsObject;
    if (po?.CollisionProperties == null) return;
    po.CollisionProperties.SolidLayerMask =
        armed ? CollisionProperties.Layers.PlayerMelee : 0;
}
```
Call `_setHandArmed(true)` at the Attacking transition; `_setHandArmed(false)` at the Peaceful transition (and once during `OnModuleActivate` for safety).

**Threading:** `OnLogicalFrame` runs on the engine logical thread. `CollisionProperties.SolidLayerMask` is a plain field. Concurrent reads happen from physics narrow-phase callbacks — `NarrowPhaseCallbacks.cs:64-100`. The field is `int`-sized, reads/writes are atomic on .NET. Worst case is a one-frame mismatch on the transition boundary, which is harmless. No lock needed.

### 4. Optional: tighten the hit window

23 frames (~380 ms at 60 Hz) is the animation duration. The actual contact arc is shorter — maybe frames 5-15. If post-A+B testing shows the hand catches NPCs who are already behind the player after the swing, narrow to e.g. `currentFrame - _lastAttackFrame ∈ [3, 15]`. Defer until manually verified.

---

## Files Affected

- `nogameCode/nogame/characters/EntityCreator.cs` — expose `RightHandEntity`, change initial mask to `0`.
- `nogameCode/nogame/modules/playerhover/WalkModule.cs` — assign `_eRightHand` from creator; pass to `WalkController`.
- `nogameCode/nogame/modules/playerhover/WalkController.cs` — add `RightHandEntity` property, `_setHandArmed` helper, call at the two transitions and in `OnModuleActivate`.

No documentation changes required.

---

## Test Plan

Manual (no automated coverage for collision events):

1. Build & launch.
2. Walk player past several citizens **without pressing fire**.
3. **Expected:** No NPC flees. (Currently they do, from arm-swing contact.)
4. Press fire (`<fire>` input — e.g. left mouse / gamepad fire button) while in range of an NPC.
5. **Expected:** Punch animation plays. NPC's `FleeStrategy` activates within the 23-frame window. NPC runs away.
6. Press fire repeatedly while out of range of NPCs — no observable effect on the world; animation plays normally.
7. Verify the hover vehicle is unaffected (different code path).

**Edge case to verify:** boarding/unboarding the vehicle. When `WalkModule` deactivates, the right hand entity is destroyed along with the player person; when reactivated, a new hand is created. `_setHandArmed` checks `IsAlive` so stale references after deactivation are safe.

---

## Dependencies / Ordering

- Can land independently of Plan A, but **Plan A should land first** because without it Plan B still leaves the body-as-vehicle bug.
- Plan A + Plan B together fully resolve the user's original complaint ("collapses on every collision"). Plan C is an additive feel improvement.

---

## Out of Scope

- Left-hand collider (none currently exists; punch left/right are visual-only — code falls through `attackHand = AttackHand.RightHand` at `WalkController.cs:403`).
- Damage / health model (NPC flees but takes no measurable harm — out of scope).
- Push-aside (Plan C).
