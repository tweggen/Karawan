
using System;
using System.Collections.Generic;


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
        public static bool TRACE_PLATFORM_MOLECULE_ADDING = false;


        private List<engine.world.IWorldOperator> _worldOperators;
        private SortedDictionary<string, engine.world.IFragmentOperator> _fragmentOperators;
        private List<Func<string, ClusterDesc, world.IFragmentOperator>> _clusterFragmentOperatorFactoryList;

        private bool _traceFragmentOperators;

        public void ApplyFragmentOperators(
            in Fragment fragment)
        {
#if false
            if( null==fragment ) {
            trace( 'WorldMetaGen.applyFragmentOperators(): fragment is null.' );
            return;
        }
        if( null==allEnv ) {
            trace( 'WorldMetaGen.applyFragmentOperators(): allEnv == null.' );
            return;
        }
if (_traceFragmentOperators) trace('WorldMetaGen: Calling fragment operators for ${fragment.getId()}...');
_fragmentOperatorTree.apply(function(fo) {
    try
    {
        if (null == fo)
        {
            trace('WorldMetaGen.applyFragmentOperators(): fo is null.');
            return;
        }
        var t0 = Sys.time();
        fo.fragmentOperatorApply(allEnv, fragment);
        var dt = Sys.time() - t0;
        if (dt > 0.001)
        {
            var oppath = fo.fragmentOperatorGetPath();
            if (_traceFragmentOperators) trace('WorldMetaGen.applyFragmentOperators(): Applying operator "$oppath" took $dt.');
        }
    }
    catch (unknown: Dynamic ) {
        trace('WorldMetaGen.applyFragmentOperators(): Unknown exception applying fragment operator "${fo.fragmentOperatorGetPath()}": '
            + Std.string(unknown) + "\n"
            + haxe.CallStack.toString(haxe.CallStack.callStack()));
    }
    });
    if (_traceFragmentOperators) trace('WorldMetaGen: Done calling fragment operators for ${fragment.getId()}...');
#endif
        }


        /**
         * For every cluster, this fragment operator generator shall be added.
         * @param fragmentOperatorFactory
         *     A factory function that will generate the actual fragment operator. It will receive
         *     the base key for that cluster/generator instance and the actual clusterdesc.
         *     TXWTODO: Only use the key in the clusterdesc?
         */
        public void metaGenAddClusterFragmentOperatorFactory(
            Func<String, ClusterDesc, world.IFragmentOperator> fragmentOperatorFactory
        )
        {
            _clusterFragmentOperatorFactoryList.Add(fragmentOperatorFactory);
        }



        public void MetaGenAddFragmentOperator(world.IFragmentOperator op)
        {
            _fragmentOperators.Add(op.FragmentOperatorGetPath(), op);
        }

        public void applyFragmentOperators(world.Fragment fragment)
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
        public void MetaGenSetupComplete()
        {
            /*
             * One time operations: Apply all world operators.
             */
            _applyWorldOperators();
        }



        private MetaGen()
        {
            _worldOperators = new();
        }

        public static MetaGen Instance()
        {
            lock(_instanceLock)
            {
                if( null == _instance )
                {
                    _instance = new MetaGen();
                }
                return _instance;
            }
        }

    }
}
