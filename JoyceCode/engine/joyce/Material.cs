using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DefaultEcs;

namespace engine.joyce
{
    public class Material
    {
        /**
         * Specify the fragment shader to use.
         * If null, use the default fragment shader.
         */
        public AnyShader FragmentShader = null;
        /**
         * Specify the vertex shader to use.
         * If null, use the default vertex shader.
         */
        public AnyShader VertexShader = null;
        
        public Texture Texture { get; set; } = null;
        public Texture EmissiveTexture { get; set; } = null;
        
        public uint AlbedoColor = 0x00000000;
        public uint EmissiveColor = 0x00000000;
        public uint EmissiveFactors = 0xffffffff;
        public bool HasTransparency = false;
        public bool IsBillboardTransform = false;
        public bool UploadImmediately = false;

        public string Name = "(unnamed)";

        public bool IsSameAs(in Material other)
        {
            return true
                   && Texture == other.Texture
                   && EmissiveTexture == other.EmissiveTexture
                   && AlbedoColor == other.AlbedoColor
                   && EmissiveColor == other.EmissiveColor
                   && EmissiveFactors == other.EmissiveFactors
                   && HasTransparency == other.HasTransparency
                   && IsBillboardTransform == other.IsBillboardTransform
                   && UploadImmediately == other.UploadImmediately;
        }

        public override string ToString()
        {
            return
                $"Texture: {{ {Texture} }}, EmissiveTexture: {{ {EmissiveTexture} }}, AlbedoColor: {AlbedoColor}, EmissiveColor: {EmissiveColor}, EmissiveFactors: {EmissiveFactors}, HasTransparency: {HasTransparency}, IsBillboardTransform: {IsBillboardTransform}";
        }
        
        
        public Material(in Texture texture) 
        { 
            Texture = texture;
        }

        public Material()
        {
        }
    }
}
