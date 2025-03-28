
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using builtin.tools.kanshu;
using DefaultEcs;
using engine;
using engine.joyce;
using Splash.components;
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
    private readonly Dictionary<AMeshParams, Resource<AMeshEntry>> _meshResources;
    private readonly Dictionary<engine.joyce.Material, Resource<AMaterialEntry>> _materialResources;
    private readonly Dictionary<engine.joyce.Model, Resource<AAnimationsEntry>> _animationsResources;


    private void _unloadAnim(Resource<AAnimationsEntry> animResource)
    {
        _threeD.UnloadAnimationsEntry(animResource.Value);
    }
    

    private void _unloadMesh(Resource<AMeshEntry> meshResource)
    {
        _threeD.UnloadMeshEntry(meshResource.Value);
    }

    
    private void _unloadMaterial(Resource<AMaterialEntry> materialResource)
    {
        _threeD.UnloadMaterialEntry(materialResource.Value);
    }


    private AAnimationsEntry _loadAnimations(Model jModel)
    {
        var aAnimationsEntry = _threeD.CreateAnimationsEntry(jModel);
        return aAnimationsEntry;
    }
    

    private AMeshEntry _loadMesh(AMeshParams meshParams)
    {
        // TXWTODO: We're not housekeeping duplicate meshes, are we?
        var aMeshEntry = _threeD.CreateMeshEntry(meshParams);
        return aMeshEntry;
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


    public PfInstance CreatePfInstance(InstanceDesc id)
    {
        IList<AMeshEntry> aMeshEntries = new List<AMeshEntry>();
        IList<AMaterialEntry> aMaterialEntries = new List<AMaterialEntry>();
        IList<AAnimationsEntry> aAnimationsEntries = new List<AAnimationsEntry>();

        lock (_lo)
        {
            /*
             * Now we first need to load the materials, then the meshes.
             * We need the material entries first, because we need to know how we resolve
             * the textures to create the mesh.
             */
            for (int i = 0; i < id.Materials.Count; ++i)
            {
                Resource<AMaterialEntry> materialResource;
                engine.joyce.Material jMaterial = id.Materials[i];
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
                
                /*
                 * Now that we have the material we need to find the associated textures as
                 * well: We need to know how the texture is represented to properly scale the
                 * mesh.
                 */
            }

            
            for (int i = 0; i < id.Meshes.Count; ++i)
            {
                engine.joyce.Material jMeshMaterial = id.Materials[id.MeshMaterials[i]];
                int nModelNodes = 0;
                if (id.ModelNodes != null)
                {
                    nModelNodes = id.ModelNodes.Count;
                }; 
                engine.joyce.ModelNode jModelNode = null;
                if (i < nModelNodes)
                {
                    jModelNode = id.ModelNodes[i];
                }
                
                // TXWTODO: somehow derive the UV scale from the material
                Resource<AMeshEntry> meshResource;
                AMeshParams meshParams = new()
                {
                    JMesh = id.Meshes[i]
                };
                
                engine.joyce.Texture jTexture = jMeshMaterial.Texture;
                if (null == jTexture)
                {
                    jTexture = jMeshMaterial.EmissiveTexture;
                }

                if (null != jTexture)
                {
                    meshParams.UVOffset = jTexture.UVOffset;
                    meshParams.UVScale = jTexture.UVScale;
                }
                else
                {
                    meshParams.UVOffset = Vector2.Zero;
                    meshParams.UVScale = Vector2.One;
                }

                if (!_meshResources.TryGetValue(meshParams, out meshResource))
                {
                    try
                    {
                        AMeshEntry aMeshEntry = _loadMesh(meshParams);
                        meshResource = new Resource<AMeshEntry>(aMeshEntry);
                        _meshResources.Add(meshParams, meshResource);
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


                bool haveEntry = false;
                if (jModelNode != null && jModelNode.Model.AllBakedMatrices != null)
                {
                    var jModel = jModelNode.Model;
                    Resource<AAnimationsEntry> animResource;
                    if (!_animationsResources.TryGetValue(jModel, out animResource))
                    {
                        try
                        {
                            var aAnimationsEntry = _loadAnimations(jModel);
                            animResource = new Resource<AAnimationsEntry>(aAnimationsEntry);
                            _animationsResources.Add(jModel, animResource);
                        }
                        catch (Exception e)
                        {
                            Error("Exception loading animation: {e}");
                        }
                    }
                    
                    aAnimationsEntries.Add(animResource.Value);
                    haveEntry = true;
                }

                if (false == haveEntry)
                {
                    aAnimationsEntries.Add(null);
                }
            }
            
        }

        /*
         * Finally, assign these arrays to the entity.
         */
        return new PfInstance(
            id,
            aMeshEntries.ToImmutableList(),
            aMaterialEntries.ToImmutableList(),
            aAnimationsEntries.ToImmutableList()
        );
    }
    

    private void _add(in Entity entity, in Splash.components.PfInstance value)
    {
        /*
         * We do not do anything on add because we assume, the data was created
         * using FillPfInstance
         */
    }


    private void _remove(in Entity entity, in Splash.components.PfInstance value)
    {
        
        bool hasInstance3 = entity.Has<engine.joyce.components.Instance3>();
        bool hasPfInstance = entity.Has<Splash.components.PfInstance>();
        
        // Trace($"hasInstance3 is {hasInstance3}, hasPfInstance is {hasPfInstance}.");

        if (hasPfInstance)
        {
            /*
             * We are in a sequence of remove entity calls.
             * The indirect call from remove Instance3 will trigger us again.
             */
            return;
        }

        // TXWTODO: Lock is superfluous, we only have one ECS Thread.
        lock (_lo)
        {
            for (int i = 0; i < value.InstanceDesc.Meshes.Count; ++i)
            {
                Resource<AMeshEntry> meshResource;
                AMeshParams aMeshParams = value.AMeshEntries[i].Params;
                // engine.joyce.Mesh jMesh = value.InstanceDesc.Meshes[i];
                if (!_meshResources.TryGetValue(aMeshParams, out meshResource))
                {
                    Error($"Unknown mesh to unreference.");
                }
                else
                {
                    if (meshResource.RemoveReference())
                    {
                        try
                        {
                            _unloadMesh(meshResource);
                        }
                        finally
                        {
                            _meshResources.Remove(aMeshParams);
                        }
                    }
                }
            }


            for (int i = 0; i < value.InstanceDesc.Materials.Count; ++i)
            {
                Resource<AMaterialEntry> materialResource;
                engine.joyce.Material jMaterial = value.InstanceDesc.Materials[i];
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
                            _unloadMaterial( materialResource);
                        }
                        finally
                        {
                            _materialResources.Remove(jMaterial);
                        }
                    }
                }
            }
            
            for (int i = 0; i < value.InstanceDesc.ModelNodes.Count; ++i)
            {
                var mn = value.InstanceDesc.ModelNodes[i];
                if (mn != null)
                {
                    var jModel = mn.Model;
                    Resource<AAnimationsEntry> animResource;
                    if (!_animationsResources.TryGetValue(jModel, out animResource))
                    {
                        Error("Unknown animations to unreference");
                    }
                    else
                    {
                        if (animResource.RemoveReference())
                        {
                            try
                            {
                                _unloadAnim(animResource);
                            }
                            finally
                            {
                                _animationsResources.Remove(jModel);
                            }
                        }
                    }
                }
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


    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (KeyValuePair<AMeshParams, Resource<AMeshEntry>> pair in _meshResources)
        {
            _unloadMesh(pair.Value);
        }

        foreach (KeyValuePair<engine.joyce.Material, Resource<AMaterialEntry>> pair in _materialResources)
        {
            _unloadMaterial(pair.Value);
        }

        foreach (KeyValuePair<engine.joyce.Model, Resource<AAnimationsEntry>> pair in _animationsResources)
        {
            _unloadAnim(pair.Value);
        }

        _meshResources.Clear();
        _materialResources.Clear();
        _animationsResources.Clear();
    }


    public InstanceManager()
    {
        _lo = new object();
        _threeD = I.Get<IThreeD>();
        _meshResources = new Dictionary<AMeshParams, Resource<AMeshEntry>>();
        _materialResources = new Dictionary<engine.joyce.Material, Resource<AMaterialEntry>>();
        _animationsResources = new Dictionary<Model, Resource<AAnimationsEntry>>();
    }
}

