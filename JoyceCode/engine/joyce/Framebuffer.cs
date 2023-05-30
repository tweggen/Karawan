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
    private uint _sequenceNumber = 0;
    public uint SequenceNumber
    {
        get => _sequenceNumber;
    }

    public uint Width;
    public uint Height; 
    
    public Framebuffer(uint width, uint height)
    {
        Width = width;
        Height = height;
    }
}