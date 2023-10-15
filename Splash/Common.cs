using engine;

namespace Splash;

public class Common
{
    public Common()
    {
        I.Register<FragmentShaderManager>(() => new FragmentShaderManager());
        I.Register<TextureManager>(() => new TextureManager());
        I.Register<LightManager>(() => new LightManager());
        I.Register<InstanceManager>(() => new InstanceManager());
        I.Register<CameraManager>(() => new CameraManager());
        I.Register<LogicalRenderer>(() => new LogicalRenderer());
    }
}