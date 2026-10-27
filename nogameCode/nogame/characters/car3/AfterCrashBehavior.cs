using System.Net.NetworkInformation;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
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

        /*
         * I collided again with something, so increase my timer. 
         */
        t = LIFETIME - (builtin.tools.RandomSource.Instance.GetFloat() + 0.5f);
    }
    

    public override void Sync(in DefaultEcs.Entity entity)
    {
    }
    
    
    public override void Behave(in Entity entity, float dt)
    {
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

            /*
             * KAR-471: hover over what is BUILT under the car - the road it crashed on, a
             * deck, a cutting below the hillside - and over the terrain only where nothing
             * is. It used to aim at the terrain and clip to it, which threw a car crashed in
             * a road cutting up onto the hillside and pressed one crashed on a bridge down
             * against the deck. The probe takes the simulation lock itself, so it runs
             * outside the lock below.
             */
            Vector3 vProbePos;
            lock (_engine.Simulation)
            {
                vProbePos = entity.Get<engine.physics.components.Body>().Reference.Pose.Position;
            }
            float terrainHeight = I.Get<engine.world.MetaGen>().Loader.GetNavigationHeightAt(vProbePos);
            float? surfaceBelow = engine.physics.HoverSurfaceProbe.SurfaceBelow(
                I.Get<engine.physics.API>(), vProbePos, entity);
            float targetHeight = engine.physics.HoverSurfaceProbe.VehicleTargetHeight(terrainHeight, surfaceBelow);

            lock (_engine.Simulation)
            {
                var prefTarget = entity.Get<engine.physics.components.Body>().Reference;

                Vector3 vTargetPos = prefTarget.Pose.Position;
                Vector3 vTargetVelocity = prefTarget.Velocity.Linear;
                {
                    var deltaY = vTargetPos.Y - targetHeight;
                    const float threshDiff = 0.01f;

                    Vector3 impulse;
                    float properVelocity = 0f;
                    if (deltaY < -threshDiff)
                    {
                        properVelocity = LevelUpThrust; // 1ms-1 up.
                    }
                    else if (deltaY > threshDiff)
                    {
                        properVelocity = -LevelDownThrust; // 1ms-1 down.
                    }

                    float deltaVelocity = properVelocity - vTargetVelocity.Y;
                    float fireRate = deltaVelocity;
                    impulse = new Vector3(0f, fireRate, 0f);
                    vTotalImpulse += impulse;
                }


                /*
                 * The rescue, for a car that has gone through the world: far below the
                 * terrain, falling, and nothing built under it. A car resting in a cutting
                 * is none of these.
                 */
                if (engine.physics.HoverSurfaceProbe.NeedsRescue(
                        terrainHeight, vTargetPos.Y, vTargetVelocity.Y, surfaceBelow))
                {
                    vTargetPos.Y = terrainHeight;
                    prefTarget.Pose.Position = vTargetPos;
                    prefTarget.Velocity.Linear = vTargetVelocity with { Y = 0f };
                }

                /*
                 * This is the same as the physics creation in playerhover.
                 * TXWTODO: Deduplicate and consolidate.
                 */
                float massShip = 500f;
                entity.Set(new engine.joyce.components.Motion(prefTarget.Velocity.Linear));

                prefTarget.ApplyImpulse(vTotalImpulse * dt * massShip, new Vector3(0f, 0f, 0f));
                prefTarget.Awake = true;
            }
        }
        else
        {
            /*
             * Switch back to previous behavior, making the car kinematic.
             */
            if (null != _oldBehavior)
            {
                ref engine.physics.components.Body cCarBody = ref entity.Get<engine.physics.components.Body>();
                cCarBody.PhysicsObject.RemoveContactListener();
                cCarBody.PhysicsObject.Flags &= ~Object.IsDynamic;
                lock (_engine.Simulation)
                {
                    var prefTarget = cCarBody.Reference;
                    prefTarget.Awake = false;
                    prefTarget.BecomeKinematic();
                }
                // TXWTODO: Test old behavior for being non-NULL.
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