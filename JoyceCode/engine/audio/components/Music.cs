
namespace engine.audio.components
{
    public struct Music
    {
        public string Url;
        public bool IsPlaying;

        public Music(in string url)
        { 
            Url = url;
            IsPlaying = false;
        }
    }
}
