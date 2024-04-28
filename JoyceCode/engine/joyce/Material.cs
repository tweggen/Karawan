using System;


namespace engine.joyce
{
    public class Material
    {
        private IdHolder _idHolder = new();

        public bool IsMergableEqual(Material o)
        {
            return (Flags&MaterialFlags.UnmergableFlags) == (o.Flags&MaterialFlags.UnmergableFlags)
                   && AlbedoColor == o.AlbedoColor
                   && EmissiveColor == o.EmissiveColor
                   && EmissiveFactors == o.EmissiveFactors
                   && FragmentShader == o.FragmentShader
                   && VertexShader == o.VertexShader
                   && (Texture == o.Texture
                       || Texture != null && Texture.IsMergableEqual(o.Texture))
                   && (EmissiveTexture == o.EmissiveTexture
                      || EmissiveTexture != null && EmissiveTexture.IsMergableEqual(o.EmissiveTexture));
        }
        
        public int GetMergableHashCode()
        {
            int h = (int)(Flags&MaterialFlags.UnmergableFlags) ^ (int)(AlbedoColor ^ EmissiveColor ^ EmissiveFactors);
            
            if (null != FragmentShader)
            {
                h ^= FragmentShader.GetHashCode();
            } 
            if (null != VertexShader)
            {
                h ^= VertexShader.GetHashCode();
            }
            if (null != EmissiveTexture)
            {
                h ^= EmissiveTexture.GetMergableHashCode();
            }
            if (null != Texture)
            {
                h ^= Texture.GetMergableHashCode();
            }
            
            return h;
        }
        
        /**
         * These flags are used in the shader.
         */
        [Flags] public enum ShaderFlags
        {
            RenderInterior = 0x00000001
        }

        [Flags] public enum MaterialFlags
        {
            /*
             * If any of the following flags differ, the materials cannot be
             * merged into the same draw calls. This is because they would change
             * the parameters of the shaders.
             */
            AddInterior = 0x00000001,
            HasTransparency = 0x00000008,
            UnmergableFlags = 0x00ffffff,
            
            /*
             * Draw calls with different values for any of the following parameters
             * can be merged into the same draw call, their values do not affect
             * the shader call.
             */
            UploadImmediately = 0x01000000,
            IsBillboardTransform = 0x02000000,
            IsUnscalable = 0x04000000
        }
        public MaterialFlags Flags = 0;
        
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
        
        public bool AddInterior
        {
            get => (Flags & MaterialFlags.AddInterior) != 0;
            set
            {
                if (value) Flags |= MaterialFlags.AddInterior;
                else Flags &= ~MaterialFlags.AddInterior;
            }
        }
        
        public bool IsBillboardTransform         
        {
            get => (Flags & MaterialFlags.IsBillboardTransform) != 0;
            set
            {
                if (value) Flags |= MaterialFlags.IsBillboardTransform;
                else Flags &= ~MaterialFlags.IsBillboardTransform;
            }
        }

        public bool IsUnscalable         
        {
            get => (Flags & MaterialFlags.IsUnscalable) != 0;
            set
            {
                if (value) Flags |= MaterialFlags.IsUnscalable;
                else Flags &= ~MaterialFlags.IsUnscalable;
            }
        }
        
        public bool HasTransparency         
        {
            get => (Flags & MaterialFlags.HasTransparency) != 0;
            set
            {
                if (value) Flags |= MaterialFlags.HasTransparency;
                else Flags &= ~MaterialFlags.HasTransparency;
            }
        }
        
        public bool UploadImmediately         
        {
            get => (Flags & MaterialFlags.UploadImmediately) != 0;
            set
            {
                if (value) Flags |= MaterialFlags.UploadImmediately;
                else Flags &= ~MaterialFlags.UploadImmediately;
            }
        }
        

        public string Name = "(unnamed)";

        public override string ToString()
        {
            return
                $"Texture: {{ {Texture} }}, EmissiveTexture: {{ {EmissiveTexture} }}, AlbedoColor: {AlbedoColor}, EmissiveColor: {EmissiveColor}, EmissiveFactors: {EmissiveFactors}, HasTransparency: {HasTransparency}, IsBillboardTransform: {IsBillboardTransform}, AddInterior: {AddInterior}";
        }
    }
}
