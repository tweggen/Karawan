using System.Numerics;
namespace Karawan.platform.cs1.splash;

/**
 * Light structure, as expected inside the shader.
 */
public struct Light
{

    public LightType type;
    public Vector3 position;
    public Vector3 target;
    public Vector4 color;
    public bool enabled;
}
    
// Light type
public enum LightType
{
    LIGHT_DIRECTIONAL = 0,
    LIGHT_POINT
}