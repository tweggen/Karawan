using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules;
using engine;
using engine.joyce;
using engine.news;
using nogame.modules.map;
using static engine.Logger;

namespace nogame.scenes.root;

public class Scene : AModule, IScene, IInputPart
{
    enum SceneMode
    {
        Gameplay,
        Demo,
    };

    private static float MY_Z_ORDER = 20f;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new MyModule<nogame.modules.World>(),
        new MyModule<builtin.modules.ScreenComposer>(),
        new MyModule<nogame.modules.playerhover.Module>(),
        new MyModule<nogame.modules.Gameplay>(),
        new MyModule<modules.debugger.Module>("nogame.CreateUI") { Activate = false },
        new MyModule<nogame.modules.skybox.Module>("nogame.CreateSkybox"),
        new MyModule<nogame.modules.osd.Display>("nogame.CreateOSD"),
        new MyModule<nogame.modules.osd.Camera>("nogame.CreateOSD") { Activate = false },
        new MyModule<nogame.modules.osd.Compass>("nogame.Compass"),
        new MyModule<nogame.modules.osd.Scores>(),
        new MyModule<builtin.map.MapViewer>(),
        new MyModule<modules.menu.Module>() { Activate = false },
        new MyModule<modules.map.Module>("nogame.CreateMap") { Activate = false },
        new MyModule<builtin.modules.Stats>()
        {
#if DEBUG
            Activate = true
#else
            Activate = false
#endif
            
        },
        new SharedModule<nogame.modules.story.Narration>() { Activate = false },
        new SharedModule<builtin.controllers.InputController>(),
        new SharedModule<engine.news.ClickModule>()
    };
    

    private bool _isMapShown = false;

    private bool _areStatsShown =
#if DEBUG
            true
#else
        false
#endif
        ;
    private bool _isUIShown = false;
    private bool _isMenuShown = false;

    private void _toggleDebugger()
    {
        var mUI = M<modules.debugger.Module>();
        if (null == mUI)
        {
            return;
        }

        bool isUIShown;
        lock (_lo)
        {
            isUIShown = _isUIShown;
            _isUIShown = !isUIShown;
        }

        if (isUIShown)
        {
            _engine.SetViewRectangle(Vector2.Zero, Vector2.Zero );
            mUI.ModuleDeactivate();
            _engine.DisableMouse();
        }
        else
        {
            _engine.SetViewRectangle(new Vector2(500f, 20f), Vector2.Zero );
            _engine.EnableMouse();
            mUI.ModuleActivate(_engine);
        }
    }


    private void _triggerPauseMenu()
    {
        if (!_engine.HasModule(M<modules.menu.Module>()))
        {
            M<modules.menu.Module>().ModuleActivate(_engine);
        }
        else
        {
            M<modules.menu.Module>().ModuleDeactivate();
        }
    }
    
    
    private void _toggleMap(Event ev)
    {
        I.Get<EventQueue>().Push(new engine.news.Event("nogame.modules.map.toggleMap", null));
    }


    private void _toggleStats()
    {
        bool areStatsShown;
        lock (_lo)
        {
            areStatsShown = !_areStatsShown;
            _areStatsShown = areStatsShown;
        }

        if (areStatsShown)
        {
            M<builtin.modules.Stats>().ModuleActivate(_engine);
        }
        else
        {
            M<builtin.modules.Stats>().ModuleDeactivate();
        }
    }


    public void InputPartOnInputEvent(Event ev)
    {
        if (ev.Type != Event.INPUT_KEY_PRESSED)
        {
            return;
        }

        switch (ev.Code)
        {
            case "(tab)":
                ev.IsHandled = true;
                _toggleMap(ev);
                break;
            case "(F12)":
                ev.IsHandled = true;
                _toggleDebugger();
                break;
            case "(F8)":
                ev.IsHandled = true;
                _toggleStats();
                break;
            case "(escape)":
                _triggerPauseMenu();
                break;
            default:
                break;
        }
    }

    
    private bool _kickoffScene()
    {
        /*
         * First check, if the world loader already is available. If not,
         * we need to postpone this scene's start.
         */
        if (engine.world.MetaGen.Instance().Loader == null)
        {
            Warning("Unable to start main scene, no world loader available.");
            return false;
        }
        
        /*
         * Inform the rest of the world that the main scene has started. 
         */
        I.Get<EventQueue>().Push(new Event("nogame.scenes.root.Scene.kickoff", "now"));
        
        /*
         * Force a zoom from further away into the player. 
         */
        _engine.QueueMainThreadAction(() =>
        {
            M<modules.osd.Camera>().ModuleActivate(_engine);
            _engine.SuggestEndLoading();
            M<modules.map.Module>().Mode = Module.Modes.MapMini;
        });
        
        Task.Delay(5000).ContinueWith(t =>
        {
            _engine.QueueMainThreadAction(() =>
            {
                M<nogame.modules.story.Narration>().ModuleActivate(_engine);
            });
        });
        
        I.Get<Boom.ISoundAPI>().SoundMask = 0xffffffff;

        return true;
    }
    
    
    public void SceneOnLogicalFrame(float dt)
    {
    }


    public void SceneKickoff()
    {
    }


    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        I.Get<InputEventPipeline>().RemoveInputPart(this);
        
        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        I.Get<SceneSequencer>().RemoveScene(this);

        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate(engine.Engine engine0)
    {
        uint fbWidth, fbHeight;
        {
            var split = engine.GlobalSettings.Get("nogame.framebuffer.resolution").Split("x");
            fbWidth = uint.Parse(split[0]);
            fbHeight = uint.Parse(split[1]);
        }
        // FIXME: We need to register the renderbuffer before we reference it in the world module. This is not beatiful. Anyway, setting up the renderer doesn't belong here.
        I.Get<ObjectRegistry<Renderbuffer>>().RegisterFactory(
            "rootscene_3d", 
            name => new Renderbuffer(name,
                fbWidth, fbHeight
                //480,270
            ));

        base.ModuleActivate(engine0);
        
        _engine.SuggestBeginLoading();

        string keyScene = "abx";

        /*
         * Create the screen composer
         */
        {
            M<ScreenComposer>().AddLayer(
                "rootscene_3d", 0,
                I.Get<ObjectRegistry<Renderbuffer>>().Get("rootscene_3d"));
            
        }

        /*
         * Now, that everything has been created, add the scene.
         */
        I.Get<SceneSequencer>().AddScene(0, this);

        /*
         * Finally, set the timeline trigger for un-blanking the cameras and starting the show.
         *
         * Kick off 2 frames before nominal start.
         */
        I.Get<Timeline>().RunAt(
            nogame.scenes.logos.Scene.TimepointTitlesongStarted, 
            TimeSpan.FromMilliseconds(9735 - 33f), _kickoffScene);

        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);

        _engine.AddModule(this);

        M<modules.map.Module>().ModuleActivate(_engine);
    }

}
