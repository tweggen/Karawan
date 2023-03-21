namespace Splash;

public interface ITextureGenerator
{
    public ATextureEntry CreatePlatformTexture(in engine.joyce.Texture jTexture);
}