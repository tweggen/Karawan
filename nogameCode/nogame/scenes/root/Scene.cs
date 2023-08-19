using engine.transform.components;
using System;
using System.Numerics;
using System.Threading;
using engine;
using engine.news;
using static engine.Logger;

namespace nogame.scenes.root;

public class Scene : engine.IScene
{
    private object _lo = new();
    
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

    private builtin.controllers.InputController _partInputController;
    
    private nogame.parts.osd.Part _partOsd;
    private nogame.parts.playerhover.Part _partPlayerhover;
    private nogame.parts.skybox.Part _partSkybox;

    private nogame.parts.map.Part _partMap;
    private nogame.parts.minimap.Part _partMiniMap;

    private bool _isMapShown = false;
    private int _isSettingUp = 0;

    private nogame.parts.menu.Part _partUI;
    private bool _isUIShown = false;

    
    public void TogglePauseMenu()
    {
        bool isUIShown;
        lock (_lo)
        {
            isUIShown = _isUIShown;
            _isUIShown = !isUIShown;
        }

        if (isUIShown)
        {
            _partUI.ModuleDeactivate();
            _engine.DisableMouse();
        }
        else
        {
            _engine.EnableMouse();
            _partUI.ModuleActivate(_engine);
        }
    }
    
    
    public void ToggleMap()
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
            _partMap.ModuleDeactivate();
            _partMiniMap.ModuleActivate(_engine);
        }
        else
        {
            /*
             * Map was invisible, so display it.
             *
             * TXWTODO: Add the map part.
             */
            _partMap.ModuleActivate(_engine);
            _partMiniMap.ModuleDeactivate();
        }
    }
    

    private void _onTouchPress(Event ev)
    {
        bool _callToggleMap = false;
        
        lock (_lo)
        {
            if (!_isMapShown)
            {
                // TXWTODO: Compute a relative position depending on view size.
                if (ev.Position.X < 150 && ev.Position.Y < 150)
                {
                    _callToggleMap = true;
                }
            }
            else
            {
                _callToggleMap = true;
            }
        }

        if (_callToggleMap)
        {
            ToggleMap();
        }
    }
    
    
    public void PartOnInputEvent(Event ev)
    {
        if (ev.Type != Event.INPUT_KEY_PRESSED)
        {
            return;
        }

        switch (ev.Code)
        {
            case "(tab)":
                ev.IsHandled = true;
                ToggleMap();
                break;
            case "(escape)":
                ev.IsHandled = true;
                TogglePauseMenu();
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
    }


    public void SceneDeactivate()
    {
        Implementations.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);
        
        _partInputController.ModuleDeactivate();
        
        _partPlayerhover.ModuleDeactivate();
        _partPlayerhover = null;
        _partSkybox.ModuleDeactivate();
        _partSkybox = null;
        if (engine.GlobalSettings.Get("nogame.CreateOSD") != "false")
        {
            _partOsd.ModuleDeactivate();
            _partOsd = null;
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

        _worldMetaGen = engine.world.MetaGen.Instance();
        if (engine.GlobalSettings.Get("nogame.CreateHouses") != "false")
        {
            _worldMetaGen.AddClusterFragmentOperatorFactory(
                (string newKey, engine.world.ClusterDesc clusterDesc) =>
                    new nogame.cities.GenerateHousesOperator(clusterDesc, newKey)
            );
        }

        if (engine.GlobalSettings.Get("nogame.CreateTrees") != "false")
        {
            _worldMetaGen.AddClusterFragmentOperatorFactory(
                (string newKey, engine.world.ClusterDesc clusterDesc) =>
                    new nogame.cities.GenerateTreesOperator(clusterDesc, newKey)
            );
        }

        if (engine.GlobalSettings.Get("world.CreateCubeCharacters") != "false")
        {
            _worldMetaGen.AddClusterFragmentOperatorFactory(
                (string newKey, engine.world.ClusterDesc clusterDesc) =>
                    new nogame.characters.cubes.GenerateCharacterOperator(clusterDesc, newKey)
            );
        }

        if (engine.GlobalSettings.Get("world.CreateCar3Characters") != "false")
        {
            _worldMetaGen.AddClusterFragmentOperatorFactory(
                (string newKey, engine.world.ClusterDesc clusterDesc) =>
                    new nogame.characters.car3.GenerateCharacterOperator(clusterDesc, newKey)
            );
        }

        if (engine.GlobalSettings.Get("world.CreateTramCharacters") != "false")
        {
            _worldMetaGen.AddClusterFragmentOperatorFactory(
                (string newKey, engine.world.ClusterDesc clusterDesc) =>
                    new nogame.characters.tram.GenerateCharacterOperator(clusterDesc, newKey)
            );
        }

        if (engine.GlobalSettings.Get("world.CreateDebris") != "false")
        {
            _worldMetaGen.AddFragmentOperator(new nogame.terrain.PlaceDebrisOperator(keyScene));
        }

        _worldMetaGen.SetupComplete();

        _worldLoader = new engine.world.Loader(_engine, _worldMetaGen);
        {
            var elevationCache = engine.elevation.Cache.Instance();
            var elevationBaseFactory = new terrain.ElevationBaseFactory();
            elevationCache.ElevationCacheRegisterElevationOperator(
                engine.elevation.Cache.LAYER_BASE + "/000002/fillGrid",
                elevationBaseFactory);
        }

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
            _partPlayerhover = new();
            _partPlayerhover.ModuleActivate(_engine);
        }

        /*
         * Create a camera controller that directly controls the camera with wasd,
         * requires the playerhover.
         */
        _ctrlFollowCamera = new(_engine, _eCamScene, _partPlayerhover.GetShipEntity());
        _ctrlFollowCamera.ActivateController();

        if (engine.GlobalSettings.Get("nogame.CreateUI") != "false") { 
            _partUI = new();
        }

        if (engine.GlobalSettings.Get("nogame.CreateSkybox") != "false") {
            _partSkybox = new();
            _partSkybox.ModuleActivate(_engine);
        }

        if (engine.GlobalSettings.Get("nogame.CreateOSD") != "false") { 
            _partOsd = new();
            _partOsd.ModuleActivate(_engine);
        }

        if (engine.GlobalSettings.Get("nogame.CreateMap") != "false") { 
            _partMap = new();
        }
        
        if (engine.GlobalSettings.Get("nogame.CreateMiniMap") != "false") { 
            _partMiniMap = new();
            _partMiniMap.ModuleActivate(_engine);
        }

        Implementations.Get<SubscriptionManager>().Subscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);

        _partInputController = Implementations.Get<builtin.controllers.InputController>();
        _partInputController.ModuleActivate(_engine);
        
        /*
         * Now, that everything has been created, add the scene.
         */
        _engine.SceneSequencer.AddScene(0, this);

        _engine.SetCameraEntity(_eCamScene);
        _engine.SetPlayerEntity(_partPlayerhover.GetShipEntity());

        /*
         * Finally, set the timeline trigger for unblanking the cameras and starting the show.
         *
         * Kick off 2 frames before nominal start.
         */
        Implementations.Get<Timeline>().RunAt(
            nogame.scenes.logos.Scene.TimepointTitlesongStarted, 
            TimeSpan.FromMilliseconds(9735 - 33f), _kickoffScene);
        
        lock (_lo)
        {
            --_isSettingUp;
        }
    }
}
