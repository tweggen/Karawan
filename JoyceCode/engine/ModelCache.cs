using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using builtin.loader;
using engine;
using engine.joyce;

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
    
    
    private string _hash(string url, in InstantiateModelParams p)
    {
        return $"FromFile('{url}'),{p.Hash()}";
    }


    private Task<Model> _instantiateModelParams(
        Model model,
        InstantiateModelParams p)
    {
        return Task.Run(() =>
        {
            var modelInfo = model.ModelInfo;
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

            Matrix4x4 m = Matrix4x4.Identity;

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

            modelInfo.AABB.Reset();
            foreach (Mesh mesh in model.InstanceDesc.Meshes)
            {
                mesh.Transform(m);
                mesh.ComputeAABB(out var meshAABB);
                modelInfo.AABB.Add(meshAABB);
            }

            /*
             * Keep in mind we need to adjust both the mesh and the model info.
             */
            modelInfo.Center = Vector3.Transform(modelInfo.Center, m);

            return model;
        });
    }
    

    private Task<(InstanceDesc InstanceDesc, ModelInfo ModelInfo)> _fromFile(string url)
    {
        return Obj.LoadModelInstance(url);
    }


    private async Task<Model> _obtain(
        string url, InstantiateModelParams p)
    {
        var resultFromFile =
            await _fromFile(url);

        Model model = new Model();
        model.InstanceDesc = resultFromFile.InstanceDesc;
        model.ModelInfo = resultFromFile.ModelInfo;
        
        model = await _instantiateModelParams(model, p);

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
        string url, InstantiateModelParams p)
    {
        string hash = _hash(url, p);
        Model model;

        var keyLock = _keyLocks.GetOrAdd(hash, x => new SemaphoreSlim(1));
        await keyLock.WaitAsync().ConfigureAwait(false);

        try
        {
            // try to get Store from cache
            if (!_cache.TryGetValue(hash, out model))
            {
                // if value isn't cached, get it from the DB asynchronously
                model = await _obtain(url, p).ConfigureAwait(false);

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