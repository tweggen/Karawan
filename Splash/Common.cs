using engine;

namespace Splash;


/**
 * Gather common functionality for all Splash renderers
 */
public class Common
{
    /**
     * Add all IoC factories for the splash renderer.
     */
    public Common()
    {
        I.Register<ShaderManager>(() => new ShaderManager());
        I.Register<TextureManager>(() => new TextureManager());
        I.Register<InstanceManager>(() => new InstanceManager());
        I.Register<CameraManager>(() => new CameraManager());
        I.Register<LogicalRenderer>(() => new LogicalRenderer());
    }
}