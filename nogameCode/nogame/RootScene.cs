using engine.transform.components;
using System;
using System.Numerics;

namespace nogame
{
    public class RootScene : engine.IScene
    {
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

        private nogame.parts.osd.Part _partOsd;
        private nogame.parts.playerhover.Part _partPlayerhover;
        private nogame.parts.skybox.Part _partSkybox;
        
        
        private void _triggerLoadWorld()
        {
            Vector3 vMe;
            if (!_eCamScene.Has<Transform3ToWorld>())
            {
                vMe = new Vector3(0f, 0f, 0f);
            }
            else
            {
                vMe = _eCamScene.Get<Transform3ToWorld>().Matrix.Translation;
            }
            _worldLoader.WorldLoaderProvideFragments(vMe);
        }
        
        
        public void SceneOnLogicalFrame( float dt )
        {
            _triggerLoadWorld();
        }


        public void SceneDeactivate()
        {
            _partPlayerhover.PartDeactivate();
            _partPlayerhover = null;
            _partSkybox.PartDeactivate();
            _partSkybox = null;
            if (engine.GlobalSettings.Get("nogame.CreateOSD") != "false")
            {
                _partOsd.PartDeactivate();
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
            _engine = engine0;

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
                _eLightMain.Set(new engine.joyce.components.DirectionalLight(new Vector4(1f, 1f, 1f, 0.0f)));
                _aTransform.SetRotation(_eLightMain, Quaternion.CreateFromAxisAngle(new Vector3(0, 0, -1), 45f * (float)Math.PI / 180f));
            }
            {
                _eLightBack = _engine.CreateEntity("RootScene.OtherLight");
                _eLightBack.Set(new engine.joyce.components.DirectionalLight(new Vector4(0.1f, 0.3f, 0.1f, 0.0f)));
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
             */
            {
                _eCamScene = _engine.CreateEntity("RootScene.SceneCamera");
                var cCamScene = new engine.joyce.components.Camera3();
                cCamScene.Angle = 60.0f;
                cCamScene.NearFrustum = 1f;

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
             */
            {
                _eCamOSD = _engine.CreateEntity("RootScene.OSDCamera");
                var cCamOSD = new engine.joyce.components.Camera3();
                cCamOSD.Angle = 60.0f;
                cCamOSD.NearFrustum = 1f;
                cCamOSD.FarFrustum = 100f;
                cCamOSD.CameraMask = 0x00010000;
                _eCamOSD.Set(cCamOSD);
                _aTransform.SetPosition(_eCamOSD, new Vector3(0f, 0f, 14f));
            }

            if (true)
            {
                _partPlayerhover = new();
                _partPlayerhover.PartActivate(_engine, this);
            }


            /*
             * Create a camera controller that directly controls the camera with wasd,
             * requires the playerhover.
             */
            _ctrlFollowCamera = new(_engine, _eCamScene, _partPlayerhover.GetShipEntity());
            _ctrlFollowCamera.ActivateController();

            if (engine.GlobalSettings.Get("nogame.CreateSkybox") != "false") {
                _partSkybox = new();
                _partSkybox.PartActivate(_engine, this);
            }

            if (engine.GlobalSettings.Get("nogame.CreateOSD") != "false") { 
                _partOsd = new();
                _partOsd.PartActivate(_engine, this);
            }

            /*
             * Now, that everything has been created, add the scene.
             */
            _engine.SceneSequencer.AddScene(0, this);

        }

        public RootScene()
        {
        }
    }
}
