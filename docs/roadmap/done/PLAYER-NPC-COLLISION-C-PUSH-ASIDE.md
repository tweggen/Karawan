# Plan C: Push-Aside Reaction on Player Body Contact

**Status:** ✅ Done (2026-05-17) — Phases 1 + 2 landed in one commit. Phase 3 (tuning) deferred until manual in-game review.
**Created:** 2026-05-17
**Complexity:** Medium (new event type + handler in 5 behaviors + light strategy work)
**Risk:** Medium (touches all citizen `OnCollision` sites; navigator deviation needs tuning)
**Depends on:** Plan A (`PLAYER-NPC-COLLISION-A-LAYER-FIX.md`, done)
**Related:** Plan B (`PLAYER-NPC-COLLISION-B-HAND-ARM-TOGGLE.md`, done)

## Implementation Note (2026-05-17)

Implemented largely as designed. Two deviations:

1. **Bump handler lives at the behavior level, not the strategy level.** TaleEntityStrategy has no central navigator (each travel sequence creates a fresh one inside `GoToStrategyPart`), so reaching the active navigator from a strategy-level event handler would have required new component plumbing. Instead, the two walking behaviors that *own* their navigator (`WalkBehavior` and `TaleWalkBehavior`) subscribe to `BumpEventPath` directly in `OnAttach` / unsubscribe in `OnDetach`, and call `Navigator.ApplyLateralBump`. Idle / conversation / recover behaviors never subscribe — bumping them is a no-op, the NPC blocks the player. Consistent with the original plan's stance that stationary NPCs are acceptable as roadblocks.

2. **Router exposes classification helpers, not just a single Dispatch.** TALE sites have custom `_cancelConversation` side effects per flag. Rather than passing pre-hook callbacks into `Dispatch`, the router exposes `IsWeapon(other)` / `IsVehicle(other)` / `IsPlayerCharacter(other)` classifiers. TALE sites run their side effect using these, then call `Dispatch` for the event push. Citizen sites with no side effects call `Dispatch` directly.

`SegmentNavigator.ApplyLateralBump(direction, magnitude=0.4 m, durationSeconds=0.4 s)` applies a transient world-space offset. The offset decays linearly to zero over the duration. Successive bumps refresh the timer and add to the running offset (capped at 0.6 m total magnitude). Y is zeroed and the direction is renormalized inside the navigator, so callers can pass the raw contact normal.

The bump direction passed in is `cev.ContactInfo.ContactNormal`. If manual testing shows the NPC moves *toward* the player rather than away, flip the sign at the dispatch site (one-line tuning, Phase 3).

Phase 3 deferred — tuning (magnitude, duration, decay shape, speed scaling, sign of contact normal) should follow manual in-game observation.

Files actually touched:
- New: `nogameCode/nogame/characters/citizen/CitizenCollisionRouter.cs`
- New: `nogameCode/nogame/characters/citizen/BumpEvent.cs`
- Modified: `nogameCode/nogame/characters/citizen/EntityStrategy.cs` — added `BumpEventPath`.
- Modified: 5 OnCollision sites (`WalkBehavior`, `IdleBehavior`, `RecoverBehavior`, `TaleWalkBehavior`, `TaleConversationBehavior`).
- Modified: `nogameCode/nogame/characters/citizen/WalkBehavior.cs` + `TaleWalkBehavior.cs` — subscribe `_onBumpEvent` in OnAttach, unsubscribe in OnDetach, forward to navigator.
- Modified: `JoyceCode/builtin/tools/SegmentNavigator.cs` — `ApplyLateralBump` + decay tick in `NavigatorBehave`.
- Modified: `nogameCode/nogameCode.projitems` — register the two new files.
- Modified: `CLAUDE.md` — added "Citizen Collision Routing" subsection.

Desktop build: 0 errors. JoyceCode.Tests: 46/46 passing. Manual in-game verification still pending.

---

## Problem

After Plan A, the walking player body no longer triggers Crash or Hit events on NPCs. NPCs are kinematic; the player's per-frame raycast (`WalkController.cs:557+`) stops the player at the contact point. The result is correct but lifeless: the player gets stuck against an NPC standing in their way, with no acknowledgement from the NPC.

The user's stated intent is that the NPC should **step aside** — a soft, brief redirect of the NPC's path so the player can pass, with no flee and no collapse.

---

## Goal

