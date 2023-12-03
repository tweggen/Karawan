using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using builtin.loader;
using engine;
using engine.joyce;
using static engine.Logger;

namespace engine;

public class ModelCache
{
    private static object _loClass = new();
    private static ModelCache _instance;
    
    private object _lo = new();
    
    /**
     * This is the per-model semaphore
     */
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

    /**
     * This is the actual cache of models we keep.
     */
    private readonly ConcurrentDictionary<string, Model> _cache = new ConcurrentDictionary<string, Model>();
    
    
    private string _hash(string url,
        ModelProperties modelProperties,
        in InstantiateModelParams? p)
    {
        string mpHash = (modelProperties != null) ? modelProperties.ToString() : "null";
        string pHash = (p != null) ? p.Hash() : "null";
        return $"FromFile('{url}'),{mpHash},{pHash}";
    }


    private Task<Model> _instantiateModelParams(
        Model model,
        ModelProperties modelProperties,
        InstantiateModelParams p)
    {
        return Task.Run(() =>
        {
            Matrix4x4 m = Matrix4x4.Identity;
            

    /*
     * We don't modify the mesh any more on loading,
     * we need to have it original to smothly implement
     * the animations.
     */
#if true
#else
            /*
             * Compute the model adjustment matrix from the modelinfo
             */
            modelInfo.ComputeAdjustMatrix(p, ref m);

            /*
             * Now apply the matrix computed by the model params
             * to the top level model matrix.
             */
            model.RootNode.Transform.Matrix *= m;

            model.ComputeAABB(out var aabb);
            modelInfo.AABB = aabb;
            
            /*
             * Keep in mind we need to adjust both the mesh and the model info.
             */
            modelInfo.Center = Vector3.Transform(modelInfo.Center, m);
#endif

            return model;
        });
    }
    

    private Task<Model> _fromFile(
        string url, ModelProperties modelProperties)
    {
        if (url.EndsWith(".obj"))
        {
            return Obj.LoadModelInstance(url, modelProperties);
        } else if (url.EndsWith(".fbx"))
        {
            return Fbx.LoadModelInstance(url, modelProperties);
        } else if (url.EndsWith(".glb"))
        {
            return GlTF.LoadModelInstance(url, modelProperties);
        }
        else
        {
            ErrorThrow($"Unsupported file format for url {url}.", m => new ArgumentException(m));
            return Task.Run(() => new Model());
        }
    }


    private async Task<Model> _obtain(
        string url, ModelProperties modelProperties, InstantiateModelParams p)
    {
        var model = await _fromFile(url, modelProperties);

        if ((p.GeomFlags & InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC) != 0)
        {
            if (null == model.RootNode || null == model.RootNode.InstanceDesc)
            {
                ErrorThrow($"Reading url {url} model does not have a root model instance defined.", m => new ArgumentException(m));
            }
        }

        model = await _instantiateModelParams(model, modelProperties, p);

        model = FindLights.Process(model);
        
        return model;
    }


    /**
     * Create an instance of the given model.
     *
     * The model might be loaded, generated or just replicated
     * from memory.
     */
    public async Task<Model> Instantiate(
        string url, ModelProperties modelProperties, InstantiateModelParams? p = null)
    {
        string hash = _hash(url, modelProperties, p);
        Model model;

        var keyLock = _keyLocks.GetOrAdd(hash, x => new SemaphoreSlim(1));
        await keyLock.WaitAsync().ConfigureAwait(false);

        try
        {
            // try to get Store from cache
            if (!_cache.TryGetValue(hash, out model))
            {
                // if value isn't cached, get it from the DB asynchronously
                model = await _obtain(url, modelProperties, p).ConfigureAwait(false);

                // cache value
                _cache.TryAdd(hash, model);
            }
        }
        finally
        {
            keyLock.Release();
        }

        return model;
    }
    

    public static ModelCache Instance()
    {
        lock (_loClass)
        {
            if (_instance == null)
            {
                _instance = new ModelCache();
            }

            return _instance;
        }
    }

    private ModelCache()
    {
        
    }
}