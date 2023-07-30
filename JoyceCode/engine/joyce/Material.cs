using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace engine.joyce
{
    public class Material
    {
        public Texture Texture { get; set; }
        public Texture EmissiveTexture { get; set; }
        public uint AlbedoColor;
        public uint EmissiveColor;
        public bool HasTransparency;
        public bool IsBillboardTransform = false;
        public bool UploadImmediately = false;

        public string Name;
        
        public Material(in Texture texture) 
        { 
            Texture = texture;
        }

        public Material()
        {
            Texture = null;
            EmissiveTexture = null;
            AlbedoColor = 0x00000000;
            HasTransparency = false;
        }
    }
}
