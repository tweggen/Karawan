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
                var eCube = _ecsWorld.CreateEntity();
                joyce.Tools.AddCubeMesh(eCube);
                _aTransform.SetPosition(eCube, new Vector3(2f, 0f, 0f));
            }

            /*
             * Create a camera.
             */
            {
                var eCamera = _ecsWorld.CreateEntity();
                var cCamera = new joyce.components.Camera3();
                cCamera.Angle = 60.0f * (float)Math.PI / 360.0f;
                cCamera.NearFrustrum = 1f;
                cCamera.FarFrustrum = 100f;
                eCamera.Set<joyce.components.Camera3>(cCamera);
                _aTransform.SetPosition(eCamera, new Vector3(0f, 0f, 5f));
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
        }
    }
}
