using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.geom;
using engine.joyce;
using engine.streets;
using engine.world;
using nogame.characters;
using nogame.characters.citizen;
using static engine.Logger;

namespace nogame.npcs.niceday;

/**
 * This fragment operator will place niceday NPCs on some of the forest
 * areas in the cities.
 */
public class FragmentOperator : IFragmentOperator
{
    private class Context
    {
        public builtin.tools.RandomSource Rnd;
        public engine.world.Fragment Fragment;
    }
    
    public static readonly string EntityName = "nogame.npcs.niceguy";

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


    private void _placeNPC(Vector3 v3Pos)
    {
        /*
         * Right now, this character does not have any behavior.
         */

        var cmd = CharacterModelDescriptionFactory.CreateNPC(_rnd);
        EntityCreator creator = new()
        {
            // no BehaviorFactory
            CharacterModelDescription = cmd,
            PhysicsName = EntityName
        };
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

            var ctx = new Context()
            {
                Rnd = new(_myKey),
                Fragment = worldFragment
            };

            /*
             * The center of the fragment relative to the cluster.
             */
           Vector3 v3Center = _clusterDesc.Pos - worldFragment.Position;

            var listForestQuarters = _clusterDesc.QuarterStore().QueryQuarters(
                _clusterAABB,
                Quarter.QuarterAttributes.Forest,
                Quarter.QuarterAttributes.Forest | Quarter.QuarterAttributes.Building
            );

            foreach (var quarter in listForestQuarters)
            {
                _placeNPC(quarter.GetCenterPoint3());
            }
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

