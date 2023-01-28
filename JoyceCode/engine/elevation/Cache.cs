using engine.elevation;
using System;
using System.Threading;
using System.Collections.Generic;

namespace engine.elevation
{
    internal class Cache
    {
        private void trace(string message)
        {
            Console.WriteLine(message);
        }

        public static string TOP_LAYER = "TOP_LAYER";

        public static string LAYER_BASE = "/000000";

        private Mutex _mutexMap;
        private string _maxLayer;

        private Dictionary<string, CacheEntry> _mapEntries;
        private Dictionary<string, FactoryEntry> _mapFactories;
        private List<string> _keysFactories;

        private bool _traceCache = false;

        private void _insertElevationFactoryEntry(
            in string id,
            in FactoryEntry elevationFactoryEntry
        )
        {
            if(_traceCache ) trace($"engine.elevation.Cache: Now inserted {id}.");
            _mapFactories.Add(id, elevationFactoryEntry);
            _keysFactories.Add(id);
            _keysFactories.Sort();
            // var f = _keysFactories[0];
            // trace('elevation.Cache: f = $f');
        }


        private string _createFactoryId(in string layer)
        {
            return "elevation-factory-" + layer;
        }


        private string _createEntryId(int x, int y, in string layer)
        {
            string id = "elevation-entry-" + layer + "-" + x + "-" + y;
            return id;
        }


        /**
         * Return the factory below the given layer
         */
        private FactoryEntry getNextFactoryEntryBelow(
            float x0, float z0,
            float x1, float z1,
            in string layer )
        {

            /*
             * We need the factory id for the map to find the starting point.
             */
            var factoryId = _createFactoryId(layer);

            /*
             * Now let's find the starting point. From the starting layer we
             * iterate down until we find a layer that intersets with our region,
             */
            FactoryEntry elevationFactoryEntry = null;
            string resultLayer;

            _mutexMap.WaitOne();
            int idx = _keysFactories.Count - 1;

            if (factoryId == TOP_LAYER)
            {
                if (idx >= 0)
                {
                    string candString = _keysFactories[idx];
                    elevationFactoryEntry = _mapFactories[candString];
                    resultLayer = _mapFactories[candString].Layer;
                }
            }
            else
            {
                while (idx >= 0)
                {
                    string candString = _keysFactories[idx];
                    if (String.Compare(candString, factoryId) < 0)
                    {
                        elevationFactoryEntry = _mapFactories[candString];
                        resultLayer = _mapFactories[candString].Layer;
                        break;
                    }
                    --idx;
                }
            }
            if (null == elevationFactoryEntry)
            {
                _mutexMap.ReleaseMutex();
                trace("engine.elevation.Cache: returning null (1).");
                return null;
            }

            /*
             * Now idx points at the candidate layer. Iterate down until we find one that
             * intersects. Basically, the result cannot be null at this point anymore,
             * because we always have a base layer.
             */
            while (idx >= 0)
            {
                // TXWTODO: Not nice. Double code. but consistent in the loop.
                string candString = _keysFactories[idx];
                elevationFactoryEntry = _mapFactories[candString];
                if (elevationFactoryEntry.ElevationOperator.ElevationOperatorIntersects(
                    x0, z0, x1, z1
                ))
                {
                    /*
                     * We found a layer that matters.
                     */
                    break;
                }
                --idx;
            }
            /*
             * Sanity check for bugs.
             */
            if (null == elevationFactoryEntry)
            {
                _mutexMap.ReleaseMutex();
                trace("engine.elevation.Cache: returning null (2).");
                return null;
            }

            _mutexMap.ReleaseMutex();
            return elevationFactoryEntry;
        }


