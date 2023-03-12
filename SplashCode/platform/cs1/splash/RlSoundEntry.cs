#if SPLASH_AUDIO
namespace Karawan.platform.cs1.splash {

    /**
     * Represent a raylib sound object. Raylib sound objects do contain a copy
     * of the samples loaded. So these are just the instances that are set up
     * by the sound manager.
     */
    public class RlSoundEntry
    {
        public Raylib_CsLo.Sound RlSound;
        public bool IsEmpty()
        {
            return RlSound.frameCount == 0;
        }
    }
}
#endif