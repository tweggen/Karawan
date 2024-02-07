using System.Numerics;

namespace Boom;

public interface ISound : IDisposable
{
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public bool IsLooped { get; set; }
    public float Volume { get; set; }
    public float Speed { get; set; }

    public uint SoundMask { get; set; }
    public void Play();
    public void Stop();
}