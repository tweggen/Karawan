using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;
using static engine.Logger;

namespace engine.world
{
    /*
     * This class takes care the world is loaded in a given range of coordinates.
     *
     * TODO: The name of this class is misleading. We really should rename it.
     *
     * This might be the class to apply all operators to a virgin void world.
     * It should have the standard "behave" interface. However, when provide fragments
     * is called, it should propage the call to the individual operators.
     *
     * The terrain container keeps references to the world meta generator
     * and the actual 3d scene.
     *
     * TODO:
     * This data structure needs to keep the actual objects and global data structures
     + of the world scene. We need to define what compoonents this data structure is made
     * of, e.g.:
     * - terrain heights
     * - objects lists
     * - cluster lists
     * - behavioural objects
     */
    public class Loader
    {
        private object _lo = new();
        
        private Engine _engine;

        private world.MetaGen _worldMetaGen = null;
        private All _all = null;

        private world.Fragment _fragCurrent = null;

        private string _strLastLoaded = "";
        private int _lastLoadedIteration = 0;
        
        private Dictionary<string,world.Fragment> _mapFrags;
        // TXWTODO: Use that lru.
        private List<world.Fragment> _lruFrags = new();

        private static int WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS = 2;

        private engine.behave.systems.WiperSystem _wiperSystem;


        private void _releaseFragmentListNoLock(IList<string> eraseList )
        {
            // TXWTODO: MOtex
            foreach (var strKey in eraseList )
            {
                if (_mapFrags.TryGetValue(strKey, out var frag))
                {
                    if (strKey==_strLastLoaded)
                    {
                        _strLastLoaded = "";
                    }
                    if (world.MetaGen.TRACE_WORLD_LOADER)
                        Trace($"WorldLoader.releaseFragmentList(): Discarding fragment {strKey}");
                    frag.WorldFragmentRemove();
                    frag.Dispose();
                    frag = null;
                    _mapFrags.Remove(strKey);
                }
            }
        }


        /**
         * Reload the current fragments
         */
        public void WorldLoaderReleaseFragments()
        {
            List<string> fragments = new();
            lock (_lo)
            {
                foreach (var kvp in _mapFrags)
                {
                    fragments.Add(kvp.Key);
                }
                _releaseFragmentListNoLock(fragments);
            }
        }


        /**
         * Load the fragments required for a given position into the 
         * current world.
         *
         * This function triggers application of all operators required for
         * that specific world fragment.
         *
         * TODO:
         * - we also should incremental loading (for LoD or networked connections),
         */
        public void WorldLoaderProvideFragments(in Vector3 pos0)
        {
            // Trace($"WorldLoader.worldLoaderProvideFragments(): Called {pos0}.");
            Vector3 pos = pos0;
            pos += new Vector3(
                world.MetaGen.FragmentSize/2f,
                world.MetaGen.FragmentSize/2f,
                world.MetaGen.FragmentSize/2f);
            pos.X /= world.MetaGen.FragmentSize;
            pos.Y /= world.MetaGen.FragmentSize;
            pos.Z /= world.MetaGen.FragmentSize;

            // Rest y to 0, we don't use heights now.
            pos.Y = 0;

            int i = (int) Math.Floor(pos.X);
            int j = (int) Math.Floor(pos.Y);
            int k = (int) Math.Floor(pos.Z);

            //Trace( [x, y, z] );
            //trace( [i, j, k ] );

            var strCurr = "fragxy-" + i + "_" + j + "_" + k;

            /*
             * Short circuit.
             * TXWTODO: Do we really need this.
             */
            lock (_lo)
            {
                if (_strLastLoaded == strCurr)
                {
                    // No need to load something new.
                    return;
                }

                _strLastLoaded = strCurr;
                ++_lastLoadedIteration;
            }

            if (world.MetaGen.TRACE_WORLD_LOADER) Trace($"Entered new terrain {strCurr}, loading.");
            for (int dz=-WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS; dz<= WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS; ++dz )
            {
                for (int dx = -WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS;
                     dx <= WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS;
                     ++dx)
                {
                    int i1 = i + dx;
                    int j1 = j;
                    int k1 = k + dz;
                    var strKey = "fragxy-" + i1 + "_" + j1 + "_" + k1;
                    if (world.MetaGen.TRACE_WORLD_LOADER)
                        Trace($"WorldMetaGen.worldLoaderProvideFragments(): Loading {strKey}");

                    /*
                     * Look, wether the corresponding fragment still is in the
                     * cache, or wether we need to load (i.e. generate) it.
                     */
                    Fragment fragment;
                    lock (_lo)
                    {
                        _mapFrags.TryGetValue(strKey, out fragment);
                        if (null != fragment)
                        {
                            // Mark as used.
                            Trace($"Using {strKey}");
                            fragment.LastIteration = _lastLoadedIteration;
                            continue;
                        }
                    }

                    /*
                     * This creates a new world fragment.
                     *
                     * World fragments are the containers for everything that
                     * comes on that please of the world. When they are created,
                     * they basically contains a canvas, in which the world is
                     * created.
                     */
                    fragment = new Fragment(
                        _engine,
                        this,
                        strKey,
                        new Index3(i1, j1, k1),
                        new Vector3(
                            world.MetaGen.FragmentSize * i1,
                            world.MetaGen.FragmentSize * j1,
                            world.MetaGen.FragmentSize * k1)
                    );
                    fragment.LastIteration = _lastLoadedIteration;
                    
                    /*
                     * The following operators already might want to access this client. 
                     */
                    _mapFrags.Add(strKey, fragment);

                    /*
                     * Apply all fragment operators.
                     */
                    try
                    {
                        world.MetaGen.Instance().ApplyFragmentOperators(fragment);
                    }
                    catch (Exception e)
                    {
                        Trace(
                            $"WorldLoader.worldLoaderProvideFragments(): Unknown exception calling applyFragmentOperators(): {e}");
                    }

                    lock (_lo)
                    {
                        _mapFrags[strKey] = fragment;
                    }
                }
            }

            lock (_lo)
            {
                _fragCurrent = _mapFrags[strCurr];
            }

            {
                var eraseList = new List<string>();

                lock (_lo)
                {
                    /*
                     * Find out the list of fragments we do not need any more.
                     */
                    foreach (KeyValuePair<string, Fragment> kvp in _mapFrags)
                    {
                        var frag = kvp.Value;
                        if (frag.LastIteration < _lastLoadedIteration)
                        {
                            eraseList.Add(kvp.Key);
                        }
                    }
                }


                if (eraseList.Count > 0)
                {
                    /*
                     * Remove behaviors outside aabb.
                     */
                    Vector3 vAA = new(
                        _fragCurrent.Position.X - WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS * world.MetaGen.FragmentSize,
                        _fragCurrent.Position.Y - WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS * world.MetaGen.FragmentSize,
                        _fragCurrent.Position.Z - WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS * world.MetaGen.FragmentSize
                        );
                    Vector3 vBB = new(
                        _fragCurrent.Position.X + (WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS+1) * world.MetaGen.FragmentSize,
                        _fragCurrent.Position.Y + (WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS+1) * world.MetaGen.FragmentSize,
                        _fragCurrent.Position.Z + (WORLD_LOADER_PRELOAD_N_SURROUNDING_FRAGMENTS+1) * world.MetaGen.FragmentSize
                        );
                    List<Vector3> aabb = new();
                    aabb.Add(vAA); 
                    aabb.Add(vBB);
                    _wiperSystem.Update(aabb);

                    lock (_lo)
                    {
                        /*
                         * Actually do release the list of fragments we do not need anymore.
                         */
                        _releaseFragmentListNoLock(eraseList);
                    }
                }
            }

        }


