using engine;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.Enumeration;
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
    

    public void Dispose()
    {
        // TXWTODO: Close openal
    }


    public void SetupDone()
    {
        _engine.LogicalFrame += _onLogicalFrame;
    }
    
    public Task<ISound> LoadSound(string url)
    {
        return Task<ISound>.Run<ISound>(() =>
        {
            OGGSound oggSound = new(_al, url);
            return new AudioSource(_al, oggSound.ALBuffer);
        });
    }


    public ISound FindSound(string url)
    {
        OGGSound oggSound;
        lock (_lo)
        {
            if (!_mapSounds.TryGetValue(url, out oggSound))
            {
                oggSound = new(_al, url);
                _mapSounds[url] = oggSound;
            }
            else
            {
                // We have the sound in _sound;
            }
        }

        return new AudioSource(_al, oggSound.ALBuffer);
    }
    

    private unsafe void _openDevice()
    {
        if (_alc.IsExtensionPresent(null, "ALC_ENUMERATION_EXT"))
        {
            using var enumeration = _alc.GetExtension<Enumeration>(null);
            foreach (string device in enumeration.GetStringList(GetEnumerationContextStringList.DeviceSpecifiers))
            {
                Console.WriteLine(device);
            }
        }
        
        Device *alDevice = _alc.OpenDevice("");
        if (alDevice == null)
        {
            ErrorThrow("Unable to open any audio device.", (m) => new InvalidOperationException(m));
            return;
        }

        var context = _alc.CreateContext(alDevice, null);
        _alc.MakeContextCurrent(context);
    }
    
    
    public void ResumeOutput()
    {
    }


    public void SuspendOutput()
    {
    }


    public API(Engine engine0)
    {
        _engine = engine0;
        
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
        _updateMovingSoundsSystem = new(_engine, this);
        
    }
}
