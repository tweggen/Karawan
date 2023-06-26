using engine;
using Silk.NET.OpenAL;
using static engine.Logger;

namespace Boom.OpenAL;

public class API : ISoundAPI
{
    private object _lo = new();
    private Engine _engine;

    
    private AL _al;
    private ALContext _alc;
    
    public void PlaySound(string uri)
    {
        // throw new NotImplementedException();
    }

    
    public void StopSound(string uri)
    {
        // throw new NotImplementedException();
    }
    

    public void Dispose()
    {
        // TXWTODO: Close openal
    }


    public void SetupDone()
    {
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

        OGGSound oggSound = new(_al, "shaklengokhsi.ogg");
        AudioSource asTitle = new(_al, oggSound.ALBuffer);
        asTitle.Play();

    }
}