using System.Numerics;

namespace engine.behave.components;

public struct ParticleEmitter
{
    public Vector3 Position;
    public float ScalePerSec;
    public Vector3 RandomPos;
    public int EmitterTimeToLive;
    public Vector3 Velocity;
    public int ParticleTimeToLive;
    public float RandomDirection;
    public float Frequency;
    public float SlowDown;
    public uint CameraMask;
}