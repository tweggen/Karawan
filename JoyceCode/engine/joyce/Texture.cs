
using System.Numerics;

namespace engine.joyce
{
    /**
     * A texture can be used to render a mesh.
     * It can either be based on a sort of uri or an im memory buffer.
     */
    public class Texture
    {
        public static string BLACK = "joyce://col00000000"; 

        public string Source;
        public engine.draw.IFramebuffer Framebuffer;

        public enum FilteringModes
        {
            Smooth,
            
            /*
             * Nearest pixel within mipmap, mipmaps linearly blended.
             */
            Pixels,
            
            /*
             * No mipmap, strictest nearest sampling pixel output.
             */
            Framebuffer
        };

        public FilteringModes FilteringMode = FilteringModes.Pixels;
        public Vector2 UVOffset = new(0f, 0f);
        public Vector2 UVScale = new (1f, 1f);
        public int Width, Height;
        
        public Vector2 Size2
        {
            get => new Vector2(Width, Height); 
        }
        
        
        public Vector2 InvSize2
        {
            get => new Vector2(1f/Width, 1f/Height); 
        }
        

        public bool IsMergableEqual(Texture o)
        {
            return o != null 
                   && FilteringMode == o.FilteringMode
                   && Source == o.Source
                   && Framebuffer == o.Framebuffer;
        }
        
        
        public int GetMergableHashCode()
        {
            int h = 0;
            h += (int)FilteringMode<<13;

            if (Source != null)
            {
                h ^= 10;
                h ^= Source.GetHashCode();
            }

            if (Framebuffer != null)
            {
                h ^= 20;
                h ^= Framebuffer.GetHashCode();
            }
            
            /*
             * Do not consider UVOffset and UVScale
             */
            return h;
        }
        
        
        public override string ToString()
        {
            if (Source != null)
            {
                return $"Texture {{ Source: \"{Source}\", UVOffset: {UVOffset}, UVScale: {UVScale} }}";
            }
            else
            {
                return $"Texture {{ Width: {Framebuffer.Width}, Height: {Framebuffer.Height}, Generation: {Framebuffer.Generation} }}";
            }
        }

        
        public bool IsValid()
        {
            return 
                (Source != null && Source != "")
                || (Framebuffer != null);
        }
        
        
        public Texture(string source)
        {
            Source = source;
            Framebuffer = null;
        }
        

        public Texture(engine.draw.IFramebuffer framebuffer)
        {
            Source = null;
            Framebuffer = framebuffer;
        }
    }
}
