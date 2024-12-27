using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using BepuPhysics;
using BepuPhysics.Collidables;
using static engine.Logger;

namespace engine.physics;

public class Object : IDisposable
{
    /*
     * Still not happy with the design. All of the
     * following is instance specific.
     */
    Engine Engine = I.Get<Engine>();
    
    public const uint IsReleased = 1;
    public const uint IsStatic = 2;
    public const uint DontFree = 4;
    public const uint IsReleasing = 8;
    public const uint HaveContactListener = 16;
    public const uint NeedContactListener = 32;
    public const uint IsActivated = 64;
    public const uint IsDeleted = 128;

    public DefaultEcs.Entity Entity = default;
    [JsonInclude]
    public uint Flags = 0;
    public int IntHandle = -1;
    public IList<Action>? ReleaseActions = null;
    
    // Shall be persisted
    [JsonInclude]
    public CollisionProperties? CollisionProperties { get; set; } = null;
    
    /**
     * Used to compute velocity.
     */
    public Vector3 LastPosition = default;
    
    /*
     * Whereas this is more class specific.
     */
    [Newtonsoft.Json.JsonIgnore]
    public Action<ContactEvent>? OnCollision { get; set; } = null;
    
    /**
     * The maximal distance to the player where these objects actually should be
     * part of the physics system.
     */
    [JsonInclude]
    public float MaxDistance = 50f;


    private void _doAddContactListenerNoLock()
    {
        if ((Flags & HaveContactListener) == 0)
        {
            if ((Flags & IsStatic) == 0)
            {
                I.Get<engine.physics.API>().AddDynamicContactListener(Entity);
            }
            else
            {
                I.Get<engine.physics.API>().AddStaticContactListener(Entity);
            }
            Flags |= HaveContactListener;
        }
    }


    private void _doRemoveContactListenerNoLock()
    {
        if ((Flags & HaveContactListener) != 0)
        {
            if ((Flags & IsStatic) == 0)
            {
                I.Get<engine.physics.API>().RemoveDynamicContactListener(Entity, new BodyHandle(IntHandle));
            } else
            {
                I.Get<engine.physics.API>().RemoveStaticsContactListener(Entity);
            }

            Flags &= ~HaveContactListener;
        }
    }


    public void Activate()
    {
        lock (this.Engine.Simulation)
        {
            if ((Flags & IsActivated) != 0)
            {
                ErrorThrow($"Entity {Entity} was supposed not to be activated.",
                    m => new InvalidOperationException(m));
                return;
            }

            Flags |= IsActivated;

            if ((Flags & NeedContactListener) != 0)
            {
                _doAddContactListenerNoLock();
            }
        }

        if (CollisionProperties != null)
        {
            if ((Flags & IsStatic) == 0)
            {
                BodyHandle bh = new(IntHandle);
                I.Get<engine.physics.API>().AddCollisionEntry(bh, CollisionProperties);
            }
            else
            {
                I.Get<engine.physics.API>().AddCollisionEntry(new StaticHandle(IntHandle), CollisionProperties);
            }
        }

        I.Get<engine.physics.ObjectCatalogue>().AddObject(this);
    }