        /**
         * Register an elevation operator for the given layer.
         *
         * An elevation operator implements a method that is able to compute
         * a given fragment of elevation on the fly. This operation will be 
         * based on elevation data on the layers "below". Therefore the elevation
         * operator receives an interface function to read data from the layers
         * below.
         */
        public void ElevationCacheRegisterElevationOperator(
            string layer,
            in IOperator elevationOperator
            )
        {
            var id = _createFactoryId(layer);
            var elevationFactoryEntry = new FactoryEntry(
                layer, elevationOperator
            );
            _mutexMap.WaitOne();
            if (String.Compare(layer, _maxLayer)>0)
            {
                _maxLayer = layer;
            }
            _insertElevationFactoryEntry(id, elevationFactoryEntry);
            _mutexMap.ReleaseMutex();
        }


        /**
         * Read elevation data from the cache. 
         * This function returns one elevation tile of the world.
         * In many cases, users will want to use a convenience function that
         * allows reading of the elevations based on coordinates
         *
         * This function returns a reference to the requested fragment.
         * The fragment either is returned from the cache or is created new
         * and (re-)computed.
         *
         * For computation, the first layer "under" the requested layer 
         * intersecting with this fragment is asked for the given elevation fragment.
         * Every layer has a separate cache to optimize performance.
         *
         * Generating a fragment is recursive. Every layer will probably ask
         * for base elevation data to modify. That way, the request will bubble 
         * down to the base layer, elevation operators will apply modifications
         * to the data from the ground to the top.
         */
        private CacheEntry ElevationCacheGetAt(
            int i, int k, 
            string layer
        )
        {

            // trace('elevation.Cache: Entry requested for layer $layer');

            var elevationAdapter = new LayerAdapter(this, layer);

            if (TOP_LAYER == layer)
            {
                layer = _maxLayer;
            }

            /*
             * If we have the entry for that layer, just return it.
             */
            var id = _createEntryId(i, k, layer);
            _mutexMap.WaitOne();
            if (_mapEntries.ContainsKey(id))
            {
                var entry = _mapEntries[id];
                _mutexMap.ReleaseMutex();
                //trace('elevation.Cache: Cache hit for $i, $k.');
                return entry;
            }
            if (_traceCache) trace($"engine.elevation.Cache: Cache MISS for {i}, {k}.");

            /*
             * We do not have the entry. So look up the factory function and
             * create it.
             */
            var factoryId = _createFactoryId(layer);
            FactoryEntry elevationFactoryEntry = null;
            if (!_mapFactories.ContainsKey(factoryId))
            {
                _mutexMap.ReleaseMutex();
                throw new InvalidOperationException(
                    $"elevation.Cache: No factory registered for layer {layer}");
            }
            elevationFactoryEntry = _mapFactories[factoryId];
            _mutexMap.ReleaseMutex();

            // TXWTODO: This is risky, we open up the mutex, depending on 
            // no cyclic dependencies.
            CacheEntry newEntry = null;
            if (null != elevationFactoryEntry.ElevationOperator)
            {
                /*
                 * We found an elevation operator.
                 */
                var gr = world.MetaGen.GroundResolution;
                var fs = world.MetaGen.FragmentSize;
                var elevationRect = new Rect(gr + 1, gr + 1);

                elevationRect.X0 = (float)( (i * fs) - fs / 2.0 );
                elevationRect.Z0 = (float)( (k * fs) - fs / 2.0 );
                elevationRect.X1 = (float)( ((i + 1) * fs) - fs / 2.0 );
                elevationRect.Z1 = (float)( ((k + 1) * fs) - fs / 2.0 );
                try
                {
                    /*
                     * Only apply the elevation operator if it really intersects
                     * with our frafment.
                     */
                    if (true
                    /* || elevationFactoryEntry.elevationOperator.elevationOperatorIntersects( 
                    elevationRect.x0,
                    elevationRect.z0,
                    elevationRect.x1,
                    elevationRect.z1
                ) */
                    )
                    {
                        elevationFactoryEntry.ElevationOperator.ElevationOperatorProcess(
                            elevationAdapter, elevationRect
                        );
                    }
                }
                catch (Exception e) {
                    /*
                     * In case there was any exception, just null out the elevationrect.
                     * TODO: or pass down?
                     *
                     * ... it already is nulled out.
                     */
                    trace($"elevation.Cache: Warning: Exception in operator: {e}");
                }

                newEntry = new CacheEntry();

                /*
                 * Simply use the elevations from the rect. Nobody will use
                 * the rect after this function.
                 */
                newEntry.elevations = elevationRect.elevations;

            } else
            {
                /*
                    * No factory found. This is not meant to happen.
                    */
                throw new InvalidOperationException(
                    $"elevation.Cache: No factory found for layer {layer} at {i}, {k}.");
            }
            if (newEntry != null)
            {
                _mutexMap.WaitOne();
                _mapEntries[id] = newEntry;
                _mutexMap.ReleaseMutex();
            }
            else
            {
                throw new InvalidOperationException(
                    $"elevation.Cache: Unable to create entry for layer $layer i: {i}, k: {k}");
            }

            /*
             * We have the entry, and we entered it into the factory, now return.
             */
            return newEntry;
        }


