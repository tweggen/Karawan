using System;
using nogame.cities;
using System.Numerics;
using engine.physics;
using engine.world;
using static engine.Logger;

namespace nogame.characters.car3;

internal class Behavior : engine.ABehavior
{
    engine.Engine _engine;
    engine.world.ClusterDesc _clusterDesc;
    engine.streets.StreetPoint _streetPoint;
    StreetNavigationController _snc;
    private Quaternion _qPrevRotation = Quaternion.Identity;


    /**
     * Handle my collisions as a kinematic object.
     * As long this behavior is active, I expect to be an kinematic object.
     * If something collides with me that has collision detection active
     * (which I do not), this collision handler is called.
     * I will transform into a dynamic object, using the new behavior AfterCrashBehavior.
     */
    public override void OnCollision(ContactEvent cev)
    {
        var me = cev.ContactInfo.PropertiesA;
        engine.physics.components.Kinetic cCarKinetic;

        if (me.Entity.Has<engine.physics.components.Kinetic>())
        {
            /*
             * Get a copy of the original.
             */
            cCarKinetic = me.Entity.Get<engine.physics.components.Kinetic>();
            cCarKinetic.Flags |= engine.physics.components.Kinetic.DONT_FREE_PHYSICS;

            /*
             * Prevent value from automatic removal, patching it in place.
             */
            me.Entity.Set(cCarKinetic);
            me.Entity.Remove<engine.physics.components.Kinetic>();

            lock (_engine.Simulation)
            {
                // TXWTODO: THis is a bit hard coded. 
                BepuPhysics.Collidables.Sphere pbodySphere = new(GenerateCharacterOperator.PhysicsRadius);
                var pinertiaSphere = pbodySphere.ComputeInertia(GenerateCharacterOperator.PhysicsMass);
                cCarKinetic.Reference.SetLocalInertia(pinertiaSphere);
                cCarKinetic.Reference.Awake = true;
            }

            me.Entity.Set(new engine.physics.components.Body(cCarKinetic.Reference, me));

            /*
             * Replace the previous behavior with the after crash behavior.
             */
            me.Entity.Get<engine.behave.components.Behavior>().Provider =
                new nogame.characters.car3.AfterCrashBehavior(_engine, me.Entity);
        }
        else
        {
            Trace("I wasn't expecting to be a kinematic dynamic object here.");
        }
    }

    
    public override void Sync(in DefaultEcs.Entity entity)
    {
        if (entity.Has<engine.physics.components.Kinetic>())
        {
            var prefTarget = entity.Get<engine.physics.components.Kinetic>().Reference;
            Vector3 vPos3 = prefTarget.Pose.Position;
            Quaternion qRotation = prefTarget.Pose.Orientation;
            _snc.TakeCurrentPosition(vPos3, qRotation);
        }

    }


    public override void Behave(in DefaultEcs.Entity entity, float dt)
    {
        _snc.NavigatorBehave(dt);

        Quaternion qOrientation = _snc.NavigatorGetOrientation();
        qOrientation = Quaternion.Slerp(_qPrevRotation, qOrientation, 0.1f);
        _qPrevRotation = qOrientation;
        engine.Implementations.Get<engine.transform.API>().SetTransforms(
            entity,
            true, 0x0000ffff,
            qOrientation,
            _snc.NavigatorGetWorldPos() with
            {
                Y = _clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight
            }
        );
    }

    
    public Behavior SetSpeed(float speed)
    {
        _snc.NavigatorSetSpeed(speed);
        return this;
    }
    
    
    public Behavior(
        in engine.Engine engine0,
        in engine.world.ClusterDesc clusterDesc0,
        in engine.streets.StreetPoint streetPoint0
    )
    {
        _engine = engine0;
        _clusterDesc = clusterDesc0;
        _streetPoint = streetPoint0;
        _snc = new StreetNavigationController(_clusterDesc, _streetPoint);
    }
}
