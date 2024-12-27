using System;
using System.Collections.Generic;
using System.Linq;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace engine.physics.components;


public struct Statics
{
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
}