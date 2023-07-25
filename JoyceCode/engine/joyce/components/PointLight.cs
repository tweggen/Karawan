using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace engine.joyce.components
{
    public struct PointLight
    {
        public Vector4 Color;
        public float Distance;

        public PointLight(in Vector4 color, float distance)
        {
            Color = color;
            Distance = distance;
        }
    }
}
