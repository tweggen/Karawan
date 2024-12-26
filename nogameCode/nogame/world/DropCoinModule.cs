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
        for (int i = 50; i < 100; i += 10)
        {
            all.Add(M<nogame.inv.coin.Factory>().CreateAt(new Vector3(160, i, 190f))());
        }

        return Task.WhenAll(all);
    });


public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
        M<Saver>().OnCreateNewGame.Add(this);
    }


    public override void ModuleDeactivate()
    {
        M<Saver>().OnCreateNewGame.Remove(this);
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
}