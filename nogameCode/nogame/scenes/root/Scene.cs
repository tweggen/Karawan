using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.modules;
using engine;
using engine.joyce;
using engine.news;

namespace nogame.scenes.root;

public class Scene : AModule, IScene, IInputPart
{
    enum SceneMode
    {
        Gameplay,
        Demo,
    };

    private static float MY_Z_ORDER = 20f;

    protected override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new MyModule<nogame.modules.World>(),
        new MyModule<builtin.modules.ScreenComposer>(),
        new MyModule<nogame.modules.playerhover.Module>(),
        new MyModule<nogame.modules.Gameplay>(),
        new MyModule<modules.menu.Module>("nogame.CreateUI") { Activate = false },
        new MyModule<nogame.modules.skybox.Module>("nogame.CreateSkybox"),
        new MyModule<nogame.modules.osd.Display>("nogame.CreateOSD"),
        new MyModule<nogame.modules.osd.Camera>("nogame.CreateOSD") { Activate = false },
        new MyModule<nogame.modules.osd.Compass>("nogame.Compass"),
        new MyModule<nogame.modules.osd.Scores>(),
        new MyModule<modules.map.Module>("nogame.CreateMap") { Activate = false},
        new MyModule<nogame.modules.minimap.Module>("nogame.CreateMiniMap"),
        new MyModule<builtin.modules.Stats>(),
        new SharedModule<builtin.controllers.InputController>(),
    };
    
    //private nogame.modules.World _moduleWorld;
    // private builtin.modules.ScreenComposer _moduleScreenComposer; 
    //private nogame.modules.playerhover.Module _modulePlayerhover;
    //private nogame.modules.Gameplay _moduleGameplay;
    //private modules.menu.Module _moduleUi = null;
    // private nogame.modules.skybox.Module _moduleSkybox;
    //private nogame.modules.osd.Display _moduleOsdDisplay;
    // private nogame.modules.osd.Camera _moduleOsdCamera;
    //private nogame.modules.osd.Scores _moduleOsdScores;
    //private modules.map.Module _moduleMap;
    //private nogame.modules.minimap.Module _moduleMiniMap;
    //private builtin.controllers.InputController _moduleInputController;
    

    private bool _isMapShown = false;

    private bool _isUIShown = false;

    private void _togglePauseMenu()
    {
        var mUI = M<modules.menu.Module>();
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
    
    
    private void _toggleMap(Event ev)
    {
        bool isMapShown; 
        lock (_lo)
        {
            isMapShown = _isMapShown;
            _isMapShown = !isMapShown;
        }

        if (isMapShown)
        {
            /*
             * Map was shown, so hide it.
             *
             * TXWTODO: Remove the map part.
             */
            M<modules.map.Module>().ModuleDeactivate();
            M<modules.minimap.Module>().ModuleActivate(_engine);
        }
        else
        {
            /*
             * Map was invisible, so display it.
             *
             * TXWTODO: Add the map part.
             */
            M<modules.map.Module>().ModuleActivate(_engine);
            M<modules.minimap.Module>().ModuleDeactivate();
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
            case "(escape)":
                ev.IsHandled = true;
                _togglePauseMenu();
                break;
            default:
                break;
        }
    }

    
    private void _kickoffScene()
    {
        I.Get<EventQueue>().Push(new Event("nogame.scenes.root.Scene.kickoff", "now"));
        
        /*
         * Force a zoom from further away into the player. 
         */
        _engine.QueueMainThreadAction(() =>
        {
            M<modules.osd.Camera>().ModuleActivate(_engine);
            _engine.SuggestEndLoading();
        });        
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
        
        I.Get<SubscriptionManager>().Unsubscribe("nogame.minimap.toggleMap", _toggleMap);

        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        _engine.SceneSequencer.RemoveScene(this);

        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate(engine.Engine engine0)
    {
        // FIXME: We need to register the renderbuffer before we reference it in the world module. This is not beatiful. Anyway, setting up the renderer doesn't belong here.
        I.Get<ObjectRegistry<Renderbuffer>>().RegisterFactory(
            "rootscene_3d", 
            name => new Renderbuffer(name,
                368, 207
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
        _engine.SceneSequencer.AddScene(0, this);

        /*
         * Finally, set the timeline trigger for unblanking the cameras and starting the show.
         *
         * Kick off 2 frames before nominal start.
         */
        I.Get<Timeline>().RunAt(
            nogame.scenes.logos.Scene.TimepointTitlesongStarted, 
            TimeSpan.FromMilliseconds(9735 - 33f), _kickoffScene);

        I.Get<SubscriptionManager>().Subscribe("nogame.minimap.toggleMap", _toggleMap);
        
        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        
        _engine.AddModule(this);
    }

}
