using System.Numerics;
using BepuPhysics.Constraints;
using Silk.NET.OpenAL;

namespace Boom.OpenAL;

public class AudioSource : Boom.ISound
{
    private AL _al;
    private uint _alBuffer = 0;
    private uint _alSource = 0;
    private bool _haveSetupDistanceModel = false;

    private Vector3 _position = new();

    public Vector3 Position
    {
        get => _position;
        set => _setPosition(value);
    }
    
    private Vector3 _velocity = new();
    public Vector3 Velocity
    {
        get => _velocity;
        set => _setVelocity(value);
    }

    private bool _isLooped = false;
    public bool IsLooped
    {
        get => _isLooped;
        set => _isLooped = value;
    }

    private float _volume = 1f;
    public float Volume
    {
        get => _volume;
        set => _setVolume(value);
    }

    private float _speed = 1f;

    public float Speed
    {
        get => _speed;
        set => _setSpeed(value);
    }


    private void _setVelocity(Vector3 velocity)
    {
        if (velocity != _velocity)
        {
            _velocity = velocity;
            _al.SetSourceProperty(_alSource, SourceVector3.Velocity, velocity);
        }
    }


    private void _setPosition(Vector3 position)
    {
        if (position != _position)
        {
            _position = position;
            _al.SetSourceProperty(_alSource, SourceVector3.Position, position);
        }
    }


    private void _setVolume(float volume)
    {
        if (volume != _volume)
        {
            _volume = volume;
            _al.SetSourceProperty(_alSource, SourceFloat.Gain, volume);
        }
    }


    private void _setSpeed(float speed)
    {
        if (speed != _speed)
        {
            _speed = speed;
            _al.SetSourceProperty(_alSource, SourceFloat.Pitch, speed);
        }
    }

    
    
    public void Play()
    {
        _al.SetSourceProperty(_alSource,SourceBoolean.Looping, _isLooped);
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