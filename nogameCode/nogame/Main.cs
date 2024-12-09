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
        new SharedModule<builtin.controllers.InputMapper>(),
        new SharedModule<builtin.tools.CameraWatcher>(),
        new SharedModule<builtin.modules.ScreenComposer>(),
        new SharedModule<nogame.modules.osd.Display>(),
        new SharedModule<nogame.modules.osd.Camera>(),
        new SharedModule<engine.news.ClickModule>(),
        new MyModule<nogame.modules.debugger.DebuggerToggle>()
    };


    private void _setupScreenComposition()
    {
        uint fbWidth, fbHeight;
        {
            var split = engine.GlobalSettings.Get("nogame.framebuffer.resolution").Split("x");
            fbWidth = uint.Parse(split[0]);
            fbHeight = uint.Parse(split[1]);
        }

        I.Get<ObjectRegistry<engine.joyce.Renderbuffer>>().RegisterFactory(
            "rootscene_3d", 
            name => new engine.joyce.Renderbuffer(name,
                fbWidth, fbHeight
                //480,270
            ));
        
        /*
         * Create the 3d layer for the main game.
         */
        {
            M<builtin.modules.ScreenComposer>().AddLayer(
                "rootscene_3d", 0,
                I.Get<ObjectRegistry<engine.joyce.Renderbuffer>>().Get("rootscene_3d"));
            
        }
    }

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
        
        I.Get<engine.gongzuo.API>().AddDefaultBinding("nogame", () => new LuaBindings());
        
        _setupScreenComposition();
        

        I.Get<SceneSequencer>().Load();
        I.Get<SceneSequencer>().Run();

        // TXWTODO: This is a nasty way to activate a first module.
        M<modules.osd.Camera>().IsModuleActive();
    }
}
