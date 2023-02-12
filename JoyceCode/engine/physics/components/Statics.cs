using System;
using System.Collections.Generic;
using System.Linq;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace engine.physics.components
{
    internal class Statics
    {
        public IList<StaticHandle> Handles;
        public IList<TypedIndex> Shapes;

        public Statics(IList<StaticHandle> listHandles, IList<TypedIndex> listShapes)
        {
            if( null != listHandles)
            {
                StaticHandle[] handles = listHandles.ToArray();
                Handles = handles;
            }
            else
            {
                Handles = null;
            }
            if( null != listShapes )
            {
                TypedIndex[] shapes = listShapes.ToArray();
                Shapes = shapes;
            }
            else
            {
                Shapes = null;
            }
        }
    }
}
