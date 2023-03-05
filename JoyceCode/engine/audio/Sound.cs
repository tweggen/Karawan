
namespace engine.audio
{
    public class Sound
    {
        public string Url;
        public float Volume;
        public float Pitch;
        public bool PlayLooped;

        public Sound(in string url, in bool playLooped, in float volume, in float pitch)
        {
            Url = url;
            Pitch = pitch;
            Volume = volume;
            PlayLooped = playLooped;
        }
    }
}
