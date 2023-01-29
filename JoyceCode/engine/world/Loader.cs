using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

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
        private Engine _engine;
        private Mutex _mutex;
        private void trace(in string message)
        {
            Console.WriteLine(message);
        }
        private world.MetaGen _worldMetaGen = null;
        private All _all = null;

        private world.Fragment _fragCurrent = null;

        private string _strLastLoaded = "";
        private int _lastLoadedIteration = 0;
        private Dictionary<string,world.Fragment> _mapFrags;

        // private var _physics: engine.Physics = null;

#if false
        public function worldLoaderProvideMap(
            xmin: Float, zmin: Float, xmax: Float, zmax: Float
        ): Array<engine.IMolecule> {
            if(world.MetaGen.TRACE_WORLD_LOADER) trace( 'WorldLoader.worldLoaderProvideMap( $xmin, $zmin, $xmax, $zmax ): Called.' );
            var molArrayMap = new Array<engine.IMolecule>();

            /*
             * Make sure to have all materials available.
             */

            // TXWTODO: This is copy/paste from the create streets operator
            WorldMetaGen.cat.catGetSingleton(
                "GenerateClusterStreetsOperator._matStreet", function()
            {
                var mat = new engine.Material("");
                mat.diffuseTexturePath = "street/streets1to4.png";
                mat.textureRepeat = true;
                mat.textureSmooth = false;
                mat.ambientColor = 0xffffff;
                mat.ambient = 0.5;
                mat.specular = 0.0;
                return mat;
            }
            );

            /*
             * Until we have a proper fragment based mapping, try to render
             * an excerpt of the cluster content.
             */

            /*
             * Find all clusters in range.
             */
            var clusterList = _worldMetaGen.metaGenGetClusterDescIn(xmin, xmax, zmin, zmax);
            if(null == clusterList) {
                if(world.MetaGen.TRACE_WORLD_LOADER) trace( 'WorldLoader.worldLoaderProvideMap(): Empty cluster list.' );
                return molArrayMap;
            }
            if(world.MetaGen.TRACE_WORLD_LOADER) trace( 'WorldLoader.worldLoaderProvideMap(): Iterating over ${clusterList.length} clusters.' );

    /*
     * This is the geom atom that we will use for streets, sharing a single material.
     */
    var g_s = new engine.PlainGeomAtom(null, null, null, "GenerateClusterStreetsOperator._matStreet");
    var g_q = new engine.PlainGeomAtom(null, null, null, "GenerateClusterQuartersOperator._matQuarter");

        // var v_s = new engine.PlainGeomAtom.VertexArray();
        // var uv_s = new engine.PlainGeomAtom.UVArray();
        // var i_s = new engine.PlainGeomAtom.IndexArray();

        /*
         * Now, for each cluster, generate an IGeomAtom representing that particular cluster.
         * Use world coordinates. Map rendering will scale.
         */
        for(cluster in clusterList ) {
            /*
             * We may be pretty stupid indeed, simply rendering each of the individual strokes.
             */
            var strokes = cluster.strokeStore().getStrokes();
            if(world.MetaGen.TRACE_WORLD_LOADER) trace( 'WorldLoader.worldLoaderProvideMap(): Iterating over ${strokes.length} strokes in cluster.' );
    var cx = cluster.x;
    var cz = cluster.z;

            for(stroke in strokes ) {
                var a = stroke.a.pos;
    var b = stroke.b.pos;
    var unit = stroke.unit; // unit from a to b
    var normal = stroke.normal; // a 2 b, normal right hand.
    var weight = stroke.weight;
    /*
     * Let's make 1m streets.
     */
    var i: Int = g_s.getNextVertexIndex();
                var nx = normal.x * weight * 2.;
    var ny = normal.y * weight * 2.;
    g_s.p(a.x-nx-unit.x + cx, a.y-ny-unit.y + cz, 0.);
                g_s.uv(0., 0.);
                g_s.p(a.x+nx-unit.x + cx, a.y+ny-unit.y + cz, 0.);
                g_s.uv(1., 0.);
                g_s.p(b.x+nx+unit.x + cx, b.y+ny+unit.y + cz, 0.);
                g_s.uv(1., 1.);
                g_s.p(b.x-nx+unit.x + cx, b.y-ny+unit.y + cz, 0.);
                g_s.uv(0., 1.);
                g_s.idx(i+0, i+1, i+2);
                g_s.idx(i+0, i+2, i+3);
                // For convenience, both sides.
                g_s.idx(i+0, i+2, i+1);
                g_s.idx(i+0, i+3, i+2);

                // trace('added street.');
            }

/*
 * Now add the estates into the map.
 */
var quarterStore = cluster.quarterStore();
for (quarter in quarterStore.getQuarters() )
{
    for (estate in quarter.getEstates() )
    {
        for (building in estate.getBuildings() )
        {
        }
    }
}
        }

        var molMap = new engine.SimpleMolecule( [g_s] );
molArrayMap.push(molMap);

return molArrayMap;
    }
