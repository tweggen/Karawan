using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using engine.news;
using static engine.Logger;

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
    
    
    static private void _setJsonNode(JsonNode? node, Action<object> action)
    {
        if (node is null)
            return;

        if (node is JsonValue value)
        {
            // Try to unwrap the underlying CLR type
            switch (value.GetValueKind())
            {
                case JsonValueKind.False:
                    action(false);
                    break;
                case JsonValueKind.True:
                    action(true);
                    break;
                case JsonValueKind.String:
                    action(value.GetValue<string>());
                    break;
                case JsonValueKind.Number:
                    // You can choose float/double/decimal depending on your needs
                    action(value.GetValue<float>());
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    break;
            }
        }
        else if (node is JsonObject)
        {
            // Object case – do nothing here
        }
        else if (node is JsonArray)
        {
            // Array case – do nothing here
        }
    }
    
    
    private void _loadProperties(JsonNode nodeProperties)
    {
        try
        {
            if (nodeProperties is JsonObject objProperties)
            {
                foreach (var kvp in objProperties)
                {
                    try
                    {
                        _setJsonNode(kvp.Value, o => _set(kvp.Key, o));
                    }
                    catch (Exception e)
                    {
                        Warning($"Error setting global setting {kvp.Key}: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading global settings: {e}");
        }
    }


    private void _whenLoaded(string path, JsonNode? jn)
    {
        if (null != jn)
        {
            _loadProperties(jn);
        }
    }
    
    private Props()
    {
        _dictConfigParams = new Dictionary<string, object>();
        _rodictConfigParams = new ReadOnlyDictionary<string, object>(_dictConfigParams);
        I.Get<engine.casette.Loader>().WhenLoaded("/properties", _whenLoaded);
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
