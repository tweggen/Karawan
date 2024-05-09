using System.Collections.Generic;

namespace CmdLine
{
    public class TextureSection
    {
        public SortedDictionary<string, Channel> Channels = new SortedDictionary<string, Channel>();
        public SortedDictionary<string, Texture> Textures = new SortedDictionary<string, Texture>();
    }
}