    public void Dispose()
    {
        I.Get<engine.physics.ObjectCatalogue>().RemoveObject(this);

        if ((Flags & IsDeleted) == 0)
        {
            ErrorThrow<InvalidOperationException>($"Trying to dispose undeleted object");
        }
        if (CollisionProperties != null)
        {
            if ((Flags & IsStatic) == 0)
            {
                BodyHandle bh = new(IntHandle);
                I.Get<engine.physics.API>().RemoveCollisionEntry(bh);
            }
            else
            {
                I.Get<engine.physics.API>().RemoveCollisionEntry(new StaticHandle(IntHandle));
            }
        }
        lock (Engine.Simulation)
        {
            if ((Flags & (DontFree|IsReleased)) != 0)
            {
                return;
            }

            if (IntHandle == -1)
            {
                Warning("Tried to dispose physics body without handle.");
                return;
            }

            Flags |= IsReleasing;
            
            if ((Flags & IsStatic) != 0)
            {
                BepuPhysics.StaticHandle sh = new(IntHandle);
                Engine.Simulation.Statics.Remove(sh);
                _doRemoveContactListenerNoLock();
            }
            else
            {
                {
                    BepuPhysics.BodyHandle bh = new(IntHandle);
                    BodyReference prefBody = Engine.Simulation.Bodies.GetBodyReference(bh);
                    if (prefBody.Constraints.Count > 0)
                    {
                        Error(
                            $"Rejecting to remove body {IntHandle} from entity {Entity}");
                    }
                }
                _doRemoveContactListenerNoLock();

                ref var location = ref Engine.Simulation.Bodies.HandleToLocation[IntHandle];
                if (location.SetIndex < 0 || location.Index < 0)
                {
                    Warning("Handle already had been removed.");
                }
                else
                {
#if true
                    actions.RemoveBody.Execute(Engine.PLog, Engine.Simulation, IntHandle);
#else
                    Engine.Simulation.Bodies.Remove(new BodyHandle(bh));
#endif
                }
            }

            if (ReleaseActions != null)
            {
                foreach (var releaseAction in ReleaseActions)
                {
                    releaseAction();
                }
            }

            Flags |= IsReleased;
            Flags &= ~IsReleasing;
        }
    }


    public void MarkDeleted()
    {
        lock (this.Engine.Simulation)
        {
            if ((Flags & IsDeleted) != 0)
            {
                ErrorThrow<InvalidOperationException>($"Already deleted {IntHandle}");
            }

            Flags |= IsDeleted;
        }
    }
    

    public Object AddContactListener()
    {
        lock (this.Engine.Simulation)
        {
            Flags |= NeedContactListener;
            
            /*
             * If the body already is activated, we need to add the contact
             * listener immediately.
             *
             * Otherwise, it will be added while activating the body.
             */
            if ((Flags & IsActivated) != 0)
            {
                _doAddContactListenerNoLock();                             
            }
        }

        return this;
    }

    
    public Object RemoveContactListener()
    {
        lock (this.Engine.Simulation)
        {
            Flags &= ~NeedContactListener;

            if ((Flags & IsActivated) != 0)
            {
                _doRemoveContactListenerNoLock();
            }
        }

        return this;
    }


    public void BecomeDynamic()
    {
        AddContactListener();
    }


    public void BecomeKinematic()
    {
        RemoveContactListener();
    }


    public Object()
    {
        Engine = I.Get<Engine>();
    }
    
    
    public Object(DefaultEcs.Entity entity, BepuPhysics.StaticHandle sh)
    {
        Entity = entity;
        Flags |= IsStatic;
        IntHandle = sh.Value;
    }

    
    public Object(DefaultEcs.Entity entity, BepuPhysics.BodyHandle bh)
    {
        Entity = entity;
        IntHandle = bh.Value;
    }


    /**
     * Must be called with simulation locked!!
     */
    public Object(
        Engine engine,
        DefaultEcs.Entity entity,
        Vector3 v3Position, Quaternion qOrientation,
        BodyInertia inertia, BepuPhysics.Collidables.TypedIndex shape)
    {
        Entity = entity;
        IntHandle = actions.CreateDynamic.Execute(engine.PLog, engine.Simulation, v3Position, qOrientation, inertia, shape);
    }
    
    
    /**
     * Must be called with simulation locked!!
     */
    public Object(
        Engine engine,
        DefaultEcs.Entity entity,
        Vector3 v3Position, Quaternion qOrientation,
        BepuPhysics.Collidables.TypedIndex shape)
    {
        Entity = entity;
        IntHandle = actions.CreateKinematic.Execute(engine.PLog, engine.Simulation, v3Position, qOrientation, shape);
    }

    
    /**
     * Must be called with simulation locked!!
     */
    public Object(
        Engine engine,
        DefaultEcs.Entity entity,
        BepuPhysics.Collidables.TypedIndex shape)
    {
        Entity = entity;
        IntHandle = actions.CreateKinematic.Execute(engine.PLog, engine.Simulation, Vector3.Zero, Quaternion.Identity, shape);
    }
}