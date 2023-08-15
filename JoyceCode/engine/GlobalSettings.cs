using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using engine.joyce.components;

namespace engine;

public class GlobalSettings
{
    static private object _staticLo = new();
    static private GlobalSettings _instance = null;

    private object _lo = new();
    private Dictionary<string, string> _dictConfigParams = new();

    private ReadOnlyDictionary<string, string> _rodictConfigParams;
    
    public ReadOnlyDictionary<string, string> Dictionary
    {
        get => _rodictConfigParams;
    }
    
    static public string Get(in string key)
    {
        return GlobalSettings.Instance()._get(key);
    }
    

    static public void Set(in string key, in string value)
    {
        GlobalSettings.Instance()._set(key, value);
    }


    private void _set(in string key, in string value)
    {
        lock(_lo)
        {
            _dictConfigParams[key] = value;
        }
    }

    
    private string _get(in string key)
    {
        lock(_lo)
        {
            if( _dictConfigParams.ContainsKey(key))
            {
                return _dictConfigParams[key];
            } else
            {
                return "";
            }
        }
    }
    
    
    private GlobalSettings()
    {
        _rodictConfigParams = new ReadOnlyDictionary<string, string>(_dictConfigParams);
    }
    

    static public GlobalSettings Instance()
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