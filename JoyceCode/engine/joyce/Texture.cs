
using System.Numerics;

namespace engine.joyce
{
    /**
     * A texture can be used to render a mesh.
     * It can either be based on a sort of uri or an im memory buffer.
     */
    public class Texture
    {
        public string Source;
        public engine.draw.IFramebuffer Framebuffer;

        public bool DoFilter = true;
        public Vector2 UVOffset = new(0f, 0f);
        public Vector2 UVScale = new (1f, 1f);


        public bool IsMergableEqual(Texture o)
        {
            return DoFilter == o.DoFilter
                   && Source == o.Source
                   && Framebuffer == o.Framebuffer;
        }
        
        
        public int GetMergableHashCode()
        {
            int h = 0;
            if (DoFilter) h ^= 1;

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
                return $"Texture {{ Source: \"{Source}\" }}";
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
