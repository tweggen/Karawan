using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.physics.components
{
    public struct Body
    {
        public BepuPhysics.BodyReference Reference;
        public CollisionProperties CollisionProperties;

        /**
         * Release function to free any additional data beyond the handles,
         * like shapes, data structures carrying shapes.
         */
        public IList<Action> ReleaseActions;

        public Body(in BodyReference bodyReference, in CollisionProperties collisionProperties)
        {
            Reference = bodyReference;
            CollisionProperties = collisionProperties;
        }
    }
}
