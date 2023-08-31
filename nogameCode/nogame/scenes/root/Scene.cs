using engine.transform.components;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using engine;
using engine.meta;
using engine.news;
using engine.world;
using static engine.Logger;

namespace nogame.scenes.root;

public class Scene : engine.IScene, engine.IInputPart
{
    private object _lo = new();


    private static float MY_Z_ORDER = 20f;
    
    private engine.Engine _engine;

    private DefaultEcs.World _ecsWorld;

    private engine.hierarchy.API _aHierarchy;
    private engine.transform.API _aTransform;

    private builtin.controllers.FollowCameraController _ctrlFollowCamera;

    private DefaultEcs.Entity _eCamScene;
    private DefaultEcs.Entity _eCamOSD;
    private DefaultEcs.Entity _eLightMain;
    private DefaultEcs.Entity _eLightBack;
    private DefaultEcs.Entity _eAmbientLight;

    private engine.world.Loader _worldLoader;
    private engine.world.MetaGen _worldMetaGen;

    private builtin.controllers.InputController _moduleInputController;
    
    private nogame.modules.osd.Module _moduleOsd;
    private nogame.modules.playerhover.Module _modulePlayerhover;
    private nogame.modules.skybox.Module _moduleSkybox;

    private modules.map.Module _moduleMap;
    private nogame.modules.minimap.Module _moduleMiniMap;

