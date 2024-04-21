using System.Numerics;

namespace Splash;

public abstract class ATextureEntry
{
    public engine.joyce.Texture JTexture;
    public Vector2 v2ScaleUV;
    public abstract bool IsUploaded();
    public abstract bool IsOutdated();

    public ATextureEntry(in engine.joyce.Texture jTexture)
    {
        JTexture = jTexture;
    }
}