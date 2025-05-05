using System.Diagnostics;
using System.Numerics;
using BepuPhysics.Constraints;
using engine;
using Silk.NET.OpenAL;
using static engine.Logger;

namespace Boom.OpenAL;

public class AudioSource : Boom.ISound
{
    private readonly object _lo = new();
    private AL _al;
    private uint _alBuffer = 0xffffffff;
    private uint _alSource = 0xffffffff;
    private bool _haveSetupDistanceModel = false;
    private ISoundAPI _api;

    private bool _traceAudio = false;

    private void _trace(in string msg)
    {
        if(_traceAudio) Trace(msg);
    }

    /*
     * Start with a far off position to avoid cranks in the beginning.
     */
    private Vector3 _position = new(0f, -10000f, 0f);

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

    private float _effectiveVolume = -0.01f;
    private float _userVolume = 0.5f;
    public float Volume
    {
        get => _userVolume;
        set => _setVolume(value);
    }

    private float _speed = 1f;

    public float Speed
    {
        get => _speed;
        set => _setSpeed(value);
    }

    private uint _soundMask = 0x00000001;
    public uint SoundMask
    {
        get => _soundMask;
        set
        {
            _soundMask = value;
            _setVolume(_userVolume);
        }
    }

    private void _whc()
    {
        // Trace("Would have crashed before.");
    }

    private bool _isValidNoLock()
    {
        return _alSource != 0xffffffff;
    }



    private void _setupDistanceStuffNoLock()
    {
        if (_haveSetupDistanceModel) return;
        _haveSetupDistanceModel = true;

        _al.SetSourceProperty(_alSource, SourceFloat.ReferenceDistance, 1f);
        _trace($"_setupDistanceStuffNoLock ReferenceDistance returned {_al.GetError().ToString()}");
        _al.SetSourceProperty(_alSource, SourceFloat.RolloffFactor, 1f);
        _trace($"_setupDistanceStuffNoLock RolloffFactor returned {_al.GetError().ToString()}");
        _al.SetSourceProperty(_alSource, SourceFloat.MaxDistance, 150f);
        _trace($"_setupDistanceStuffNoLock MaxDistance returned {_al.GetError().ToString()}");
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


    private void _setVolumeNoLock(float requestedVolume)
    {
        float volume;
        
        // TXWTODO: Optimize the call.
        if (_userVolume != requestedVolume)
        {
            _userVolume = requestedVolume;
        }
        bool isActive = (_api.SoundMask & _soundMask) != 0;
        float newVolume = isActive ? requestedVolume : 0f;
        if (newVolume != _effectiveVolume)
        {
            if (_isValidNoLock())
            {
                _al.SetSourceProperty(_alSource, SourceFloat.Gain, newVolume);
                _effectiveVolume = newVolume;
            }
        }
    }

    private void _setVolume(float requestedVolume)
    {
        lock (_lo)
        {
            _setVolumeNoLock(requestedVolume);
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
            _setVolumeNoLock(_userVolume);
            _al.SetSourceProperty(_alSource, SourceBoolean.Looping, _isLooped);
            _trace($"SetSourceProperty _isLooped returned {_al.GetError().ToString()}");
            _al.SourcePlay(_alSource);
            _trace($"SourcePlay returned {_al.GetError().ToString()}");
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
            _alSource = 0xffffffff;
        }
        else
        {
            _whc();
        }
        
    }

    
    public AudioSource(AL al, uint alBuffer)
    {
        _api = I.Get<ISoundAPI>();
        _al = al;
        _alBuffer = alBuffer;
        _alSource = _al.GenSource();
        _trace($"GenSource returned {_al.GetError().ToString()}");
        uint[] buffers = { _alBuffer };
        _al.SourceQueueBuffers(_alSource, buffers);
        _trace($"SourceQueueBuffers returned {_al.GetError().ToString()}");
    }
}