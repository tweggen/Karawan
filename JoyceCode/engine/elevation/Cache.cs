using System;
using System.Threading;
using System.Collections.Generic;
using System.Numerics;
using engine.geom;
using engine.joyce;
using engine.world;
using static engine.Logger;

namespace engine.elevation
{
    public class Cache
    {
        static private object _classLock = new();
        static private Cache _instance = null;

        public const string TOP_LAYER = "TOP_LAYER";

        public const string LAYER_BASE = "/000000";

        private readonly object _lo = new();
        private string _maxLayer;

        private Dictionary<string, CacheEntry> _mapEntries;
        private Dictionary<string, FactoryEntry> _mapFactories;
        private List<string> _keysFactories;

        private bool _traceCache = false;

        
        static public Index3 PosToIndex3(in Vector3 pos)
        {
            float ess = world.MetaGen.FragmentSize / world.MetaGen.GroundResolution;
            
            float x = pos.X + ess / 2f;
            float z = pos.Z + ess / 2f;
            x /= ess;
            z /= ess;

            return new Index3(
                (int)Math.Floor(x),
                0,
                (int) Math.Floor(z)
            );
        }


        static public Vector3 Index3ToPos(in Index3 idx)
        {
            float ess = world.MetaGen.FragmentSize / world.MetaGen.GroundResolution;
            return new Vector3(
                idx.I * ess,
                0f,
                idx.K * ess
            );
        }


        static public Index3 ToFragment(in Index3 idx)
        {
            float gr = world.MetaGen.GroundResolution;
            float i = (float)idx.I + gr / 2f;
            float k = (float)idx.K + gr / 2f;
            i /= gr;
            k /= gr;

            return new Index3(
                (int)Math.Floor(i),
                0,
                (int) Math.Floor(k)
            );
        }


        private void _insertElevationFactoryEntryNoLock(
            in string id,
            in FactoryEntry elevationFactoryEntry
        )
        {
            if(_traceCache ) Trace($"Now inserted {id}.");
            _mapFactories.Add(id, elevationFactoryEntry);
            _keysFactories.Add(id);
            _keysFactories.Sort();
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
            engine.geom.Rect2 rect2,
            in string layer )
        {
            AABB aabb = new(rect2);
            
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

            lock (_lo)
            {
                int idx = _keysFactories.Count - 1;

                if (factoryId ==
                    TOP_LAYER) // TXWTODO: factory id contains the prefixed layer string, elevataion-factory-TOP_LAYER
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
                    Trace("Returning null (1).");
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
                    if (elevationFactoryEntry.ElevationOperator.ElevationOperatorIntersects(aabb))
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
                    Trace("Returning null (2).");
                    return null;
                }

                return elevationFactoryEntry;
            }
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
            lock (_lo)
            {
                if (String.Compare(layer, _maxLayer) > 0)
                {
                    _maxLayer = layer;
                }

                _insertElevationFactoryEntryNoLock(id, elevationFactoryEntry);
            }
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

            // Trace($"Entry requested for layer {layer}");

            var elevationAdapter = new LayerAdapter(this, layer);

            if (TOP_LAYER == layer)
            {
                layer = _maxLayer;
            }

            /*
             * If we have the entry for that layer, just return it.
             */
            var id = _createEntryId(i, k, layer);
            FactoryEntry? elevationFactoryEntry = null;

            lock (_lo)
            {
                if (_mapEntries.TryGetValue(id, out var entry))
                {
                    return entry;
                }

                if (_traceCache) Trace($"Cache MISS for {i}, {k}.");

                /*
                 * We do not have the entry. So look up the factory function and
                 * create it.
                 */
                var factoryId = _createFactoryId(layer);
                if (!_mapFactories.ContainsKey(factoryId))
                {
                    ErrorThrow($"No factory registered for layer {layer}", le => new InvalidOperationException(le));
                }

                elevationFactoryEntry = _mapFactories[factoryId];
            }

            // TXWTODO: This is risky, we open up the mutex, depending on 
            // no cyclic dependencies.
            CacheEntry newEntry = null;
            if (null != elevationFactoryEntry.ElevationOperator)
            {
                /*
                 * We found an elevation operator.
                 */
                var gr = world.MetaGen.GroundResolution;
                var elevationSegment = new ElevationSegment(gr + 1, gr + 1);

                world.MetaGen.GetFragmentRect(i, k, out elevationSegment.Rect2);
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
                            elevationAdapter, elevationSegment
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
                    Warning($"Exception in operator: {e}");
                }

                newEntry = new CacheEntry();

