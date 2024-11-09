using engine;

namespace nogame.modules.playerhover;

public class PlingPlayer
{
    private object _lo = new();
    
    private int _plingCounter;

    private static readonly int FirstPling = 1;
    private static readonly int LastPling = 19;

    private Boom.ISound[] _arrPlings;
    
    
    public void PlayPling()
    {
        Boom.ISound[] arrPlings;
        lock (_lo)
        {
            arrPlings = _arrPlings;
        }

        if (null == arrPlings)
        {
            return;
        }
        arrPlings[_plingCounter - FirstPling].Stop();
        arrPlings[_plingCounter - FirstPling].Play();
    }


    private void _loadPlings()
    {
        Boom.ISound[] arrLoadingPlings;

        var api = I.Get<Boom.ISoundAPI>();
        arrLoadingPlings = new Boom.ISound[LastPling - FirstPling + 1];
        for (int i = FirstPling-1; i < LastPling; ++i)
        {
            arrLoadingPlings[i] = api.FindSound($"pling{(i+1):D2}.ogg");
            arrLoadingPlings[i].Volume = 0.025f;
        }

        lock (_lo)
        {
            _arrPlings = arrLoadingPlings;
        }
    }

    public void Reset()
    {
        lock (_lo)
        {
            _plingCounter = FirstPling;
        }
    }

    public void Next()
    {
        lock (_lo)
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
    }

    public PlingPlayer()
    {
        I.Get<Engine>().Run(() => _loadPlings());
        Reset();
    }
}