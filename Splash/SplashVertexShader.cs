using ObjLoader.Loader.Common;

namespace Splash;

public class SplashVertexShader : engine.joyce.VertexShader
{
    public string Source { get; set; }

    public bool IsValid()
    {
        return !Source.IsNullOrEmpty();
    }
}