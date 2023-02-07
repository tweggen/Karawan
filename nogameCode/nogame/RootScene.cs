using DefaultEcs;
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

        private builtin.controllers.WASDController _wasdController;

        private DefaultEcs.Entity _eCamera;

        private engine.world.Loader _worldLoader;
        private engine.world.MetaGen _worldMetaGen;

        public void SceneOnLogicalFrame( float dt )
        { 
        }

        public void SceneOnPhysicalFrame( float dt )
        {
            var vMe = _eCamera.Get<Transform3ToWorld>().Matrix.Translation;
            _worldLoader.WorldLoaderProvideFragments(vMe);
            _engine.Render3D();
        }

        public void SceneDeactivate()
        {
            _wasdController.DeactivateController();
            _wasdController = null;

            /*
             * Null out everything we don't need when the scene is unloaded.
             */
            _engine.RemoveScene(this);
        }

        public void SceneActivate(engine.Engine engine0)
        {
            _engine = engine0;

            _worldMetaGen = engine.world.MetaGen.Instance();
            _worldMetaGen.AddClusterFragmentOperatorFactory(
                (string newKey, engine.world.ClusterDesc clusterDesc) =>
                    new nogame.cities.GenerateHousesOperator(clusterDesc, newKey)
            );
            _worldMetaGen.AddClusterFragmentOperatorFactory(
                )
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
             * Some local shortcuts
             */
            _ecsWorld = _engine.GetEcsWorld();
            _aHierarchy = _engine.GetAHierarchy();
            _aTransform = _engine.GetATransform();

            /*
             * Local state
             */

            /*
             * Create a camera.
             */
            {
                _eCamera = _ecsWorld.CreateEntity();
                var cCamera = new engine.joyce.components.Camera3();
                cCamera.Angle = 60.0f;
                cCamera.NearFrustrum = 1f;
                cCamera.FarFrustrum = 1000f;
                cCamera.CameraMask = 0x00000001;
                _eCamera.Set<engine.joyce.components.Camera3>(cCamera);
                _aTransform.SetPosition(_eCamera, new Vector3(0f, 40f, 100f));
            }

            /*
             * Create a camera controller that directly controls the camera with wasd
             */
            _wasdController = new(_engine, _eCamera);
            _wasdController.ActivateController();

            _engine.AddScene(0, this);
        }

        public RootScene()
        {
        }
    }
}
