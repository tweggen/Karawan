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

        private DefaultEcs.Entity _eCubeNear;
        private DefaultEcs.Entity _eCubeFar;
        private DefaultEcs.Entity _eCubeParent;

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
                _eCubeParent = _ecsWorld.CreateEntity();
                _aTransform.SetPosition(_eCubeParent, new Vector3(0f, 0f, 0f));

                var jMesh = joyce.mesh.Tools.CreateCubeMesh();

                _eCubeNear = _ecsWorld.CreateEntity();
                _eCubeNear.Set<joyce.components.Instance3>(new joyce.components.Instance3(jMesh));
                _aTransform.SetPosition(_eCubeNear, new Vector3(2.5f, 0f, 0f));
                _aTransform.SetVisible(_eCubeNear, true);
                _aHierarchy.SetParent(_eCubeNear, _eCubeParent);

                _eCubeFar = _ecsWorld.CreateEntity();
                _eCubeFar.Set<joyce.components.Instance3>(new joyce.components.Instance3(jMesh));
                _aTransform.SetPosition(_eCubeFar, new Vector3(-1.5f, 0f, 0f));
                _aTransform.SetVisible(_eCubeFar, true);
                _aHierarchy.SetParent(_eCubeFar, _eCubeParent);

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
            if( true ) {
                var qNear = Quaternion.CreateFromAxisAngle(
                    Vector3.Normalize(new Vector3(0.2f, 1f, 0.4f)),
                    _testCount * (float)Math.PI / 180f);
                _aTransform.SetRotation(_eCubeNear, qNear);
            }
            if( true ) {
                var qParent = Quaternion.CreateFromAxisAngle(
                    Vector3.Normalize(new Vector3(2.1f, 0.2f, -1.4f)),
                    (50f+_testCount/2) * (float)Math.PI / 180f);
                _aTransform.SetRotation(_eCubeParent, qParent);
            }
            if( true )
            {
                if( 0==(_testCount&0x30) )
                {
                    _aTransform.SetVisible(_eCubeParent, false);
                } else
                {
                    _aTransform.SetVisible(_eCubeParent, true);
                }
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
