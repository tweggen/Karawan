using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using builtin.loader;
using engine;
using static engine.Logger;

namespace engine.joyce;


public class ModelCache
{
    private engine.Engine _engine = I.Get<engine.Engine>();


    internal class ConsumerEntry
    {
        public SemaphoreSlim Sem = new(0);
    }
    
    
    internal class ModelCacheEntry
    {
        public object LockObject = new();

        public List<ConsumerEntry> ConsumerList = new();
        
        public enum EntryState
        {
            Placeholder,
            PlaceholderLoading,
            Loaded,
            Error
        };
        public Model Model;
        public EntryState State;
        public readonly ModelCacheParams ModelCacheParams;

        public ModelCacheEntry(ModelCacheParams mcp)
        {
            ModelCacheParams = mcp;
        }
    };

    
    /**
     * This protects the dictionary.
     */
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

    
    /**
     * This is the actual cache of models we keep.
     */
    private readonly ConcurrentDictionary<string, ModelCacheEntry> _cache = new ConcurrentDictionary<string, ModelCacheEntry>();
    
    
    private Task<Model> _instantiateModelParams(
        Model model,
        ModelProperties modelProperties,
        InstantiateModelParams p)
    {
        return _engine.Run(() =>
        {
            var mnRoot = model.RootNode;
            if (mnRoot != null)
            {
                var id = mnRoot.InstanceDesc;

                /*
                 * If this is an non-hjierarchical model, we bake the model params
                 * directly into the instancedesc.
                 */
                // TXWTODO: Where do we apply maximal distance for hierarchical models? 
                if (id != null && (mnRoot.Children == null || mnRoot.Children.Count == 0))
                {
                    Matrix4x4 m = Matrix4x4.Identity;
                    id.ComputeAdjustMatrix(p, ref m);
                    id.ModelTransform *= m;
                    id.MaxDistance = p.MaxDistance;
                }
            }
            return model;
        });
    }
    

    private Task<Model> _fromFile(
        string url, ModelProperties modelProperties)
    {
        if (url.EndsWith(".obj"))
        {
            return I.Get<Obj>().LoadModelInstance(url, modelProperties);
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
            return _engine.Run(() => new Model());
        }
    }


    private async Task<Model> _obtain(ModelCacheEntry mce)
    {
        var mcp = mce.ModelCacheParams;
        var model = await _fromFile(mcp.Url, mcp.Properties);

        if (mcp.Params != null && (mcp.Params.GeomFlags & InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC) != 0)
        {
            if (null == model.RootNode || null == model.RootNode.InstanceDesc)
            {
                ErrorThrow($"Reading url {mcp.Url} model does not have a root model instance defined.",
                    m => new ArgumentException(m));
            }

            model.RootNode.InstanceDesc.ModelCacheParams = mcp;
        }

        model = await _instantiateModelParams(model, mcp.Properties, mcp.Params);

        model = FindLights.Process(model);
        
        return model;
    }


    private SemaphoreSlim _sem(string hash)
    {
        var keyLock = _keyLocks.GetOrAdd(hash, x => new SemaphoreSlim(1));
        return keyLock;
    }
    
    
    async Task _tryLoad(ModelCacheEntry mce)
    {
        var mcp = mce.ModelCacheParams;
        lock (mce.LockObject)
        { 
            /*
             * Only start loading if the model is in placeholder state.
             */
            if (mce.State != ModelCacheEntry.EntryState.Placeholder)
            {
                return;
            }

            mce.State = ModelCacheEntry.EntryState.PlaceholderLoading;
        }

        try
        {
            Model model = await _obtain(mce);

            /*
             * transition to the proper state and notify the owners.
             */
            await _engine.TaskMainThread(() =>
            {
                lock (mce.LockObject)
                {
                    mce.Model.FillPlaceholderFrom(model);
                    mce.State = ModelCacheEntry.EntryState.Loaded;
                    foreach (var ce in mce.ConsumerList)
                    {
                        ce.Sem.Release();
                    }

                    mce.ConsumerList.Clear();
                    // Trace($"Resolved #{mcp.Url}");
                }
            });

        }
        catch (Exception exception)
        {
            Error($"Unable to load model {mcp}: {exception}.");
            lock (mce.LockObject)
            {
                mce.State = ModelCacheEntry.EntryState.Error;
                foreach (var ce in mce.ConsumerList)
                {
                    ce.Sem.Release();
                }
                mce.ConsumerList.Clear();
            }
        }
    }

