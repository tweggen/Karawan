using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace engine.joyce.components
{
    public struct PointLight
    {
        public Vector3 Target;
        public Vector4 Color;
        public float Distance;
        public float CosOpening = -1f;

        public override string ToString()
        {
            return $"Target: {Target.ToString()}, Color: {Color.ToString()}, Distance: {Distance}, CosOpening: {CosOpening}";
        }


        public PointLight(in Vector4 color, float distance)
        {
            Target = -Vector3.UnitZ;
            Color = color;
            Distance = distance;
            /*
             * No constraint
             */
            CosOpening = -2f;
        }
        
        
        public PointLight(in Vector3 target, in Vector4 color, float distance, float cosOpening)
        {
            Target = Vector3.Normalize(target);
            Color = color;
            Distance = distance;
            CosOpening = cosOpening;
        }

    }
}
