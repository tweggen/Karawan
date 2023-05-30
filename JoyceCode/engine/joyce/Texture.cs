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
        public Framebuffer Framebuffer;

        public Texture(string source)
        {
            Source = source;
            Framebuffer = null;
        }

        public Texture(Framebuffer framebuffer)
        {
            Source = null;
            Framebuffer = framebuffer;
        }
    }
}
