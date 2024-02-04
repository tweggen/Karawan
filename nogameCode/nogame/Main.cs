using System.Collections.Generic;
using engine;

namespace nogame;

/**
 * This is the main game module.
 * It triggers all other modules required by this game and can
 * handle custom startup code.
 */
public class Main : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>(),
    };


    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);
        
        // TXWTODO: Looks a bit out of place, looks more like platform specific.
        I.Get<Boom.ISoundAPI>().SetupDone();
    }
}
