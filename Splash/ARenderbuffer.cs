namespace Splash;

public abstract class ARenderbuffer
{
    readonly public engine.joyce.Renderbuffer JRenderbuffer;
    abstract public bool IsUploaded();
    
    public ARenderbuffer(in engine.joyce.Renderbuffer jRenderbuffer)
    {
        JRenderbuffer = jRenderbuffer;
    }
        
}