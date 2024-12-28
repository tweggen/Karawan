using System;
using System.Collections.Generic;
using System.Linq;
using BepuPhysics;

namespace engine.physics.components;


public struct Statics
{
    /**
     * Can describe these statics, like what they are supposed to
     * collide with or emit events, or what model they belong to.
     */
    public physics.Object? PhysicsObject;
    
    
    /**
     * Static handles associated with this component
     */
    public IList<StaticHandle> Handles;

    
    /**
     * Release function to free any additional data beyond the handles,
     * like shapes, data structures carrying shapes.
     */
    public IList<Action> ReleaseActions;
    

    public Statics(IList<StaticHandle> listHandles, IList<Action> releaseActions)
    {
        if (null != listHandles)
        {
            StaticHandle[] handles = listHandles.ToArray();
            Handles = handles;
        }
        else
        {
            Handles = null;
        }

        if (null != releaseActions)
        {
            ReleaseActions = releaseActions;
        }
        else
        {
            ReleaseActions = null;
        }
    }

    
    public Statics(StaticHandle sh)
    {
        Handles = new List<StaticHandle>() { sh };
        ReleaseActions = new List<Action>()
        {
            () =>
            {
                // FIXME: Release the sh.
            }
        };
    }
    
    
    public Statics(Object? po, StaticHandle sh)
    {
        PhysicsObject = po;
        Handles = new List<StaticHandle>() { sh };
        ReleaseActions = new List<Action>()
        {
            () =>
            {
                // FIXME: Release the sh.
            }
        };
    }


    /**
     * Constructor for deserialization. Serializeable properties would be
     * setup by hand. 
     */
    public Statics()
    {
    }
}