using System;
using nogame.cities;
using System.Numerics;
using engine.physics;
using engine.world;
using static engine.Logger;

namespace nogame.characters.car3;

internal class Behavior : builtin.tools.SimpleNavigationBehavior
{
    private engine.Engine _engine;
    private engine.world.ClusterDesc _clusterDesc;
    private engine.streets.StreetPoint _streetPoint;
    private StreetNavigationController _snc;
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
        base.OnCollision(cev);
        var me = cev.ContactInfo.PropertiesA;
        engine.physics.components.Kinetic cCarKinetic;

        if (me.Entity.Has<engine.physics.components.Kinetic>())
        {
            /*
             * Get a copy of the original.
             */
            cCarKinetic = me.Entity.Get<engine.physics.components.Kinetic>();
            var bodyHandle = cCarKinetic.Reference.Handle;
            cCarKinetic.Flags |= engine.physics.components.Kinetic.DONT_FREE_PHYSICS;

            /*
             * Prevent value from automatic removal, patching it in place.
             */
            me.Entity.Set(cCarKinetic);
            me.Entity.Remove<engine.physics.components.Kinetic>();

            lock (_engine.Simulation)
            {
                // TXWTODO: This is a bit hard coded. 
                BepuPhysics.Collidables.Sphere pbodySphere = new(GenerateCharacterOperator.PhysicsRadius);
                var pinertiaSphere = pbodySphere.ComputeInertia(GenerateCharacterOperator.PhysicsMass);
                
                /*
                 * We need to call Simulation.Bodies.SetLocalInertia to remove the kinematic from a
                 * couple of lists. 
                 */
                _engine.Simulation.Bodies.SetLocalInertia(bodyHandle, pinertiaSphere);
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
        base.Sync(entity);
        if (entity.Has<engine.physics.components.Kinetic>())
        {
            var prefTarget = entity.Get<engine.physics.components.Kinetic>().Reference;
            Vector3 vPos3 = prefTarget.Pose.Position;
            Quaternion qRotation = prefTarget.Pose.Orientation;
            _snc.NavigatorSetTransformation(vPos3, qRotation);
        }

    }


    public float Speed
    {
        get => _snc.Speed;
        set => _snc.Speed = value;
    }
    
    
    public Behavior(
        in engine.Engine engine0,
        in engine.world.ClusterDesc clusterDesc0,
        in engine.streets.StreetPoint streetPoint0
    ) : base(
        engine0, 
        new StreetNavigationController(clusterDesc0,streetPoint0)
        {
            Height = MetaGen.ClusterNavigationHeight
        })
    {
        _engine = engine0;
        _snc = Navigator as StreetNavigationController;
        _clusterDesc = clusterDesc0;
        _streetPoint = streetPoint0;
    }
}
