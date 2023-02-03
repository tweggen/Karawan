using Java.Lang;
using Java.Util.Functions;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace engine.world
{
    public class GenerateClustersOperator : IWorldOperator
    {
        private string _strKey;
        private engine.RandomSource _rnd;
        private tools.NameGenerator _nameGenerator;

    public function worldOperatorGetPath(): String {
        return 'none/GenerateClustersOperator';
    }

    /**
     * Create the actual cities.
     *
     * Clusters/cities are the objects in the void which may be 
     * interconnected by roads/trains .
     *
     * The actual generation function shall be replaced by a reference
     * to an [global] operator doing the actual work. This function shall just 
     * manage the actual extents. However, since this is once per world, we 
     * currently keep it in the world metagen.
     * That way, the actual cluster operator working on the fragments can just
     * refer to this data.
     */
    public function worldOperatorApply(worldMetaGen: WorldMetaGen): Void
    {
        var nameGenerator: tools.NameGenerator = tools.NameGenerator.require();

        var nClusters:Int = 0;

        /* 
         *
         * This is 100 cities in 50 by 50 kilometers max.
         * Remember the cities might become merged.
         */
        var nMaxClusters: Int = Std.int ((worldMetaGen.maxWidth/1000.) * (worldMetaGen.maxHeight/1000.) / 5. );


        /*
         * Array of clusters (cities).
         * Clusters are somehow centered around these points.
         * One point is guaranteed to be at 0.
         */
        var acd:Array<ClusterDesc> = new Array<ClusterDesc>();

        /*
         * Create the cluster list. The cluster list will make itself a global
         * entity available in the catalogue.
         */
        var clusterList = new ClusterList(worldMetaGen);

    var idxCluster: Int = 0;
        trace( "GenerateClustersOperator: Generating cluster list..." );

        {
            var clusterDesc:ClusterDesc = new ClusterDesc('cluster-'+_strKey+'-'+idxCluster);

    clusterDesc.x = -10 * _rnd.getFloat();
    clusterDesc.y = 0;
            clusterDesc.z = 10;
            clusterDesc.size = 1000.;
            clusterDesc.name = nameGenerator.createWord(_rnd);
            acd[nClusters++] = clusterDesc;
            ++idxCluster;
        }

/*
 * Now generate a couple of further clusters. 
 * Currently, they should not exceed a fragment size in size.
 */
while (nClusters < nMaxClusters)
{
    var clusterDesc:ClusterDesc = new ClusterDesc('cluster-' + _strKey + '-' + idxCluster);
    ++idxCluster;

    var x3 = _rnd.getFloat();
    x3 = x3 * x3;
    clusterDesc.size = 300.+ 3000.* x3;
    clusterDesc.x = (worldMetaGen.maxWidth - 2 * clusterDesc.size) * (_rnd.getFloat() - 0.5);
    clusterDesc.z = (worldMetaGen.maxHeight - 2 * clusterDesc.size) * (_rnd.getFloat() - 0.5);
    // TXWTODO: But why random and not the height in the landscape? Because I generate the landscape city operator only later on in this file.
    clusterDesc.y = 10 + _rnd.getFloat() * 30;
    clusterDesc.name = nameGenerator.createWord(_rnd);
    acd[nClusters++] = clusterDesc;
}

/*
 * Use a trivial approach: 
 * for each cluster that is not merged, iterate through all other 
 * clusters that are not merged. If any of them overlaps, delete
 * both of them and create a merged cluster. Append it to the list.
 * This terminates worst case with a single cluster.
 */

// trace( "GenerateClustersOperator: Merging "+nClusters+" clusters..." );

var nMerges:Int = 0;

var idx:Int = 0;
while (idx < nClusters)
{
    //trace( idx );
    var candClusterDesc:ClusterDesc = acd[idx];
    // Already merged, ignore.
    if (candClusterDesc.merged)
    {
        ++idx;
        continue;
    }

    var idxTest:Int = 0;
    while (idxTest < nClusters)
    {
        //trace( idxTest );

        // Ignore myself.
        if (idx == idxTest)
        {
            ++idxTest;
            continue;
        }
        var testClusterDesc:ClusterDesc = acd[idxTest];
        // Continue, this one is merged.
        if (testClusterDesc.merged)
        {
            ++idxTest;
            continue;
        }

        // This one is not merged.
        var distance:Float = Math.sqrt(
            (candClusterDesc.x - testClusterDesc.x) * (candClusterDesc.x - testClusterDesc.x)
            + (candClusterDesc.z - testClusterDesc.z) * (candClusterDesc.z - testClusterDesc.z));

        var minDist = candClusterDesc.size + testClusterDesc.size;

        // Don't overlap.
        if (distance > minDist)
        {
            ++idxTest;
            continue;
        }

        /*
         * Compute new size. Should be the minimal size that covers both
         * former clusters.
         */
        /*
         * Do not merge away the start cluster.
         */
        if (0 == idxTest)
        {
            candClusterDesc.merged = true;
            ++idxTest;
            continue;
        }
        if (0 == idx)
        {
            testClusterDesc.merged = true;
            ++idxTest;
            continue;
        }
        var larger:ClusterDesc;
        var smaller:ClusterDesc;
        if (testClusterDesc.size > candClusterDesc.size)
        {
            larger = testClusterDesc;
            smaller = candClusterDesc;
        }
        else
        {
            smaller = testClusterDesc;
            larger = candClusterDesc;
        }
        if (true || distance < (larger.size - smaller.size))
        {
            // Larger overlaps the smaller. Discard the smaller.
            smaller.merged = true;
            // trace('Smaller is merged');
            ++idxTest;
            continue;
        }

        //trace( "Creating merge: "+distance+" is less than "+minDist
        //    +" but more than "+(larger.size-smaller.size) );

        // Merge them.
        var newClusterDesc:ClusterDesc = new ClusterDesc('cluster-' + _strKey + '-' + idxCluster);
        ++idxCluster;
        newClusterDesc.size = minDist;
        newClusterDesc.x =
            (candClusterDesc.x * candClusterDesc.size
                + testClusterDesc.x * testClusterDesc.size)
            / (candClusterDesc.size + testClusterDesc.size);
        newClusterDesc.z =
            (candClusterDesc.z * candClusterDesc.size
                + testClusterDesc.z * testClusterDesc.size)
            / (candClusterDesc.size + testClusterDesc.size);
        newClusterDesc.y =
            (candClusterDesc.y * candClusterDesc.size
                + testClusterDesc.y * testClusterDesc.size)
            / (candClusterDesc.size + testClusterDesc.size);
        newClusterDesc.name = candClusterDesc.name;
        testClusterDesc.merged = true;
        candClusterDesc.merged = true;

        //trace( newClusterDesc );

        acd[nClusters++] = newClusterDesc;
        nMerges++;
        ++idxTest;
    }
    ++idx;

    // trace( idx );
}

/*
 * Remove merged clusters.
 */
acd = acd.filter(function(c) { return !c.merged; });
nClusters = acd.length;

trace("GenerateClustersOperator: Merged " + nMerges + " clusters to " + (nClusters - nMerges * 2));
trace("GenerateClustersOperator: Computing data highways...");

/*
 * Now generate per cluster info.
 */
for (i in 0...nClusters )
{
    var cl1 = acd[i];

    /*
     * Add closest cluster.
     */
    for (j in 0...nClusters )
    {
        var cl2 = acd[j];
        cast(cl1, ClusterDesc);
        cast(cl2, ClusterDesc);
        cl1.addClosest(cl2);
    }

    var rndIdx:Int = Math.floor(_rnd.getFloat() * 1000000.0);

    var newKey: String = _strKey + rndIdx;

    /*
     * TXWTODO: World Gen shall generate them, or some kind of operator
     * dependency definition.
     */

    /*
     * Shape the elevation floor.
     */
    if (true)
    {
        var elevationCache = cast(
            WorldMetaGen.cat.catGetEntity('elevation.Cache'),
            engine.elevation.Cache
        );
        var clusterElevationOperator = new ops.elevation.ClusterBaseElevationOperator(
            cl1,
            newKey
        );
        elevationCache.elevationCacheRegisterElevationOperator(
            engine.elevation.Cache.LAYER_BASE + '/000100/flattenCluster/$newKey',
            clusterElevationOperator
        );
    }


    var clusterFragmentOperatorList = worldMetaGen.metaGenGetClusterFragmentOperatorFactoryList();
    for (clusterFragmentOperatorFactory in clusterFragmentOperatorList )
    {
        try
        {
            worldMetaGen.metaGenAddFragmentOperator(
                clusterFragmentOperatorFactory(newKey, cl1)
            );
        }
        catch (unknown: Dynamic ) {
    trace('GenerateClustersOperator.worldOperatorApply(): Exception calling clusterFragmentOperatorFactory.');
}
            }

            /*
             * And generate operators per cluster.
             */
            // trace('GenerateClustersOperator: Cluster "${cl1.id}" at @${cl1.x}, ${cl1.z}');
#if 0
            worldMetaGen.metaGenAddFragmentOperator(
                new ops.fragment.GenerateClusterStreetsOperator(
                    cl1, newKey
            ));
#end
#if 0
            worldMetaGen.metaGenAddFragmentOperator(
                new ops.fragment.GenerateClusterQuartersOperator(
                    cl1, newKey
            ));
#end
#if 0
            worldMetaGen.metaGenAddFragmentOperator(
                new ops.fragment.GenerateHousesOperator(
                    cl1, newKey
            ));
#end
#if 0
            worldMetaGen.metaGenAddFragmentOperator(
                new ops.fragment.GenerateTreesOperator(
                    cl1, newKey
            ));
#end
#if 0
            worldMetaGen.metaGenAddFragmentOperator(
                new ops.fragment.GenerateCubeCharacterOperator(
                    cl1, newKey
            ));
#end
#if 0
            worldMetaGen.metaGenAddFragmentOperator(
                new ops.fragment.GenerateTramCharacterOperator(
                    cl1, newKey
            ));
#end
#if 0
            worldMetaGen.metaGenAddFragmentOperator(
                new ops.fragment.GenerateCar3CharacterOperator(
                    cl1, newKey
            ));
#end
        }

        /*
         * Now add the clusters to the clusterList and thus to the catalogue.
         */ 
        for(clusterDesc in acd) {
            clusterList.addCluster(clusterDesc);
        }
        trace( "GenerateClustersOperator: Done." );
    }

    public function new(strKey: String) {
        _strKey = 'clusters-'+strKey;
        _rnd = new engine.RandomSource(_strKey);

        // TXWTODO: Move this to the game specific objects.
    }    
    }
}
