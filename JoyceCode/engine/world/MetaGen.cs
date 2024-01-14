
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
    public static Vector3 FragmentSize3 = new(FragmentSize, FragmentSize, FragmentSize);
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

    public static bool TRACE_CHARACTER_MIGRATION = false;
    public static bool TRACE_WORLD_LOADER = false;
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

    private List<engine.world.IWorldOperator> _worldBuildingOperators = new();
    private List<engine.world.IWorldOperator> _worldPopulatingOperators = new();
    private SortedDictionary<string, engine.world.IFragmentOperator> _fragmentOperators = new();
    private List<Func<string, ClusterDesc, world.IFragmentOperator>> _clusterFragmentOperatorFactoryList = new();

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


        /*
         * Get and possibly compile the fragment operator tree.
         */
        AExecNode enRoot = _getEnRoot();

        if (null != enRoot)
        {
            Task.Run(() => {
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
            });

        }
    }



    /**
     * Execute all world operators for this metagen.
     * This can be terrain generatation, cluster generation etc. .
     */
    private void _applyOperators(IList<IWorldOperator> operators)
    {
        Trace("WorldMetaGen: Calling operators...");
        Task applyTask = new Task(async () =>
        {
            foreach (var o in operators)
            {
                try
                {
                    var oppath = o.WorldOperatorGetPath();
                    Trace($"WorldMetaGen.applyWorldOperators(): Applying operator '{oppath}'...");
                    // var t0 = Sys.time();
                    await o.WorldOperatorApply(this)();
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
        _applyOperators(_worldBuildingOperators);
    }


    /**
     * Call this after the world has been globally initialised.
     * It initializes global, non-per-fragment characters and objects.
     * They already may use the world loader.
     */
    public void Populate()
    {
        _applyOperators(_worldPopulatingOperators);
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

        _unitExecDesc();

        _worldBuildingOperators.Add(new engine.world.GenerateClustersOperator(_myKey));
    }


    public void WorldBuildingOperatorAdd(IWorldOperator worldOperator)
    {
        lock (_lo)
        {
            _worldBuildingOperators.Add(worldOperator);
        }
    }


    public void WorldPopulatingOperatorAdd(IWorldOperator worldOperator)
    {
        lock (_lo)
        {
            _worldPopulatingOperators.Add(worldOperator);
        }
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

