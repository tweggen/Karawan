using System;
using System.Collections.Generic;
using System.Numerics;
using engine.geom;
using static engine.Logger;

namespace engine.world
{
    /*
     * This class takes care the world is loaded according to the ranges defined by
     * viewer objects registered here.
     *
     * TXWTODO: Maybe terrain generation is a bit too interweaved into this class.
     */
    public class Loader
    {
        private object _lo = new();
        
        private Engine _engine;

        private int _lastLoadedIteration = 0;

        /**
         * Keep all fragments we created by their PosKey.
         */
        private SortedDictionary<uint, world.Fragment> _mapFrags = new();
        
        /**
         * Keep all visibility requests we have right now, keyed by PosKey.
         */
        private SortedSet<FragmentVisibility> _setVisib = new(FragmentVisibility.PosComparer);
        
        private engine.behave.systems.WiperSystem _wiperSystem;

        
        /*
         * We keep a fragment at least as long it takes to drive through it wiht 60 km/h.
         */
        public readonly float KeepFragmentMinTime = MetaGen.FragmentSize / (60*1000/3600);
        
        
        private void _releaseFragmentList(IList<uint> eraseList )
        {
            List<Fragment> fragList = new();

            lock (_lo)
            {
                foreach (var posKey in eraseList)
                {
                    if (_mapFrags.TryGetValue(posKey, out var frag))
                    {
                        if (world.MetaGen.TRACE_WORLD_LOADER)
                        {
                            Trace($"Discarding fragment {frag.GetId()}");
                        }

                        fragList.Add(frag);
                        _mapFrags.Remove(posKey);
                    }
                }
            }

            foreach (var frag in fragList)
            {
                frag.RemoveFragmentEntities();
                frag.Dispose();
            }
        }


        /**
         * Reload the current fragments
         */
        public void WorldLoaderReleaseFragments()
        {
            List<uint> fragments = new();
            lock (_lo)
            {
                foreach (var kvp in _mapFrags)
                {
                    fragments.Add(kvp.Key);
                }
                _releaseFragmentList(fragments);
            }
        }


        private List<IViewer> _listViewers = new();
        
        
        /**
         * Add this viewer to this list of viewers that shall be rendered.
         */
        public void AddViewer(IViewer iViewer)
        {
            lock (_lo)
            {
                _listViewers.Add(iViewer);
            }
        }


        public void RemoveViewer(IViewer iViewer)
        {
            lock (_lo)
            {
                _listViewers.Remove(iViewer);
            }
        }


        private void _findFragment(FragmentVisibility visib)
        {
            /*
             * Look, whether the corresponding fragment still is in the
             * cache, or whether we need to load (i.e. generate) it.
             */
            Fragment fragment = null;
            lock (_lo)
            {
                if (_mapFrags.TryGetValue(visib.PosKey(), out fragment))
                {
                    // Mark as used.
                    if (world.MetaGen.TRACE_WORLD_LOADER)
                    {
                        Trace($"Using existing version of {visib}");
                    }

                    fragment.LastIteration = _lastLoadedIteration;
                }
            }

            if (null == fragment)
            {

                if (world.MetaGen.TRACE_WORLD_LOADER)
                {
                    Trace($"Creating fragment {visib}");
                }

                /*
                 * This creates a new world fragment.
                 *
                 * World fragments are the containers for everything that
                 * comes on that please of the world. When they are created,
                 * they basically contains a canvas, in which the world is
                 * created.
                 */
                fragment = new Fragment(_engine, this, visib) { LastIteration = _lastLoadedIteration };


                lock (_lo)
                {
                    /*
                     * The following operators already might want to access this client.
                     */
                    _mapFrags.Add(visib.PosKey(), fragment);
                }
            }

            /*
             * No matter, if newly created or just found again, we need to have all requested
             * aspects of the fragment visible.
             * Apply all fragment operators.
             */
            try
            {
                fragment.EnsureVisibility(visib);
            }
            catch (Exception e)
            {
                Trace($"Exception calling applyFragmentOperators(): {e}");
            }
        }


