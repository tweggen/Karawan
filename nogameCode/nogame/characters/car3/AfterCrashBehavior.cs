using System.Numerics;
using DefaultEcs;
using engine;

namespace nogame.characters.car3;

public class AfterCrashBehavior : IBehavior
{
    private engine.Engine _engine;
    private DefaultEcs.Entity _entity;

    private IBehavior _oldBehavior = null;

    static private float LIFETIME = 10f;
    private float t = 0;
    
    
    public void Behave(in Entity entity, float dt)
    {
        if (t < LIFETIME)
        {
            t += dt;
            var prefTarget = entity.Get<engine.physics.components.Body>().Reference;

            /*
             * Lift it up to the ground.
             * Warning: Shared with WASDPhysics
             */
            Vector3 vTotalImpulse = new Vector3(0f, 9.81f, 0f);

            float LevelUpThrust = 16f;
            float LevelDownThrust = 16f;

            Vector3 vTargetPos = prefTarget.Pose.Position;
            Vector3 vTargetVelocity = prefTarget.Velocity.Linear;
            float heightAtTarget = engine.world.MetaGen.Instance().Loader.GetNavigationHeightAt(vTargetPos);
            {
                var properDeltaY = 0;
                var deltaY = vTargetPos.Y - (heightAtTarget+properDeltaY);
                const float threshDiff = 0.01f;

                Vector3 impulse;
                float properVelocity = 0f;
                if ( deltaY < -threshDiff )
                {
                    properVelocity = LevelUpThrust; // 1ms-1 up.
                }
                else if( deltaY > threshDiff )
                {
                    properVelocity = -LevelDownThrust; // 1ms-1 down.
                }
                float deltaVelocity = properVelocity - vTargetVelocity.Y;
                float fireRate = deltaVelocity;
                impulse = new Vector3(0f, fireRate, 0f);
                vTotalImpulse += impulse;
            }
            
            
            /*
             * Clip at bottom
             */
            if( vTargetPos.Y < heightAtTarget )
            {
                vTargetPos.Y = heightAtTarget;
                prefTarget.Pose.Position = vTargetPos;
                vTotalImpulse += new Vector3(0f, 10f, 0f);
            }

            /*
             * This is the same as the physics creation in playerhover.
             * TXWTODO: Deduplicate and consolidate.
             */
            float massShip = 500f;
            entity.Set(new engine.joyce.components.Motion(prefTarget.Velocity.Linear));
            prefTarget.ApplyImpulse(vTotalImpulse * dt * massShip, new Vector3(0f, 0f, 0f));
        }
        else
        {
            if (null != _oldBehavior)
            {
                var cCarDynamic = entity.Get<engine.physics.components.Body>();
                cCarDynamic.Flags |= engine.physics.components.Body.DONT_FREE_PHYSICS;
                entity.Set(cCarDynamic);
                entity.Remove<engine.physics.components.Body>();
                entity.Set(
                    new engine.physics.components.Kinetic(
                        cCarDynamic.Reference, 
                        cCarDynamic.CollisionProperties));
                entity.Get<engine.behave.components.Behavior>().Provider = _oldBehavior;
            }
        }
    }

    
    public AfterCrashBehavior(engine.Engine engine0, DefaultEcs.Entity entity0)
    {
        _engine = engine0;
        _entity = entity0;
        if (_entity.Has<engine.behave.components.Behavior>())
        {
            _oldBehavior = _entity.Get<engine.behave.components.Behavior>().Provider;
        }
    }
}