using engine;
using engine.news;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.Enumeration;
using Silk.NET.OpenAL.Extensions.Soft;

using static engine.Logger;

namespace Boom.OpenAL;

unsafe public class API : Boom.ISoundAPI
{
    private object _lo = new();
    private Engine _engine;
    private systems.UpdateMovingSoundSystem _updateMovingSoundsSystem;
    
    private AL _al;
    private ALContext _alc;
    private Device* _alDevice = null;
    private SortedDictionary<string, OGGSound> _mapSounds = new();


    public AL AL
    {
        get => _al;
    }
    
    private void OnOnLogicalFrame(object sender, float dt)
    {
        // _createMusicSystem.Update(dt);
        _updateMovingSoundsSystem.Update(dt);
    }
    

    public void Dispose()
    {
        // TXWTODO: Close openal
        Implementations.Get<SubscriptionManager>().Unsubscribe("lifecycle.resume", ResumeOutput);
        Implementations.Get<SubscriptionManager>().Unsubscribe("lifecycle.suspend", SuspendOutput);
    }


    public void SetupDone()
    {
        _engine.OnLogicalFrame += OnOnLogicalFrame;
        Implementations.Get<SubscriptionManager>().Subscribe("lifecycle.resume", ResumeOutput);
        Implementations.Get<SubscriptionManager>().Subscribe("lifecycle.suspend", SuspendOutput);
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


    private ReopenDevices _extReopenDevices;
    private bool _haveReopenDevices = false;


    private string _researchDeviceName()
    {
        string firstDevice = "";
        if (_alc.IsExtensionPresent(null, "ALC_ENUMERATION_EXT"))
        {
            using var enumeration = _alc.GetExtension<Enumeration>(null);
            foreach (string device in enumeration.GetStringList(GetEnumerationContextStringList.DeviceSpecifiers))
            {
                Trace($"Found openal sound device {device}.");
                if (firstDevice == "" && device != "")
                {
                    Trace( $"Using openal sound device {device}");
                    firstDevice = device;
                }
            }
        }

        return firstDevice;
    }

    private void _researchOutputMode()
    {
        int major = 0x4321, minor = 0x5432;
        _alc.GetContextProperty(_alDevice, GetContextInteger.MajorVersion, 1, &major);
        _alc.GetContextProperty(_alDevice, GetContextInteger.MinorVersion, 1, &minor);
        int nAttrs = 0;
        _alc.GetContextProperty(_alDevice, GetContextInteger.AttributesSize, 1, &nAttrs);
        if (nAttrs != 0)
        {
            int[] attrs = new int[nAttrs];
            fixed (int* pAttrs = &attrs[0])
            {
                _alc.GetContextProperty(_alDevice, GetContextInteger.AttributesSize, nAttrs, pAttrs);
            }

            for(int i=0; i<nAttrs; ++i)
            {
                //Trace($"Property {attrs[i]}");
            }
        }


        Trace($"MajorVersion is {major}.{minor}.");

        if (_alc.IsExtensionPresent(_alDevice, "ALC_SOFT_output_mode"))
        {
            Trace("Have output_mode extension.");
        }
    }
    
    
    private unsafe void _openDevice()
    {
        _haveReopenDevices = _al.TryGetExtension<ReopenDevices>(out _extReopenDevices);
        Trace($"haveReopenDevices = {_haveReopenDevices}");

        string deviceName = _researchDeviceName();
        _alDevice = _alc.OpenDevice(null);
        if (_alDevice != null)
        {
            _currentContext = _alc.CreateContext(_alDevice, null);
            if (_currentContext != null)
            {
                _alc.MakeContextCurrent(_currentContext);
                Trace($"MakeCurrentContext returned {_al.GetError().ToString()} alc error {_alc.GetError(_alDevice).ToString()}");
            }
            else
            {
                Trace($"CreateContext returned {_al.GetError().ToString()} alc error {_alc.GetError(_alDevice).ToString()}");
            }

            _researchOutputMode();
        }
        else
        {
            Trace($"OpenDevice returned {_al.GetError().ToString()}");
            _currentContext = null;
        }
    }
    

    private Silk.NET.OpenAL.Context *_currentContext = null;
    
    public void ResumeOutput(Event ev)
    {
        Trace($"Resume output called reason '{ev.Code}'");

        string deviceName = _researchDeviceName();
        _alDevice = _alc.OpenDevice(deviceName);
        
        if (_alDevice != null)
        {
            _currentContext = _alc.CreateContext(_alDevice, null);
            if (_currentContext != null)
            {
                bool result = _alc.MakeContextCurrent(_currentContext);
                Trace($"MakeCurrentContext returned {result}, al error {_al.GetError().ToString()} alc error {_alc.GetError(_alDevice).ToString()}");
                _alc.ProcessContext(_currentContext);
            }
        }
        else
        {
            _currentContext = null;
        }
    }


    public void SuspendOutput(Event ev)
    {
        // _currentContext = _alc.GetCurrentContext();
        if (_currentContext != null)
        {
            _alc.MakeContextCurrent(null);
            _alc.DestroyContext(_currentContext);
            _alc.CloseDevice(_alDevice);
            _alDevice = null;
        }
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
        Trace($"DistanceModel returned {_al.GetError().ToString()} ");
        _al.SetListenerProperty(ListenerFloat.Gain, 4f);
        Trace($"SetListenerProperty returned {_al.GetError().ToString()} ");
        _updateMovingSoundsSystem = new(_engine, this);
    }
}
