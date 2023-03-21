namespace Splash;

public abstract class ATextureEntry
{
    public engine.joyce.Texture JTexture;
    public abstract bool IsUploaded();

    public ATextureEntry(in engine.joyce.Texture jTexture)
    {
        JTexture = jTexture;
    }
}