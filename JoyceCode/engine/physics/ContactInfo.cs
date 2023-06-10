using System.Numerics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;

namespace engine.physics
{
    public class ContactInfo
    {
        public CollidableReference EventSource;
        public CollidablePair ContactPair;
        public Vector3 ContactOffset;
        public Vector3 ContactNormal;
        public float Depth;
        public CollisionProperties PropertiesA;
        public CollisionProperties PropertiesB;

        public ContactInfo(
            CollidableReference eventSource, 
            CollidablePair contactPair, 
            Vector3 contactOffset, 
            Vector3 contactNormal, 
            float depth)
        {
            EventSource = eventSource;
            ContactPair = contactPair;
            ContactOffset = contactOffset;
            ContactNormal = contactNormal;
            Depth = depth;
        }
    }
}
