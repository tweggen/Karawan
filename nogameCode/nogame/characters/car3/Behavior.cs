using System;
using nogame.cities;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.physics;
using engine.world;
using static engine.Logger;

namespace nogame.characters.car3;

internal class Behavior :
    builtin.tools.SimpleNavigationBehavior
{
    private StreetNavigationController _snc;
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
                CharacterCreator.PInertiaSphere);
            // TXWTODO: I would like to have the object stop more realistic. This is why I have a physics engine.
            cCarBody.Reference.MotionState.Velocity = Vector3.Zero;
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
    

    public Behavior()
    {
    }
}
