using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.physics.components
{
    public struct Body
    {
        public BepuPhysics.BodyHandle Handle;

        public Body(in BodyHandle handle)
        {
            Handle = handle;
        }
    }
}
