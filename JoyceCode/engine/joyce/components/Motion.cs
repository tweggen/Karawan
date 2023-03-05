using System.Numerics;

namespace engine.joyce.components
{

    public struct Motion
    {
        public Vector3 Velocity;

        public Motion(in Vector3 velocity)
        {
            Velocity = velocity;
        }
            
    }

}