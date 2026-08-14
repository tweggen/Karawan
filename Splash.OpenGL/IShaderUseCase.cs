namespace Splash.OpenGL;


public class ShaderLocs
{
}

public interface IShaderUseCase
{
    public string Name { get; }
    public ShaderLocs Compile(SkProgramEntry sh);
}
