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

    public Func<Task> WorldOperatorApply() => new(() =>
    {
        List<Task> all = new();
        for (int i = 45; i < 100; i += 3)
        {
            all.Add(M<nogame.inv.coin.Factory>().CreateAt(new Vector3(164, i, 137f))());
        }

        return Task.WhenAll(all);
    });


    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();
        
        M<Saver>().OnCreateNewGame.Add(this);
    }


    protected override void OnModuleDeactivate()
    {
        M<Saver>().OnCreateNewGame.Remove(this);
    }
}