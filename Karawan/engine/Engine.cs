using System;
using System.Numerics;


namespace Karawan.engine
{
    class Engine
    {
        private DefaultEcs.World _ecsWorld;
        private engine.IPlatform _platform;


        private void _selfTest()
        {
            var aHierarchy = new engine.hierarchy.API( this );
            var aTransform = new engine.transform.API(this);

            /*
             * Create a simple hierarchy test case
             */
            {
                var eParent = _ecsWorld.CreateEntity();
                var eKid1 = _ecsWorld.CreateEntity();
                var eKid2 = _ecsWorld.CreateEntity();

                aHierarchy.SetParent(eKid1, eParent);
                aHierarchy.SetParent(eKid2, eParent);
                aHierarchy.Update();
                aHierarchy.SetParent(eKid1, null);
                aHierarchy.SetParent(eKid2, eKid1);
                aHierarchy.SetParent(eKid2, eParent);
            }

            /*
             * Create a cube positioned at 2/0/0
             */
            {
                var eCube = _ecsWorld.CreateEntity();
                joyce.Tools.AddCubeMesh(eCube);
                aTransform.SetPosition(eCube, new Vector3(2f, 0f, 0f));
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
                aTransform.SetPosition(eCamera, new Vector3(0f, 0f, 5f));
            }

        }

        public DefaultEcs.World GetEcsWorld()
        {
            return _ecsWorld;
        }


        public void OnPhysicalFrame(float dt)
        {
        }


        /**
         * Call after all dependencies are set.
         */
        public void SetupDone()
        {

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
