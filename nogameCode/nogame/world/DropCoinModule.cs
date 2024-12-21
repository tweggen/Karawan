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
    
    public Func<Task> WorldOperatorApply() => M<nogame.inv.coin.Factory>().CreateAt(new Vector3(0, 80f, 0f));
    

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