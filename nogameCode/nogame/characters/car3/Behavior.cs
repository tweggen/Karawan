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
    private bool _cutCollisions = (bool) engine.Props.Get("nogame.CutCollision", false);


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
        if (_cutCollisions) return;
        
        var me = cev.ContactInfo.PropertiesA;
        ref engine.physics.components.Body cCarBody = ref me.Entity.Get<engine.physics.components.Body>();

        /*
         * Become a dynamic thing with the proper inertia.
         */
        cCarBody.PhysicsObject.AddContactListener();
        lock (_engine.Simulation)
        {
            /*
             * We need to call Simulation.Bodies.SetLocalInertia to remove the kinematic from a
             * couple of lists. 
             */
            _engine.Simulation.Bodies.SetLocalInertia(
                cCarBody.Reference.Handle,
                GenerateCharacterOperator.PInertiaSphere);
        }

        /*
         * Replace the previous behavior with the after crash behavior.
         */
        me.Entity.Get<engine.behave.components.Behavior>().Provider =
            new nogame.characters.car3.AfterCrashBehavior(_engine, me.Entity);
    }

    
    public override void Sync(in DefaultEcs.Entity entity)
    {
        base.Sync(entity);
        if (!entity.Has<engine.physics.components.Body>())
        {
            ErrorThrow($"I was expecting that {entity} has  no body component.", m => new InvalidOperationException(m));
            return;
        }

        lock (_engine.Simulation)
        {
            var prefTarget = entity.Get<engine.physics.components.Body>().Reference;
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
