using System;
using static engine.Logger;

namespace builtin;

public class CountedEnabler : IDisposable
{
    private object _lo = new();
    private int _counter = 0;

    private Action<bool> _toggleAction;

    
    public void Add()
    {
        bool doEnable = false;
        lock (_lo)
        {
            ++_counter;
            if (1 == _counter)
            {
                doEnable = true;
            }
        }

        if (doEnable)
        {
            _toggleAction(true);
        }
    }


    public void Remove()
    {
        bool doDisable = false;
        lock (_lo)
        {
            if (0 == _counter)
            {
                ErrorThrow<InvalidOperationException>("Mismatch disabling entity.");
            }

            if (1 == _counter)
            {
                doDisable = true;
            }

            --_counter;
        }

        if (doDisable)
        {
            _toggleAction(false);
        }
    }
    

    public void Dispose()
    {
        if (_counter > 0)
        {
            _toggleAction(false);
        }
    }

    
    public CountedEnabler(Action<bool> toggleAction)
    {
        _toggleAction = toggleAction;
    }
}