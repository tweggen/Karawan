using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using engine.news;

namespace engine;

public class Props
{
    static private object _staticLo = new();
    static private Props _instance = null;

    private object _lo = new();
    private Dictionary<string, object> _dictConfigParams = new();

    private ReadOnlyDictionary<string, object> _rodictConfigParams;
    
    public ReadOnlyDictionary<string, object> Dictionary
    {
        get => _rodictConfigParams;
    }
    
    static public object Get(in string key, object defaultValue)
    {
        return Props.Instance()._get(key, defaultValue);
    }
    

    static public void Set(in string key, object value)
    {
        Props.Instance()._set(key, value);
    }


    private void _set(in string key, object value)
    {
        bool doEmitEvent = false;
        lock (_lo)
        {
            bool doSet = false;
            if (_dictConfigParams.TryGetValue(key, out var oldValue))
            {
                if (oldValue != value)
                {
                    doSet = true;
                }
            }
            else
            {
                doSet = true;
            }

            if (doSet)
            {
                _dictConfigParams[key] = value;
                doEmitEvent = true;
            }
        }

        if (doEmitEvent)
        {
            /*
             * In initialization situations, the event queue might not be available.
             */
            EventQueue eq = null;
            try
            {
                eq = I.Get<EventQueue>();
            }
            catch (System.Exception e)
            {
                
            }

            if (null != eq)
            {
                eq.Push(
                    new PropertyEvent(
                        $"{PropertyEvent.PROPERTY_CHANGED}.{key}",
                        key, value));
            }
        }
    }

    
    private object _get(in string key, object defaultValue)
    {
        lock(_lo)
        {
            if (_dictConfigParams.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }
    }
    
    
    private Props()
    {
        _rodictConfigParams = new ReadOnlyDictionary<string, object>(_dictConfigParams);
    }
    

    static public Props Instance()
    {
        lock (_staticLo)
        {
            if (null == _instance)
            {
                _instance = new();
            }

            return _instance;
        }
    }

}
