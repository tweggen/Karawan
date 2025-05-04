using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using builtin.loader;
using builtin.tools;
using engine;
using engine.geom;
using engine.joyce.components;
using engine.physics;
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

    
    private ModelCacheEntry _triggerLoad(ModelCacheParams mcp)
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
                modelCacheEntry.Model = new Model();
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


    private void _triggerBuildEntities(DefaultEcs.Entity eTarget, Model model, ModelCacheParams mcp)
    {
#if true
        _engine.QueueMainThreadAction(() =>
        {
            /*
             * Actually build it up.
             */
            BuildPerInstance(eTarget, model, mcp);
        });
#else
        ModelBuilder modelBuilder = new(_engine, model, mcp.Params);
        modelBuilder.BuildEntity(eTarget);
#endif
    }
    
    
    /**
     * Immediately return a model which has an instance desc that we
     * already can use even if it has not been filled with content.
     * Build this instance asynchronously in the background.
     */
    public Model InstantiatePlaceholder(DefaultEcs.Entity eTarget, ModelCacheParams mcp)
    {
        // Trace($"Called with url {mcp.Url}");
        ModelCacheEntry mce = _triggerLoad(mcp);

        ConsumerEntry ce = null;
        Model? model = null;
        bool wasLoaded = false;
        
        lock (mce.LockObject)
        {
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
                        // ErrorThrow never returns
                        break;
                
                    case ModelCacheEntry.EntryState.Loaded:
                        model = mce.Model;
                        wasLoaded = true;
                        break;
                    
                    case ModelCacheEntry.EntryState.Placeholder:
                    case ModelCacheEntry.EntryState.PlaceholderLoading:
                        /*
                         * Still loading, make ourselves wait.
                         */
                        ce = new();
                        mce.ConsumerList.Add(ce);
                        model = mce.Model;
                        wasLoaded = false;
                        break;
                }
            }
        }
        
        /*
         * Remember what we are built from.
         */
        _engine.QueueMainThreadAction(() =>
        {
            eTarget.Set(new FromModel() { Model = model, ModelCacheParams = mcp });
        });
        
        if (wasLoaded)
        {
            /*
             * Was loaded? Then trigger building per instance right now.
             */
            _triggerBuildEntities(eTarget, model, mcp);
        } 
        else
        {

            /*
             * Not loaded yet? then wait async.
             */

            ce.Sem.WaitAsync().ContinueWith(tWait =>
            {
                /*
                 * Look, if we already can return, because the entry has the right type.
                 */
                lock (mce.LockObject)
                {
                    // need to go on looping for "placeholder" as well.
                    switch (mce.State)
                    {
                        case ModelCacheEntry.EntryState.Error:
                        default:
                            ErrorThrow<InvalidOperationException>($"Unable to load model {mcp}.");
                            // TXWTODO: We would need to invalidate the model that already had been returned.
                            break;

                        case ModelCacheEntry.EntryState.Loaded:
                            break;
                    }
                }
                _triggerBuildEntities(eTarget, model, mcp);
            });
        }
        
        return model;
    }


    /**
     * Create an instance of the given model.
     *
     * The model might be loaded, generated or just replicated
     * from memory.
     */
    public async Task<Model> LoadModel(ModelCacheParams mcp)
    {
        // Trace($"Called with url {mcp.Url}");
        ModelCacheEntry mce = _triggerLoad(mcp);

        ConsumerEntry ce = null;
        Model? model = null;
        
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
                    // ErrorThrow never returns
                    break;
                
                case ModelCacheEntry.EntryState.Loaded:
                    model = mce.Model;
                    break;
                
                case ModelCacheEntry.EntryState.Placeholder:
                case ModelCacheEntry.EntryState.PlaceholderLoading:
                    /*
                     * Still loading, make ourselves wait.
                     */
                    ce = new();
                    mce.ConsumerList.Add(ce);
                    break;
            }
        }

        /*
         * We already have a model? Then trigger per instance work.
         */
        if (model == null)
        {
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
                    case ModelCacheEntry.EntryState.Error:
                        ErrorThrow<InvalidOperationException>($"Unable to load model {mcp}.");
                        // TXWTODO: We would need to invalidate the model that already had been returned.
                        break;

                    case ModelCacheEntry.EntryState.Loaded:
                        model = mce.Model;
                        break;
                }
            }
        }
        return model;
    }


    enum ModelShape
    {
        Sphere,
        Cylinder,
        Cuboid
    }

    public void BuildPerInstancePhysics(in DefaultEcs.Entity eRoot,
        ModelBuilder modelBuilder, Model model, ModelCacheParams mcp)
    {
        if (model.RootNode == null || model.RootNode.InstanceDesc == null)
        {
            return;
        }

        if ((mcp.Params.GeomFlags & InstantiateModelParams.BUILD_PHYSICS) != 0)
        {
            /*
             * Create the physics into eTarget based on a lot of assumptions.
             */
            ShapeFactory shapeFactory = I.Get<ShapeFactory>();

            engine.physics.Object po;

            /*
             * Naming physics makes them identifiable. Pull the name from the
             * entity name if no name has been given.
             */
            string strPhysicsName = "unnamed";
            if (eRoot.Has<EntityName>())
            {
                strPhysicsName = eRoot.Get<EntityName>().Name;
            }

            // TXWTODO: Find a smart way to build the physics inside the modelbuilder.
            ModelShape modelShape = ModelShape.Sphere;
            float radius = 1f;
            float height = 1f;
            AABB aabb = new();
            if (model.RootNode != null && model.RootNode.InstanceDesc != null)
            {
                /*
                 * If we have aabbs, we can create a cuboid.
                 */
                modelShape = ModelShape.Cuboid;
                aabb = model.RootNode.InstanceDesc.AABBTransformed;
                Vector3 v3Size = aabb.BB - aabb.AA;

                float rMin = Single.Min(v3Size.X, Single.Min(v3Size.Y, v3Size.Z));
                float rMax = Single.Max(v3Size.X, Single.Max(v3Size.Y, v3Size.Z));

                if (rMax > 0f)
                {
                    float ratio = rMin / rMax;
                    if (0.8f < ratio && 1.2f > ratio)
                    {
                        radius = aabb.Radius;
                        modelShape = ModelShape.Sphere;
                    }
                }

                if (v3Size.X > 0f)
                {
                    float topviewratio = v3Size.Z / v3Size.X;

                    if (0.5f < topviewratio && 1.85f > topviewratio)
                    {
                        radius = (v3Size.X+v3Size.Z)/2.0f;
                        height = v3Size.Y;
                        modelShape = ModelShape.Cylinder;
                    }
                }
            }

            var collisionProperties = new CollisionProperties
            {
                Entity = eRoot,
                Name = strPhysicsName,
                Flags =
                    (((mcp.Params.GeomFlags & InstantiateModelParams.PHYSICS_TANGIBLE) != 0)
                        ? CollisionProperties.CollisionFlags.IsTangible
                        : 0)
                    | (((mcp.Params.GeomFlags & InstantiateModelParams.PHYSICS_DETECTABLE) != 0)
                        ? CollisionProperties.CollisionFlags.IsDetectable
                        : 0)
                    | (((mcp.Params.GeomFlags & InstantiateModelParams.PHYSICS_CALLBACKS) != 0)
                        ? CollisionProperties.CollisionFlags.TriggersCallbacks
                        : 0),
                LayerMask = mcp.Params.CollisionLayers
            };

            lock (_engine.Simulation)
            {
                if ((mcp.Params.GeomFlags & InstantiateModelParams.PHYSICS_STATIC) != 0)
                {
                    /*
                     * build a static physics object.
                     */
                    
                    Vector3 v3Pos = Vector3.Zero;

                    /*
                     * Statics must be placed absolute, they are not dynamically moved.
                     * So we fall back on the transform 3 property, which probably is not the best idea.
                     */
                    if (eRoot.Has<Transform3>())
                    {
                        v3Pos = eRoot.Get<Transform3>().Position;
                    }

                    TypedIndex shape;
                    switch (modelShape)
                    {
                        default:
                        case ModelShape.Cuboid:
                            /*
                             * TXWTODO: Create cuboids.
                             */
                            shape = shapeFactory.GetSphereShape(aabb.Radius);
                            break;
                        case ModelShape.Sphere:
                            shape = shapeFactory.GetSphereShape(radius);
                            break;
                        case ModelShape.Cylinder:
                            shape = shapeFactory.GetCylinderShape(radius, height);
                            break;
                    }
                    StaticHandle staticHandle = _engine.Simulation.Statics.Add(
                        new StaticDescription(
                            v3Pos,
                            Quaternion.Identity,
                            shape
                        ));
                    
                    po = new(eRoot, staticHandle)
                    {
                        CollisionProperties = collisionProperties
                    };
                    
                    if ((mcp.Params.GeomFlags & InstantiateModelParams.PHYSICS_OWN_CALLBACKS) != 0)
                    {
                        po.AddContactListener();
                    }

                    eRoot.Set(new engine.physics.components.Statics(po, staticHandle));
                }
                else
                {
                    /*
                     * Build a non-static object.
                     * Depending on the mass, this is a dynamic or kinetmatic object.
                     * Be on the save side and try to assign it to a position as taken from a
                     * TransformToWorld.
                     */

                    Vector3 v3Pos = Vector3.Zero;

                    if (eRoot.Has<Transform3ToWorld>())
                    {
                        v3Pos = eRoot.Get<Transform3ToWorld>().Matrix.Translation;
                    }

                    BodyReference prefSphere;
                    TypedIndex shape;
                    switch (modelShape)
                    {
                        default:
                        case ModelShape.Cuboid:
                            /*
                             * TXWTODO: Create cuboids.
                             */
                            shape = shapeFactory.GetSphereShape(aabb.Radius);
                            break;
                        case ModelShape.Sphere:
                            shape = shapeFactory.GetSphereShape(radius);
                            break;
                        case ModelShape.Cylinder:
                            shape = shapeFactory.GetCylinderShape(radius, height);
                            break;
                    }
                    
                    po = new(_engine, eRoot, shape, v3Pos, Quaternion.Identity)
                    {
                        CollisionProperties = collisionProperties
                    };
                    
                    if ((mcp.Params.GeomFlags & InstantiateModelParams.PHYSICS_OWN_CALLBACKS) != 0)
                    {
                        po.AddContactListener();
                    }
                    
                    prefSphere = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
                    prefSphere.Awake = false;
                    
                    eRoot.Set(new engine.physics.components.Body(po, prefSphere));
                }
            }
        }
    }


    /**
     * Build the model's physics into the actual model's root.
     */
    public void BuildPerInstance(in DefaultEcs.Entity eRoot, Model model, ModelCacheParams mcp)
    {
        if (model.RootNode == null || model.RootNode.InstanceDesc == null)
        {
            return;
        }

        ModelBuilder modelBuilder = new(_engine, model, mcp.Params);

        /*
         * Create the geometry into eTarget.
         */
        // TXWTODO: Not only build geometry, but also physics and sound.
        modelBuilder.BuildEntity(eRoot);
        
        BuildPerInstancePhysics(eRoot, modelBuilder, model, mcp);
    }
}
