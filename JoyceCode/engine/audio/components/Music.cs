
namespace engine.audio.components
{
    public struct Music
    {
        public string Url;
        public bool IsPlaying;

        public override string ToString()
        {
            return $"{base.ToString()}, Url={Url}, IsPlaying={(IsPlaying?"true":"false")}";
        }

        public Music(in string url)
        { 
            Url = url;
            IsPlaying = false;
        }
    }
}
