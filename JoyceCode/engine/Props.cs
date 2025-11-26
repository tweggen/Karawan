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
    private Dictionary<string, object> _dictConfigParams;
    private ReadOnlyDictionary<string, object> _rodictConfigParams;
    
    public ReadOnlyDictionary<string, object> Dictionary
    {
        get
        {
            return _rodictConfigParams;
        }
    }

    static public object Get(in string key, object defaultValue)
    {
        return Props.Instance()._get(key, defaultValue);
    }
    
    static public object Find(in string key, object defaultValue)
    {
        return Props.Instance()._find(key, defaultValue);
    }
    

    static public void Set(in string key, object value)
    {
        Props.Instance()._set(key, value);
    }


    private void _set(in string key, object value)
    {
        bool doEmitEvent = false;

        Dictionary<string, object> oldConfigParams, newConfigParams;
        lock (_lo)
        {
            /*
             * Shall we set a new value or is there no modification at all?
             */
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
                oldConfigParams = _dictConfigParams;
                newConfigParams = new(oldConfigParams);
                newConfigParams[key] = value;
                _dictConfigParams = newConfigParams;
                _rodictConfigParams = newConfigParams.AsReadOnly();
            
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
    
    
    private object _find(in string key, object defaultValue)
    {
        lock(_lo)
        {
            if (_dictConfigParams.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                Dictionary<string, object> oldConfigParams, newConfigParams;
                
                oldConfigParams = _dictConfigParams;
                newConfigParams = new(oldConfigParams);
                newConfigParams[key] = defaultValue;
                _dictConfigParams = newConfigParams;
                _rodictConfigParams = newConfigParams.AsReadOnly();
                
                return defaultValue;
            }
        }
    }
    
    
    private Props()
    {
        _dictConfigParams = new Dictionary<string, object>();
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
