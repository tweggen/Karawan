using System.Numerics;
using engine.joyce;

namespace engine.behave.components;

public struct Particle
{
    public Vector3 Position;
    public int TimeToLive;
    public Quaternion Orientation;
    public Vector3 VelocityPerFrame;
    public int _reserved;
    public Quaternion SpinPerFrame;
}