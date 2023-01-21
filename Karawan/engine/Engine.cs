using System;
using System.Numerics;


namespace Karawan.engine
{
    class Engine
    {
        private DefaultEcs.World _ecsWorld;
        private engine.IPlatform _platform;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private DefaultEcs.Entity _eCube;
        private DefaultEcs.Entity _eCamera;

        private int _testCount;

        private void _selfTest()
        {
            /*
             * Create a simple hierarchy test case
             */
            {
                var eParent = _ecsWorld.CreateEntity();
                var eKid1 = _ecsWorld.CreateEntity();
                var eKid2 = _ecsWorld.CreateEntity();

                _aHierarchy.SetParent(eKid1, eParent);
                _aHierarchy.SetParent(eKid2, eParent);
                _aHierarchy.Update();
                _aHierarchy.SetParent(eKid1, null);
                _aHierarchy.SetParent(eKid2, eKid1);
                _aHierarchy.SetParent(eKid2, eParent);
            }

            /*
             * Create a cube positioned at 2/0/0
             */
            {
                _eCube = _ecsWorld.CreateEntity();
                var jMesh = joyce.mesh.Tools.CreateCubeMesh();
                _eCube.Set<joyce.components.Instance3>(new joyce.components.Instance3(jMesh));
                _aTransform.SetPosition(_eCube, new Vector3(0.1f, 0f, 0f));
            }

            /*
             * Create a camera.
             */
            {
                _eCamera = _ecsWorld.CreateEntity();
                var cCamera = new joyce.components.Camera3();
                cCamera.Angle = 60.0f; 
                cCamera.NearFrustrum = 1f;
                cCamera.FarFrustrum = 100f;
                _eCamera.Set<joyce.components.Camera3>(cCamera);
                _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 10f));
            }

        }


        public engine.hierarchy.API GetAHierarchy()
        {
            return _aHierarchy;
        }

        public engine.transform.API GetATransform()
        {
            return _aTransform;
        }

        public DefaultEcs.World GetEcsWorld()
        {
            return _ecsWorld;
        }


        public void OnPhysicalFrame(float dt)
        {
            _testCount++;
            /*
             * Do test rotation
             */
            {
                var q = Quaternion.CreateFromAxisAngle(
                    Vector3.Normalize(new Vector3(0.2f, 1f, 0.4f)),
                    _testCount * (float)Math.PI / 180f);
                _aTransform.SetRotation(_eCube, q);
            }
            _aHierarchy.Update();
            _aTransform.Update();
        }

         
        /**
         * Call after all dependencies are set.
         */
        public void SetupDone()
        {
            _aHierarchy = new engine.hierarchy.API(this);
            _aTransform = new engine.transform.API(this);
        }


        public void PlatformSetupDone()
        {
            _selfTest();
        }


        public Engine( engine.IPlatform platform )
        {
            _platform = platform;
            _ecsWorld = new DefaultEcs.World();
            _testCount = 0;            
        }
    }
}
