using System.Numerics;
namespace Splash;

/**
 * Light structure, as expected inside the shader.
 */
public struct Light
{

    public LightType type;
    public Vector3 position;
    public Vector3 target;
    public Vector4 color;
    /**
     * Opening for directional light source.
     */
    public float param1;
    public float param2;
    public bool enabled;
}
    
// Light type
public enum LightType
{
    LIGHT_DIRECTIONAL = 0,
    LIGHT_POINT
}