using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.world;

namespace nogame.world;

public class DropCoinModule : AModule, IWorldOperator
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.inv.coin.Factory>(),
        new SharedModule<Saver>(),
    };


    public string WorldOperatorGetPath() => "nogame/world/coins";

    /**
     * Seed a new game with a column of coins the player falls through.
     *
     * This used to be a hard coded column at world (164, 45..99, 137) - no cluster, no
     * player, no terrain - which in the shipped world is 102 m away in plan from where
     * the player actually appears and 36 m below it. It cannot read the player position
     * out of the GameState it is called with: Saver.CallOnCreateNewGame runs against a
     * brand new GameState whose PlayerPosition is still Vector3.Zero, and that zero is
     * exactly what makes PlayerPosition.GetPlayerPosition resolve a start lazily, much
     * later. So it asks engine.world.PlayerStart, which both this and the player ask and
     * which remembers its answer, rather than resolving a second start of its own.
     */
    public Func<Task> WorldOperatorApply() => new(() =>
    {
        StartPose pose = PlayerStart.Find();

        List<Task> all = new();
        foreach (var v3Coin in PlayerStart.StartingItemColumn(pose.V3World))
        {
            all.Add(M<nogame.inv.coin.Factory>().CreateAt(v3Coin)());
        }

        return Task.WhenAll(all);
    });


    protected override void OnModuleActivate()
    {
        M<Saver>().OnCreateNewGame.Add(this);
    }


    protected override void OnModuleDeactivate()
    {
        M<Saver>().OnCreateNewGame.Remove(this);
    }
}