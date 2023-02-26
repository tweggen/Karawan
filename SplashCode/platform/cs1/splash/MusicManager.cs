using DefaultEcs;
using DefaultEcs.Resource;
using engine;
using Raylib_CsLo;
using System;
using System.Collections.Generic;
using System.Text;

namespace Karawan.platform.cs1.splash
{
    internal class MusicManager : AResourceManager<engine.audio.Music, RlMusicEntry>
    {
        engine.Engine _engine;
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
        }

        protected override unsafe void Unload(engine.audio.Music jMusic, RlMusicEntry rlMusicEntry)
        {
            Console.WriteLine($"MusicManager: Unloading Music");
            Raylib.UnloadMusicStream(rlMusicEntry.RlMusic);
            base.Unload(jMusic, rlMusicEntry);
        }

        public MusicManager(engine.Engine engine)
        {
            _engine = engine;
        }
    }
}
