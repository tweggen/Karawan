using System;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.physics;
using nogame.cities;
using static engine.Logger;

namespace nogame.characters.citizen;

public class Behavior : builtin.tools.SimpleNavigationBehavior
{
    /**
     * This is the entity that shall be updated to have the proper animation running.
     */
    public DefaultEcs.Entity EntityAnimation;

    public required CharacterModelDescription CharacterModelDescription;
    
    public static uint NDrawCallsPerCharacterBatch { get; set; } = 2;

    public float _previousSpeed = Single.MinValue;

    /**
     * How long should this behavior not set up any animation
     */
    private int _debugNoAnimation = 60;
    
    /**
     * Verify that the animation of the character matches the behavior.
     */
    private void _updateAnimation(DefaultEcs.Entity entity)
    {
        if (!entity.IsAlive) return;
        float speed = (Navigator as SegmentNavigator)!.Speed;
        if (speed == _previousSpeed) return;
        /*
         * Ensure we have a gpu navigation state component, including the animation
         * state.
         */
        if (!entity.Has<engine.joyce.components.GPUAnimationState>())
        {
            entity.Set(new engine.joyce.components.GPUAnimationState()
            {
                AnimationState = CharacterModelDescription.AnimationState 
            });
        }
        
        ref var cGpuAnimationState = ref entity.Get<engine.joyce.components.GPUAnimationState>();
        ref var cFromModel = ref entity.Get<engine.joyce.components.FromModel>();
        ref var model = ref cFromModel.Model;
        string strAnimation;

        if (speed > 7f / 3.6f)
        {
            strAnimation = CharacterModelDescription.RunAnimName;
        }
        else if (speed > 0f)
        {
            strAnimation = CharacterModelDescription.WalkAnimName;
        }
        else
        {
            strAnimation = CharacterModelDescription.IdleAnimName;
        }

        _previousSpeed = speed;
        cGpuAnimationState.AnimationState?.SetAnimation(model, strAnimation, 0);
    }

    
    public override void OnCollision(ContactEvent cev)
    {
        base.OnCollision(cev);
        var me = cev.ContactInfo.PropertiesA;
        // _engine.AddDoomedEntity(me.Entity);
        
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
            new nogame.characters.citizen.AfterCrashBehavior(_engine, me.Entity)
            {
                CharacterModelDescription = CharacterModelDescription
            };
    }


    public override void Behave(in Entity entity, float dt)
    {
        base.Behave(in entity, dt);
        _updateAnimation(entity);
    }


    public override void Sync(in DefaultEcs.Entity entity)
    {
        base.Sync(entity);
    }
    

    public Behavior()
    {
    }

}