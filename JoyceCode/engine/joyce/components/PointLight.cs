using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace engine.joyce.components
{
    public class PointLight
    {
        public Vector4 Color;

        public PointLight(in Vector4 color)
        {
            Color = color;
        }
    }
}
