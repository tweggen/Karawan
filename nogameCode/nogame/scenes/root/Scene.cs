using engine.joyce.components;
using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.joyce;
using engine.news;
using engine.world;
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

    protected override IEnumerable<ModuleDependency> ModuleDepends() => new List<ModuleDependency>()
    {
    };
    
    private builtin.controllers.InputController _moduleInputController;
    
    private nogame.modules.osd.Display _moduleOsdDisplay;
    private nogame.modules.osd.Camera _moduleOsdCamera;
    private nogame.modules.osd.Scores _moduleOsdScores;
    private nogame.modules.playerhover.Module _modulePlayerhover;
    private nogame.modules.Gameplay _moduleGameplay;
    private nogame.modules.skybox.Module _moduleSkybox;
    private nogame.modules.World _moduleWorld;
    private builtin.modules.ScreenComposer _moduleScreenComposer; 

    private modules.map.Module _moduleMap;
    private nogame.modules.minimap.Module _moduleMiniMap;

    private bool _isMapShown = false;

    private modules.menu.Module _moduleUi = null;
    private bool _isUIShown = false;

    private void _togglePauseMenu()
    {
        if (null == _moduleUi)
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
            _moduleUi.ModuleDeactivate();
            _engine.DisableMouse();
        }
        else
        {
            _engine.SetViewRectangle(new Vector2(500f, 20f), Vector2.Zero );
            _engine.EnableMouse();
            _moduleUi.ModuleActivate(_engine);
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
            _moduleMap.ModuleDeactivate();
            _moduleMiniMap.ModuleActivate(_engine);
        }
        else
        {
            /*
             * Map was invisible, so display it.
             *
             * TXWTODO: Add the map part.
             */
            _moduleMap.ModuleActivate(_engine);
            _moduleMiniMap.ModuleDeactivate();
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
            _moduleOsdCamera.ModuleActivate(_engine);
            _engine.SuggestEndLoading();
        });        
    }
    
    
    public void SceneOnLogicalFrame(float dt)
    {
    }


    public void SceneDeactivate()
    {
        I.Get<InputEventPipeline>().RemoveInputPart(this);
        
        I.Get<SubscriptionManager>().Unsubscribe("nogame.minimap.toggleMap", _toggleMap);

        _moduleInputController.ModuleDeactivate();
        
        _moduleGameplay?.ModuleDeactivate();
        _moduleGameplay = null;
        _modulePlayerhover?.ModuleDeactivate();
        _modulePlayerhover = null;
        _moduleOsdScores?.ModuleDeactivate();
        _moduleOsdScores = null;
        _moduleSkybox?.ModuleDeactivate();
        _moduleSkybox = null;
        if (engine.GlobalSettings.Get("nogame.CreateOSD") != "false")
        {
            _moduleOsdCamera?.ModuleDeactivate();
            _moduleOsdCamera = null;
            _moduleOsdDisplay?.ModuleDeactivate();
            _moduleOsdDisplay = null;
        }

        _moduleWorld?.ModuleDeactivate();
        _moduleWorld = null;

        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        _engine.SceneSequencer.RemoveScene(this);

        _engine = null;
    }
    

    public void SceneActivate(engine.Engine engine0)
    {
        I.Get<ObjectRegistry<Renderbuffer>>().RegisterFactory(
            "rootscene_3d", 
            name => new Renderbuffer(name,
                368, 207
                //480,270
                ));
        
        _engine = engine0;
        _engine.SuggestBeginLoading();

        string keyScene = "abx";

        if (true)
        {
            _moduleWorld = new();
            _moduleWorld.ModuleActivate(_engine);
        }

        /*
         * Create the screen composer
         */
        {
            _moduleScreenComposer = new();
            _moduleScreenComposer.ModuleActivate(_engine);
            _moduleScreenComposer.AddLayer(
                "rootscene_3d", 0,
                I.Get<ObjectRegistry<Renderbuffer>>().Get("rootscene_3d"));
            
        }

        if (true)
        {
            _modulePlayerhover = new();
            _modulePlayerhover.ModuleActivate(_engine);

            _moduleGameplay = new();
            _moduleGameplay.ModuleActivate(_engine);
        }

        if (engine.GlobalSettings.Get("nogame.CreateUI") != "false") { 
            _moduleUi = new();
        }

        if (engine.GlobalSettings.Get("nogame.CreateSkybox") != "false") {
            _moduleSkybox = new();
            _moduleSkybox.ModuleActivate(_engine);
        }

        if (engine.GlobalSettings.Get("nogame.CreateOSD") != "false") { 
            _moduleOsdDisplay = new();
            _moduleOsdDisplay.ModuleActivate(_engine);
            _moduleOsdCamera = new();
        }

        _moduleOsdScores = new();
        _moduleOsdScores.ModuleActivate(_engine);

        if (engine.GlobalSettings.Get("nogame.CreateMap") != "false") { 
            _moduleMap = new();
        }
        
        if (engine.GlobalSettings.Get("nogame.CreateMiniMap") != "false") { 
            _moduleMiniMap = new();
            _moduleMiniMap.ModuleActivate(_engine);
        }
        
        _moduleInputController = I.Get<builtin.controllers.InputController>();
        _moduleInputController.ModuleActivate(_engine);
        
        /*
         * Now, that everything has been created, add the scene.
         */
        _engine.SceneSequencer.AddScene(0, this);

        _engine.SetPlayerEntity(_modulePlayerhover.GetShipEntity());

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
    }


    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);
    }
}
