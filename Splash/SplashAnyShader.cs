namespace Splash;

public class SplashAnyShader : engine.joyce.AnyShader
{
    public string Source { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Source);
    }
}