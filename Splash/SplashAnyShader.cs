namespace Splash;

public class SplashAnyShader : engine.joyce.AnyShader
{
    public string Source { get; set; }
    public int CompareTo(object? o)
    {
        if (o == null) return 1;
        SplashAnyShader? a = o as SplashAnyShader;
        if (a == null) return 1;
        return System.String.Compare(Source, a.Source, System.StringComparison.Ordinal);
    }
    
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Source);
    }
}
