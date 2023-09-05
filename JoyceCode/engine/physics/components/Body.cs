using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.physics.components
{
    public struct Body
    {
        static public uint DONT_FREE_PHYSICS = 1;
        public BepuPhysics.BodyReference Reference;
        public CollisionProperties CollisionProperties;
        public uint Flags = 0;

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
