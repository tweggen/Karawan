using System;
using System.Numerics;
using static engine.Logger;

namespace engine;

public struct Color
{
    private static byte _b(float f) => (byte)(Single.Clamp(f, 0f, 1f) * 255f);
    private static string _s(float f) => $"{_b(f):X2}";

    static public string Vector4ToString(in Vector4 v) => $"#{_s(v.W)}{_s(v.X)}{_s(v.Y)}{_s(v.Z)}";

    static public Vector4 StringToVector4(in string color)
    {
        // Remove the '#' character if present
        if (!color.StartsWith("#"))
        {
            ErrorThrow<ArgumentException>($"Invalid color string format: {color}");
        }

        byte a = byte.Parse(color.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
        byte r = byte.Parse(color.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(color.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(color.Substring(7, 2), System.Globalization.NumberStyles.HexNumber);

        // Convert to Vector4
        return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
    }


    static public uint StringToUInt(in string color)
    {
        // Remove the '#' character if present
        if (!color.StartsWith("#"))
        {
            ErrorThrow<ArgumentException>($"Invalid color string format: {color}");
        }

        byte a = byte.Parse(color.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
        byte r = byte.Parse(color.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(color.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(color.Substring(7, 2), System.Globalization.NumberStyles.HexNumber);

        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)r;
    }


    static public uint Vector4ToUint(in Vector4 v)
    {
        byte a = _b(v.W);        
        byte r = _b(v.X);        
        byte g = _b(v.Y);        
        byte b = _b(v.Z);        
        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)r;
    }
    
    
    static public uint Vector3ToUint(in Vector3 v)
    {
        byte a = 255;   
        byte r = _b(v.X);        
        byte g = _b(v.Y);        
        byte b = _b(v.Z);        
        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)r;
    }
}