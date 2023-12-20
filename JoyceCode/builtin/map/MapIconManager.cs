using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.joyce;
using static engine.Logger;

namespace builtin.map;

/**
 * Compile and destroy meshes from MapIcon components.
 */
public class MapIconManager : IDisposable
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
    private readonly Dictionary<engine.world.components.MapIcon.IconCode, Resource<InstanceDesc>> _instanceDescResources;


    public int MapIconsHoriz { get; set; } = 4;
    public int MapIconsVert { get; set; } = 4;
    public string MapIconsTexture { get; set; } = "mapicons.png";
    

    private void _unloadInstanceDesc(engine.world.components.MapIcon.IconCode mapIcon, Resource<InstanceDesc> instanceDescEntry)
    {
        // Currently, we don't seriosly unload anything.
    }


    /**
     * Create an instance desc from a given map icon.
     * Creating meshes from map icons is simple, we just create a rectangular mesh referencing
     * the icon texture.
     */
    private InstanceDesc _createInstanceDesc(in engine.world.components.MapIcon.IconCode jMapIcon)
    {
        int idx = (int)jMapIcon;

        Vector2 v2Pos = new(
            ((float)(idx%MapIconsHoriz))/(float)MapIconsHoriz,
            ((float)(idx/MapIconsVert))/(float)MapIconsVert
            );
        var jMesh = engine.joyce.mesh.Tools.CreatePlaneMesh(
            $"mapIcon{jMapIcon}", 5000f*Vector2.One,
            v2Pos, new Vector2(1f / MapIconsHoriz, 0f), new Vector2(0f, 1f / MapIconsVert)
            );
        var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(
            I.Get<ObjectRegistry<Material>>().Get("builtin.map.mapicons"),
            jMesh), Single.MaxValue);
        return jInstanceDesc;
    }


    private void _add(in Entity entity, in engine.world.components.MapIcon.IconCode value)
    {
        Resource<InstanceDesc> instanceDescResource;
        engine.world.components.MapIcon.IconCode mapIcon = value;
        if (!_instanceDescResources.TryGetValue(mapIcon, out instanceDescResource))
        {
            try
            {
                InstanceDesc jInstanceDesc = _createInstanceDesc(mapIcon);
                instanceDescResource = new Resource<InstanceDesc>(jInstanceDesc);
                _instanceDescResources.Add(mapIcon, instanceDescResource);
            }
            catch (Exception e)
            {
                Error("Exception creating mapIcon mesh: {e}");
            }
        }

        if (null != instanceDescResource)
        {
            instanceDescResource.AddReference();
            entity.Set(new engine.joyce.components.Instance3(instanceDescResource.Value));
        }
    }


    private void _remove(in Entity entity, in engine.world.components.MapIcon.IconCode value)
    {
        Resource<InstanceDesc> instanceDescResource;
        if (!_instanceDescResources.TryGetValue(value, out instanceDescResource))
        {
            Error($"Unknown mapIcon to unreference.");
        }
        else
        {
            if (instanceDescResource.RemoveReference())
            {
                try
                {
                    _unloadInstanceDesc(value, instanceDescResource);
                }
                finally
                {
                    _instanceDescResources.Remove(value);
                }
            }
        }

        if (entity.Has<engine.joyce.components.Instance3>())
        {
            entity.Remove<engine.joyce.components.Instance3>();
        }
    }

    
    private void _onAdded(in Entity entity, in engine.world.components.MapIcon value)
    {
        _add(entity, value.Code);
    }


    private void _onChanged(in Entity entity,
        in engine.world.components.MapIcon oldValue,
        in engine.world.components.MapIcon newValue)
    {
        _remove(entity, oldValue.Code);
        _add(entity, newValue.Code);
    }
    

    private void _onRemoved(in Entity entity, in engine.world.components.MapIcon value)
    {
        _remove(entity, value.Code);
    }
    

    public IDisposable Manage(World world)
    {
        IEnumerable<IDisposable> GetSubscriptions(World w)
        {
            yield return w.SubscribeEntityComponentAdded<engine.world.components.MapIcon>(_onAdded);
            yield return w.SubscribeEntityComponentChanged<engine.world.components.MapIcon>(_onChanged);
            yield return w.SubscribeEntityComponentRemoved<engine.world.components.MapIcon>(_onRemoved);
        }

        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }
        
        int nInitialEntites = 0;
        var entities = world.GetEntities().With<engine.world.components.MapIcon>().AsEnumerable();
        foreach (DefaultEcs.Entity entity in entities)
        {
            _onAdded(entity, entity.Get<engine.world.components.MapIcon>());
            ++nInitialEntites;
        }
        
        return GetSubscriptions(world).Merge();
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (KeyValuePair<engine.world.components.MapIcon.IconCode, Resource<InstanceDesc>> pair in _instanceDescResources)
        {
            _unloadInstanceDesc(pair.Key, pair.Value);
        }

        _instanceDescResources.Clear();
    }


    public MapIconManager()
    {
        _lo = new object();
        _instanceDescResources = new Dictionary<engine.world.components.MapIcon.IconCode, Resource<InstanceDesc>>();
        I.Get<ObjectRegistry<Material>>().RegisterFactory("builtin.map.mapicons",
            (name) => new engine.joyce.Material()
            {
                AlbedoColor = (bool)engine.Props.Get("debug.options.flatshading", false) != true
                    ? 0x00000000
                    : 0xff333333,
                Texture = new engine.joyce.Texture(MapIconsTexture),
                HasTransparency = true
            });

    }
}