        /**
         * Return the default navigational height for a given
         * position. This is the terrain's height, if no road
         * is present there.
         */
        public float GetNavigationHeightAt(in Vector3 position)
        {
            /*
             * Are we in a cluster? Do an inefficient search.
             */
            ClusterDesc cluster = ClusterList.Instance().GetClusterAt(position);
            if (cluster != null)
            {
                return cluster.AverageHeight + MetaGen.ClusterNavigationHeight;
            }
            else
            {
                return GetHeightAt(position.X, position.Z) + MetaGen.VoidNavigationHeight;
            }
        }

        
        public Vector3 ApplyNavigationHeight(in Vector3 position, float heightOffset=0)
        {
            return new Vector3(position.X, heightOffset + GetNavigationHeightAt(position), position.Z);
        }

        
        /**
         * Return the default walking height for this position.
         * Return the height at the given position of the world.
         * If the position is unknown, it might very well be 0.0.
         * This method does not force loading the particular are of 
         * the world.
         *
         * TXWTODO: This does not require the entire worldLoader 
         * but only the elevation.
         */
        public engine.elevation.ElevationPixel GetElevationPixelAt(
            float x0,
            float z0,
            string layer = engine.elevation.Cache.TOP_LAYER
            )
        {
            /*
             * Convert global  to fragment-local coordinates
             */
            float x = x0 + world.MetaGen.FragmentSize / 2f;
            float z = z0 + world.MetaGen.FragmentSize / 2f;
            x /= world.MetaGen.FragmentSize;
            z /= world.MetaGen.FragmentSize;

            int i = (int) Math.Floor(x);
            int k = (int) Math.Floor(z);

            // TXWTODO: find a more suitable "new" API for this.
            var elevationCache = engine.elevation.Cache.Instance();
            var entry = elevationCache.ElevationCacheGetBelow(i, k, layer);

            float localX = x0 - (world.MetaGen.FragmentSize) * i;
            float localZ = z0 - (world.MetaGen.FragmentSize) * k;

            var epx = entry.GetElevationPixelAt(localX, localZ);

            return epx;
        }


        public float GetHeightAt(
            float x0,
            float z0,
            string layer = engine.elevation.Cache.TOP_LAYER
        )
        {
            return GetElevationPixelAt(x0, z0, layer).Height;
        }

        /**
         * Global initialise function
         */
        private void _init()
        {
            _mapFrags = new();
        }


        /**
         * Constructor
         */
        public Loader(
            Engine engine,
            world.MetaGen worldMetaGen
        )
        {
            _engine = engine;
            _wiperSystem = new(engine);
            _worldMetaGen = worldMetaGen;
            _worldMetaGen.SetLoader(this);
            _init();
        }
    }
}
