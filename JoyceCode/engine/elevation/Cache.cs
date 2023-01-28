using engine.elevation;
using System;
using System.Threading;
using System.Collections.Generic;
using engine.joyce.components;

namespace engine.elevation
{
    public class Cache
    {
        static private object _instanceLock = new object();
        static private Cache _instance = null;

        private void trace(string message)
        {
            Console.WriteLine(message);
        }

        public const string TOP_LAYER = "TOP_LAYER";

        public const string LAYER_BASE = "/000000";

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
        private FactoryEntry _getNextFactoryEntryBelow(
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
            in string layer,
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
            in string layer0
        )
        {
            string layer = layer0;

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


        public CacheEntry ElevationCacheGetBelow(
            int i, int k, 
            in string layer
        )
        {

            // TXWTODO: Double code, also in CacheGetAt.
            var fs = world.MetaGen.FragmentSize;
            float x0 = (float)( (i * fs) - fs / 2.0 );
            float z0 = (float)( (k * fs) - fs / 2.0 );
            float x1 = (float)( ((i + 1) * fs) - fs / 2.0 );
            float z1 = (float)( ((k + 1) * fs) - fs / 2.0 );

            FactoryEntry entry = _getNextFactoryEntryBelow(
                x0, z0, x1, z1, layer);
            if (null == entry)
            {
                throw new InvalidOperationException(
                    $"elevation.Cache: No entry found below '{layer}'.");
            }
            return ElevationCacheGetAt(i, k, entry.Layer);
        }


        /**
         * Return an elevation rect describing all elevations within the given boundaries.
         */
        private Rect _elevationCacheGetRectAt(
            float x0, float z0,
            float x1, float z1,
            string layer
        )
        {
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

            Rect elevationRect = null;

            var fs = world.MetaGen.FragmentSize;
            var gr = world.MetaGen.GroundResolution;

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

            int i0 = (int) Math.Floor((x0 + fs / 2f) / fs);
            int k0 = (int) Math.Floor((z0 + fs / 2f) / fs);
            // var x0Local: Float = x0 - fs*i0;
            // var z0Local: Float = z0 - fs*k0;
            int ex0 = (int)((x0 + fs / 2.0) / ess);
            int ez0 = (int)((z0 + fs / 2.0) / ess);

            int i1 = (int) Math.Floor((x1 + fs / 2f) / fs);
            int k1 = (int) Math.Floor((z1 + fs / 2f) / fs);
            // var x1Local: Float = x1 - fs*i1;
            // var z1Local: Float = z1 - fs*k1;
            int ex1 = (int)((x1 + fs / 2.0) / ess);
            int ez1 = (int)((z1 + fs / 2.0) / ess);

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
            elevationRect.X0 = x0;
            elevationRect.Z0 = z0;
            elevationRect.X1 = x1;
            elevationRect.Z1 = z1;

            /* 
             * At this point, ex0,ez0 - ex1,ez1 contain the indices of the
             * source cache entries.
             */
            for (int k=k0; k<(k1 + 1); ++k)
            {

                /*
                 * Let ezLocal contain the limits of the global 
                 * elevation indices by the local tile.
                 */
                int ezLocal0 = k * gr;
                int ezLocal1 = (k + 1) * gr - 1;
                var ezOrg0 = ezLocal0;

                if (ezLocal0 < ez0)
                {
                    ezLocal0 = ez0;
                }
                if (ezLocal1 > ez1)
                {
                    ezLocal1 = ez1;
                }

                for (int i=i0; i<(i1 + 1); ++i)
                {
                    int exLocal0 = i * gr;
                    int exLocal1 = (i + 1) * gr - 1;
                    var exOrg0 = exLocal0;
                    var srcCacheEntry = this.ElevationCacheGetAt(i, k, layer);

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

                    for (int ez=ezLocal0; ez<(ezLocal1 + 1); ++ez)
                    {
                        destX = exLocal0 - ex0;
                        srcX = exLocal0 - exOrg0;
                        for (int ex=exLocal0; ex<(exLocal1 + 1); ++ex)
                        {
                            var elevation = srcCacheEntry.elevations[srcZ,srcX];
                            elevationRect.elevations[destZ,destX] = elevation;
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


        public Rect ElevationCacheGetRectBelow(
            float x0, float z0,
            float x1, float z1,
            in string layer
        ) {
            FactoryEntry entry = _getNextFactoryEntryBelow(
                x0, z0, x1, z1, layer);
            if (null == entry)
            {
                throw new InvalidOperationException(
                    $"elevation.Cache: No entry found below '{layer}'." );
            }
            return _elevationCacheGetRectAt(x0, z0, x1, z1, entry.Layer);
        }


        /**
         * Create a new elevation array factory.
         * This factory is associated with the given worldMetaGen.
         */
        public Cache()
        {
            _mapEntries = new();
            _keysFactories = new();
            _mapFactories = new();
            _mutexMap = new();
            // WorldMetaGen.cat.catAddGlobalEntity('elevation.Cache', this);
            _maxLayer = "";
        }

        static public Cache Instance()
        {
            lock (_instanceLock)
            {
                if (null == _instance)
                {
                    _instance = new Cache();
                }
                return _instance;
            }
        }
    }
}
