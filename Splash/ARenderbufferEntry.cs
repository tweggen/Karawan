namespace Splash;

public abstract class ARenderbufferEntry
{
    readonly public engine.joyce.Renderbuffer JRenderbuffer;
    abstract public bool IsUploaded();
    
    public ARenderbufferEntry(in engine.joyce.Renderbuffer jRenderbuffer)
    {
        JRenderbuffer = jRenderbuffer;
    }
        
}