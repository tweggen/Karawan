
using System;
using System.Collections.Generic;
using DefaultEcs;
using static engine.Logger;

namespace engine.gongzuo.systems;

public class LuaScriptManager : IDisposable
{
    private sealed class Resource<ValueType>
    {
        public readonly ValueType Value;

        private int _referencesCount = 0;

        public Resource(ValueType value)
        {
            Value = value;
        }

        public void AddReference() => ++_referencesCount;
        public bool RemoveReference() => (--_referencesCount) == 0;
    }


    private readonly object _lo;
    private readonly Dictionary<LuaScriptEntry, Resource<LuaScriptEntry>> _luaScriptResources;

    /**
     * Well, removing PfInstance from within Remove Instance3 seems correct,
     * however, when deleting the entity, this triggers RemoveInstance3 twice.
     * So keep track.
     */
    private int _inRemoveInstance3 = 0;

    private void _unloadMesh(in LuaScriptEntry luaScriptEntry)
    {
        // TXWTODO: Unload script entry
    }

    private void _loadMesh(in LuaScriptEntry luaScriptEntry)
    {
        // TXWTODO: Load script entry
    }


    private void _onAdded(in Entity entity, in components.LuaScript value) => _add(entity, value);


    private void _onChanged(in Entity entity, in components.LuaScript oldValue, in components.LuaScript newValue)
    {
        _add(entity, newValue);
        _remove(entity, oldValue);
    }

    private void _onRemoved(in Entity entity, in components.LuaScript value) => _remove(entity, value);


    private void _add(in Entity entity, in components.LuaScript value)
    {
        lock (_lo)
        {
            if (!_luaScriptResources.TryGetValue(value.LuaScriptEntry, out var luaScriptResource))
            {
                try
                {
                    _loadMesh(value.LuaScriptEntry);
                    luaScriptResource = new Resource<LuaScriptEntry>(value.LuaScriptEntry);
                    _luaScriptResources.Add(value.LuaScriptEntry, luaScriptResource);
                }
                catch (Exception e)
                {
                    Error("Exception loading lua script function: {e}");
                }
               
                if (null != luaScriptResource)
                {
                    luaScriptResource.AddReference();
                }
            }
        }
    }


    private void _remove(in Entity entity, in components.LuaScript value)
    {
        // TXWTODO: Lock is superfluous, we only have one ECS Thread.
        lock (_lo)
        {
            Resource<LuaScriptEntry> luaScriptResource;
            var luaScriptEntry = value.LuaScriptEntry;
            if (!_luaScriptResources.TryGetValue(luaScriptEntry, out luaScriptResource))
            {
                Error($"Unknown mesh to unreference.");
            }
            else
            {
                if (luaScriptResource.RemoveReference())
                {
                    try
                    {
                        _unloadMesh(value.LuaScriptEntry);
                    }
                    finally
                    {
                        _luaScriptResources.Remove(value.LuaScriptEntry);
                    }
                }
            }
        }
    }
    

    public IDisposable Manage(World world)
    {
        IEnumerable<IDisposable> GetSubscriptions(World w)
        {
            yield return w.SubscribeEntityComponentAdded<components.LuaScript>(_onAdded);
            yield return w.SubscribeEntityComponentChanged<components.LuaScript>(_onChanged);
            yield return w.SubscribeEntityComponentRemoved<components.LuaScript>(_onRemoved);
        }

        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }

        int nInitialEntites = 0;
        var entities = world.GetEntities().With<components.LuaScript>().AsEnumerable();
        foreach (DefaultEcs.Entity entity in entities)
        {
            _onAdded(entity, entity.Get<components.LuaScript>());
            ++nInitialEntites;
        }
        Trace($"Added {nInitialEntites} initial entites.");

        return GetSubscriptions(world).Merge();
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (var pair in _luaScriptResources)
        {
            _unloadMesh(pair.Key);
        }

        _luaScriptResources.Clear();
    }


    public LuaScriptManager()
    {
        _lo = new object();
        _luaScriptResources = new Dictionary<LuaScriptEntry, Resource<LuaScriptEntry>>();
    }
}