                /*
                 * Simply use the elevations from the rect. Nobody will use
                 * the rect after this function.
                 */
                newEntry.elevations = elevationSegment.Elevations;

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
                lock (_lo)
                {
                    _mapEntries[id] = newEntry;
                }
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


        // TXWTODO: THis should go to a convenience file.
        public bool ElevationCacheRayCast(Vector3 v3Start, Vector3 v3Direction, float maxlen, out float intersect)
        {
            /*
             * Step along the grid one step at time
             */
            intersect = 0f;

            Vector3 v3Major, v3Minor;
            Index3 i3Major, i3Minor;

            if (v3Direction.X > 0f)
            {
                if (v3Direction.Z > 0f)
                {
                    // +X, +Y
                    if (v3Direction.X > v3Direction.Z)
                    {
                        // +X, +Y, x -> y
                        v3Major = Vector3.UnitX;
                        v3Minor = Vector3.UnitZ;
                        i3Major = new(1, 0, 0);
                        i3Minor = new(0, 0, 1);
                    }
                    else
                    {
                        // +X, +Y, y -> x
                        v3Major = Vector3.UnitZ;
                        v3Minor = Vector3.UnitX;
                        i3Major = new(0, 0, 1);
                        i3Minor = new(1, 0, 0);
                    }
                }
                else
                {
                    // +X, -Y
                    if (v3Direction.X > -v3Direction.Z)
                    {
                        // +X, -Y, x -> y
                        v3Major = Vector3.UnitX;
                        v3Minor = -Vector3.UnitZ;
                        i3Major = new(1, 0, 0);
                        i3Minor = new(0, 0, -1);
                    }
                    else
                    {
                        // +X, -Y, y -> x
                        v3Major = -Vector3.UnitZ;
                        v3Minor = Vector3.UnitX;
                        i3Major = new(0, 0, -1);
                        i3Minor = new(1, 0, 0);
                    }
                }
            }
            else
            {
                if (v3Direction.Z > 0f)
                {
                    // -X, +Y
                    if (-v3Direction.X > v3Direction.Z)
                    {
                        // -X, +Y, x -> y
                        v3Major = -Vector3.UnitX;
                        v3Minor = Vector3.UnitZ;
                        i3Major = new(-1, 0, 0);
                        i3Minor = new(0, 0, 1);
                    }
                    else
                    {
                        // -X, +Y, y -> x
                        v3Major = Vector3.UnitZ;
                        v3Minor = -Vector3.UnitX;
                        i3Major = new(0, 0, 1);
                        i3Minor = new(-1, 0, 0);
                    }
                }
                else
                {
                    // -X, -Y
                    if (-v3Direction.X > -v3Direction.Z)
                    {
                        // -X, -Y, x -> y
                        v3Major = -Vector3.UnitX;
                        v3Minor = -Vector3.UnitZ;
                        i3Major = new(-1, 0, 0);
                        i3Minor = new(0, 0, -1);
                    }
                    else
                    {
                        // -X, -Y, y -> x
                        v3Major = -Vector3.UnitZ;
                        v3Minor = -Vector3.UnitX;
                        i3Major = new(0, 0, -1);
                        i3Minor = new(-1, 0, 0);
                    }
                }
            }

            var fs = world.MetaGen.FragmentSize;
            var gr = world.MetaGen.GroundResolution;

            /*
             * The resulting step size, for either dimenstion between two points.
             */
            var ess = fs / gr;


            Index3 i3CacheFragment = new(0, 0, 0);
            CacheEntry ceCurrent = null;
            Vector3 v3CacheFragment = Vector3.Zero;
            
            /*
             * To setup iteration, we progress from v3Start to the closest
             * elevation tile before or at the start.
             * Closest means, it is the tile immediately before v3Start
             * in terms of v3Major.
             *
             * That way, we would find out very early intersections with the
             * terrain.
             */

            float lDirection = v3Direction.Length();
            float fRayToDir = Vector3.Dot(v3Major, v3Direction) / lDirection;
            
            /*
             * v3Ray advances one step in major direction and is a scaled
             * version of v3Direction.
             */
            Vector3 v3Ray = v3Direction / Vector3.Dot(v3Major, v3Direction);

            /*
             * This is the actual major coordinate including major sign
             * of the start of the ray.
             */
            float realMajorStart = Vector3.Dot(v3Start, v3Major);
            
            /*
             * This is the actual start of the ray, starting at the grid
             * before the ray.
             */
            float gridMajorStart = Single.Floor(realMajorStart);

            /*
             * This is the difference by which the grid start is before the actual start. 
             */
            float dStart = gridMajorStart - realMajorStart;

            /*
             * Inside this loop, we iterate in matters of elevation pixels aka tiles.
             * We know that we started at an elevation pixel, so we just need to
             * increment one step in the "major" direction.
             *
             * Also, in every step, we need to calculate the action "height" of the
             * raycast beam.
             *
             * This is done with projection by means of the dot product.
             *
             * Also, for prevision reasons, we count the iterations in an integer variable.
             * cnt == 0 means the start position "behind" the v3Start.
             */
            int cntMax = (int) Single.Ceiling(maxlen * world.MetaGen.GroundResolution / (world.MetaGen.FragmentSize));
            for (int cnt=0; cnt<cntMax; cnt++ )
            {
                /*
                 * Am I above or below where I am?
                 */

                /*
                 * What is the x/y coordinate of the dot product at this point?
                 */
                float dCurrent = - dStart 
                                 + (float)cnt
                                 * world.MetaGen.FragmentSize
                                 / world.MetaGen.GroundResolution;
                Vector3 v3RayEndCurrent = v3Start + v3Ray * dCurrent;
                Index3 i3TileCurrent = PosToIndex3(v3RayEndCurrent);
                
                /*
                 * First make sure we can look up everything. 
                 */
                Index3 i3FragmentCurrent = ToFragment(i3TileCurrent);
                if (null == ceCurrent || i3FragmentCurrent != i3CacheFragment)
                {
                    ceCurrent = ElevationCacheGetAt(i3FragmentCurrent.I, i3FragmentCurrent.J, TOP_LAYER);
                    i3CacheFragment = i3FragmentCurrent;
                    v3CacheFragment = Fragment.Index3ToPos(i3CacheFragment);
                }
                
                /*
                 * Now look, if we are above or below the grounds here.
                 */
                ElevationPixel epCurrent = ceCurrent.GetElevationPixelAt(v3RayEndCurrent-v3CacheFragment);
                
                float height = v3RayEndCurrent.Y;
                float yTooMuch = epCurrent.Height - height;

                if (yTooMuch>=0)
                {
                    if (v3Ray.Y > 0.01)
                    {
                        /*
                         * Compute, where exactly the intersection would be.
                         */
                        float majorTooMuch = Vector3.Dot(v3Ray, v3Major) * yTooMuch / v3Ray.Y;

                        // TXWTODO: This is inexact and does not consider the actual elwvation.
                        intersect = (dCurrent-majorTooMuch) * fRayToDir / lDirection;
                    }
                    else
                    {
                        intersect = dCurrent * fRayToDir / lDirection;
                    }

                    return true;
                }
            }

            intersect = 0f;
            return false;
        }


