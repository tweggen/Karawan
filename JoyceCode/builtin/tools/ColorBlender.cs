using System.Collections.Generic;
using System.Numerics;

namespace builtin.tools;


public class ColorBlender
{

    public LowerBoundsSortedDictionary<float, Vector4> MapColors = new();
    
    private void _colorBlend(float a, float b, float x, Vector4 va, Vector4 vb, out Vector4 v4Color)
    {
        float d = b - a;
        if (0 == d)
        {
            v4Color = vb;
        }
        else
        {
            x -= a;
            x /= b - a;
            v4Color = vb * x + va * (1f - x);
        }
    }

    public void GetColor(float x, out Vector4 v4Blend)
    {
        MapColors.FindBounds(x, out var lower, out var upper);
        _colorBlend(lower, upper, x, MapColors[lower], MapColors[upper], out v4Blend);
    }
    
    public void GetColor(float x, out uint col)
    {
        MapColors.FindBounds(x, out var lower, out var upper);
        _colorBlend(lower, upper, x, MapColors[lower], MapColors[upper], out var v4Blend);
        col = ((uint)((byte)(v4Blend.W * 255)) << 24) 
              | ((uint)((byte)(v4Blend.Z * 255)) << 16) 
              | ((uint)((byte)(v4Blend.Y * 255)) << 8)
              | ((uint)((byte)(v4Blend.X * 255)) << 0);
    }
    
}