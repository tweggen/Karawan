using System.Numerics;

namespace Splash;

public abstract class ATextureEntry
{
    public engine.joyce.Texture JTexture;

    public enum ResourceState
    {
        Created,
        Loading,
        Uploading,
        Using,
        Outdated,
        Dead
    }
    
    /**
     * UV scale is valid after the texture is filled.
     */
    public Vector2 v2ScaleUV;
    public abstract ResourceState State { get; }

    public bool IsUploaded()
    {
        return State >= ResourceState.Using;
    }

    public bool IsOutdated()
    {
        return State == ResourceState.Outdated;
    }

    public ATextureEntry(in engine.joyce.Texture jTexture)
    {
        JTexture = jTexture;
    }
}