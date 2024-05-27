using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules;
using engine;
using engine.behave;
using engine.behave.systems;
using engine.joyce;
using engine.news;
using nogame.modules.playerhover;
using static engine.Logger;
using Module = nogame.modules.map.Module;

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
        new SharedModule<nogame.modules.World>(),
        new SharedModule<engine.behave.SpawnModule>(),
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
        new MyModule<builtin.modules.Stats>() { Activate = false },
        new MyModule<nogame.modules.daynite.FogColor>(),
        new SharedModule<nogame.modules.story.Narration>() { Activate = false },
        new SharedModule<builtin.controllers.InputController>(),
        new SharedModule<engine.news.ClickModule>(),
    };

    private bool _isMapShown = false;

    private bool _areStatsShown = false;
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
            mUI.ModuleActivate();
        }
    }


    private void _triggerPauseMenu()
    {
        if (!_engine.HasModule(M<modules.menu.Module>()))
        {
            M<modules.menu.Module>().ModuleActivate();
        }
        else
        {
            M<modules.menu.Module>().ModuleDeactivate();
        }
    }

    private void _triggerPauseMenu(engine.news.Event ev) => _triggerPauseMenu();
    
    
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
            M<builtin.modules.Stats>().ModuleActivate();
        }
        else
        {
            M<builtin.modules.Stats>().ModuleDeactivate();
        }
    }


    public void InputPartOnInputEvent(Event ev)
    {
        switch (ev.Type)
        {
            case Event.INPUT_BUTTON_PRESSED:
                switch (ev.Code)
                {
                    case "<map>":
                        ev.IsHandled = true;
                        _toggleMap(ev);
                        break;
                    case "<menu>":
                        ev.IsHandled = true;
                        _triggerPauseMenu();
                        break;
                        
                }
                break;
            
            case Event.INPUT_KEY_PRESSED:
                switch (ev.Code)
                {
                    case "(F12)":
                        ev.IsHandled = true;
                        _toggleDebugger();
                        break;
                    case "(F8)":
                        ev.IsHandled = true;
                        _toggleStats();
                        break;
                    default:
                        break;
                }
                break;
            
            case Event.INPUT_GAMEPAD_BUTTON_PRESSED:
                switch (ev.Code)
                {
                    default:
                        break;
                }

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
        if (I.Get<engine.world.MetaGen>().Loader == null)
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
            M<modules.osd.Camera>().ModuleActivate();
            _engine.SuggestEndLoading();
            M<modules.map.Module>().Mode = Module.Modes.MapMini;
        });
        
        Task.Delay(5000).ContinueWith(t =>
        {
            _engine.QueueMainThreadAction(() =>
            {
                M<nogame.modules.story.Narration>().ModuleActivate();
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

        I.Get<SubscriptionManager>().Unsubscribe("nogame.modules.menu.toggleMenu", _triggerPauseMenu);

        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        I.Get<SceneSequencer>().RemoveScene(this);

        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate()
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

        base.ModuleActivate();
        
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

        M<SpawnModule>().AddSpawnOperator(new nogame.characters.car3.SpawnOperator());
        
        I.Get<SubscriptionManager>().Subscribe("nogame.modules.menu.toggleMenu", _triggerPauseMenu);
        
        _engine.AddModule(this);
        M<modules.map.Module>().ModuleActivate();
    }

}
