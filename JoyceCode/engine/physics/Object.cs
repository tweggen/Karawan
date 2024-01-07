using System;
using System.Collections.Generic;
using System.ComponentModel;
using BepuPhysics;
using static engine.Logger;

namespace engine.physics;

public class Object : IDisposable
{
    public const uint IsReleased = 1;
    public const uint IsStatic = 2;
    public const uint DontFree = 4;
    public const uint IsReleasing = 8;
    public const uint HaveContactListener = 16;

    public DefaultEcs.Entity Entity;
    public uint Flags = 0;
    //public BepuPhysics.BodyReference Reference = default;
    public int IntHandle = -1;
    public IList<Action> ReleaseActions = null;

    
    public void Dispose()
    {
        Engine engine = I.Get<Engine>();
        
        lock (engine.Simulation)
        {
            if ((Flags & (DontFree|IsReleased)) != 0)
            {
                return;
            }

            if (IntHandle == -1)
            {
                Warning("Tried to dispose physics body without handle."):;
                return;
            }

            Flags |= IsReleasing;
            
            if ((Flags & IsStatic) != 0)
            {
                BepuPhysics.StaticHandle sh = new(IntHandle);
                engine.Simulation.Statics.Remove(sh);
            }
            else
            {
                BepuPhysics.BodyHandle bh = new(IntHandle);
                
                if ((Flags & HaveContactListener) != 0)
                {
                    I.Get<engine.physics.API>().RemoveContactListener(Entity, bh);
                    Flags &= ~HaveContactListener;
                }

                ref var location = ref engine.Simulation.Bodies.HandleToLocation[IntHandle];
                if (location.SetIndex < 0 || location.Index < 0)
                {
                    Warning("Handle already had been removed.");
                }
                else
                {
                    engine.Simulation.Bodies.Remove(bh);
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


    public Object AddContactListener()
    {
        I.Get<engine.physics.API>().AddContactListener(Entity);
    }

    
    public Object(DefaultEcs.Entity entity, BepuPhysics.StaticHandle sh)
    {
        Entity = entity;
        Flags |= IsStatic;
        IntHandle = sh.Value;
    }

    
    public Object(DefaultEcs.Entity entity, BepuPhysics.BodyHandle sh)
    {
        Entity = entity;
        IntHandle = sh.Value;
    }
}