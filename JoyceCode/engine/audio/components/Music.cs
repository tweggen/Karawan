using System;
using System.Collections.Generic;
using System.Text;

namespace engine.audio.components
{
    public struct Music
    {
        public string Url;

        public Music(in string url)
        { Url = url; }
    }
}
