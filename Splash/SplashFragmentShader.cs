using ObjLoader.Loader.Common;

namespace Splash;

public class SplashFragmentShader : engine.joyce.AnyShader
{
    public string Source { get; set; }

    public bool IsValid()
    {
        return !Source.IsNullOrEmpty();
    }
}