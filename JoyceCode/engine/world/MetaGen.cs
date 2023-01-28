using Java.Util.Functions;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.world
{
    public class MetaGen
    {
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


        private MetaGen()
        {

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
