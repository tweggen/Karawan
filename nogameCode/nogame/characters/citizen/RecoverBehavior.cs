using System.Net.NetworkInformation;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;
using static engine.Logger;

namespace nogame.characters.citizen;

public class RecoverBehavior : ABehavior
{
    private bool _deathAnimationTriggered = false;
    
    public required CharacterModelDescription CharacterModelDescription;

    private bool _makeDynamic = false;

    public override void OnCollision(ContactEvent cev)
    {
        base.OnCollision(cev);
        
        /*
         * Notify the owning strategy about the collision.
         */
        var me = cev.ContactInfo.PropertiesA;
        var other = cev.ContactInfo.PropertiesB;
        /*
         * Only notify if collided with the player or anything that collides which each
         * other or the player (0x07) or is hit by the player (0x0010)
         */
        if (other != null)
        {
            if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyWeapon))
            {
                I.Get<EventQueue>().Push(new Event(EntityStrategy.HitEventPath(me.Entity), ""));
            }

            if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyVehicle))
            {
                I.Get<EventQueue>().Push(new Event(EntityStrategy.CrashEventPath(me.Entity), ""));
            }
        }
    }
    
    
    private void _behaveKeepAboveGround(in Entity entity, float dt)
    {
        if (!entity.IsAlive) return;

        /*
         * Lift it up to the ground.
         * Warning: Shared with WASDPhysics
         */
        Vector3 vTotalImpulse = new Vector3(0f, -9.81f, 0f);

        float LevelUpThrust = 16f;
        float LevelDownThrust = 16f;

        lock (_engine.Simulation)
        {
            var prefTarget = entity.Get<engine.physics.components.Body>().Reference;

            Vector3 vTargetPos = prefTarget.Pose.Position;
            float heightAtTarget = I.Get<engine.world.MetaGen>().Loader.GetNavigationHeightAt(vTargetPos);

            /*
             * Clip at bottom
             */
            if (vTargetPos.Y < heightAtTarget)
            {
                vTargetPos.Y = heightAtTarget;
                prefTarget.Pose.Position = vTargetPos;
            }

            // TXWTODO: Why do we not apply the downward impulse.
#if false
            float massPerson = CharacterCreator.PhysicsMass;
            entity.Set(new engine.joyce.components.Motion(prefTarget.Velocity.Linear));

            prefTarget.ApplyImpulse(vTotalImpulse * dt * massPerson, new Vector3(0f, 0f, 0f));
#endif

            if (_makeDynamic)
            {
                prefTarget.Awake = true;
            }
        }
    }


    private void _behaveTriggerDeathAnimation(in Entity entity, float dt)
    {
        if (!entity.IsAlive) return;

        if (!_deathAnimationTriggered)
        {
            _deathAnimationTriggered = true;
            if (entity.Has<GPUAnimationState>() && entity.Has<engine.joyce.components.FromModel>())
            {
                ref var cGpuAnimationState = ref entity.Get<GPUAnimationState>();
                ref var cFromModel = ref entity.Get<engine.joyce.components.FromModel>();
                ref var model = ref cFromModel.Model;

                cGpuAnimationState.AnimationState?.SetAnimation(model, CharacterModelDescription.DeathAnimName, 0, true);
            }
        }
        
    }
    

    public override void Behave(in Entity entity, float dt)
    {
        _behaveTriggerDeathAnimation(entity, dt);
        _behaveKeepAboveGround(entity, dt);        
    }


    public override void OnAttach(in engine.Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        
        /*
         * Do not make the thing dynamic, just let it fall.
         */
        if (_makeDynamic)
        {
            /*
             * Make me a dynamic object to respond to the collision.
             */
            ref engine.physics.components.Body cCitizenBody = ref entity0.Get<engine.physics.components.Body>();
            cCitizenBody.PhysicsObject?.MakeDynamic(cCitizenBody.Reference, CharacterCreator.PInertiaCylinder);
        }
    }
}