        public CacheEntry ElevationCacheGetBelow(
            int i, int k, 
            in string layer
        )
        {

            // TXWTODO: Double code, also in CacheGetAt.
            var fs = world.MetaGen.FragmentSize;
            world.MetaGen.GetFragmentRect(i, k, out var rect2Fragment);

            FactoryEntry entry = _getNextFactoryEntryBelow(rect2Fragment, layer);
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
        private ElevationSegment _elevationCacheGetRectAt(
            engine.geom.Rect2 rect2,
            string layer
        )
        {
            float x0 = rect2.A.X;
            float z0 = rect2.A.Y;
            float x1 = rect2.B.X;
            float z1 = rect2.B.Y;
            
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

            ElevationSegment elevationSegment = null;

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
            elevationSegment = new ElevationSegment(nHoriz + 1, nVert + 1);
            elevationSegment.Rect2.A.X = x0;
            elevationSegment.Rect2.A.Y = z0;
            elevationSegment.Rect2.B.X = x1;
            elevationSegment.Rect2.B.Y = z1;

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
                            elevationSegment.Elevations[destZ,destX] = elevation;
                            // trace('elevation is $elevation');
                            ++destX;
                            ++srcX;
                        }
                        ++destZ;
                        ++srcZ;
                    }
                }
            }

            return elevationSegment;
        }


        public ElevationSegment ElevationCacheGetRectBelow(
            engine.geom.Rect2 rect2,
            in string layer
        ) {
            FactoryEntry entry = _getNextFactoryEntryBelow(rect2, layer);
            if (null == entry)
            {
                throw new InvalidOperationException(
                    $"elevation.Cache: No entry found below '{layer}'." );
            }
            return _elevationCacheGetRectAt(rect2, entry.Layer);
        }


        /**
         * Create a new elevation array factory.
         * This factory is associated with the given worldMetaGen.
         */
        private Cache()
        {
            _mapEntries = new();
            _keysFactories = new();
            _mapFactories = new();
            // WorldMetaGen.cat.catAddGlobalEntity('elevation.Cache', this);
            _maxLayer = "";
        }

        static public Cache Instance()
        {
            lock (_classLock)
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
