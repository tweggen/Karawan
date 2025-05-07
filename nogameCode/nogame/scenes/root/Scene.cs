using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules;
using engine;
using engine.behave;
using engine.behave.systems;
using engine.joyce;
using engine.world;
using engine.news;
using nogame.modules;
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

    public float MY_Z_ORDER { get; set; } = 20f;

    /*
     * For debugging, we count the frames inside this scene.
     */
    private uint _nFrame = 0;
    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.World>(),
        
        new SharedModule<AutoSave>(),
        new SharedModule<Saver>(),
        
        new SharedModule<engine.behave.SpawnController>(),
        new MyModule<nogame.modules.skybox.Module>("nogame.CreateSkybox"),
        new MyModule<nogame.modules.osd.Compass>("nogame.Compass"),
        new MyModule<nogame.modules.osd.Scores>(),
        new MyModule<builtin.map.MapViewer>(),
        new MyModule<modules.menu.PauseMenuModule>() { ShallActivate = false },
        new MyModule<modules.map.Module>("nogame.CreateMap") { ShallActivate = false },
        new MyModule<builtin.modules.Stats>() { ShallActivate = false },
        new SharedModule<nogame.modules.story.Narration>(),
        new SharedModule<builtin.controllers.InputController>(),
        new SharedModule<InputEventPipeline>(),
    };

    private bool _isMapShown = false;

    private bool _areStatsShown = false;
    private bool _isMenuShown = false;

    
    private DefaultEcs.Entity _eCamScene;
    private DefaultEcs.Entity _eLightMain;
    private DefaultEcs.Entity _eLightBack;
    private DefaultEcs.Entity _eAmbientLight;


    private engine.joyce.TransformApi _aTransform;


    private void _triggerPauseMenu()
    {
        if (!_engine.HasModule(M<modules.menu.PauseMenuModule>()))
        {
            ActivateMyModule<modules.menu.PauseMenuModule>();
        }
        else
        {
            DeactivateMyModule<modules.menu.PauseMenuModule>();
        }
    }
    
    private void _triggerPauseMenu(engine.news.Event ev) => _triggerPauseMenu();


    private void _triggerSave(engine.news.Event ev)
    {
        M<Saver>().Save("Quicksave");
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
            ActivateMyModule<builtin.modules.Stats>();
        }
        else
        {
            DeactivateMyModule<builtin.modules.Stats>();
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
            _engine.SuggestEndLoading();
            M<modules.map.Module>().Mode = Module.Modes.MapMini;
            _eCamScene.Get<engine.joyce.components.Camera3>().CameraFlags &=
                ~engine.joyce.components.Camera3.Flags.PreloadOnly;
        });
        
        I.Get<Boom.ISoundAPI>().SoundMask = 0xffffffff;

        return true;
    }

    
    private void _create3dEntites()
    {
        /*
         * Directional light
         */
        
        if (true) {
            _eLightMain = _engine.CreateEntity("RootScene.DirectionalLight");
            _eLightMain.Set(new engine.joyce.components.DirectionalLight(new Vector4(0.7f, 0.8f, 0.9f, 0.0f)));
            _aTransform.SetRotation(_eLightMain, Quaternion.CreateFromAxisAngle(new Vector3(0, 0, -1), 45f * (float)Math.PI / 180f));
        }
        if (true) {
            _eLightBack = _engine.CreateEntity("RootScene.OtherLight");
            _eLightBack.Set(new engine.joyce.components.DirectionalLight(new Vector4(0.2f, 0.2f, 0.0f, 0.0f)));
            _aTransform.SetRotation(_eLightBack, Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 180f * (float)Math.PI / 180f));
        }
        
        /*
         * Ambient light
         */
        if (true) {
            _eAmbientLight = _engine.CreateEntity("RootScene.AmbientLight");
            _eAmbientLight.Set(new engine.joyce.components.AmbientLight(new Vector4(0.01f, 0.01f, 0.01f, 0.0f)));
        }

        if (false)
        {
            /*
             * Debug lights
             */
            _eAmbientLight = _engine.CreateEntity("RootScene.AmbientLight");
            _eAmbientLight.Set(new engine.joyce.components.AmbientLight(new Vector4(0.1f, 0.1f, 0.1f, 0.0f)));
            _eLightMain = _engine.CreateEntity("RootScene.DirectionalLight");
            _eLightMain.Set(new engine.joyce.components.DirectionalLight(new Vector4(0.8f, 0.8f, 0.8f, 0.0f)));
            _aTransform.SetPosition(_eLightMain, Vector3.Zero);
        }

        /*
         * Create a scene camera.
         * Keep it invisible.
         */
        {
            _eCamScene = _engine.CreateEntity("RootScene.SceneCamera");
            var cCamScene = new engine.joyce.components.Camera3();
            cCamScene.Angle = 60.0f;
            cCamScene.NearFrustum = 1f;
            cCamScene.CameraFlags = 
                engine.joyce.components.Camera3.Flags.PreloadOnly
                | engine.joyce.components.Camera3.Flags.RenderSkyboxes
                | engine.joyce.components.Camera3.Flags.EnableFog;

            /*
             * We need to be as far away as the skycube is. Plus a bonus.
             */
            cCamScene.FarFrustum = (float)Math.Sqrt(3) * 1000f + 100f;
            cCamScene.Renderbuffer = I.Get<ObjectRegistry<Renderbuffer>>().Get("rootscene_3d");
            cCamScene.CameraMask = 0x00000001;
            _eCamScene.Set(cCamScene);
            I.Get<TransformApi>().SetCameraMask(_eCamScene, 0x00000001);
            I.Get<TransformApi>().SetVisible(_eCamScene, true);
            // No set position, done by controller
        }
        _engine.Camera.Value = _eCamScene;
        
    }
    
    
    public void SceneOnLogicalFrame(float dt)
    {
        _nFrame++;
        if (_nFrame < 100)
        {
            //Trace($"Displaying root scene frame {_nFrame}.");
        }
    }


    private void _onSetAmbientLight(engine.news.Event ev)
    {
        if (_eAmbientLight.IsAlive && _eAmbientLight.IsEnabled() &&
            _eAmbientLight.Has<engine.joyce.components.AmbientLight>())
        {
            _eAmbientLight.Get<engine.joyce.components.AmbientLight>().Color = Color.StringToVector4(ev.Code);

        }
    }
    
    
    public void SceneKickoff()
    {
        /*
         * This is the default kickoff, we leave it empty.
         */
    }


    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);

        I.Get<SubscriptionManager>().Unsubscribe("nogame.modules.menu.toggleMenu", _triggerPauseMenu);

        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        I.Get<SceneSequencer>().RemoveScene(this);
    }
    

    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();
        
        _aTransform = I.Get<TransformApi>();
        
        _engine.SuggestBeginLoading();

        /*
         * Now, that everything has been created, add the scene.
         */
        I.Get<SceneSequencer>().AddScene(0, this);

        M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        
        // TXWTODO: Generalize this.
        M<SpawnController>().AddSpawnOperator(new nogame.characters.car3.SpawnOperator());
        M<SpawnController>().AddSpawnOperator(new nogame.characters.citizen.SpawnOperator());
        
        I.Get<SubscriptionManager>().Subscribe("nogame.modules.menu.save", _triggerSave);
        I.Get<SubscriptionManager>().Subscribe("nogame.modules.menu.toggleMenu", _triggerPauseMenu);
        I.Get<SubscriptionManager>().Subscribe("nogame.scenes.root.setAmbientLight", _onSetAmbientLight);
        
        ActivateMyModule<modules.map.Module>();

        _create3dEntites();
        
        /*
         * Finally, set the timeline trigger for un-blanking the cameras and starting the show.
         *
         * Kick off 2 frames before nominal start.
         */
        I.Get<Timeline>().RunAt(
            nogame.scenes.logos.Scene.TimepointTitlesongStarted, 
            TimeSpan.FromMilliseconds(9735 - 33f), _kickoffScene);


    }

}
