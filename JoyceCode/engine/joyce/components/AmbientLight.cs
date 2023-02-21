using System.Numerics;

namespace engine.joyce.components
{
    public class AmbientLight
    {
        public Vector4 Color;
        public AmbientLight(in Vector4 color) 
        { 
            Color = color;
        }
    }
}
