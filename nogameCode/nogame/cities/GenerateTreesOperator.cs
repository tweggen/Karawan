using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine.joyce;
using static engine.Logger;

namespace nogame.cities;


public class GenerateTreesOperator : engine.world.IFragmentOperator 
{
    private class Context
    {
        public builtin.tools.RandomSource Rnd;
        public engine.world.Fragment Fragment;
    }
    
    private engine.world.ClusterDesc _clusterDesc;
    private string _myKey;

    public string FragmentOperatorGetPath()
    {
        return $"8001/GenerateTreesOperator/{_myKey}/{_clusterDesc.IdString}";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    public Func<Task> FragmentOperatorApply(engine.world.Fragment worldFragment, engine.world.FragmentVisibility visib) => new (async () =>
    {
        if (0 == (visib.How & engine.world.FragmentVisibility.Visible3dAny))
        {
            return;
        }

        var ctx = new Context()
        {
            Rnd = new(_myKey),
            Fragment = worldFragment
        };
        

        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        /*
         * Iterate through all quarters in the clusters and generate lots and houses.
         */
        var quarterStore = _clusterDesc.QuarterStore();

        List<InstanceDesc> listInstanceDesc = new();

        bool mergeTrees = true;

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
                if (ctx.Rnd.GetFloat() <= 0.7f)
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
                        worldFragment, treePos, ctx.Rnd.Get16()));
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
                            min.X + ctx.Rnd.GetFloat() * extent.X,
                            inFragmentY,
                            min.Z + ctx.Rnd.GetFloat() * extent.Z
                        );
                        // TXWTODO: Check, if it is inside.
                        if (!estate.IsInside(treePos)) continue;
                        treePos.X += cx;
                        treePos.Z += cz;
                        listInstanceDesc.Add(_treeInstanceGenerator.CreateInstance(
                            worldFragment, treePos, ctx.Rnd.Get16()));
                        nPlanted++;
                    }
                }
            }

        }
     
        // TXWTODO: Algorithmically decide between these methods to optimize performance.
        if (!mergeTrees)
        {
            /*
             * Do not merge the meshes. Enable the renderer to use instanced calls.
             */
            foreach (var instance in listInstanceDesc)
            {
                worldFragment.AddStaticInstance("nogame.cities.trees", instance);
            }
        } 
        else
        {
            MatMesh mmTrees = new();
            foreach (var instance in listInstanceDesc)
            {
                mmTrees.Add(instance);
            }
            // TXWTODO: Merge this, this is inefficient.
            var mmMerged = MatMesh.CreateMerged(mmTrees);
            var id = engine.joyce.InstanceDesc.CreateFromMatMesh(mmMerged, 1000f);
            worldFragment.AddStaticInstance("nogame.cities.trees", id);
        }

    });
    
    
    static readonly TreeInstanceGenerator _treeInstanceGenerator = new();
    
    public GenerateTreesOperator(
        engine.world.ClusterDesc clusterDesc,
        string strKey
    ) {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
    }
    
    
    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateTreesOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}
