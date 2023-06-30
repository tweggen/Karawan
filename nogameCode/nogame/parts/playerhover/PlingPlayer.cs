using engine;

namespace nogame.parts.playerhover;

public class PlingPlayer
{
    private int _plingCounter;

    private static readonly int FirstPling = 1;
    private static readonly int LastPling = 19;

    private Boom.ISound[] _arrPlings;
    
    
    public void PlayPling()
    {
        _arrPlings[_plingCounter].Play();
    }


    private void _loadPlings()
    {
        var api = Implementations.Get<Boom.ISoundAPI>();
        _arrPlings = new Boom.ISound[LastPling - FirstPling + 1];
        for (int i = FirstPling-1; i < LastPling; ++i)
        {
            _arrPlings[i] = api.CreateAudioSource($"pling{(i+1):D2}.ogg");
            _arrPlings[i].Volume = 0.025f;
        }
    }

    public void Reset()
    {
        _plingCounter = FirstPling;
    }

    public void Next()
    {
        if (_plingCounter >= LastPling)
        {
            _plingCounter = FirstPling;
        }
        else
        {
            _plingCounter++;
        }
    }

    public PlingPlayer()
    {
        _loadPlings();
        Reset();
    }
}