    private bool _isMapShown = false;
    private int _isSettingUp = 0;

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
            _moduleUi.ModuleDeactivate();
            _engine.DisableMouse();
        }
        else
        {
            _engine.EnableMouse();
            _moduleUi.ModuleActivate(_engine);
        }
    }
    
    
    private void _toggleMap()
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
    

    private void _onTouchPress(Event ev)
    {
        bool _callToggleMap = false;
        
        lock (_lo)
        {
            // TXWTODO: Compute a relative position depending on view size.
            if (ev.Position.X < 150 && ev.Position.Y < 150) 
            {
                _callToggleMap = true;
            }
        }

        if (_callToggleMap)
        {
            _toggleMap();
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
                _toggleMap();
                break;
            case "(escape)":
                ev.IsHandled = true;
                _togglePauseMenu();
                break;
            default:
                break;
        }
    }

    
    private void _triggerLoadWorld()
    {
        lock (_lo)
        {
            ++_isSettingUp;
        }
        
        Vector3 vMe;
        if (!_eCamScene.Has<Transform3ToWorld>())
        {
            return;
        }
        
        vMe = _eCamScene.Get<Transform3ToWorld>().Matrix.Translation;
        // TXWTODO: We don't precisely know when we have the first valid position 
        if (vMe != Vector3.Zero)
        {
            _worldLoader.WorldLoaderProvideFragments(vMe);
        }

        lock (_lo)
        {
            --_isSettingUp;
        }
    }


    private void _kickoffScene()
    {
        _engine.QueueMainThreadAction(() =>
        {
            _ctrlFollowCamera.ForcePreviousZoomDistance(150f);
            _eCamScene.Get<engine.joyce.components.Camera3>().CameraFlags &=
                ~engine.joyce.components.Camera3.Flags.PreloadOnly;
            _eCamOSD.Get<engine.joyce.components.Camera3>().CameraFlags &=
                ~engine.joyce.components.Camera3.Flags.PreloadOnly;
        });        
    }
    
    
    public void SceneOnLogicalFrame(float dt)
    {
        _triggerLoadWorld();
        
                    
        // TXWTODO: Remove this workaround. We still need a smart idea, who can read the analog controls.
        var frontZ = Implementations.Get<InputEventPipeline>().GetFrontZ();
        if (frontZ != nogame.modules.playerhover.WASDPhysics.MY_Z_ORDER)
        {
            _ctrlFollowCamera.EnableInput(false);
        }
        else
        {
            _ctrlFollowCamera.EnableInput(true);
        }
    }


    public void SceneDeactivate()
    {
        Implementations.Get<InputEventPipeline>().RemoveInputPart(this);

        Implementations.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);
        
        _moduleInputController.ModuleDeactivate();
        
        _modulePlayerhover.ModuleDeactivate();
        _modulePlayerhover = null;
        _moduleSkybox.ModuleDeactivate();
        _moduleSkybox = null;
        if (engine.GlobalSettings.Get("nogame.CreateOSD") != "false")
        {
            _moduleOsd.ModuleDeactivate();
            _moduleOsd = null;
        }
        _ctrlFollowCamera.DeactivateController();
        _ctrlFollowCamera = null;

        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        _engine.SceneSequencer.RemoveScene(this);

        _ecsWorld = null;
        _aHierarchy = null;
        _aTransform = null;
        _engine = null;
    }
    

    public void SceneActivate(engine.Engine engine0)
    {
        lock (_lo)
        {
            ++_isSettingUp;
        }
        
        _engine = engine0;

        string keyScene = "abx";

        _worldMetaGen = MetaGen.Instance();
        _worldLoader = _worldMetaGen.Loader;

        /*
         * trigger generating the world at the starting point.
         */ 
        _triggerLoadWorld();


        /*
         * Some local shortcuts
         */
        _ecsWorld = _engine.GetEcsWorld();
        _aHierarchy = _engine.GetAHierarchy();
        _aTransform = _engine.GetATransform();

        /*
         * Global objects.
         */
        
        /*
         * Directional light
         */
        {
            _eLightMain = _engine.CreateEntity("RootScene.DirectionalLight");
            _eLightMain.Set(new engine.joyce.components.DirectionalLight(new Vector4(0.7f, 0.8f, 0.9f, 0.0f)));
            _aTransform.SetRotation(_eLightMain, Quaternion.CreateFromAxisAngle(new Vector3(0, 0, -1), 45f * (float)Math.PI / 180f));
        }
        {
            _eLightBack = _engine.CreateEntity("RootScene.OtherLight");
            _eLightBack.Set(new engine.joyce.components.DirectionalLight(new Vector4(0.2f, 0.2f, 0.0f, 0.0f)));
            _aTransform.SetRotation(_eLightBack, Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 180f * (float)Math.PI / 180f));
        }
        
        /*
         * Ambient light
         */
        {
            _eAmbientLight = _engine.CreateEntity("RootScene.AmbientLight");
            _eAmbientLight.Set(new engine.joyce.components.AmbientLight(new Vector4(0.01f, 0.01f, 0.01f, 0.0f)));
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
            cCamScene.CameraFlags = engine.joyce.components.Camera3.Flags.PreloadOnly;

            /*
             * We need to be as far away as the skycube is. Plus a bonus.
             */
            cCamScene.FarFrustum = (float)Math.Sqrt(3) * 1000f + 100f;
            cCamScene.CameraMask = 0x00000001;
            _eCamScene.Set(cCamScene);
            // No set position, done by controller
        }
        
        /*
         * Create an osd camera
         * Keep it invisible.
         */
        {
            _eCamOSD = _engine.CreateEntity("RootScene.OSDCamera");
            var cCamOSD = new engine.joyce.components.Camera3();
            cCamOSD.Angle = 0f; //60.0f;
            cCamOSD.NearFrustum = 1 / Single.Tan(30f * Single.Pi / 180f);
            cCamOSD.FarFrustum = 100f;  
            cCamOSD.CameraMask = 0x00010000;
            cCamOSD.CameraFlags = engine.joyce.components.Camera3.Flags.PreloadOnly;
            _eCamOSD.Set(cCamOSD);
            _aTransform.SetPosition(_eCamOSD, new Vector3(0f, 0f, 14f));
        }


        if (true)
        {
            _modulePlayerhover = new();
            _modulePlayerhover.ModuleActivate(_engine);
        }

        /*
         * Create a camera controller that directly controls the camera with wasd,
         * requires the playerhover.
         */
        _ctrlFollowCamera = new(_engine, _eCamScene, _modulePlayerhover.GetShipEntity());
        _ctrlFollowCamera.ActivateController();

        if (engine.GlobalSettings.Get("nogame.CreateUI") != "false") { 
            _moduleUi = new();
        }

        if (engine.GlobalSettings.Get("nogame.CreateSkybox") != "false") {
            _moduleSkybox = new();
            _moduleSkybox.ModuleActivate(_engine);
        }

        if (engine.GlobalSettings.Get("nogame.CreateOSD") != "false") { 
            _moduleOsd = new();
            _moduleOsd.ModuleActivate(_engine);
        }

        if (engine.GlobalSettings.Get("nogame.CreateMap") != "false") { 
            _moduleMap = new();
        }
        
        if (engine.GlobalSettings.Get("nogame.CreateMiniMap") != "false") { 
            _moduleMiniMap = new();
            _moduleMiniMap.ModuleActivate(_engine);
        }

        Implementations.Get<SubscriptionManager>().Subscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);

        _moduleInputController = Implementations.Get<builtin.controllers.InputController>();
        _moduleInputController.ModuleActivate(_engine);
        
        /*
         * Now, that everything has been created, add the scene.
         */
        _engine.SceneSequencer.AddScene(0, this);

        _engine.SetCameraEntity(_eCamScene);
        _engine.SetPlayerEntity(_modulePlayerhover.GetShipEntity());

        /*
         * Finally, set the timeline trigger for unblanking the cameras and starting the show.
         *
         * Kick off 2 frames before nominal start.
         */
        Implementations.Get<Timeline>().RunAt(
            nogame.scenes.logos.Scene.TimepointTitlesongStarted, 
            TimeSpan.FromMilliseconds(9735 - 33f), _kickoffScene);

        Implementations.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        
        lock (_lo)
        {
            --_isSettingUp;
        }
    }
}
