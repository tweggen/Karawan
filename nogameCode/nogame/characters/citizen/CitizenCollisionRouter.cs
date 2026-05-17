using System.Numerics;
using engine;
using engine.news;
using engine.physics;

namespace nogame.characters.citizen;

/**
 * Single source of truth for classifying collisions an NPC's physics
 * body receives and translating them into NPC-strategy events.
 *
 * Five NPC behaviors (WalkBehavior, IdleBehavior, RecoverBehavior,
 * TaleWalkBehavior, TaleConversationBehavior) used to each carry their
 * own copy of the same flag-test logic. This class is the one place
 * the rules live.
 *
 * Weapon contact -> HitEvent (NPC strategy reacts by fleeing).
 * Vehicle contact -> CrashEvent (NPC strategy reacts by recovering).
 * Pure player-body contact -> BumpEvent (NPC navigator side-steps).
 *
 * When more than one bit is set (e.g. a player weapon also carries the
 * PlayerCharacter bit -- not the current configuration, but defensive),
 * Hit/Crash take precedence and the bump is suppressed.
 */
internal static class CitizenCollisionRouter
{
    public static bool IsWeapon(CollisionProperties other) =>
        other != null
        && 0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyWeapon);

    public static bool IsVehicle(CollisionProperties other) =>
        other != null
        && 0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyVehicle);

    public static bool IsPlayerCharacter(CollisionProperties other) =>
        other != null
        && 0 != (other.SolidLayerMask & CollisionProperties.Layers.PlayerCharacter);


    public static void Dispatch(CollisionProperties me,
                                CollisionProperties other,
                                Vector3 contactNormal)
    {
        if (me == null || other == null) return;
        var q = I.Get<EventQueue>();

        bool isWeapon = IsWeapon(other);
        bool isVehicle = IsVehicle(other);

        if (isWeapon)
        {
            q.Push(new Event(EntityStrategy.HitEventPath(me.Entity), ""));
        }
        if (isVehicle)
        {
            q.Push(new Event(EntityStrategy.CrashEventPath(me.Entity), ""));
        }
        if (!isWeapon && !isVehicle && IsPlayerCharacter(other))
        {
            q.Push(new BumpEvent(
                EntityStrategy.BumpEventPath(me.Entity),
                contactNormal));
        }
    }
}
