using System.Numerics;

namespace engine;


public class ModelInfo
{
    public override string? ToString()
    {
        return $"ModelInfo {{ AABB: {AABB}, Center: {Center} }}";
    }
    public engine.geom.AABB AABB = new();
    public Vector3 Center = new();
}
