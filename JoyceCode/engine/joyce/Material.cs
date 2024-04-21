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
         * These flags are used in the shader.
         */
        public enum Flags
        {
            RenderInterior = 0x00000001
        }
        
        /**
         * Specify the fragment shader to use.
         * If null, use the default fragment shader.
         */
        public string FragmentShader = null;
        
        /**
         * Specify the vertex shader to use.
         * If null, use the default vertex shader.
         */
        public string VertexShader = null;
        
        public Texture Texture { get; set; } = null;
        public Texture EmissiveTexture { get; set; } = null;
        
        public uint AlbedoColor = 0x00000000;
        public uint EmissiveColor = 0x00000000;
        public uint EmissiveFactors = 0xffffffff;
        
        public bool AddInterior = false;
        public bool IsBillboardTransform = false;
        public bool IsUnscalable = false;
        
        public bool HasTransparency = false;
        public bool UploadImmediately = false;

        public string Name = "(unnamed)";

        public override string ToString()
        {
            return
                $"Texture: {{ {Texture} }}, EmissiveTexture: {{ {EmissiveTexture} }}, AlbedoColor: {AlbedoColor}, EmissiveColor: {EmissiveColor}, EmissiveFactors: {EmissiveFactors}, HasTransparency: {HasTransparency}, IsBillboardTransform: {IsBillboardTransform}, AddInterior: {AddInterior}";
        }
    }
}
