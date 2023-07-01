using engine;
using Silk.NET.OpenAL;
using static engine.Logger;

namespace Boom.OpenAL;

public class API : Boom.ISoundAPI
{
    private object _lo = new();
    private Engine _engine;
    private systems.UpdateMovingSoundSystem _updateMovingSoundsSystem;
    
    private AL _al;
    private ALContext _alc;

    private SortedDictionary<string, OGGSound> _mapSounds = new();


    public AL AL
    {
        get => _al;
    }
    
    private void _onLogicalFrame(object sender, float dt)
    {
        // _createMusicSystem.Update(dt);
        _updateMovingSoundsSystem.Update(dt);
    }


    
    public void PlaySound(string uri)
    {
        // TXWTODO: This leaks all sources. 
        ISound audioSource = CreateAudioSource(uri);
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
    

    public ISound CreateAudioSource(string url)
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

        ISound asTitle = CreateAudioSource("shaklengokhsi.ogg");
        asTitle.Volume = 0.05f;
        asTitle.Play();

    }
}