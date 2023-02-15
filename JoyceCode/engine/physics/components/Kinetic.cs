using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.physics.components
{
    public struct Kinetic
    {
        public BepuPhysics.BodyReference Reference;

        public Kinetic(in BodyReference reference)
        {
            Reference = reference;
        }

    }
}
