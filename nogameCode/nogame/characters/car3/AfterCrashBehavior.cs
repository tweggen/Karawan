using System.Net.NetworkInformation;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.physics;
using static engine.Logger;

namespace nogame.characters.car3;

public class AfterCrashBehavior : ABehavior
{
    private engine.Engine _engine;
    private DefaultEcs.Entity _entity;

    private IBehavior _oldBehavior = null;

    static private float LIFETIME = 10f;
    private float t = 0;
    

    public override void OnCollision(ContactEvent cev)
    {
        var me = cev.ContactInfo.PropertiesA;
        engine.physics.components.Kinetic cCarKinetic;

        if (me.Entity.Has<engine.physics.components.Body>())
        {
            /*
             * I collided again with something, so increase my timer. 
             */
            t = LIFETIME - (builtin.tools.RandomSource.Instance.GetFloat() + 0.5f);
            
            Trace("Another collision with me, a dynamic being.");
        }
        else
        {
            Trace("I wasn't expecting to be a kinematic physics object here.");
            return;
        }
    }

    public override void Sync(in DefaultEcs.Entity entity)
    {
    }
    
    
    public override void Behave(in Entity entity, float dt)
    {
        var prefTarget = entity.Get<engine.physics.components.Body>().Reference;
        if (t < LIFETIME)
        {
            t += dt;

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
            lock (_engine.Simulation)
            {
                prefTarget.ApplyImpulse(vTotalImpulse * dt * massShip, new Vector3(0f, 0f, 0f));
                prefTarget.Awake = true;
            }
        }
        else
        {
            if (null != _oldBehavior)
            {
                var cCarDynamic = entity.Get<engine.physics.components.Body>();
                cCarDynamic.Flags |= engine.physics.components.Body.DONT_FREE_PHYSICS;
                entity.Set(cCarDynamic);
                entity.Remove<engine.physics.components.Body>();

                lock (_engine.Simulation)
                {
                    prefTarget.Awake = false;
                    prefTarget.BecomeKinematic();
                }

                entity.Set(
                    new engine.physics.components.Kinetic(
                        cCarDynamic.Reference, 
                        cCarDynamic.CollisionProperties));
                entity.Get<engine.behave.components.Behavior>().Provider = _oldBehavior;
                _oldBehavior.Sync(entity);
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