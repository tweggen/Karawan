using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.physics.components
{
    public struct Kinetic
    {
        public BepuPhysics.BodyReference Reference;
        public physics.CollisionProperties CollisionProperties;

        /**
         * Release function to free any additional data beyond the handles,
         * like shapes, data structures carrying shapes.
         */
        public IList<Action> ReleaseActions;

        public Kinetic(in BodyReference reference, in CollisionProperties collisionProperties)
        {
            Reference = reference;
            CollisionProperties = collisionProperties;
        }

    }
}
