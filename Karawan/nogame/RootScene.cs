using System;
using System.Numerics;
using Karawan.engine;

namespace Karawan.nogame
{
    public class RootScene : engine.IScene
    {
        private engine.Engine _engine;

        private DefaultEcs.World _ecsWorld;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private DefaultEcs.Entity _eCubeNear;
        private DefaultEcs.Entity _eCubeFar;
        private DefaultEcs.Entity _eCubeParent;

        private DefaultEcs.Entity _eCamera;

        private int _testCount;

        public void SceneOnLogicalFrame( float dt )
        {
            _testCount++;
            /*
             * Do test rotation
             */
            if (true)
            {
                var qNear = Quaternion.CreateFromAxisAngle(
                    Vector3.Normalize(new Vector3(0.2f, 1f, 0.4f)),
                    _testCount * (float)Math.PI / 180f);
                _aTransform.SetRotation(_eCubeNear, qNear);
            }
            if (true)
            {
                var qParent = Quaternion.CreateFromAxisAngle(
                    Vector3.Normalize(new Vector3(2.1f, 0.2f, -1.4f)),
                    (50f + _testCount / 2) * (float)Math.PI / 180f);
                _aTransform.SetRotation(_eCubeParent, qParent);
            }
            if (false)
            {
                if (0 == (_testCount & 0x30))
                {
                    _aTransform.SetVisible(_eCubeParent, false);
                }
                else
                {
                    _aTransform.SetVisible(_eCubeParent, true);
                }
            }
            else
            {
                _aTransform.SetVisible(_eCubeParent, true);
            }

        }

        public void SceneDeactivate()
        {
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
             * Create a cube positioned at 2/0/0
             */
            {
                _eCubeParent = _ecsWorld.CreateEntity();
                _aTransform.SetPosition(_eCubeParent, new Vector3(0f, 0f, 0f));

                var jMesh = engine.joyce.mesh.Tools.CreateCubeMesh();

                _eCubeNear = _ecsWorld.CreateEntity();
                _eCubeNear.Set<engine.joyce.components.Instance3>(new engine.joyce.components.Instance3(jMesh));
                _aTransform.SetPosition(_eCubeNear, new Vector3(2.5f, 0f, 0f));
                _aTransform.SetVisible(_eCubeNear, true);
                _aHierarchy.SetParent(_eCubeNear, _eCubeParent);

                _eCubeFar = _ecsWorld.CreateEntity();
                _eCubeFar.Set<engine.joyce.components.Instance3>(new engine.joyce.components.Instance3(jMesh));
                _aTransform.SetPosition(_eCubeFar, new Vector3(-1.5f, 0f, 0f));
                _aTransform.SetVisible(_eCubeFar, true);
                _aHierarchy.SetParent(_eCubeFar, _eCubeParent);

            }

            /*
             * Create a camera.
             */
            {
                _eCamera = _ecsWorld.CreateEntity();
                var cCamera = new engine.joyce.components.Camera3();
                cCamera.Angle = 60.0f;
                cCamera.NearFrustrum = 1f;
                cCamera.FarFrustrum = 100f;
                _eCamera.Set<engine.joyce.components.Camera3>(cCamera);
                _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 10f));
            }

            _engine.AddScene(this);

        }

        public RootScene()
        {
            _testCount = 0;
        }
    }
}
