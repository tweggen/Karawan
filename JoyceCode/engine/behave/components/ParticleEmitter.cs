using System.Numerics;

namespace engine.behave.components;

public struct ParticleEmitter
{
    public Vector3 Position;
    public float ScalePerSec;
    public Vector3 RandomPos;
    public int EmitterTimeToLive;
    public Vector3 Velocity;
    public Quaternion RotationVelocity;
    public int ParticleTimeToLive;
    public engine.joyce.InstanceDesc InstanceDesc;
    public float RandomDirection;
    public float Frequency;
    public float SlowDown;
    public float MaxDistance;
    public uint CameraMask;
}