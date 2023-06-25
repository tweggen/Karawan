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

    public API(Engine engine)
    {
        _engine = engine;
        ALContext? alc = ALContext.GetApi(true);
        if (null == alc)
        {
            ErrorThrow("Unable to get OpenAL context.", (m) => new InvalidOperationException(m));
            return;
        }

        _alc = alc;

        _al = AL.GetApi();
        _al.SetListenerProperty(ListenerFloat.Gain, 4f);

        OGGSound oggSound = new(_al, "shaklengokhsi.ogg");
        AudioSource asTitle = new(_al, oggSound.ALBuffer);
        asTitle.Play();

    }
}