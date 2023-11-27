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
        in InstantiateModelParams p)
    {
        string mpHash = (modelProperties != null) ? modelProperties.ToString() : "null";
        return $"FromFile('{url}'),{mpHash},{p.Hash()}";
    }


    private void _applyModelParamMatrix(ModelInfo modelInfo, InstantiateModelParams p, ref Matrix4x4 m)
    {
        /*
         * Now, according to the instantiateModelParams, modify the data we loaded.
         */
        Vector3 vReCenter = new(
            (p.GeomFlags & InstantiateModelParams.CENTER_X) != 0
                ? (
                    (p.GeomFlags & InstantiateModelParams.CENTER_X_POINTS) != 0
                        ? modelInfo.Center.X
                        : modelInfo.AABB.Center.X)
                : 0f,
            (p.GeomFlags & InstantiateModelParams.CENTER_Y) != 0
                ? (
                    (p.GeomFlags & InstantiateModelParams.CENTER_Y_POINTS) != 0
                        ? modelInfo.Center.Y
                        : modelInfo.AABB.Center.Y)
                : 0f,
            (p.GeomFlags & InstantiateModelParams.CENTER_Z) != 0
                ? (
                    (p.GeomFlags & InstantiateModelParams.CENTER_Z_POINTS) != 0
                        ? modelInfo.Center.Z
                        : modelInfo.AABB.Center.Z)
                : 0f
        );

        if (vReCenter != Vector3.Zero)
        {
            m = m * Matrix4x4.CreateTranslation(-vReCenter);
        }

        int rotX = ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_X90)) ? 1 : 0) +
                   ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_X180)) ? 2 : 0);
        int rotY = ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_Y90)) ? 1 : 0) +
                   ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_Y180)) ? 2 : 0);
        int rotZ = ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_Z90)) ? 1 : 0) +
                   ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_Z180)) ? 2 : 0);

        if (0 != rotX)
        {
            m = m * Matrix4x4.CreateRotationX(Single.Pi * rotX / 2f);
        }

        if (0 != rotY)
        {
            m = m * Matrix4x4.CreateRotationY(Single.Pi * rotY / 2f);
        }

        if (0 != rotZ)
        {
            m = m * Matrix4x4.CreateRotationZ(Single.Pi * rotZ / 2f);
        }
    }
    

    private Task<Model> _instantiateModelParams(
        Model model,
        ModelProperties modelProperties,
        InstantiateModelParams p)
    {
        return Task.Run(() =>
        {
            var modelInfo = model.ModelInfo;

            Matrix4x4 m = Matrix4x4.Identity;
            _applyModelParamMatrix(modelInfo, p, ref m);

            modelInfo.AABB.Reset();
            // TXWTODO: Rework this before introducing hierarchies of nodes.
            foreach (Mesh mesh in model.RootNode.InstanceDesc.Meshes)
            {
                mesh.Transform(m);
                modelInfo.AABB.Add(mesh.AABB);
            }

            /*
             * Keep in mind we need to adjust both the mesh and the model info.
             */
            modelInfo.Center = Vector3.Transform(modelInfo.Center, m);

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
        string url, ModelProperties modelProperties, InstantiateModelParams p)
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