using System.Collections.Generic;
using System;

namespace Karawan.platform.cs1.splash
{

    /**
     * We implement an audio stream for every sample.
     */
    public class SoundManager
    {
        private object _lo = new();
        private engine.Engine _engine;
        private WaveManager _waveManager;

        private List<RlSoundEntry> _listRlSoundEntries;

        public void LoadSound(engine.audio.Sound jSound, Action<RlSoundEntry> actSound)
        {
            _waveManager.FindWave(jSound.Url, (RlWaveEntry rlWaveEntry) =>
            {
                RlSoundEntry rlSoundEntry = new();
                rlSoundEntry.RlSound = Raylib_CsLo.Raylib.LoadSoundFromWave(rlWaveEntry.RlWave);
                // We do not unload the wave because we re-use the buffer.
                // TXWTODO: Unreference wave in case we do not want to cache everything.
                actSound(rlSoundEntry);
            });
        }

        public void StartSound(RlSoundEntry rlSoundEntry)
        {
            Raylib_CsLo.AudioStream rlAudioStream;
            Raylib_CsLo.Raylib.PlaySoundMulti(rlSoundEntry.RlSound);
            lock(_lo)
            {
                _listRlSoundEntries.Add(rlSoundEntry);
            }
        }

        public void StopSound(RlSoundEntry rlSoundEntry)
        {
            // ... we can't stop multi sound.
            // So we unfortunately need that to be done by a background thread.
        }

        public void UnloadSound(RlSoundEntry rlSoundEntry)
        {
            // ...needs to be done in the background.   
        }

        public void Update()
        {
            List<RlSoundEntry> deadSounds = new();
            lock(_lo)
            {
                _listRlSoundEntries.RemoveAll((RlSoundEntry rlSoundEntry) =>
                {
                    if (rlSoundEntry.IsEmpty())
                    {
                        return false;
                    }
                    if (Raylib_CsLo.Raylib.IsSoundPlaying(rlSoundEntry.RlSound))
                    {
                        return false;
                    }
                    deadSounds.Add(rlSoundEntry);
                    return true;
                });
            }
            foreach(var rlSoundEntry in deadSounds)
            {
                Raylib_CsLo.Raylib.UnloadSound(rlSoundEntry.RlSound);
                rlSoundEntry.RlSound.frameCount = 0;
            }
        }

        public SoundManager(engine.Engine engine, WaveManager waveManager)
        {
            _engine = engine;
            _waveManager = waveManager;
        }
    }

}