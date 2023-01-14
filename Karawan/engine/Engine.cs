

namespace Karawan.engine
{
    class Engine
    {
        private DefaultEcs.World _ecsWorld;
        private engine.IPlatform _platform;

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

        }

        public Engine( engine.IPlatform platform )
        {
            _platform = platform;
            _ecsWorld = new DefaultEcs.World();
        }
    }
}
