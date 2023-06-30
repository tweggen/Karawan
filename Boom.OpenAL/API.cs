using engine;
using Silk.NET.OpenAL;
using static engine.Logger;

namespace Boom.OpenAL;

public class API : ISoundAPI
{
    private object _lo = new();
    private Engine _engine;
    private systems.UpdateMovingSoundSystem _updateMovingSoundsSystem;
    
    private AL _al;
    private ALContext _alc;

    private SortedDictionary<string, OGGSound> _mapSounds = new();
    private DefaultEcs.Entity _cameraEntity;
    
    private void _onLogicalFrame(object sender, float dt)
    {
        // _createMusicSystem.Update(dt);
        _updateMovingSoundsSystem.Update(dt);
    }


    private void _onCameraEntityChanged(object? sender, DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_cameraEntity != entity)
            {
                entity = _cameraEntity;
                isChanged = true;
            }
        }
    }
    
    
    public void PlaySound(string uri)
    {
        // TXWTODO: This leaks all sources. 
        AudioSource audioSource = CreateAudioSource(uri);
        audioSource.Play();
    }

    
    public void StopSound(string uri)
    {
    }
    

    public void Dispose()
    {
        // TXWTODO: Close openal
    }


    public void SetupDone()
    {
        _engine.LogicalFrame += _onLogicalFrame;
        _engine.CameraEntityChanged += _onCameraEntityChanged;
        _onCameraEntityChanged(_engine, _engine.GetCameraEntity());
    }


    public OGGSound FindSound(string url)
    {
        OGGSound _sound;
        lock (_lo)
        {
            if (!_mapSounds.TryGetValue(url, out _sound))
            {
                _sound = new(_al, url);
                _mapSounds[url] = _sound;
            }
            else
            {
                // We have the sound in _sound;
            }
        }

        return _sound;
    }
    

    public AudioSource CreateAudioSource(string url)
    {
        OGGSound oggSound = FindSound(url);
        AudioSource audioSource = new(_al, oggSound.ALBuffer);
        return audioSource;
    }
    

    private unsafe void _openDevice()
    {
        Device *alDevice = _alc.OpenDevice("");
        if (alDevice == null)
        {
            ErrorThrow("Unable to open any audio device.", (m) => new InvalidOperationException(m));
            return;
        }

        var context = _alc.CreateContext(alDevice, null);
        _alc.MakeContextCurrent(context);
    }
    

    public API(Engine engine)
    {
        _engine = engine;
        
        try {
            
            ALContext? alc = ALContext.GetApi(true);
            if (null == alc)
            {
                ErrorThrow("Unable to get OpenAL context.", (m) => new InvalidOperationException(m));
                return;
            }

            _alc = alc;

            _al = AL.GetApi();
            
        } catch( Exception ex)
        {
            ALContext? alc = ALContext.GetApi();
            if (null == alc)
            {
                ErrorThrow("Unable to open any OpenAL context.", (m) => new InvalidOperationException(m));
                return;
            }

            _alc = alc;
            _al = AL.GetApi();
        }

        _openDevice();
        _al.DistanceModel(DistanceModel.InverseDistance);
        _al.SetListenerProperty(ListenerFloat.Gain, 4f);
        _updateMovingSoundsSystem = new(engine, this);

        // AudioSource asTitle = CreateAudioSource("shaklengokhsi.ogg");
        // asTitle.Play();

    }
}