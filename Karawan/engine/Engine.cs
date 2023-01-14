

namespace Karawan.engine
{
    class Engine
    {
        private DefaultEcs.World _ecsWorld;
        private engine.IPlatform _platform;

        public Engine( engine.IPlatform platform )
        {
            _platform = platform;
            _ecsWorld = new DefaultEcs.World();
        }
    }
}
