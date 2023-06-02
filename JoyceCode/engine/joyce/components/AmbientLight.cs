using System.Numerics;

namespace engine.joyce.components
{
    public struct AmbientLight
    {
        public Vector4 Color;
        public AmbientLight(in Vector4 color) 
        { 
            Color = color;
        }
    }
}
