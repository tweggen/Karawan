

using engine;

namespace Boom
{
    public class API
    {
        private engine.Engine _engine;

        private void _onLogicalFrame(object sender, float dt)
        {
        }

        public void SetupDone()
        {
            _engine.LogicalFrame += _onLogicalFrame;
        }

        public API(Engine engine)
        {
            _engine = engine;
        }
    }
}