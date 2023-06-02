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

        private DefaultEcs.Entity _eCamera;
        private DefaultEcs.Entity _eLightMain;
        private DefaultEcs.Entity _eLightBack;
        private DefaultEcs.Entity _eAmbientLight;

        private engine.world.Loader _worldLoader;
        private engine.world.MetaGen _worldMetaGen;

        private osd.Part _partOsd;
        private playerhover.Part _partPlayerhover;
        private skybox.Part _partSkybox;

        private systems.CubeSpinnerSystem _systemCubeSpinner;

        private void _triggerLoadWorld()
        {
            Vector3 vMe;
            if (!_eCamera.Has<Transform3ToWorld>())
            {
                vMe = new Vector3(0f, 0f, 0f);
            }
            else
            {
                vMe = _eCamera.Get<Transform3ToWorld>().Matrix.Translation;
            }
            _worldLoader.WorldLoaderProvideFragments(vMe);
        }

       
        
        public void SceneOnLogicalFrame( float dt )
        {
            _triggerLoadWorld();
        }


        public void SceneDeactivate()
        {
            _systemCubeSpinner.Dispose();
            _systemCubeSpinner = null;
            _partPlayerhover.PartDeactivate();
            _partPlayerhover = null;
            _partSkybox.PartDeactivate();
            _partSkybox = null;
            _partOsd.PartDeactivate();
            _partOsd = null;
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
             * Local state
             */

            _partOsd = new();
            _partPlayerhover = new();
            _partSkybox = new();

            _engine.SceneSequencer.AddScene(0, this);

            _partOsd.PartActivate(_engine, this);
            _partPlayerhover.PartActivate(_engine, this);
            _partSkybox.PartActivate(_engine, this);

            /*
             * Directional light
             */
            {
                _eLightMain = _ecsWorld.CreateEntity();
                _eLightMain.Set(new engine.joyce.components.DirectionalLight(new Vector4(1f, 1f, 1f, 0.0f)));
                _aTransform.SetRotation(_eLightMain, Quaternion.CreateFromAxisAngle(new Vector3(0, 0, -1), 45f * (float)Math.PI / 180f));
            }
            {
                _eLightBack = _ecsWorld.CreateEntity();
                _eLightBack.Set(new engine.joyce.components.DirectionalLight(new Vector4(0.1f, 0.3f, 0.1f, 0.0f)));
                _aTransform.SetRotation(_eLightBack, Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 180f * (float)Math.PI / 180f));
            }
            /*
             * Ambient light
             */
            {
                _eAmbientLight = _ecsWorld.CreateEntity();
                _eAmbientLight.Set(new engine.joyce.components.AmbientLight(new Vector4(0.01f, 0.01f, 0.01f, 0.0f)));
            }

            /*
             * Helper systems
             */
            _systemCubeSpinner = new(_engine);
              
            /*
             * Finally, create the camera.
             */
            /*
             * Create a camera.
             */
            {
                _eCamera = _ecsWorld.CreateEntity();
                var cCamera = new engine.joyce.components.Camera3();
                cCamera.Angle = 60.0f;
                cCamera.NearFrustum = 1f;

                /*
                 * We need to be as far away as the skycube is. Plus a bonus.
                 */
                cCamera.FarFrustum = (float)Math.Sqrt(3) * 1000f + 100f;
                cCamera.CameraMask = 0x00000001;
                _eCamera.Set<engine.joyce.components.Camera3>(cCamera);
                // No set position
                // _aTransform.SetPosition(_eCamera, new Vector3(0f, 30f, 30f));
            }

            /*
             * Create a camera controller that directly controls the camera with wasd
             */
            _ctrlFollowCamera = new(_engine, _eCamera, _partPlayerhover.GetShipEntity());
            _ctrlFollowCamera.ActivateController();
        }

        public RootScene()
        {
        }
    }
}