        /**
         * Unload fragments that are not required any more.
         *
         * We are using different criteria to unload
         * - if a fragment is required by the current set of visible items, we keep it.
         * - if a fragment is younger than KeepFragmentMinTime seconds, we keep it.
         * - if a fragment has been loaded in just this iteration, we keep it.
         */
        private void _purgeFragments()
        {
            /*
             * Describe an AABB of things not to be deleted.
             */
            AABB aabb = new();

            /*
             * We do some timeout tests to avoid constant reloading.
             * So let's take the current time.
             */
            DateTime now = DateTime.Now;
            
            /*
             * Now create a list of fragments that may be deleted.
             */
            var eraseList = new List<uint>();
            lock (_lo)
            {
                /*
                 * Find out the list of fragments we do not need any more.
                 *
                 * Keep the fragments in memory for a time.
                 */
                foreach (KeyValuePair<uint, Fragment> kvp in _mapFrags)
                {
                    var frag = kvp.Value;

                    /*
                     * Is there a visibility request for this fragment?
                     */
                    if (_setVisib.TryGetValue(frag.Visibility, out var visib))
                    {
                        /*
                         * We are supposed to be visible in some way.
                         */

                        /*
                         * Remember the aabb inside of which we want to keep all
                         * [character]entites.
                         */
                        if (0 != (frag.Visibility.How & FragmentVisibility.Visible3dAny))
                        {
                            aabb.Add(frag.AABB);
                        }

                        if (false)
                        {
                            /*
                             * If visibility has been requested, do not remove it.
                             */
                            if ((frag.Visibility.How & FragmentVisibility.VisibleAny) == 0)
                            {
                                Error("visibility set without visibility request.");
                            }
                        }
                    }
                    else
                    {
                        /*
                         * We are not required to be visible any more.
                         */

                        /*
                         * Do not delete any fragment to soon.
                         */
                        if ((now - frag.LoadedAt).TotalSeconds < KeepFragmentMinTime)
                        {
                            continue;
                        }

                        /*
                         * Well, not required, not loaded to soon, so purge it.
                         */
                        if (frag.LastIteration < _lastLoadedIteration)
                        {
                            eraseList.Add(kvp.Key);
                        }
                    }
                }
            }


            if (eraseList.Count > 0)
            {
                /*
                 * Remove behaviors outside aabb.
                 */
                _engine.QueueCleanupAction(() => _wiperSystem.Update(aabb));

                /*
                 * Actually do release the list of fragments we do not need anymore.
                 */
                _releaseFragmentList(eraseList);
            }
        }


        private new SortedSet<FragmentVisibility> _updateSetVisib()
        {
            List<IViewer> lsViewers;
            lock (_lo)
            {
                /*
                 * If we have no viewers installed, just return the previous
                 * set of visibility requests.
                 */
                if (0 == _listViewers.Count)
                {
                    return _setVisib;
                }
                lsViewers = new (_listViewers);
            }

            /*
             * Collect the requests.
             */
            IList<FragmentVisibility> lsVisib = new List<FragmentVisibility>();
            foreach (var iViewer in lsViewers)
            {
                iViewer.GetVisibleFragments(ref lsVisib);
            }

            /*
             * Merge the visibility requests of the current iteration.
             */
            SortedSet<FragmentVisibility> setVisib = new(FragmentVisibility.PosComparer);
            foreach (var visib in lsVisib)
            {
                if (setVisib.TryGetValue(visib, out var oldVisib))
                {
                    byte newHow = (byte) (visib.How | oldVisib.How);
                    if (newHow > oldVisib.How)
                    {
                        oldVisib.How = newHow;
                        setVisib.Add(oldVisib);
                    }
                }
                else
                {
                    setVisib.Add(visib);
                }
            }
            
            lock (_lo)
            {
                /*
                 * Update the current generation.
                 */
                ++_lastLoadedIteration;

                _setVisib = new SortedSet<FragmentVisibility>(setVisib, FragmentVisibility.PosComparer);
            }

            return setVisib;
        }
        

        /**
         * Load the fragments required for a given position into the
         * current world.
         *
         * This function triggers application of all operators required for
         * that specific world fragment.
         *
         * This function must be called in the logical thread only!
         */
        public void WorldLoaderProvideFragments()
        {
            var setVisib = _updateSetVisib();
            
            /*
             * Now we need to trigger actions
             * - load / modify fragment if it shall be visible.
             *   This is reflected by our local setVisib state.
             * - have everything purged that is not supposed to
             *   be visible.
             */
            foreach (var visib in setVisib)
            {
                _findFragment(visib);                
            }

            _purgeFragments();
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
         * Constructor
         */
        public Loader(Engine engine)
        {
            _engine = engine;
            _wiperSystem = new(engine);
        }
    }
}
