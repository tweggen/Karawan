
using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.world
{
    public class MetaGen
    {
        private void trace(string message)
        {
            Console.WriteLine(message);
        }
        private static readonly object _instanceLock = new object();
        private static MetaGen _instance;

        public static float FragmentSize = 400f;
        public static float MaxWidth = 30000f;
        public static float MaxHeight = 30000f;
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

        public static float  CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE = 2.0f;

        /**
         * This is our seed. It also will be used for other sub-parts of
         * the world-
         */
        private string _myKey;

        private List<engine.world.IWorldOperator> _worldOperators;
        private SortedDictionary<string, engine.world.IFragmentOperator> _fragmentOperators;
        private List<Func<string, ClusterDesc, world.IFragmentOperator>> _clusterFragmentOperatorFactoryList;

        private bool _traceFragmentOperators = true;

        public List<Func<string,ClusterDesc,world.IFragmentOperator>> 
            GetClusterFragmentOperatorFactoryList()
        {
            return _clusterFragmentOperatorFactoryList;
        }



        /**
         * For every cluster, this fragment operator generator shall be added.
         * @param fragmentOperatorFactory
         *     A factory function that will generate the actual fragment operator. It will receive
         *     the base key for that cluster/generator instance and the actual clusterdesc.
         *     TXWTODO: Only use the key in the clusterdesc?
         */
        public void AddClusterFragmentOperatorFactory(
                Func<String, ClusterDesc, world.IFragmentOperator> fragmentOperatorFactory
        )
        {
            _clusterFragmentOperatorFactoryList.Add(fragmentOperatorFactory);
        }



        public void AddFragmentOperator(world.IFragmentOperator op)
        {
            _fragmentOperators.Add(op.FragmentOperatorGetPath(), op);
        }

        public void ApplyFragmentOperators(world.Fragment fragment)
        {
            if( null==fragment ) {
                throw new ArgumentException( $"WorldMetaGen.applyFragmentOperators(): fragment is null." );
            }
            if (_traceFragmentOperators) trace($"WorldMetaGen: Calling fragment operators for {fragment.GetId()}...");
            foreach( KeyValuePair<string, IFragmentOperator> kvp in _fragmentOperators ) {
                try
                {
                    var t0 = DateTime.Now.Ticks;
                    kvp.Value.FragmentOperatorApply(fragment);
                    var dt = DateTime.Now.Ticks - t0;
                    if (dt > 0.001)
                    {
                        var oppath = kvp.Value.FragmentOperatorGetPath();
                        if (_traceFragmentOperators) trace($"WorldMetaGen.applyFragmentOperators(): Applying operator '{oppath}' took {dt}.");
                    }
                }
                catch (Exception e) {
                    trace($"WorldMetaGen.applyFragmentOperators(): Unknown exception applying fragment operator '{kvp.Value.FragmentOperatorGetPath()}': {e}')");
                }
            }
            if (_traceFragmentOperators) trace($"WorldMetaGen: Done calling fragment operators for {fragment.GetId()}...");
        }


        /**
         * Execute all world operators for this metagen.
         * This can be terrain generatation, cluster generation etc. .
         */
        private void _applyWorldOperators()
        {
            trace("WorldMetaGen: Calling world operators...");
            foreach(var o in _worldOperators) {
                try {
                    var oppath = o.WorldOperatorGetPath();
                    trace( $"WorldMetaGen.applyWorldOperators(): Applying operator '{oppath}'...");
                    // var t0 = Sys.time();
                    o.WorldOperatorApply(this);
                    // var dt = Sys.time() - t0;
                    // trace( 'WorldMetaGen.applyWorldOperators(): Applying operator "$oppath" took $dt.');
                } catch(Exception e) {
                    trace($"WorldMetaGen.applyWorldOperators(): Unknown exception applying world operator: {e}");
                }
            }
            trace("WorldMetaGen: Done calling world operators.");
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



        private MetaGen()
        {
            _myKey = "mydear";

            MaxPos = new Vector3(MaxWidth / 2f - 1f, 0f, MaxHeight / 2f - 1f);
            MinPos = new Vector3(-MaxWidth / 2f + 1f, 0f, -MaxWidth / 2f + 1f);

            _worldOperators = new();
            _fragmentOperators = new();
            _clusterFragmentOperatorFactoryList= new();

            _worldOperators.Add(new world.GenerateClustersOperator(_myKey));

            /*
             * Create a fragment operator that reads the elevations after 
             * the elevation pipeline.
             */
            AddFragmentOperator(new world.CreateTerrainOperator(_myKey));

            /*
             * Create a fragment operator that creates a ground mesh.
             */
            AddFragmentOperator(new world.CreateTerrainMeshOperator(_myKey));

            AddClusterFragmentOperatorFactory(
                (string newKey, ClusterDesc clusterDesc)=>
                    new engine.streets.GenerateClusterStreetsOperator(clusterDesc, newKey)
            );
            AddClusterFragmentOperatorFactory(
                (string newKey, ClusterDesc clusterDesc) =>
                    new engine.streets.GenerateClusterQuartersOperator(clusterDesc, newKey)
            );
        }

        public static MetaGen Instance()
        {
            lock(_instanceLock)
            {
                if( null == _instance )
                {
                    _instance = new MetaGen();
                    _instance.SetupComplete();
                }
                return _instance;
            }
        }

    }
}
