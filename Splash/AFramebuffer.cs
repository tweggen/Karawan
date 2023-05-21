namespace Splash;

public abstract class AFramebuffer
{
    readonly public engine.joyce.Framebuffer JFramebuffer;
    abstract public bool IsUploaded();
    
    public AFramebuffer(in engine.joyce.Framebuffer jFramebuffer)
    {
        JFramebuffer = jFramebuffer;
    }
        
}