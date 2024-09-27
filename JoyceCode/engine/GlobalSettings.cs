using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Numerics;
using engine.joyce.components;
using engine.news;
using ObjLoader.Loader.Common;

namespace engine;

public class GlobalSettings
{
    static private object _staticLo = new();
    static private GlobalSettings _instance;

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


    public static Vector2 ParseSize(string viewSize)
    {
        int x=320, y=200;
        if (viewSize.IsNullOrEmpty()) viewSize = "320x200";


        int xpos = viewSize.IndexOf('x');
        if (xpos != -1)
        {
            string ypart = viewSize.Substring(xpos + 1);
            if (Int32.TryParse(viewSize.Substring(0, xpos), out x)) {}
            if (Int32.TryParse(ypart, out y)) {}
        }

        return new Vector2(x, y);
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