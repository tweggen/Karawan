using System.Numerics;

namespace engine.joyce.components
{

    public struct Motion
    {
        public Vector3 Velocity;

        public override string ToString()
        {
            return $"Velocity: {Velocity.ToString()}";
        }

        public Motion(in Vector3 velocity)
        {
            Velocity = velocity;
        }
            
    }

}