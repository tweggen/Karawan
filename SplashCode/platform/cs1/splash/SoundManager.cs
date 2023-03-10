using System.Collections.Generic;
using System;

namespace Karawan.platform.cs1.splash
{

    public class SoundManager
    {
        private engine.Engine _engine;
        private WaveManager _waveManager;

        public void LoadSound(engine.audio.Sound jSound, Action<RlSoundEntry> actSound)
        {
            _waveManager.FindWave(jSound.Url, (RlWaveEntry rlWaveEntry) =>
            {
                // Nothing.
            });
        }

        public SoundManager(engine.Engine engine, WaveManager waveManager)
        {
            _engine = engine;
            _waveManager = waveManager;
        }
    }

}