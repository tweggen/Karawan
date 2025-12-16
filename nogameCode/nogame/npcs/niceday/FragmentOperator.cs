using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using builtin.map;
using engine;
using engine.geom;
using engine.joyce;
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
        return async () => { };
    }


    public FragmentOperator(
        in engine.world.ClusterDesc clusterDesc,
        string strKey
    )
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;

        I.Get<ObjectRegistry<Material>>().RegisterFactory("engine.streets.materials.cluster",
            name => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff262222)
            });
    }


    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new nogame.npcs.niceday.FragmentOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}

