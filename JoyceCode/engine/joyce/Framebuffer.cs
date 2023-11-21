#if false
namespace engine.joyce;


/**
 * Represent an in-CPU-memory buffer for a 2d image.
 * This can be
 * - the target of a texture download
 * - the target of 2d rendering
 * - the source of a texture
 */
public class Framebuffer
{
    public uint Width;
    public uint Height; 
    
    public Framebuffer(uint width, uint height)
    {
        Width = width;
        Height = height;
    }
}
#endif
<