using System.Numerics;

namespace engine.joyce.components
{
    /**
     * A directional light always points to positive X direction (1,0,0)
     * It requires a transformation to be guided properly.
     */
    public class DirectionalLight
    {
        public Vector4 Color;

        
        public override string ToString()
        {
            return $"Color: {Color.ToString()}";
        }
        
        
        public DirectionalLight(in Vector4 color)
        {
            Color = color;
        }
    }
}