    private ModelCacheEntry _triggerInstantiate(ModelCacheParams mcp)
    {
        string hash = mcp.GetHashCode();
        bool tryLoad = false;

        ModelCacheEntry modelCacheEntry;

        var keyLock = _sem(hash);
        keyLock.Wait();
        try
        {
            /*
             * Obtain an existing model structure or create a new one.
             * Note, that both of these operations are atomic and return
             * a valid model structure. Which, however, might change
             * asynchronously.
             */
            if (!_cache.TryGetValue(hash, out modelCacheEntry))
            {
                modelCacheEntry = new(mcp);
                var id = new InstanceDesc(mcp);
                modelCacheEntry.Model = new Model(id);
                modelCacheEntry.State = ModelCacheEntry.EntryState.Placeholder;
                _cache.TryAdd(hash, modelCacheEntry);
                
                /*
                 * Trigger async loading of item.
                 */
                tryLoad = true;

            }
            else
            {
                /*
                 * Then we have the modelCacheEntry
                 */
            }
        }
        finally
        {
            keyLock.Release();
        }

        
        /*
         * If the model had not been loaded before, trigger loading of the model.
         */
        if (tryLoad)
        {
            Task.Run(() =>
            {
                _tryLoad(modelCacheEntry);
            });
        }

        return modelCacheEntry;
    }

    
    /**
     * Immediately return a model which has an instance desc that we
     * already can use even if it has not been filled with content.
     */
    public Model InstantiatePlaceholder(ModelCacheParams mcp)
    {
        var mce = _triggerInstantiate(mcp);
        lock (mce.LockObject)
        {
            return mce.Model;
        }
    }


    /**
     * Create an instance of the given model.
     *
     * The model might be loaded, generated or just replicated
     * from memory.
     */
    public async Task<Model> Instantiate(ModelCacheParams mcp)
    {
        ModelCacheEntry mce = _triggerInstantiate(mcp);

        ConsumerEntry ce;
        
        /*
         * Look, if we already can return, because the entry has the right type.
         */
        lock (mce.LockObject)
        {
            // need to go on looping for "placeholder" as well.
            switch (mce.State)
            {
                case ModelCacheEntry.EntryState.Error:
                    ErrorThrow<InvalidOperationException>($"Unable to load model {mcp}.");
                    return mce.Model;
                case ModelCacheEntry.EntryState.Loaded:
                    return mce.Model;
            }

            ce = new();
            mce.ConsumerList.Add(ce);
        }

        /*
         * Wait, until someone wakes are callers up.
         */
        await ce.Sem.WaitAsync();
        /*
         * Look, if we already can return, because the entry has the right type.
         */
        lock (mce.LockObject)
        {
            // need to go on looping for "placeholder" as well.
            switch (mce.State)
            {
                default:
                    ErrorThrow<InvalidOperationException>($"Unable to load model {mcp}.");
                    return mce.Model;
                case ModelCacheEntry.EntryState.Error:
                    ErrorThrow<InvalidOperationException>($"Unable to load model {mcp}.");
                    return mce.Model;
                case ModelCacheEntry.EntryState.Loaded:
                    return mce.Model;
            }
        }
    }

    
    static int nextReqId = 0;

    public async Task<Model> Instantiate(
        string url,
        ModelProperties? modelProperties,
        InstantiateModelParams? p)
    {
        int reqId = ++nextReqId; 
        // Trace($"Requested #{reqId} {url}");
        Task<Model> tModel = Instantiate(new ModelCacheParams()
        {
            Url = url,
            Properties = modelProperties,
            Params = p
        });
        Model model = await tModel;
        // Trace($"Have #{reqId} {url}");
        return model;
    }
}
