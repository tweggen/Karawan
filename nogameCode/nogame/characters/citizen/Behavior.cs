using System;
using System.Numerics;
using engine.physics;
using static engine.Logger;

namespace nogame.characters.citizen;

public class Behavior : builtin.tools.SimpleNavigationBehavior
{
    public override void OnCollision(ContactEvent cev)
    {
        base.OnCollision(cev);
        var me = cev.ContactInfo.PropertiesA;
        _engine.AddDoomedEntity(me.Entity);
        
        #if false
        ref engine.physics.components.Body cCitizenBody = ref me.Entity.Get<engine.physics.components.Body>();

        /*
         * Become a dynamic thing with the proper inertia.
         */
        lock (_engine.Simulation)
        {
            /*
             * We need to call Simulation.Bodies.SetLocalInertia to remove the kinematic from a
             * couple of lists.
             */
            _engine.Simulation.Bodies.SetLocalInertia(
                cCitizenBody.Reference.Handle,
                CharacterCreator.PInertiaSphere);
            // TXWTODO: I would like to have the object stop more realistic. This is why I have a physics engine.
            cCitizenBody.Reference.MotionState.Velocity = Vector3.Zero;
        }

        cCitizenBody.PhysicsObject.Flags |= engine.physics.Object.IsDynamic;
        cCitizenBody.PhysicsObject.AddContactListener();

        /*
         * Replace the previous behavior with the after crash behavior.
         */
        me.Entity.Get<engine.behave.components.Behavior>().Provider =
            new nogame.characters.citizen.AfterCrashBehavior(_engine, me.Entity);
    #endif
    }

    
    public override void Sync(in DefaultEcs.Entity entity)
    {
        base.Sync(entity);
    }
    

    public Behavior()
    {
    }

}