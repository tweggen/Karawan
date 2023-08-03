using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine.joyce;
using static engine.Logger;

namespace nogame.cities;


public class GenerateTreesOperator : engine.world.IFragmentOperator 
{
    
    private engine.world.ClusterDesc _clusterDesc;
    private engine.RandomSource _rnd;
    private string _myKey;


    public string FragmentOperatorGetPath()
    {
        return $"8001/GenerateTreesOperator/{_myKey}/";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    public Task FragmentOperatorApply(engine.world.Fragment worldFragment) => new Task(() =>
    {
        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        float fsh = engine.world.MetaGen.FragmentSize / 2.0f;

        /*
         * We don't apply the operator if the fragment completely is
         * outside our boundary box (the cluster)
         */
        {
            float csh = _clusterDesc.Size / 2.0f;
            if (
                (cx - csh) > (fsh)
                || (cx + csh) < (-fsh)
                || (cz - csh) > (fsh)
                || (cz + csh) < (-fsh)
            )
            {
                return;
            }
        }

        // trace( 'GenerateHousesOperator(): cluster "${_clusterDesc.name}" (${_clusterDesc.id}) in range');
        _rnd.Clear();

        /*
         * Iterate through all quarters in the clusters and generate lots and houses.
         */
        var quarterStore = _clusterDesc.QuarterStore();

        List<InstanceDesc> listInstanceDesc = new();

        foreach (var quarter in quarterStore.GetQuarters())
        {
            if (quarter.IsInvalid())
            {
                Trace("Skipping invalid quarter.");
                continue;
            }

            /*
             * Compute some properties of this quarter.
             * - is it convex?
             * - what is it extend?
             * - what is the largest side?
             */
            foreach (var estate in quarter.GetEstates())
            {
                /*
                 * Only consider this estate, if the center coordinate 
                 * is within this fragment.
                 */
                var center = estate.GetCenter();
                center.X += cx;
                center.Z += cz;
                if (!worldFragment.IsInsideLocal(center.X, center.Z))
                {
                    continue;
                }

                // TXWTODO: The 2.15 is copied from GenerateClusterQuartersOperator
                var inFragmentY = _clusterDesc.AverageHeight + 2.15f;

                /*
                 * Ready to add a tree if there is no house.
                 */
                var buildings = estate.GetBuildings();
                if (buildings.Count > 0)
                {
                    continue;
                }

                /*
                 * But don't use every estate, just some.
                 */
                if (_rnd.GetFloat() <= 0.7f)
                {
                    continue;
                }

                /*
                 * OK, let's generate a number of trees at random positions within this estate.
                 * 
                 * There's a couple of different techniques: 
                 * - one in the center
                 * - some grid of trees
                 * - random placement
                 * - along the edges.
                 * 
                 * Base the decision on the quarter's size.
                 */
                var poly = estate.GetIntPoly();
                if (0 == poly.Count)
                {
                    continue;
                }

                var area = estate.GetArea();
                float areaPerTree = 1.6f;
                if (area < areaPerTree)
                {
                    /*
                     * If the area of the estate is less than 4m2, we just plant a 
                     * single tree.
                     */
                    var treePos = center with { Y = inFragmentY };
                    listInstanceDesc.Add(_treeInstanceGenerator.CreateInstance(
                        worldFragment, treePos, _rnd.Get16()));
                }
                else if (area >= (2f * areaPerTree))
                {
                    /*
                     * Just one other algo: random.
                     * We guess that one tree takes up 1x1m, i.e. 1m2
                     */

                    var nTrees = (int)((area + 1f) / areaPerTree);
                    if (nTrees > 80) nTrees = 80;
                    var nPlanted = 0;
                    var iterations = 0;
                    var extent = estate.GetMaxExtent();
                    var min = estate.getMin();
                    while (nPlanted < nTrees && iterations < 4 * nTrees)
                    {
                        iterations++;
                        var treePos = new Vector3(
                            min.X + _rnd.GetFloat() * extent.X,
                            inFragmentY,
                            min.Z + _rnd.GetFloat() * extent.Z
                        );
                        // TXWTODO: Check, if it is inside.
                        if (!estate.IsInside(treePos)) continue;
                        treePos.X += cx;
                        treePos.Z += cz;
                        listInstanceDesc.Add(_treeInstanceGenerator.CreateInstance(
                            worldFragment, treePos, _rnd.Get16()));
                        nPlanted++;
                    }
                }
            }

        }

        /*
         * This is a large amount of instances, reduce them.
         * That way we can't use instance rendering for the trees but reduce draw calls.
         */
        try
        {
            InstanceDesc mergedInstanceDesc = InstanceDesc.CreateMergedFrom(listInstanceDesc);
            worldFragment.AddStaticInstance("nogame.cities.trees", mergedInstanceDesc, null);
        }
        catch (Exception e)
        {
            Trace($"Unknown exception: {e}");
        }

    });
    
    static TreeInstanceGenerator _treeInstanceGenerator = new();
    
    public GenerateTreesOperator(
        engine.world.ClusterDesc clusterDesc,
        string strKey
    ) {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new engine.RandomSource(strKey);
   }
}
