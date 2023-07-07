using System.Diagnostics;
using System.Numerics;
using BepuPhysics.Constraints;
using Silk.NET.OpenAL;
using static engine.Logger;

namespace Boom.OpenAL;

public class AudioSource : Boom.ISound
{
    private readonly object _lo = new();
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

    private void _whc()
    {
        // Trace("Would have crashed before.");
    }

    private bool _isValidNoLock()
    {
        return _alSource != 0;
    }



    private void _setupDistanceStuffNoLock()
    {
        if (_haveSetupDistanceModel) return;
        _haveSetupDistanceModel = true;

        _al.SetSourceProperty(_alSource, SourceFloat.ReferenceDistance, 1f);
        _al.SetSourceProperty(_alSource, SourceFloat.RolloffFactor, 1f);
        _al.SetSourceProperty(_alSource, SourceFloat.MaxDistance, 150f);
    }

    private void _setVelocity(Vector3 velocity)
    {
        lock (_lo)
        {
            if (velocity != _velocity)
            {
                if (_isValidNoLock())
                {
                    _al.SetSourceProperty(_alSource, SourceVector3.Velocity, velocity);
                    _velocity = velocity;
                }
            }
        }
    }


    private void _setPosition(Vector3 position)
    {
        lock (_lo)
        {
            if (position != _position)
            {
                if (_isValidNoLock())
                {
                    _al.SetSourceProperty(_alSource, SourceVector3.Position, position);
                    _position = position;
                }
            }
        }
    }


    private void _setVolume(float volume)
    {
        lock (_lo)
        {
            if (volume != _volume)
            {
                if (_isValidNoLock())
                {
                    _al.SetSourceProperty(_alSource, SourceFloat.Gain, volume);
                    _volume = volume;
                }
            }
        }
    }


    private void _setSpeed(float speed)
    {
        lock (_lo)
        {
            if (speed != _speed)
            {
                if (_isValidNoLock())
                {
                    _al.SetSourceProperty(_alSource, SourceFloat.Pitch, speed);
                    _speed = speed;
                }
            }
        }
    }

    
    
    public void Play()
    {
        lock (_lo)
        {
            if (!_isValidNoLock())
            {
                return;
            }

            _setupDistanceStuffNoLock();
            _al.SetSourceProperty(_alSource, SourceBoolean.Looping, _isLooped);
            _al.SourcePlay(_alSource);
        }
    }

    
    public void Stop()
    {
        lock (_lo)
        {
            if (!_isValidNoLock())
            {
                return;
            }

            _al.SourceStop(_alSource);
        }
    }

    
    public void Dispose()
    {
        /*
         * TXWTODO: Properly dispose the original buffer by using reference counting.
         * Today, for simplicity, we assume to never delete it.
         */
        if (_isValidNoLock())
        {
            _al.DeleteSource(_alSource);
            _alSource = 0;
        }
        else
        {
            _whc();
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