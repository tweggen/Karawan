

using Boom.systems;
using BoomCode.systems;
using engine;

namespace Boom
{
    public class API
    {
        private engine.Engine _engine;
        private CreateMusicSystem _createMusicSystem;
        private AudioPlaybackEngine _audioPlaybackEngine;
        private systems.UpdateMovingSoundSystem _updateMovingSoundsSystem;

        private void _onLogicalFrame(object sender, float dt)
        {
            _createMusicSystem.Update(dt);
            _updateMovingSoundsSystem.Update(dt);
        }

        public void SetupDone()
        {
            _engine.LogicalFrame += _onLogicalFrame;
        }

        public API(Engine engine)
        {
            _engine = engine;
            _createMusicSystem = new(engine);
            _updateMovingSoundsSystem = new UpdateMovingSoundSystem(engine);
        }
    }
}