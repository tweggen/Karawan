using System.Collections.Generic;
using System.Numerics;
using engine;

namespace builtin.modules.inventory;

public class Module : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        //new MyModule<MapDB>() {}
    };

    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        // I.Get<engine.gongzuo.API>().AddDefaultBinding("inv", () => new InvLuaBindings())
        _engine.AddModule(this);
    }
} 