#endif

        public void WorldLoaderBehave()
        {
            foreach (KeyValuePair<string, world.Fragment> kvp in _mapFrags )
            {
                try
                {
                    kvp.Value.WorldFragmentBehave();
                }
                catch (Exception e)
                {
                    trace($"Unknown exception: {e}");
                }
            }
        }


        private void _releaseFragmentList(IList<string> eraseList )
        {
            foreach (var strKey in eraseList )
            {
                var frag = _mapFrags[strKey];
                if (world.MetaGen.TRACE_WORLD_LOADER) trace($"WorldLoader.releaseFragmentList(): Discarding fragment {strKey}");
                frag.WorldFragmentRemove();
                frag.WorldFragmentUnload();
                _mapFrags.Remove(strKey);
                frag = null;
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
            if (world.MetaGen.TRACE_WORLD_LOADER) trace($"WorldLoader.worldLoaderProvideFragments(): Called {pos0}.");
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

            //trace( [x, y, z] );
            //trace( [i, j, k ] );

            var strCurr = "fragxy-" + i + "_" + j + "_" + k;

            /*
             * Create a list of maps 
             */
            if (_strLastLoaded == strCurr)
            {
                // No need to load something new.
                return;
            }
            _strLastLoaded = strCurr;
            ++_lastLoadedIteration;

            if (world.MetaGen.TRACE_WORLD_LOADER) trace("WorldMetaGen.worldLoaderProvideFragments(): Entered new terrain " + strCurr + ", loading.");
            for (int dz=-2; dz<3; ++dz )
            {
                for (int dx=-2; dx<3; ++dx )
                {
                    int i1 = i + dx;
                    int j1 = j;
                    int k1 = k + dz;
                    var strKey = "fragxy-" + i1 + "_" + j1 + "_" + k1;
                    if (world.MetaGen.TRACE_WORLD_LOADER) trace($"WorldMetaGen.worldLoaderProvideFragments(): Loading {strKey}");

                    /*
                     * Look, wether the corresponding fragment still is in the
                     * cache, or wether we need to load (i.e. generate) it.
                     */
                    Fragment fragment;
                    _mapFrags.TryGetValue(strKey, out fragment);
                    if (null != fragment)
                    {
                        // Mark as used.
                        // trace( "Using "+strKey );
                        fragment.lastIteration = _lastLoadedIteration;
                    }
                    else
                    {

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
                            strKey,
                            new Index3(i1, j1, k1),
                            new Vector3(
                                world.MetaGen.FragmentSize * i1,
                                world.MetaGen.FragmentSize * j1,
                                world.MetaGen.FragmentSize * k1)
                            );
                        fragment.lastIteration = _lastLoadedIteration;


                        /*
                         * The following operators already might want to access this client. 
                         */
                        _mapFrags.Add(strKey, fragment);

                        /*
                         * Before we apply any operators, load static structures.
                         */
                        try
                        {
                            fragment.WorldFragmentLoadStatic();
                        }
                        catch (Exception e) {
                            trace($"WorldLoader.worldLoaderProvideFragments(): Unknown exception calling worldFragmentLoadStatic(): {e}");
                        }

                        /*
                         * Apply all fragment operators.
                         */
                        try
                        {
                            world.MetaGen.Instance().ApplyFragmentOperators(fragment);
                        }
                        catch (Exception e) {
                            trace($"WorldLoader.worldLoaderProvideFragments(): Unknown exception calling applyFragmentOperators(): {e}");
                        }
                        _mapFrags[strKey] = fragment;

                        try
                        {
                            fragment.WorldFragmentAdd(1f);
                        }
                        catch (Exception e) {
                            trace($"WorldLoader.worldLoaderProvideFragments(): Unknown exception calling worldFragmentAdd(): {e}");
                        }
                    }
                }
            }
            _fragCurrent = _mapFrags[strCurr];

            var eraseList = new List<string>();

            /*
             * Find out the list of fragments we do not need any more.
             */
            foreach (KeyValuePair<string,Fragment> kvp in _mapFrags )
            {
                var frag = kvp.Value;
                if (frag.lastIteration < _lastLoadedIteration)
                {
                    eraseList.Add(kvp.Key);
                }
            }

            /*
                * Actually do release the list of fragments we do not need anymore.
                */
            _releaseFragmentList(eraseList);

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
        public float WorldLoaderGetHeightAt(
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

            float height = entry.GetHeightAt(localX, localZ);

            return height;
        }

#if false
        /**
         * Given a character, assign it to the proper fragment.
         * A character can live only it its fragment is locked to cache.
         */
        public function worldLoaderSortinCharacter(
    character: ICharacter,
        localCharacter: FragmentCharacterData
    ): Void
{
    if (WorldMetaGen.TRACE_CHARACTER_MIGRATION) trace('WorldLoader.worldLoaderSortinCharacter(): Called for for ${character.id}.');
    var pos = character.characterGetWorldPos();
    pos.x += WorldMetaGen.fragmentSize / 2;
    pos.z += WorldMetaGen.fragmentSize / 2;
    pos.x /= WorldMetaGen.fragmentSize;
    pos.z /= WorldMetaGen.fragmentSize;

    var i:Int = Math.floor(pos.x);
    var j:Int = Math.floor(0. );
    var k:Int = Math.floor(pos.z);

    // If this fragment is in the fragment cache add the character to that fragment.
    var strCurr = "fragxy-" + i + "_" + j + "_" + k;

    var fragment: WorldFragment = _mapFrags.get(strCurr);
    if (null != fragment)
    {
        if (null == localCharacter)
        {
            if (WorldMetaGen.TRACE_CHARACTER_MIGRATION) trace('WorldLoader.worldLoaderSortinCharacter(): Need to create new localCharacter for ${character.id}.');
            localCharacter = new FragmentCharacterData();
        }
        fragment.worldFragmentAddCharacter(character, localCharacter);
    }
    else
    {
        if (null != localCharacter)
        {
            localCharacter.dismiss();
        }
        if (WorldMetaGen.TRACE_CHARACTER_MIGRATION) trace('WorldLoader.worldLoaderSortinCharaceter(): Character ${character.id} is lost, no fragment present for $strCurr.');
    }

}
#endif

        /**
         * Global initialise function
         */
        private void _init()
        {
            _mapFrags = new();
        }


        private void _releaseFragments()
        {
            _fragCurrent = null;

            _strLastLoaded = "";

            var eraseList = new List<string>( _mapFrags.Keys );

            _releaseFragmentList(eraseList);

            // leave last loaded iteration.
            _mapFrags = new();
        }


        /**
         * We shall forget anything dynamic.
         */
        public void Regenerate()
        {
            _releaseFragments();
        }

        /**
         * Constructor
         */
        public Loader(
            Engine engine,
            world.MetaGen worldMetaGen
        )
        {
            _mutex = new();
            _engine = engine;
#if false
            /*
             * First of all, run the unit tests.
             */
            {
                var unit = new Unit();
                unit.run();
            }
#endif
            _worldMetaGen = worldMetaGen;
            _init();
        }
    }
}
