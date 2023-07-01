
namespace engine.audio
{
    public class Sound
    {
        public string Url;
        public float Volume;
        public float Pitch;
        public bool IsLooped;

        public Sound(in string url, in bool isLooped, in float volume, in float pitch)
        {
            Url = url;
            Pitch = pitch;
            Volume = volume;
            IsLooped = isLooped;
        }
    }
}
