using System;
using engine;
using engine.behave;
using engine.physics;

namespace builtin.tools;

public class AutoRemoveBehavior : ABehavior
{
    public required float Lifetime { get; set; } = 1.0f;

    private bool _isDoomed = false;


    public override void Behave(in DefaultEcs.Entity entity, float dt)
    {
        if (_isDoomed)
        {
            /*
             * Dooming does not destroy. The engine drains its doomed set later in the
             * frame and skips that drain entirely on every eighth frame and whenever
             * the frame budget is gone, so this entity is still alive on the next
             * frame and BehaviorSystem still ticks us. Without this latch we would
             * doom it again on every one of those frames.
             *
             * Every other self-removing behaviour in the tree carries the same latch
             * (CubeVanishBehavior, PolytopeVanishBehaviour, FollowQuestToastBehavior);
             * this one was the exception, which is what surfaced as the crash inside
             * AddDoomedEntity.
             */
            return;
        }

        Lifetime -= dt;
        if (Lifetime <= 0)
        {
            _isDoomed = true;
            I.Get<engine.Engine>().AddDoomedEntity(entity);
        }
    }
}