using BepuPhysics;
using DefaultEcs;
using DefaultEcs.Resource;
using engine;
using Karawan.platform.cs1.splash.components;
using Raylib_CsLo;
using System;
using System.Collections.Generic;
using System.Text;

namespace Karawan.platform.cs1.splash
{
    internal class MusicManager : AResourceManager<engine.audio.Music, RlMusicEntry>
    {
        private object _lo = new object();
        private engine.Engine _engine;
        private System.Threading.Thread _audioThread;

        private List<RlMusicEntry> _rlMusicEntries = new();

        private void _audioFunction()
        {
            while (_engine.IsRunning())
            {
                List<RlMusicEntry> rlMusicEntries;
                lock (_lo)
                {
                    rlMusicEntries = _rlMusicEntries;
                }
                foreach (var rlMusicEntry in rlMusicEntries)
                {
                    Raylib_CsLo.Raylib.UpdateMusicStream(rlMusicEntry.RlMusic);
                }
                 System.Threading.Thread.Sleep(15);
            }
        }


        protected override unsafe RlMusicEntry Load(engine.audio.Music jMusic)
        {
            string resourcePath = _engine.GetConfigParam("Engine.ResourcePath");
            RlMusicEntry rlMusicEntry = new();
            rlMusicEntry.RlMusic = Raylib_CsLo.Raylib.LoadMusicStream(resourcePath+jMusic.Url);
            return rlMusicEntry;
        }

        protected override void OnResourceLoaded(in Entity entity, engine.audio.Music jMusic, RlMusicEntry rlMusicEntry)
        {
            entity.Set(new components.RlMusic(rlMusicEntry));
            Raylib_CsLo.Raylib.PlayMusicStream(rlMusicEntry.RlMusic);
            lock (_lo)
            {
                _rlMusicEntries.Add(rlMusicEntry);
            }
        }

        protected override unsafe void Unload(engine.audio.Music jMusic, RlMusicEntry rlMusicEntry)
        {
            lock (_lo)
            {
                _rlMusicEntries.Remove(rlMusicEntry);
            }
            Console.WriteLine($"MusicManager: Unloading Music");
            Raylib.UnloadMusicStream(rlMusicEntry.RlMusic);
            base.Unload(jMusic, rlMusicEntry);
        }

        public MusicManager(engine.Engine engine)
        {
            _engine = engine;
            _audioThread = new(_audioFunction);
            _audioThread.Start();
        }
    }
}
