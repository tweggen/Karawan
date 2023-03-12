#if SPLASH_AUDIO
namespace Karawan.platform.cs1.splash.components
{

    public struct RlSound
    {
        public RlSoundEntry SoundEntry;

        public RlSound(RlSoundEntry soundEntry)
        {
            SoundEntry = soundEntry;
        }
    }
}
#endif