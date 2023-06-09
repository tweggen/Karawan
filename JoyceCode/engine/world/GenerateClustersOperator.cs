﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using static engine.Logger;

namespace engine.world
{
    public class GenerateClustersOperator : world.IWorldOperator
    {
        static private void trace(string message)
        {
            Console.WriteLine(message);
        }

        private string _strKey;
        private engine.RandomSource _rnd;
        private tools.NameGenerator _nameGenerator;

        public string WorldOperatorGetPath()
        {
            return "none/GenerateClustersOperator";
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
        public void WorldOperatorApply(world.MetaGen worldMetaGen)
        {
            tools.NameGenerator nameGenerator = tools.NameGenerator.Instance();

            int nClusters = 0;

            /* 
             *
             * This is 100 cities in 50 by 50 kilometers max.
             * Remember the cities might become merged.
             */
            int nMaxClusters = (int) ((world.MetaGen.MaxWidth/1000f) * (world.MetaGen.MaxHeight/1000f) / 5f );
            Trace($"Generating a maximum of {nMaxClusters} cluster.");


            /*
             * Array of clusters (cities).
             * Clusters are somehow centered around these points.
             * One point is guaranteed to be at 0.
             */
            List<ClusterDesc> acd = new();

            /*
             * Create the cluster list. The cluster list will make itself a global
             * entity available in the catalogue.
             */
            var clusterList = ClusterList.Instance();

            int idxCluster = 0;
            trace( "GenerateClustersOperator: Generating cluster list..." );

            {
                var clusterDesc = new ClusterDesc("cluster-"+_strKey+"-"+idxCluster);

                clusterDesc.Pos = new Vector3( -10f * _rnd.getFloat(), 0f, 10f);
                clusterDesc.Size = 1000f;
                clusterDesc.Name = nameGenerator.CreateWord(_rnd);
                acd.Add( clusterDesc ); nClusters++;
                ++idxCluster;
            }

            /*
             * Now generate a couple of further clusters. 
             * Currently, they should not exceed a fragment size in size.
             */
            while (nClusters < nMaxClusters)
            {
                var clusterDesc = new ClusterDesc("cluster-" + _strKey + "-" + idxCluster);
                ++idxCluster;

                var x3 = _rnd.getFloat();
                x3 = x3 * x3;
                clusterDesc.Size = 300f+ 3000f* x3;
                clusterDesc.Pos = new Vector3(
                    (world.MetaGen.MaxWidth - 2f * clusterDesc.Size) * (_rnd.getFloat() - 0.5f),
                    10f + _rnd.getFloat() * 30f,
                    (world.MetaGen.MaxHeight - 2f * clusterDesc.Size) * (_rnd.getFloat() - 0.5f)
                );
                // TXWTODO: But why random and not the height in the landscape? Because I generate the landscape city operator only later on in this file.
                clusterDesc.Name = nameGenerator.CreateWord(_rnd);
                acd.Add(clusterDesc); nClusters++;
            }

            /*
             * Use a trivial approach: 
             * for each cluster that is not merged, iterate through all other 
             * clusters that are not merged. If any of them overlaps, delete
             * both of them and create a merged cluster. Append it to the list.
             * This terminates worst case with a single cluster.
             */

            // trace( "GenerateClustersOperator: Merging "+nClusters+" clusters..." );

            int nMerges = 0;

            int idx = 0;
            while (idx < nClusters)
            {
                //trace( idx );
                ClusterDesc candClusterDesc = acd[idx];
                // Already merged, ignore.
                if (candClusterDesc.Merged)
                {
                    ++idx;
                    continue;
                }

                int idxTest = 0;
                while (idxTest < nClusters)
                {
                    //trace( idxTest );

                    // Ignore myself.
                    if (idx == idxTest)
                    {
                        ++idxTest;
                        continue;
                    }
                    ClusterDesc testClusterDesc = acd[idxTest];
                    // Continue, this one is merged.
                    if (testClusterDesc.Merged)
                    {
                        ++idxTest;
                        continue;
                    }

                    // This one is not merged.
                    float distance = (float) Vector3.Distance(candClusterDesc.Pos, testClusterDesc.Pos);

                    var minDist = candClusterDesc.Size + testClusterDesc.Size;

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
                        candClusterDesc.Merged = true;
                        ++idxTest;
                        continue;
                    }
                    if (0 == idx)
                    {
                        testClusterDesc.Merged = true;
                        ++idxTest;
                        continue;
                    }
                    ClusterDesc larger;
                    ClusterDesc smaller;
                    if (testClusterDesc.Size > candClusterDesc.Size)
                    {
                        larger = testClusterDesc;
                        smaller = candClusterDesc;
                    }
                    else
                    {
                        smaller = testClusterDesc;
                        larger = candClusterDesc;
                    }
                    if (true || distance < (larger.Size - smaller.Size))
                    {
                        // Larger overlaps the smaller. Discard the smaller.
                        smaller.Merged = true;
                        // trace('Smaller is merged');
                        ++idxTest;
                        continue;
                    }

                    //trace( "Creating merge: "+distance+" is less than "+minDist
                    //    +" but more than "+(larger.size-smaller.size) );

            // Merge them.
            ClusterDesc newClusterDesc = new ClusterDesc("cluster-" + _strKey + "-" + idxCluster);
            ++idxCluster;
            newClusterDesc.Size = minDist;
           newClusterDesc.Pos = (candClusterDesc.Pos + testClusterDesc.Pos)
                        / (candClusterDesc.Size + testClusterDesc.Size);
                
            newClusterDesc.Name = candClusterDesc.Name;
            testClusterDesc.Merged = true;
            candClusterDesc.Merged = true;

            //trace( newClusterDesc );

            acd.Add(newClusterDesc); nClusters++;
            nMerges++;
            ++idxTest;
        }
        ++idx;

        // trace( idx );
    }

    /*
     * Remove merged clusters.
     */
    acd = acd.FindAll(c => !c.Merged);
    nClusters = acd.Count;

    trace("GenerateClustersOperator: Merged " + nMerges + " clusters to " + (nClusters - nMerges * 2));
    trace("GenerateClustersOperator: Computing data highways...");

    /*
     * Now generate per cluster info.
     */
    for(int i=0; i<nClusters; i++)
    {
        var cl1 = acd[i];

        /*
         * Add closest cluster.
         */
        for(int j=0; j<nClusters; j++ )
        {
            var cl2 = acd[j];
            // cast(cl1, ClusterDesc);
            // cast(cl2, ClusterDesc);
            cl1.AddClosest(cl2);
        }

        int rndIdx = (int) Math.Floor(_rnd.getFloat() * 1000000.0f);

        string newKey = _strKey + rndIdx;

        /*
         * TXWTODO: World Gen shall generate them, or some kind of operator
         * dependency definition.
         */

        /*
         * Shape the elevation floor.
         */
        if (true)
        {
                    var elevationCache = engine.elevation.Cache.Instance();
            var clusterElevationOperator = new elevation.ClusterBaseElevationOperator(
                cl1,
                newKey
            );
            elevationCache.ElevationCacheRegisterElevationOperator(
                engine.elevation.Cache.LAYER_BASE + $"/000100/flattenCluster/{newKey}",
                clusterElevationOperator
            );
        }


        var clusterFragmentOperatorList = worldMetaGen.GetClusterFragmentOperatorFactoryList();
        foreach(var clusterFragmentOperatorFactory in clusterFragmentOperatorList)
        {
            try
            {
                worldMetaGen.AddFragmentOperator(
                    clusterFragmentOperatorFactory(newKey, cl1)
                );
            }
            catch (Exception e) {
                trace($"GenerateClustersOperator.worldOperatorApply(): Exception calling clusterFragmentOperatorFactory: {e}");
            }
        }

                /*
                 * And generate operators per cluster.
                 */
                // trace('GenerateClustersOperator: Cluster "${cl1.id}" at @${cl1.x}, ${cl1.z}');
#if false
                worldMetaGen.metaGenAddFragmentOperator(
                    new ops.fragment.GenerateClusterStreetsOperator(
                        cl1, newKey
                ));
#endif
#if false
                worldMetaGen.metaGenAddFragmentOperator(
                    new ops.fragment.GenerateClusterQuartersOperator(
                        cl1, newKey
                ));
#endif
#if false
                worldMetaGen.metaGenAddFragmentOperator(
                    new ops.fragment.GenerateHousesOperator(
                        cl1, newKey
                ));
#endif
#if false
                worldMetaGen.metaGenAddFragmentOperator(
                    new ops.fragment.GenerateTreesOperator(
                        cl1, newKey
                ));
#endif
#if false
                worldMetaGen.metaGenAddFragmentOperator(
                    new ops.fragment.GenerateCubeCharacterOperator(
                        cl1, newKey
                ));
#endif
#if false
                worldMetaGen.metaGenAddFragmentOperator(
                    new ops.fragment.GenerateTramCharacterOperator(
                        cl1, newKey
                ));
#endif
#if false
                worldMetaGen.metaGenAddFragmentOperator(
                    new ops.fragment.GenerateCar3CharacterOperator(
                        cl1, newKey
                ));
#endif
            }

            /*
             * Now add the clusters to the clusterList and thus to the catalogue.
             */ 
            foreach(var clusterDesc in acd) {
                clusterList.AddCluster(clusterDesc);
            }
            trace( "GenerateClustersOperator: Done." );
        }

        public GenerateClustersOperator(string strKey) 
        {
            _strKey = "clusters-"+strKey;
            _rnd = new engine.RandomSource(_strKey);

            // TXWTODO: Move this to the game specific objects.
        }    
    }
}
