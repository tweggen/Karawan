namespace Splash.components;

public struct PfRenderbuffer
{
    public engine.joyce.Renderbuffer Renderbuffer;
    public ARenderbufferEntry RenderbufferEntry;
    public PfRenderbuffer(engine.joyce.Renderbuffer renderbuffer)
    {
        Renderbuffer = renderbuffer;
    }
}