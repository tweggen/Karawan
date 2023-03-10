namespace Karawan.platform.cs1.splash
{

    public class RlWaveEntry
    {
        public Raylib_CsLo.Wave RlWave;
        public engine.audio.Sound JSound;

        public bool HasRlWave()
        {
            return RlWave.channels != 0;
        }
        
        public RlWaveEntry(in engine.audio.Sound jSound)
        {
            JSound = jSound;
            RlWave = new Raylib_CsLo.Wave();
        }
    }
}