        public function elevationCacheGetBelow(
            i: Int, k: Int, 
            layer: String
        ): CacheEntry {

            // TXWTODO: Double code, also in CacheGetAt.
            var fs = WorldMetaGen.fragmentSize;
            var x0 = (i * fs) - fs / 2.0;
            var z0 = (k * fs) - fs / 2.0;
            var x1 = ((i + 1) * fs) - fs / 2.0;
            var z1 = ((k + 1) * fs) - fs / 2.0;

            var entry: FactoryEntry = getNextFactoryEntryBelow(
                x0, z0, x1, z1, layer);
            if (null == entry)
            {
                throw 'elevation.Cache: No entry found below "$layer".';
            }
            return elevationCacheGetAt(i, k, entry.layer);
        }


        /**
         * Return an elevation rect describing all elevations within the given boundaries.
         */
        private function elevationCacheGetRectAt(
            x0: Float, z0: Float,
            x1: Float, z1: Float,
            layer: String
        ) : Rect {

            // trace('ElecationCache: Rect requested for layer $layer');

            /* 
             * Sort the arguments. I feel like this is a bit too defensive.
             */
            if (x1 < x0)
            {
                var t = x0;
                x0 = x1;
                x1 = t;
            }
            if (z1 < z0)
            {
                var t = z0;
                z0 = z1;
                z1 = t;
            }

            var elevationRect: Rect = null;

            var fs = WorldMetaGen.fragmentSize;
            var gr = WorldMetaGen.groundResolution;

            /*
             * The resulting step size, for either dimenstion between two points.
             */
            var ess = fs / gr;

            /*
             * First compute the first and the last fragment, as well as the indices within 
             * the fragment.
             *
             * - i, k: Index of the elevation cache entry we are working on.
             *
             * - xLocal, zLocal: top coordinate of the elevation cache entry
             *   we are working on.
             *
             * - ex, ez: Index of the elevation point globally.
             */

            var i0: Int = Math.floor((x0 + fs / 2.) / fs);
            var k0: Int = Math.floor((z0 + fs / 2.) / fs);
            // var x0Local: Float = x0 - fs*i0;
            // var z0Local: Float = z0 - fs*k0;
            var ex0: Int = Std.int((x0 + fs / 2.0) / ess);
            var ez0: Int = Std.int((z0 + fs / 2.0) / ess);

            var i1: Int = Math.floor((x1 + fs / 2.) / fs);
            var k1: Int = Math.floor((z1 + fs / 2.) / fs);
            // var x1Local: Float = x1 - fs*i1;
            // var z1Local: Float = z1 - fs*k1;
            var ex1: Int = Std.int((x1 + fs / 2.0) / ess);
            var ez1: Int = Std.int((z1 + fs / 2.0) / ess);

            /*
             * Create a new elevation rect containing the indices as
             * bounded by ex[0..1] and ez[0..1]
             */
            var nHoriz = ex1 - ex0;
            var nVert = ez1 - ez0;

            /* 
             * Create the target rectangle. Note: We need one more per dimenstion.
             */
            elevationRect = new Rect(nHoriz + 1, nVert + 1);
            elevationRect.x0 = x0;
            elevationRect.z0 = z0;
            elevationRect.x1 = x1;
            elevationRect.z1 = z1;

            /* 
             * At this point, ex0,ez0 - ex1,ez1 contain the indices of the
             * source cache entries.
             */
            for (k in k0... (k1 + 1))
            {

                /*
                 * Let ezLocal contain the limits of the global 
                 * elevation indices by the local tile.
                 */
                var ezLocal0: Int = k * gr;
                var ezLocal1: Int = (k + 1) * gr - 1;
                var ezOrg0 = ezLocal0;

                if (ezLocal0 < ez0)
                {
                    ezLocal0 = ez0;
                }
                if (ezLocal1 > ez1)
                {
                    ezLocal1 = ez1;
                }

                for (i in i0... (i1 + 1))
                {
                    var exLocal0: Int = i * gr;
                    var exLocal1: Int = (i + 1) * gr - 1;
                    var exOrg0 = exLocal0;
                    var srcCacheEntry = this.elevationCacheGetAt(i, k, layer);

                    /*
                     * For each of these required source cache entries
                     * look what we can copy to the destination.
                     */
                    if (exLocal0 < ex0)
                    {
                        exLocal0 = ex0;
                    }
                    if (exLocal1 > ex1)
                    {
                        exLocal1 = ex1;
                    }


                    var destZ = ezLocal0 - ez0;
                    var srcZ = ezLocal0 - ezOrg0;

                    // trace('Iterating from ezLocal0:=$ezLocal0 to ezLocal1:=$ezLocal1, exLocal0:=$exLocal0 to exLocal1:=$exLocal1');

                    // Yes, the calculation is superfluous at this point. Just for debug output.
                    var destX = exLocal0 - ex0;
                    var srcX = exLocal0 - exOrg0;
                    // trace('Starting destX:=$destX, destZ:=$destZ, srcX:=$srcX, srcZ:=$srcZ');

                    for (ez in ezLocal0... (ezLocal1 + 1))
                    {
                        destX = exLocal0 - ex0;
                        srcX = exLocal0 - exOrg0;
                        for (ex in exLocal0... (exLocal1 + 1))
                        {
                            var elevation = srcCacheEntry.elevations[srcZ][srcX];
                            elevationRect.elevations[destZ][destX] = elevation;
                            // trace('elevation is $elevation');
                            ++destX;
                            ++srcX;
                        }
                        ++destZ;
                        ++srcZ;
                    }
                }
            }

            return elevationRect;
        }


        public function elevationCacheGetRectBelow(
            x0: Float, z0: Float,
            x1: Float, z1: Float,
            layer: String
        ) : Rect {
            var entry: FactoryEntry = getNextFactoryEntryBelow(
                x0, z0, x1, z1, layer);
            if (null == entry)
            {
                throw 'elevation.Cache: No entry found below "$layer".';
            }
            return elevationCacheGetRectAt(x0, z0, x1, z1, entry.layer);
        }


        /**
         * Create a new elevation array factory.
         * This factory is associated with the given worldMetaGen.
         */
        public function new(worldMetaGen: WorldMetaGen)
        {
            _mapEntries = new Map<String, CacheEntry>();
            _keysFactories = new Array<String>();
            _mapFactories = new Map<String, FactoryEntry>();
            _mutexMap = new Mutex();
            _worldMetaGen = worldMetaGen;
            WorldMetaGen.cat.catAddGlobalEntity('elevation.Cache', this);
            _maxLayer = "";
        }
    }
}
