using System.Collections.Generic;
using engine;
using nogame.world;

namespace nogame.modules;

public class GameSetup : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        /*
         * Modules to populate the world after world-building.
         */
        new MyModule<DropCoinModule>(),
    };
    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
    }
}