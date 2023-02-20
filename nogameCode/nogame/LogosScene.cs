using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace nogame
{
    public class LogosScene : engine.IScene
    {
        engine.Engine _engine;

        private DefaultEcs.World _ecsWorld;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private DefaultEcs.Entity _eCamera;

        public void SceneOnLogicalFrame(float dt)
        {
        }

        public void SceneOnPhysicalFrame(float dt)
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
            /*
             * Some local shortcuts
             */
            _ecsWorld = _engine.GetEcsWorld();
            _aHierarchy = _engine.GetAHierarchy();
            _aTransform = _engine.GetATransform();

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
                _aTransform.SetPosition(_eCamera, new Vector3(0f, 30f, 30f));
            }


            _engine.AddScene(5, this);

        }

        public LogosScene()
        {
        }
    }
}