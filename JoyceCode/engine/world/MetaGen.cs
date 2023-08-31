
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using static engine.Logger;
using System.Threading.Tasks;
using engine.meta;
using Trace = System.Diagnostics.Trace;

namespace engine.world;

public class MetaGen
{
    private static readonly object _classLock = new object();
    private static MetaGen _instance;

    public static float FragmentSize = 400f;
    public static float MaxWidth = 90000f;
    public static float MaxHeight = 90000f;

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

    public static bool TRACE_CHARACTER_MIGRATION = false;
    public static bool TRACE_WORLD_LOADER = true;
    public static bool TRACE_LOAD_AUDIO_BUFFER = false;
    public static bool TRACE_LOAD_BITMAP = false;
    public static bool TRACE_LOAD_FONT = false;
    public static bool TRACE_LOAD_BYTES = false;
    public static bool TRACE_PLATFORM_MOLECULE_ADDING = true;
    public static bool TRACE_FRAGMENT_OPEARTORS = false;

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

    private List<engine.world.IWorldOperator> _worldOperators;
    private SortedDictionary<string, engine.world.IFragmentOperator> _fragmentOperators;
    private List<Func<string, ClusterDesc, world.IFragmentOperator>> _clusterFragmentOperatorFactoryList;

    private meta.ExecDesc _edRoot;

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


    private void _setEdRoot(meta.ExecDesc edRoot)
    {
        lock (_lo)
        {
            _edRoot = edRoot;
        }
    }


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
                    _fragmentOperators.Add(op.FragmentOperatorGetPath(), op);
                }
                catch (Exception e)
                {
                    Error($"Exception while instantiating and adding cluster fragment operator: {e}.");
                }
            }
        }
    }


    //private List<Task> _listRunningFragmentOperators = new();
    

    /**
     * Some major work is lifted here, so there are some basic optimizations.
     * So we perform an (inefficient) clipping, which operator shall be scheduled
     * at all. And if there are too much, we offload the task creation to a task
     * of its own.
     */
    public void ApplyFragmentOperators(world.Fragment fragment)
    {
        if (null == fragment)
        {
            throw new ArgumentException($"WorldMetaGen.applyFragmentOperators(): fragment is null.");
        }

        if (TRACE_FRAGMENT_OPEARTORS) Trace($"WorldMetaGen: Calling fragment operators for {fragment.GetId()}...");


        ExecDesc edRoot;
        lock (_lo)
        {
            edRoot = _edRoot;
        }

        if (null != edRoot)
        {
            // TXWTODO: Do we really need to compile this every time?
            var enRoot = engine.meta.ExecNodeFactory.CreateExecNode(
                edRoot,
                new ExecScope(
                    new Dictionary<string, object>()
                    {
                        { "strKey", _myKey }
                    },
                    new Dictionary<string, IEnumerable<object>>()
                    {
                        { "clusterDescList", engine.world.ClusterList.Instance().GetClusterList() }
                    }
                )
            );

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

                    // Trace($"Running Fragment Operator \"{op.FragmentOperatorGetPath()}\".");

                    return Task.Run(op.FragmentOperatorApply(fragment));
                }
                else
                {
                    ErrorThrow("Invalid operator instance specified.", m => new InvalidOperationException(m));
                    return null;
                }
            });

#if false
            lock (_lo)
            {
                _listRunningFragmentOperators.Add(tApplyFragmentOperators);
            }
#endif
        }
    }


#if false
    public bool IsLoading()
    {
        bool isLoading = false;
        lock (_lo)
        {
            _listRunningFragmentOperators.RemoveAll(t =>
            {
                if (!t.IsCompleted)
                {
                    isLoading = true;
                    return false;
                }
                else
                {
                    return true;
                }
            });
        }

        return false;
    }
#endif


    /**
     * Execute all world operators for this metagen.
     * This can be terrain generatation, cluster generation etc. .
     */
    private void _applyWorldOperators()
    {
        Trace("WorldMetaGen: Calling world operators...");
        foreach (var o in _worldOperators)
        {
            try
            {
                var oppath = o.WorldOperatorGetPath();
                Trace($"WorldMetaGen.applyWorldOperators(): Applying operator '{oppath}'...");
                // var t0 = Sys.time();
                o.WorldOperatorApply(this);
                // var dt = Sys.time() - t0;
                // trace( 'WorldMetaGen.applyWorldOperators(): Applying operator "$oppath" took $dt.');
            }
            catch (Exception e)
            {
                Warning($"WorldMetaGen.applyWorldOperators(): Unknown exception applying world operator: {e}");
            }
        }
        Trace("WorldMetaGen: Done calling world operators.");
    }



    /**
     * Call this after you added all of the modules.
     */
    public void SetupComplete()
    {
        /*
         * One time operations: Apply all world operators.
         */
        _applyWorldOperators();
    }


    private void _unitExecDesc()
    {
#if false
        meta.ExecDesc ed1 = new()
        {
            Mode = ExecDesc.ExecMode.Sequence,
            Children = new()
            {
                new ExecDesc()
                {
                    Mode = ExecDesc.ExecMode.Task,
                    Implementation = "nogame.test.prerequisites"
                },
                new()
                {
                    Mode = ExecDesc.ExecMode.Parallel,
                    Children = new()
                    {
                        new()
                        {
                            Mode = ExecDesc.ExecMode.Task,
                            Implementation = "nogame.test.candy1"
                        },
                        new()
                        {
                            Mode = ExecDesc.ExecMode.Task,
                            Implementation = "nogame.test.camdy2"
                        },
                    }
                }
            }
        };

        JsonSerializerOptions options = new()
        {
            ReferenceHandler = ReferenceHandler.Preserve,
            WriteIndented = true
        };
        string jsonEd1 = JsonSerializer.Serialize(ed1, options);
        Trace("Serializer output:");
        Trace(jsonEd1);

        meta.ExecDesc? ed2 = JsonSerializer.Deserialize<meta.ExecDesc>(jsonEd1, options);
        Trace(ed2.ToString());
#endif
    }


    private MetaGen()
    {
        _myKey = "mydear";

        MaxPos = new Vector3(MaxWidth / 2f - 1f, 0f, MaxHeight / 2f - 1f);
        MinPos = new Vector3(-MaxWidth / 2f + 1f, 0f, -MaxWidth / 2f + 1f);

        _worldOperators = new();
        _fragmentOperators = new();
        _clusterFragmentOperatorFactoryList = new();

        _unitExecDesc();

        _worldOperators.Add(new engine.world.GenerateClustersOperator(_myKey));

#if false
            if (engine.GlobalSettings.Get("world.CreateStreetAnnotations") != "false")
            {
                AddClusterFragmentOperatorFactory(
                    (string newKey, ClusterDesc clusterDesc) =>
                        new engine.streets.GenerateClusterStreetAnnotationsOperator(clusterDesc, newKey)
                );
            } 
#endif
    }


    public void SetLoader(in world.Loader loader)
    {
        _loader = loader;
    }


    public static MetaGen Instance()
    {
        lock (_classLock)
        {
            if (null == _instance)
            {
                _instance = new MetaGen();
            }

            return _instance;
        }
    }

}

