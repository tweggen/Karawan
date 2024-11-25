
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using static engine.Logger;
using System.Threading.Tasks;
using engine.meta;

namespace engine.world;

public class MList<T>
{
    private readonly object _lo;
    private List<T> _list = new();

    public void Add(T o)
    {
        lock (_lo)
        {
            _list.Add(o);
        }
    }


    public void Remove(T o)
    {
        lock (_lo)
        {
            _list.Remove(o);
        }
    }
    
    
    public IImmutableList<T> List()
    {
        lock (_lo)
        {
            return _list.ToImmutableList();
        }
    }

    public MList(object lo)
    {
        _lo = lo;
    }
}


public class MetaGen
{
    public static float FragmentSize = 400f;
    public static Vector3 FragmentSize3 = new(FragmentSize, FragmentSize, FragmentSize);
    public static Vector2 FragmentSize2 = new(FragmentSize, FragmentSize);
    public static float MaxWidth = 90000f;
    public static float MaxHeight = 90000f;
    public static Vector2 MaxSize = new(MaxWidth, MaxHeight);

    public static readonly geom.AABB AABB = new(
        new Vector3(-MaxWidth, -10000, -MaxHeight),
        new Vector3(MaxWidth, 10000, MaxHeight));

    public static float ClusterNavigationHeight = 3f;
    public static float VoidNavigationHeight = 3.5f;

    public Vector3 MaxPos;
    public Vector3 MinPos;

    /**
     * FragmentSize / 20. We should compute this
     */
    public static int GroundResolution = 20;

    public static bool TRACE_WORLD_LOADER = false;
    public static bool TRACE_FRAGMENT_OPEARTORS = false;
    public static bool TRACE_CLUSTER_OPEARTORS = false;

    public static float CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE = 2.0f;

    private Loader _loader = null;

    public Loader Loader
    {
        get => _loader;
    }

    /**
     * This is our seed. It also will be used for other sub-parts of
     * the world-
     */
    private string _myKey;

    private object _lo = new();

    public MList<engine.world.IWorldOperator> WorldBuildingOperators { get; }
    public MList<engine.world.IWorldOperator> WorldPopulatingOperators { get; }
    private readonly List<Func<string, ClusterDesc, world.IFragmentOperator>> _clusterFragmentOperatorFactoryList = new();
    public MList<world.IClusterOperator> ClusterOperators;
    
    private meta.ExecDesc _edRoot;
    private meta.AExecNode _enRoot;

    public meta.ExecDesc EdRoot
    {
        get => _getEdRoot();
        set => _setEdRoot(value);   
    }


    private meta.ExecDesc _getEdRoot()
    {
        lock (_lo)
        {
            return _edRoot;
        }
    }


    private AExecNode _getEnRoot()
    {
        ExecDesc edRoot;
        lock (_lo)
        {
            if (_enRoot != null)
            {
                return _enRoot;
            }

            edRoot = _edRoot;
        }
        /*
         * Yes, this is a race condition.
         */
        AExecNode enRoot = null;
        if (edRoot != null)
        {
            try
            {
                enRoot = engine.meta.ExecNodeFactory.CreateExecNode(
                    edRoot,
                    new ExecScope(
                        new Dictionary<string, object>()
                        {
                            { "strKey", _myKey }
                        },
                        new Dictionary<string, Func<IEnumerable<object>>>()
                        {
                            { "clusterDescList", engine.world.ClusterList.Instance().GetClusterList }
                        }
                    )
                );
                lock (_lo)
                {
                    _enRoot = enRoot;
                }
            }
            catch (Exception e)
            {
                Error($"Exception compiling fragment operator tree: {e}.");
                enRoot = null;
            }
        }
        return enRoot;
    }
    

    private void _setEdRoot(meta.ExecDesc edRoot)
    {
        Trace("Setting new execdescription.");

        lock (_lo)
        {
            _enRoot = null;
            _edRoot = edRoot;
        }
    }


    static public void GetFragmentRect(int i, int k, out engine.geom.Rect2 rect2)
    {
        float fs = FragmentSize;
        rect2 = new()
        {
            A = new((float)((i * fs) - fs / 2.0), (float)((k * fs) - fs / 2.0)),
            B = new((float)(((i + 1) * fs) - fs / 2.0), (float)(((k + 1) * fs) - fs / 2.0))
        };
    }

    #if false
    public void GenerateFragmentOperatorsForCluster(string key, ClusterDesc cluster)
    {
        lock (_lo)
        {
            foreach (var clusterFragmentOperatorFactory in _clusterFragmentOperatorFactoryList)
            {
                IFragmentOperator op;
                try
                {
                    op = clusterFragmentOperatorFactory(key, cluster);
                }
                catch (Exception e)
                {
                    Error($"Exception while instantiating and adding cluster fragment operator: {e}.");
                }
            }
        }
    }
    #endif


