

namespace Karawan.engine
{
    class Engine
    {
        private DefaultEcs.World _ecsWorld;
        private engine.IPlatform _platform;


        private void _selfTest()
        {
            var aHierarchy = new engine.hierarchy.API( this );

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
