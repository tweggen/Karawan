using System.Numerics;
using Silk.NET.OpenAL;

namespace Boom.OpenAL;

public class AudioSource : IDisposable
{
    private AL _al;
    private uint _alBuffer = 0;
    private uint _alSource = 0;
    
    public Vector3 Position;
    public Vector3 Velocity;
    public bool IsLooped;
    public float Gain;
    public float Pan;

    public void Play()
    {
        _al.SetSourceProperty(_alSource,SourceBoolean.Looping, IsLooped);
        _al.SourcePlay(_alSource);
    }

    
    public void Stop()
    {
        _al.SourceStop(_alSource);
    }

    
    public void Dispose()
    {
        /*
         * TXWTODO: Properly dispose the original buffer by using reference counting.
         * Today, for simplicity, we assume to never delete it.
         */
        if (_alSource != 0)
        {
            _al.DeleteSource(_alSource);
        }
        
    }

    
    public AudioSource(AL al, uint alBuffer)
    {
        _al = al;
        _alBuffer = alBuffer;
        _alSource = _al.GenSource();
        uint[] buffers = { _alBuffer };
        _al.SourceQueueBuffers(_alSource, buffers);
    }
}