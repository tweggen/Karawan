using System.Numerics;

namespace engine.behave.components;

public struct Particle
{
    public Vector3 Velocity;
    public int TimeToLive;
    public Quaternion Spin;
}