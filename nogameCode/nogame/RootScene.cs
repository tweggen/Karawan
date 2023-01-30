using DefaultEcs;
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

        private DefaultEcs.Entity _eCamera;

        private engine.world.Loader _worldLoader;
        private engine.world.MetaGen _worldMetaGen;

        private Vector3 _vMe;

        private int _tickCounter;

        public void SceneOnLogicalFrame( float dt )
        { 
            ++_tickCounter;

            var q = Quaternion.CreateFromAxisAngle(
                    new Vector3(0f, 1f, 0f),
                    (float)_tickCounter / 60f * 30f * (float)Math.PI / 180f);
            // Console.WriteLine($"rot {q}");
            _aTransform.SetRotation(
                _eCamera, q);
        }

        public void SceneOnPhysicalFrame( float dt )
        {
            _engine.Render3D();
        }

        public void SceneDeactivate()
        {
            /*
             * Null out everything we don't need when the scene is unloaded.
             */
            _engine.RemoveScene(this);
        }

        public void SceneActivate(engine.Engine engine0)
        {
            _engine = engine0;

            _vMe = new Vector3(0f, 0f, 0f);
            _worldMetaGen = engine.world.MetaGen.Instance();
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
                _aTransform.SetPosition(_eCamera, new Vector3(0f, 100f, 100f));
            }

            _worldLoader.WorldLoaderProvideFragments(_vMe);

            _engine.AddScene(0, this);
        }

        public RootScene()
        {
        }
    }
}
