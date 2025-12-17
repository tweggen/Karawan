using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.geom;
using engine.joyce;
using engine.streets;
using engine.world;
using static engine.Logger;

namespace nogame.npcs.niceday;

/**
 * This fragment operator will place niceday NPCs on some of the forest
 * areas in the cities.
 */
public class FragmentOperator : IFragmentOperator
{
    static private object _lo = new();
    private engine.world.ClusterDesc _clusterDesc;
    private AABB _clusterAABB;
    private string _myKey;


    public void FragmentOperatorGetAABB(out AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    public string FragmentOperatorGetPath()
    {
        return $"9020/nogame.npcs.niceday/{_myKey}/{_clusterDesc.IdString}";
    }


    public Func<Task> FragmentOperatorApply(Fragment worldFragment, FragmentVisibility visib)
    {
        /*
         * Look for a forest estate and place a character there.
         */
        return async () =>
        {
            /*
             * Only create for 3d visibility, not for map.
             */
            if (0 == (visib.How & engine.world.FragmentVisibility.Visible3dAny))
            {
                return;
            }

            /*
             * The center of the fragment relative to the cluster.
             */
            Vector3 v3Center = _clusterDesc.Pos - worldFragment.Position;

            var listForestQuarters = _clusterDesc.QuarterStore().QueryQuarters(
                _clusterAABB,
                Quarter.QuarterAttributes.Forest,
                Quarter.QuarterAttributes.Forest | Quarter.QuarterAttributes.Building
            );
        };
    }


    public FragmentOperator(
        in engine.world.ClusterDesc clusterDesc,
        string strKey
    )
    {
        _clusterDesc = clusterDesc;
        _clusterAABB = clusterDesc.AABB;
        _myKey = strKey;
    }


    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new nogame.npcs.niceday.FragmentOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}

