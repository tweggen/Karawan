using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using engine;
using engine.behave;
using static engine.Logger;


namespace builtin.entitySaver;

public class ConverterRegistry : AModule
{
    private object _lo = new();
    private Dictionary<Type, Func<Context, JsonConverter>> _mapConverters = new();
    private FrozenDictionary<Type, Func<Context, JsonConverter>> _roConverters;
    private Dictionary<Type, Func<Context, JsonConverter>> _mapInterfaceConverters = new();

    public JsonConverter CreateConverter(Context context, Type type, JsonSerializerOptions options)
    {
        lock (_lo)
        {
            if (!_roConverters.TryGetValue(type, out var factory))
            {
                ErrorThrow<ArgumentException>($"Unable to create converter for {type}");
            }

            return factory(context);
        }
    }


    private bool _tryMatchInterface(Type type, out Func<Context, JsonConverter> factory)
    {
        foreach (var kvp in _mapInterfaceConverters)
        {
            if (kvp.Key.IsAssignableFrom(type))
            {
                factory = kvp.Value;
                _mapInterfaceConverters[type] = kvp.Value;
                return true;
            }
        }

        factory = default;
        return false;
    }

    
    public bool CanConvert(Type type)
    {
        lock (_lo)
        {
            if (_roConverters.ContainsKey(type))
            {
                return true;
            }

            if (_tryMatchInterface(type, out var _))
            {
                return true;
            }
        }

        return false;
    }
    
    
    public void RegisterConverter(Type type, Func<Context, JsonConverter> factory)
    {
        lock (_lo)
        {
            _mapConverters.Add(type, factory);
            _roConverters = _mapConverters.ToFrozenDictionary();
        }
    }


    public void RegisterInterfaceConverter(Type type, Func<Context, JsonConverter> factory)
    {
        lock (_lo)
        {
            _mapInterfaceConverters.Add(type, factory);
        }
    }
    
    
    public ConverterRegistry()
    {
        /*
         * Builtin types
         */
        _mapConverters.Add(typeof(Matrix4x4), context => new Matrix4x4JsonConverter());
        _mapConverters.Add(typeof(Vector3), context => new Vector3JsonConverter());
        _mapConverters.Add(typeof(Quaternion), context => new QuaternionJsonConverter());

        _mapConverters.Add(typeof(engine.joyce.InstanceDesc), context => new engine.joyce.InstanceDescConverter());

        _mapInterfaceConverters.Add(typeof(IBehavior), context => new InterfacePointerConverter<IBehavior>());
        
        _roConverters = _mapConverters.ToFrozenDictionary();
    }


    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
    }
}
