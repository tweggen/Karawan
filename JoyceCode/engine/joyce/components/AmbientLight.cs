using System.Numerics;

namespace engine.joyce.components
{
    public struct AmbientLight
    {
        public Vector4 Color;

        public override string ToString()
        {
            return $"Color: {Color.ToString()}";
        }
        
        
        public AmbientLight(in Vector4 color) 
        { 
            Color = color;
        }
    }
}
