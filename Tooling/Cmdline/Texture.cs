using System.Collections.Generic;

namespace CmdLine
{
    public class Texture
    {
        public string Name;
        public SortedDictionary<string, Resource> Channels = new SortedDictionary<string, Resource>();
    }
}