    public void ApplyClusterOperators(ClusterDesc clusterDesc)
    {
        if (null == clusterDesc)
        {
            throw new System.ArgumentException($"WorldMetaGen.applyClusterOperators(): clusterDesc is null.");
        }

        if (TRACE_CLUSTER_OPEARTORS) Trace($"WorldMetaGen: Calling cluster operators for {clusterDesc.Name}...");

        var e = I.Get<Engine>();

        foreach (var opCluster in ClusterOperators.List())
        {
            try
            {
                opCluster.ClusterOperatorApply(clusterDesc);
            }
            catch (Exception x)
            {
                Error($"Caught exception while applying cluster operator for cluster {clusterDesc.Name}: {x}");
            }
        }
    }
    

    /**
     * Some major work is lifted here, so there are some basic optimizations.
     * So we perform an (inefficient) clipping, which operator shall be scheduled
     * at all. And if there are too much, we offload the task creation to a task
     * of its own.
     */
    public void ApplyFragmentOperators(world.Fragment fragment, engine.world.FragmentVisibility visib)
    {
        if (null == fragment)
        {
            throw new System.ArgumentException($"WorldMetaGen.applyFragmentOperators(): fragment is null.");
        }

        if (TRACE_FRAGMENT_OPEARTORS) Trace($"WorldMetaGen: Calling fragment operators for {fragment.GetId()}...");
        
        var e = fragment.Engine;


        /*
         * Get and possibly compile the fragment operator tree.
         */
        AExecNode enRoot = _getEnRoot();

        if (null != enRoot)
        {
            e.Run(() => {
                Task tApplyFragmentOperators = enRoot.Execute((object instance) =>
                {
                    var op = instance as IFragmentOperator;
                    if (null != op)
                    {
                        op.FragmentOperatorGetAABB(out var aabb);
                        if (!aabb.IntersectsXZ(fragment.AABB))
                        {
                            return null;
                        }

                        if (TRACE_FRAGMENT_OPEARTORS) Trace($"Running Fragment Operator \"{op.FragmentOperatorGetPath()}\".");

                        return e.Run(op.FragmentOperatorApply(fragment, visib));
                    }
                    else
                    {
                        ErrorThrow("Invalid operator instance specified.", m => new InvalidOperationException(m));
                        return null;
                    }
                });
                fragment.Visibility.How |= visib.How;
            });

        }
    }



    /**
     * Execute all world operators for this metagen.
     * This can be terrain generatation, cluster generation etc. .
     */
    private void _applyWorldOperators(MList<IWorldOperator> operators)
    {
        Trace("WorldMetaGen: Calling operators...");
        Task applyTask = new Task(async () =>
        {
            foreach (var o in operators.List())
            {
                try
                {
                    var oppath = o.WorldOperatorGetPath();
                    Trace($"WorldMetaGen.applyWorldOperators(): Applying operator '{oppath}'...");
                    // var t0 = Sys.time();
                    await o.WorldOperatorApply()();
                    // var dt = Sys.time() - t0;
                    // trace( 'WorldMetaGen.applyWorldOperators(): Applying operator "$oppath" took $dt.');
                }
                catch (Exception e)
                {
                    Warning($"WorldMetaGen.applyWorldOperators(): Unknown exception applying world operator: {e}");
                }
            }
        });
        applyTask.RunSynchronously();

        Trace("WorldMetaGen: Done calling world operators.");
    }



    /**
     * Call this after you added all of the modules.
     * This invokes all the world-building operators. Calling this might be
     * required to create vital infrastructure.
     * They may not call the world loader.-
     */
    public void SetupComplete()
    {
        _applyWorldOperators(WorldBuildingOperators);
    }


    /**
     * Call this after the world has been globally initialised.
     * It initializes global, non-per-fragment characters and objects.
     * They already may use the world loader.
     */
    public void Populate()
    {
        _applyWorldOperators(WorldPopulatingOperators);
    }
    
    
    public MetaGen()
    {
        _myKey = "mydear";

        WorldBuildingOperators = new(_lo);
        WorldPopulatingOperators = new(_lo);
        ClusterOperators = new(_lo);
        
        MaxPos = new Vector3(MaxWidth / 2f - 1f, 0f, MaxHeight / 2f - 1f);
        MinPos = new Vector3(-MaxWidth / 2f + 1f, 0f, -MaxWidth / 2f + 1f);

        WorldBuildingOperators.Add(new engine.world.GenerateClustersOperator(_myKey));
    }


    public void SetLoader(in world.Loader loader)
    {
        _loader = loader;
    }
}

