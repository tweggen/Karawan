using System.Net.NetworkInformation;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.joyce;
using engine.joyce.components;
using engine.physics;
using static engine.Logger;

namespace nogame.characters.citizen;

public class AfterCrashBehavior : ABehavior
{
    static private float LIFETIME = 10f;
    private float t = 0;
    private bool _deathAnimationTriggered = false;
    
    public required CharacterModelDescription CharacterModelDescription;
    

    public override void OnCollision(ContactEvent cev)
    {
        var me = cev.ContactInfo.PropertiesA;

        /*
         * I collided again with something, so increase my timer. 
         */
        t = builtin.tools.RandomSource.Instance.GetFloat()*2.0f + 0.5f;
    }
    

    public override void Behave(in Entity entity, float dt)
    {
        if (!_deathAnimationTriggered)
        {
            _deathAnimationTriggered = true;
            if (entity.Has<GPUAnimationState>() && entity.Has<engine.joyce.components.FromModel>())
            {
                ref var cGpuAnimationState = ref entity.Get<GPUAnimationState>();
                ref var cFromModel = ref entity.Get<engine.joyce.components.FromModel>();
                ref var model = ref cFromModel.Model;

                cGpuAnimationState.AnimationState?.SetAnimation(model, CharacterModelDescription.DeathAnimName, 0);
            }
        }
        if (t < LIFETIME)
        {
            t += dt;

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

                prefTarget.Awake = true;
            }
        }
        else
        {
            /*
             * Remove any behavior and kill entity.
             * TXWTODO: This should go to death strategy.
             */
            entity.Remove<engine.behave.components.Behavior>();
            _engine.AddDoomedEntity(entity);
        }
    }


    public override void OnAttach(in engine.Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        
        /*
         * Make me a dynamic object to respond to the collision.
         */
        ref engine.physics.components.Body cCitizenBody = ref entity0.Get<engine.physics.components.Body>();
        cCitizenBody.PhysicsObject?.MakeDynamic(cCitizenBody.Reference, CharacterCreator.PInertiaCylinder);
    }
}