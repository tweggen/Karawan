using System.Collections.Generic;
using builtin.tools;
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
        new SharedModule<builtin.controllers.InputMapper>(),
        new SharedModule<builtin.tools.CameraWatcher>()
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
        
        // TXWTODO: Looks a bit out of place, looks more like platform specific.
        I.Get<Boom.ISoundAPI>().SetupDone();
        
        I.Get<engine.gongzuo.API>().AddDefaultBinding("nogame", new LuaBindings());
    }
}