When the player body contacts an NPC:
- The NPC briefly deviates laterally (perpendicular to the player's approach), enough to clear the player's path (~0.4 m).
- After ~400 ms the deviation decays and the NPC resumes its normal route.
- No animation change beyond what `WalkBehavior` already drives (walk/run).
- TALE NPCs behave the same way.

---

## Design Sketch

### 1. New event: `BumpEvent`

In `nogameCode/nogame/characters/citizen/EntityStrategy.cs`, alongside `CrashEventPath` and `HitEventPath`:

```csharp
static public string BumpEventPath(in DefaultEcs.Entity e) =>
    $"@{e.ToString()}/nogame.characters.citizen.onBump";
```

Mirror in `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` (which uses the citizen `EntityStrategy.HitEventPath` / `CrashEventPath` directly — keep reusing the citizen path strings for consistency, so a single set of strings covers both strategy families).

The event payload encodes the bump direction (Vector3, normalized, world-space, NPC→player). One option: pass via a `BumpEvent : Event` subclass with a `Vector3 Direction` field; alternatively use the `Event.Data` string field with a parseable encoding. The subclass is cleaner — there's already precedent in the codebase (grep `: Event` in `engine.news` consumers).

### 2. Centralize collision routing

There are currently **five** copies of the `(AnyWeapon → HitEvent, AnyVehicle → CrashEvent)` pattern:

- `WalkBehavior.OnCollision` (`WalkBehavior.cs:75-86`)
- `IdleBehavior.OnCollision` (`IdleBehavior.cs:44-56`)
- `RecoverBehavior.OnCollision` (`RecoverBehavior.cs:35-46`)
- `TaleWalkBehavior.OnCollision` (`TaleWalkBehavior.cs:141-154`)
- `TaleConversationBehavior.OnCollision` (`TaleConversationBehavior.cs:117-130`)

This drift risk is already documented in conversation. Before adding a third branch, factor into a single static helper:

```csharp
// nogameCode/nogame/characters/citizen/CitizenCollisionRouter.cs
internal static class CitizenCollisionRouter
{
    public static void Dispatch(CollisionProperties me, CollisionProperties other,
                                Vector3 contactNormal)
    {
        if (other == null || me == null) return;
        var q = I.Get<EventQueue>();
        if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyWeapon))
            q.Push(new Event(EntityStrategy.HitEventPath(me.Entity), ""));
        if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyVehicle))
            q.Push(new Event(EntityStrategy.CrashEventPath(me.Entity), ""));
        if (0 != (other.SolidLayerMask & CollisionProperties.Layers.PlayerCharacter))
            q.Push(new BumpEvent(EntityStrategy.BumpEventPath(me.Entity), contactNormal));
    }
}
```

Each of the 5 sites reduces to a single call. (`contactNormal` is available on `ContactEvent.ContactInfo` — confirm field name during implementation.)

**Order matters:** AnyWeapon takes precedence over PlayerCharacter (a punch-while-walking should flee, not just step aside). With Plan B's hand-armed gating, the hand is `SolidLayerMask = 0` when not punching, so the precedence question only arises during the swing window — where flee is correct.

### 3. Bump handler in `EntityStrategy` / `TaleEntityStrategy`

`EntityStrategy._onBumpEvent` is a new handler subscribed in `OnEnter` / unsubscribed in `OnExit` (mirror lines 144/131). The handler should **not** transition strategies — bumping should not interrupt walk. Instead, write a `BumpRequest` onto the entity (or on the navigator) that the next `WalkBehavior.Behave` tick reads.

Two implementations to choose from:

**Option C1 — Navigator deviation (recommended).**
Add a transient `LateralOffset` field on `SegmentNavigator` (or a wrapping `BumpedNavigator`): a `Vector3 offset` plus a `DateTime expiresAt`. Each `Behave` tick, `SegmentNavigator.GetTargetPosition` adds the offset (scaled by remaining lifetime, decaying linearly to zero). When the bump handler fires, set `offset = bumpDirection * 0.4f`, `expiresAt = now + 400ms`. Subsequent bumps within the window refresh the timer (and re-add to offset, capped at ~0.6 m so repeated bumps don't snowball).

**Option C2 — Transient step-aside sub-strategy.**
Add a `StepAsideStrategy` next to `FleeStrategy`/`RecoverStrategy`. `EntityStrategy` triggers it on bump. Strategy lasts ~400 ms then `GiveUpStrategy` returns to `walk`. Heavier (whole strategy transition for a 400 ms event); risk of strategy thrashing if the player stays in contact across multiple frames.

**Recommend C1.** Cheaper, no strategy churn, naturally composes when the player drags along an NPC.

### 4. TALE path

`TaleEntityStrategy` (`TaleEntityStrategy.cs:537-538`) subscribes the same `CrashEventPath` and `HitEventPath`. Add `BumpEventPath` subscription there too, with the same handler logic (operating on `TaleWalkBehavior`'s navigator, which is path-based — same lateral-offset trick if the navigator API matches; if it doesn't, this part may need a small adapter).

---

## Files Affected

**New:**
- `nogameCode/nogame/characters/citizen/CitizenCollisionRouter.cs` — shared dispatch helper.
- `nogameCode/nogame/characters/citizen/BumpEvent.cs` — event class with direction.

**Modified:**
- `nogameCode/nogame/characters/citizen/EntityStrategy.cs` — add `BumpEventPath`, subscribe/unsubscribe in `OnEnter`/`OnExit`, add `_onBumpEvent` handler writing to navigator.
- `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs` — mirror subscription.
- `nogameCode/nogame/characters/citizen/WalkBehavior.cs` — replace inline `OnCollision` logic with `CitizenCollisionRouter.Dispatch`.
- `nogameCode/nogame/characters/citizen/IdleBehavior.cs` — same.
- `nogameCode/nogame/characters/citizen/RecoverBehavior.cs` — same.
- `nogameCode/nogame/characters/citizen/TaleWalkBehavior.cs` — same.
- `nogameCode/nogame/characters/citizen/TaleConversationBehavior.cs` — same.
- `builtin.tools.SegmentNavigator` (path TBD; in `builtin/tools/`) — add `LateralOffset` + decay logic.

**Documentation:**
- `CLAUDE.md` — mention the new BumpEvent in the "ForceSpawn API" or a new "Collision Routing" subsection (one paragraph).
- Move this plan: `docs/roadmap/proposed/` → `docs/roadmap/done/` on completion.

---

## Open Design Questions

1. **Contact normal availability:** `ContactEvent.ContactInfo` — does it expose contact normal? If not, derive direction from `(playerPos - npcPos).Normalize()` using the entities' transforms. Trivial.
2. **Should NPCs bump each other?** Currently NPC bodies are `Npc` (which includes `NpcCharacter`), and `PlayerCharacter` is a different bit. NPC-NPC bumps wouldn't fire under the proposed dispatch (the test is specifically `& PlayerCharacter`). If we want NPCs to step aside for each other too: change the test to `& (PlayerCharacter | NpcCharacter)`. Defer until visual results from the player case are evaluated.
3. **Decay shape:** linear vs ease-out for the offset decay. Start with linear; only tune if it looks bad.
4. **Run vs walk:** does a running player produce a bigger bump? Could scale offset by `playerSpeed`. Start with constant 0.4 m; refine if "running through a crowd" feels wrong.

---

## Test Plan

Manual:

1. Plan A + Plan B + Plan C merged. Build & launch.
2. Walk into a stationary-looking citizen (during their idle).
3. **Expected:** Citizen takes a short lateral step (~0.4 m), plays walk animation briefly, then resumes normal idle / walking. Player continues forward without being blocked.
4. Run through a cluster of 3-4 citizens on a sidewalk.
5. **Expected:** Each citizen steps aside on contact. No collapses, no flees, no flickering between strategies.
6. Punch a citizen mid-bump.
7. **Expected:** Flee takes precedence (the `HitEvent` is queued in the same dispatch; `FleeStrategy` overrides walk + bump). Citizen flees.
8. Run a TALE-populated cluster and bump TALE NPCs.
9. **Expected:** Same step-aside; TALE schedule is preserved (no decay-drop, no conversation interruption beyond the brief lateral offset).
10. Drive a vehicle into a bumped NPC.
11. **Expected:** `CrashEvent` still overrides; NPC collapses normally.

Regression risk: the 5-site `OnCollision` factoring. Test the existing weapon/vehicle paths still work (punch a citizen with the hand armed; drive into a citizen).

---

## Sequencing

- **Phase 1:** Factor the 5 sites into `CitizenCollisionRouter` without behavior change. Land and verify nothing regresses.
- **Phase 2:** Add `BumpEvent` + dispatch + handler + navigator offset. Land.
- **Phase 3:** Tuning pass (offset magnitude, decay duration, speed scaling).

Each phase is its own commit. Phase 1 is the riskiest — it touches all citizen collision sites for a no-op refactor.

---

## Out of Scope

- NPC-NPC step-aside (open question 2).
- Animation-driven stumble (would need new animation in models — keep purely navigational).
- Damage system (Plan B's punch causes flee, not health damage; that's a separate concern).
