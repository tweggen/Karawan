using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace engine.joyce
{
    public class Material
    {
        public Texture Texture { get; set; } = null;
        public Texture EmissiveTexture { get; set; } = null;
        public uint AlbedoColor = 0x00000000;
        public uint EmissiveColor = 0x00000000;
        public uint EmissiveFactors = 0xffffffff;
        public bool HasTransparency = false;
        public bool IsBillboardTransform = false;
        public bool UploadImmediately = false;

        public string Name = "(unnamed)";
        
        public Material(in Texture texture) 
        { 
            Texture = texture;
        }

        public Material()
        {
        }
    }
}
