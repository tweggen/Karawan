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

    private readonly engine.world.ClusterDesc _clusterDesc;
    private readonly string _myKey;
    private readonly Engine _engine;


    public void FragmentOperatorGetAABB(out AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    public string FragmentOperatorGetPath()
    {
        return $"9020/nogame.npcs.niceday/{_myKey}/{_clusterDesc.IdString}";
    }


    private async Task _placeNPC(Context ctx, PositionDescription pod)
    {
        /*
         * Right now, this character does not have any behavior.
         */

        var cmd = CharacterModelDescriptionFactory.CreateNPC(ctx.Rnd);
        if (!EntityStrategy.TryCreate(ctx.Rnd, pod, cmd, out var entityStrategy))
        {
            return;
        }


        EntityCreator creator = new()
        {
            // TXWTODO: Establish this behavior in the RestStrategy.
            // BehaviorFactory = entity => new NearbyBehavior() {EPOI = entity},
            CharacterModelDescription = cmd,
            PhysicsName = EntityName,
            Fragment = ctx.Fragment,
            EntityStrategyFactory = e => entityStrategy,
            InitialAnimName = cmd.IdleAnimName
        };
        var model = await creator.CreateAsync();
        _engine.QueueEntitySetupAction(EntityName, eTarget => creator.CreateLogical(eTarget));
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

            Trace($"Starting nogame.npcs.niceday.FragmentOperator for {_clusterDesc.IdString} fragment {worldFragment.GetId()}");
            var ctx = new Context()
            {
                Rnd = new(_myKey),
                Fragment = worldFragment
            };

            var aabbClusterRelFrag = worldFragment.AABB;
            aabbClusterRelFrag.Offset(-_clusterDesc.Pos);
            
            var listForestQuarters = _clusterDesc.QuarterStore().QueryQuarters(
                aabbClusterRelFrag,
                Quarter.QuarterAttributes.Forest,
                Quarter.QuarterAttributes.Forest | Quarter.QuarterAttributes.Building
            );

            foreach (var quarter in listForestQuarters)
            {
                PositionDescription pod = new()
                {
                    Fragment = worldFragment,
                    ClusterDesc = _clusterDesc,
                    Quarter = quarter,
                    Position = I.Get<MetaGen>().Loader.GetWalkingPosAt(
                        _clusterDesc,
                        _clusterDesc.Pos + quarter.GetCenterPoint3()),
                };
                // Trace($"Putting npc in {_clusterDesc.IdString} in {quarter.GetCenterPoint()}");
                await _placeNPC(ctx, pod); 
            }
        };
    }


    public FragmentOperator(
        in engine.world.ClusterDesc clusterDesc,
        string strKey
    )
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _engine = I.Get<Engine>();
    }


    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new nogame.npcs.niceday.FragmentOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}

