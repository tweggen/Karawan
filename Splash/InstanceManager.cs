
using System;
using System.Collections.Generic;
using DefaultEcs;
using static engine.Logger;

namespace Splash;

public class InstanceManager : IDisposable
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
    private readonly IThreeD _threeD;
    private readonly Dictionary<engine.joyce.Mesh, Resource<AMeshEntry>> _meshResources;
    private readonly Dictionary<engine.joyce.Material, Resource<AMaterialEntry>> _materialResources;


    private void _unloadMesh(engine.joyce.Mesh jMesh, Resource<AMeshEntry> meshResource)
    {
        _threeD.UnloadMeshEntry(meshResource.Value);
    }

    private void _unloadMaterial(engine.joyce.Material jMesh, Resource<AMaterialEntry> materialResource)
    {
        _threeD.UnloadMaterialEntry(materialResource.Value);
    }


    private AMeshEntry _loadMesh(in engine.joyce.Mesh jMesh)
    {
        return _threeD.CreateMeshEntry(jMesh);
    }


    private AMaterialEntry _loadMaterial(in engine.joyce.Material jMaterial)
    {
        return _threeD.CreateMaterialEntry(jMaterial);
    }


    private void _onAdded(in Entity entity, in Splash.components.PfInstance value) => _add(entity, value);


    private void _onChanged(in Entity entity, in Splash.components.PfInstance oldValue,
        in Splash.components.PfInstance newValue)
    {
        _add(entity, newValue);
        _remove(entity, oldValue);
    }

    private void _onRemoved(in Entity entity, in Splash.components.PfInstance value) => _remove(entity, value);


    private void _add(in Entity entity, in Splash.components.PfInstance value)
    {
        IList<AMeshEntry> aMeshEntries = new List<AMeshEntry>();
        IList<AMaterialEntry> aMaterialEntries = new List<AMaterialEntry>();

        lock (_lo)
        {
            for (int i = 0; i < value.Meshes.Count; ++i)
            {
                Resource<AMeshEntry> meshResource;
                engine.joyce.Mesh jMesh = value.Meshes[i];
                if (!_meshResources.TryGetValue(jMesh, out meshResource))
                {
                    try
                    {
                        AMeshEntry aMeshEntry = _loadMesh(jMesh);
                        meshResource = new Resource<AMeshEntry>(aMeshEntry);
                        _meshResources.Add(jMesh, meshResource);
                    }
                    catch (Exception e)
                    {
                        Error("Exception loading mesh: {e}");
                    }
                }

                if (null != meshResource)
                {
                    aMeshEntries.Add(meshResource.Value);
                    meshResource.AddReference();
                }
            }

            for (int i = 0; i < value.Materials.Count; ++i)
            {
                Resource<AMaterialEntry> materialResource;
                engine.joyce.Material jMaterial = value.Materials[i];
                if (!_materialResources.TryGetValue(jMaterial, out materialResource))
                {
                    try
                    {
                        AMaterialEntry aMaterialEntry = _loadMaterial(jMaterial);
                        materialResource = new Resource<AMaterialEntry>(aMaterialEntry);
                        _materialResources.Add(jMaterial, materialResource);
                    }
                    catch (Exception e)
                    {
                        Error("Exception loading mesh: {e}");
                    }
                }

                if (null != materialResource)
                {
                    aMaterialEntries.Add(materialResource.Value);
                    materialResource.AddReference();
                }
            }
        }

        // TXWTODO: Looks inefficient
        /*
         * Finally, assign these arrays to the entity.
         */
        entity.Get<Splash.components.PfInstance>().AMaterialEntries = aMaterialEntries;
        entity.Get<Splash.components.PfInstance>().AMeshEntries = aMeshEntries;
    }


    private void _remove(in Entity entity, in Splash.components.PfInstance value)
    {
        lock (_lo)
        {
            for (int i = 0; i < value.Meshes.Count; ++i)
            {
                Resource<AMeshEntry> meshResource;
                engine.joyce.Mesh jMesh = value.Meshes[i];
                if (!_meshResources.TryGetValue(jMesh, out meshResource))
                {
                    Error($"Unknown mesh to unreference.");
                }
                else
                {
                    if (meshResource.RemoveReference())
                    {
                        try
                        {
                            _unloadMesh(jMesh, meshResource);
                        }
                        finally
                        {
                            _meshResources.Remove(jMesh);
                        }
                    }
                }
            }
            value.Meshes.Clear();

            if (value.AMaterialEntries != null)
            {
                for (int i = 0; i < value.Materials.Count; ++i)
                {
                    Resource<AMaterialEntry> materialResource;
                    engine.joyce.Material jMaterial = value.Materials[i];
                    if (!_materialResources.TryGetValue(jMaterial, out materialResource))
                    {
                        Error("Unknown material to unreference.");
                    }
                    else
                    {
                        if (materialResource.RemoveReference())
                        {
                            try
                            {
                                _unloadMaterial(jMaterial, materialResource);
                            }
                            finally
                            {
                                _materialResources.Remove(jMaterial);
                            }
                        }
                    }
                }

                value.Materials.Clear();
                value.MeshMaterials.Clear();
            }
        }
    }
    

    /**
     * If the user replaces the new instance3 specifying the
     * mesh to use, we remove the pre-compiled PfInstance.
     */
    private void _onChanged(in DefaultEcs.Entity entity,
        in engine.joyce.components.Instance3 cOldInstance,
        in engine.joyce.components.Instance3 cNewInstance)
    {
        entity.Remove<Splash.components.PfInstance>();
    }


    /**
     * If the user removes the new instance3 specifying the
     * mesh to use, we remove the pre-compiled PfInstance.
     */
    private void _onRemoved(in DefaultEcs.Entity entity,
        in engine.joyce.components.Instance3 cOldInstance)
    {
        entity.Remove<Splash.components.PfInstance>();
    }

    
    public IDisposable Manage(World world)
    {
        IEnumerable<IDisposable> GetSubscriptions(World w)
        {
            yield return w.SubscribeEntityComponentAdded<Splash.components.PfInstance>(_onAdded);
            yield return w.SubscribeEntityComponentChanged<Splash.components.PfInstance>(_onChanged);
            yield return w.SubscribeEntityComponentRemoved<Splash.components.PfInstance>(_onRemoved);
            yield return w.SubscribeEntityComponentChanged<engine.joyce.components.Instance3>(_onChanged);
            yield return w.SubscribeEntityComponentRemoved<engine.joyce.components.Instance3>(_onRemoved);
        }

        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }

        int nInitialEntites = 0;
        var entities = world.GetEntities().With<Splash.components.PfInstance>().AsEnumerable();
        foreach (DefaultEcs.Entity entity in entities)
        {
            _onAdded(entity, entity.Get<Splash.components.PfInstance>());
            ++nInitialEntites;
        }
        Trace($"Added {nInitialEntites} initial entites.");

        return GetSubscriptions(world).Merge();
    }


    /// <summary>
    /// Unloads all loaded resources.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (KeyValuePair<engine.joyce.Mesh, Resource<AMeshEntry>> pair in _meshResources)
        {
            _unloadMesh(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<engine.joyce.Material, Resource<AMaterialEntry>> pair in _materialResources)
        {
            _unloadMaterial(pair.Key, pair.Value);
        }

        _meshResources.Clear();
        _materialResources.Clear();
    }

    /// <summary>
    /// Creates an instance of type <see cref="AResourceManager{TInfo, TResource}"/>.
    /// </summary>
    public InstanceManager(in IThreeD threeD)
    {
        _lo = new object();
        _threeD = threeD;
        _meshResources = new Dictionary<engine.joyce.Mesh, Resource<AMeshEntry>>();
        _materialResources = new Dictionary<engine.joyce.Material, Resource<AMaterialEntry>>();
    }
}

