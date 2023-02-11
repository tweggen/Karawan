using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.physics.components
{
    public struct Body
    {
        public BepuPhysics.BodyReference Reference;

        public Body(in BodyReference reference)
        {
            Reference = reference;
        }
    }
}
