#if SPLASH_AUDIO
using System.Collections.Generic;
using System;
using Raylib_CsLo;

namespace Karawan.platform.cs1.splash
{

    public class WaveManager
    {
        private readonly engine.Engine _engine;
        
        private object _lo = new();
        private readonly SortedDictionary<string, RlWaveEntry> _dictSounds = new();

        /**
         * Obtain a particular wave, passing it to the action.
         */
        public void FindWave(in string url, Action<RlWaveEntry> actWave)
        {
            RlWaveEntry rlWaveEntry;
            
            /*
             * TXWTODO: That's a very inefficient way of loading.
             */
            lock (_lo)
            {
                if (_dictSounds.ContainsKey(url))
                {
                    rlWaveEntry = _dictSounds[url];
                }
                else
                {
                    rlWaveEntry = new RlWaveEntry();
                    
                    string resourcePath = _engine.GetConfigParam("Engine.ResourcePath");
                    string path = resourcePath + url;
                    Raylib_CsLo.Raylib.LoadWave(path);
                    _dictSounds[url] = rlWaveEntry;
                }
            }

            actWave(rlWaveEntry);
        }
        
        public WaveManager(engine.Engine engine)
        {
            _engine = engine;
        }
    }
}
#endif