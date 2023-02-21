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

        private builtin.controllers.FollowCameraController _ctrlFollowCamera;

        private DefaultEcs.Entity _eCamera;

        private engine.world.Loader _worldLoader;
        private engine.world.MetaGen _worldMetaGen;

        private playerhover.Part _partPlayerhover;
        private skybox.Part _partSkybox;

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

        public void SceneOnPhysicalFrame( float dt )
        {
        }

        public void SceneDeactivate()
        {
            _partPlayerhover.PartDeactivate();
            _partPlayerhover = null;
            _partSkybox.PartDeactivate();
            _partSkybox = null;
            _ctrlFollowCamera.DeactivateController();
            _ctrlFollowCamera = null;

            /*
             * Null out everything we don't need when the scene is unloaded.
             */
            _engine.RemoveScene(this);

            /*
             * Null out everything we don't need when the scene is unloaded.
             */
            _engine.RemoveScene(this);
            _ecsWorld = null;
            _aHierarchy = null;
            _aTransform = null;
            _engine = null;

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
                (string newKey, engine.world.ClusterDesc clusterDesc) =>
                    new nogame.cubes.GenerateCubeCharacterOperator(clusterDesc, newKey)
           );
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

            _partPlayerhover = new();
            _partSkybox = new();

            _engine.AddScene(0, this);

            _partPlayerhover.PartActivate(_engine, this);
            _partSkybox.PartActivate(_engine, this);

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
