using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace engine.joyce
{
    public class Material
    {
        public Texture AmbientTexture { get; set; }
        public Texture Texture { get; set; }

        public uint AlbedoColor;

        public Material(in Texture texture) 
        { 
            Texture = texture;
        }

        public Material()
        {
            AlbedoColor = 0xffffff;
            Texture = null;
        }
    }
}
