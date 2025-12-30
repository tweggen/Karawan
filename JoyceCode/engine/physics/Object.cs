using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using BepuPhysics;
using BepuPhysics.Collidables;
using Silk.NET.Maths;
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
    public const uint IsGenerated = 256;
    public const uint IsDynamic = 512;

    public DefaultEcs.Entity Entity = default;
    [JsonInclude]
    public uint Flags = 0;
    public int IntHandle = -1;
    public IList<Action>? ReleaseActions = null;
    
    
    public static Vector3 OffPosition = new Vector3(0f, -10000f, 0f);
    
    
    /**
     * Offset of the physics object to the parent object.
     */
    public Vector3 BodyOffset { get; set; } = Vector3.Zero;
    
    /**
     * Additional rotation, before applying the offset, to the parent object.
     */
    public Quaternion BodyRotation { get; set; } = Quaternion.Identity;
    
    /*
     * Now, to create an actual physics object, we use these opcodes
     */
    [JsonInclude]
    public List<actions.ABase>? CreateOpcodes;  
    
    
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
    [JsonIgnore]
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
                I.Get<engine.physics.API>().AddNonstaticContactListener(Entity);
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
                /*
                 * We do not pass the entity, because in many cases it might not exist any more.
                 */
                I.Get<engine.physics.API>().RemoveDynamicContactListener(
                    ((Flags & IsDynamic) != 0)?CollidableMobility.Dynamic:CollidableMobility.Kinematic,
                    new BodyHandle(IntHandle));
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


    /*
     * If the body already is activated, we need to add the contact
     * listener immediately. Or remove it, if no longer required.
     *
     * Otherwise, it will be added while activating the body.
     */
    private void _adjustContactListenerNoLock()
    {
        if (0 == (Flags & IsActivated))
        {
            return;
        }
        
        if (0 != (Flags & NeedContactListener))
        {
            if (0 != (Flags & HaveContactListener))
            {
                
            }
            else
            {
                _doAddContactListenerNoLock();
            }
        }
        else
        {
            if (0 != (Flags & HaveContactListener))
            {
                _doRemoveContactListenerNoLock();
            }
            else
            {
                
            }
        }
    }
    

    public Object AddContactListener()
    {
        lock (this.Engine.Simulation)
        {
            if (0 == (Flags & NeedContactListener))
            {
                Flags |= NeedContactListener;

                _adjustContactListenerNoLock();
            }
        }

        return this;
    }

    
    public Object RemoveContactListener()
    {
        lock (this.Engine.Simulation)
        {
            if (0 != (Flags & NeedContactListener))
            {
                Flags &= ~NeedContactListener;

                _adjustContactListenerNoLock();
            }
        }

        return this;
    }


    public void MakeKinematic(ref BodyReference pref)
    {
        lock (this.Engine.Simulation)
        {
            if (0 == (Flags & IsDynamic)) return;
            /*
             * Caution, we need to remove the contact listener while
             * we still are dynamic.
             */
            RemoveContactListener();
            Flags &= ~IsDynamic;
            
            pref.Awake = false;
            pref.BecomeKinematic();
        }
    }

    
    public void MakeDynamic(in BodyReference pref, in BodyInertia inertia)
    {
        lock (this.Engine.Simulation)
        {
            if (0 != (Flags & IsDynamic)) return;
            Flags |= IsDynamic;
            
            this.Engine.Simulation.Bodies.SetLocalInertia(pref.Handle, inertia);
            pref.MotionState.Velocity = Vector3.Zero;
            
            /*
             * It is important to add the contact listener after the body
             * has been made dynamic.
             */
            AddContactListener();
        }
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


    /**
     * Must be called with simulation locked!!
     */
    public Object(
        Engine engine,
        DefaultEcs.Entity entity,
        BodyInertia inertia,
        BepuPhysics.Collidables.TypedIndex shape,
        Vector3 Position, Quaternion Orientation,
        Vector3 bodyOffset = default
        )
    {
        Entity = entity;
        Flags = IsDynamic;
        IntHandle = actions.CreateDynamic.Execute(engine.PLog, engine.Simulation, Position+BodyOffset, Orientation, inertia, shape);
        BodyOffset = bodyOffset;
    }
    
    
    /**
     * Must be called with simulation locked!!
     */
    public Object(
        Engine engine,
        DefaultEcs.Entity entity,
        BepuPhysics.Collidables.TypedIndex shape,
        Vector3 Position, Quaternion Orientation,
        Vector3 bodyOffset = default
        )
    {
        Entity = entity;
        IntHandle = actions.CreateKinematic.Execute(engine.PLog, engine.Simulation, Position+BodyOffset, Orientation, shape);
        BodyOffset = bodyOffset;
    }

    
    /**
     * Must be called with simulation locked!!
     */
    public Object(
        Engine engine,
        DefaultEcs.Entity entity,
        BepuPhysics.Collidables.TypedIndex shape,
        Vector3 bodyOffset = default)
    {
        Entity = entity;
        IntHandle = actions.CreateKinematic.Execute(engine.PLog, engine.Simulation, BodyOffset, Quaternion.Identity, shape);
        BodyOffset = bodyOffset;
    }
}