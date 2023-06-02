using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
