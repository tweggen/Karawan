namespace Karawan.platform.cs1.splash
{

    public class RlWaveEntry
    {
        public Raylib_CsLo.Wave RlWave;

        public bool HasRlWave()
        {
            return RlWave.channels != 0;
        }
        
        public RlWaveEntry()
        {
            RlWave = new Raylib_CsLo.Wave();
        }
    }
}