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
    private static readonly engine.Dc _dc = engine.Dc.Physics;

    /*
     * Still not happy with the design. All of the
     * following is instance specific.
     */
    /*
     * NOT a field initialiser, deliberately. Assigned in each constructor body instead.
     *
     * C# emits field initialisers into the prologue of EVERY constructor, before the body runs.
     * On Mono/ARM64 (.NET 9.0.13, Android) a call in that prologue corrupts the constructor's
     * TRAILING value-type argument: it is read 8 bytes early and picks up the last two floats of
     * the preceding parameter. Measured here as `Vector3 bodyOffset` arriving as
     * <Orientation.Z, Orientation.W, 0> instead of <0,0,0>, and reproduced in isolation by
     * engine.physics.AbiProbe case M - which differs from the twelve passing cases in exactly one
     * respect: it has a prologue containing a call.
     *
     * The knock-on was severe: the ship's BodyInertia was then forwarded to CreateDynamic.Execute
     * with the same 8-byte skew, producing an INDEFINITE inertia tensor that amplified angular
     * impulses ~442x and reversed them.
     *
     * Keep every constructor body assigning this explicitly, and do not reintroduce
     * `= I.Get<Engine>()` here.
     */
    Engine Engine;
    
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
    
    
    /*
     * NOT initialised here, for the same reason Engine is not - see the comment above.
     *
     * `Vector3.Zero` and `Quaternion.Identity` are static PROPERTIES, so both compile to a
     * `call` in IL and both land in the prologue of every constructor. The JIT normally
     * intrinsifies them to a zeroing, and an earlier revision of this file assumed that made
     * them safe. That assumption was drawn from a device run which ALSO had the field-by-field
     * inertia repair in HoverModule still in place - remove the repair and the angular runaway
     * comes straight back, with the ctor fix present and probeRev=14. The repair was doing
     * real work, so the prologue was still corrupting the trailing argument.
     *
     * default(Vector3) is <0,0,0> and default(Quaternion) is (0,0,0,0) - NOT identity - so
     * BodyRotation must be assigned explicitly in every constructor body rather than left to
     * the field default. AbiProbe case N isolates this: its prologue contains ONLY these two
     * static property reads and nothing else.
     */
    /**
     * Offset of the physics object to the parent object.
     */
    public Vector3 BodyOffset { get; set; }

    /**
     * Additional rotation, before applying the offset, to the parent object.
     */
    public Quaternion BodyRotation { get; set; }
    
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
                        Error(_dc,
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

    
    /*
     * THE RULE, for every constructor below:
     *
     *     CONSUME EVERY PARAMETER FIRST. DO ANYTHING THAT CALLS AFTERWARDS.
     *
     * What matters is not where a value comes from - the instance registry or a parameter -
     * but WHEN the call happens relative to reading the trailing value-type argument. A call
     * in a field initialiser runs in the prologue, i.e. before the whole body, which is the
     * case AbiProbe M reproduces. A call at the TOP OF THE BODY is the same hazard one step
     * later: the argument has still not been read when the frame is disturbed.
     *
     * So `Engine = I.Get<Engine>()` is not itself the problem, and the two constructors here
     * may keep using the registry. The problem is doing it BEFORE `sh.Value` is read.
     */
    public Object()
    {
        /*
         * No parameters at all, so there is nothing that could be corrupted and the order is
         * free. Kept in the same shape as the others so it does not read as an exception.
         */
        BodyOffset = Vector3.Zero;
        BodyRotation = Quaternion.Identity;
        Engine = I.Get<Engine>();
    }


    public Object(DefaultEcs.Entity entity, BepuPhysics.StaticHandle sh)
    {
        /*
         * `sh` is a TRAILING value-type parameter, so it must be read before any call - and
         * `I.Get<Engine>()`, `Vector3.Zero` and `Quaternion.Identity` are all calls.
         */
        Entity = entity;
        IntHandle = sh.Value;
        Flags |= IsStatic;

        BodyOffset = Vector3.Zero;
        BodyRotation = Quaternion.Identity;
        Engine = I.Get<Engine>();
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

        /*
         * Parameters first - see THE RULE above. Copying them into locals is what actually
         * reads the argument slots, so it must happen before Vector3.Zero and
         * Quaternion.Identity, both of which are static property CALLS.
         */
        Engine = engine;
        Entity = entity;
        Flags = IsDynamic;
        Vector3 v3Position = Position;
        Quaternion qOrientation = Orientation;
        BodyInertia liInertia = inertia;
        BepuPhysics.Collidables.TypedIndex tiShape = shape;
        Vector3 v3BodyOffset = bodyOffset;

        BodyOffset = Vector3.Zero;
        BodyRotation = Quaternion.Identity;

        /*
         * NOTE the body is created at `v3Position + BodyOffset` where BodyOffset is still
         * ZERO - i.e. at v3Position. That is pre-existing behaviour, preserved deliberately:
         * using v3BodyOffset here instead would move every offset body at creation, which is
         * a physics change and must not ride along in a regression fix.
         */
        IntHandle = actions.CreateDynamic.Execute(
            engine.PLog, engine.Simulation, v3Position + BodyOffset, qOrientation, liInertia, tiShape);
        BodyOffset = v3BodyOffset;
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
        // Parameters first - see THE RULE above.
        Engine = engine;
        Entity = entity;
        Vector3 v3Position = Position;
        Quaternion qOrientation = Orientation;
        BepuPhysics.Collidables.TypedIndex tiShape = shape;
        Vector3 v3BodyOffset = bodyOffset;

        BodyOffset = Vector3.Zero;
        BodyRotation = Quaternion.Identity;

        IntHandle = actions.CreateKinematic.Execute(
            engine.PLog, engine.Simulation, v3Position + BodyOffset, qOrientation, tiShape);
        BodyOffset = v3BodyOffset;
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
        // Parameters first - see THE RULE above. bodyOffset is the trailing one.
        Engine = engine;
        Entity = entity;
        BepuPhysics.Collidables.TypedIndex tiShape = shape;
        Vector3 v3BodyOffset = bodyOffset;

        BodyOffset = Vector3.Zero;
        BodyRotation = Quaternion.Identity;

        IntHandle = actions.CreateKinematic.Execute(
            engine.PLog, engine.Simulation, BodyOffset, BodyRotation, tiShape);
        BodyOffset = v3BodyOffset;
    }
}