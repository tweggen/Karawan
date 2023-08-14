using System.Numerics;

namespace engine.draw.components;

public struct OSDText
{
    public static uint ENABLE_DISTANCE_FADE = 0x00000001;

    public string Text;
    public uint FontSize;
    public uint TextColor;
    public uint FillColor;
    public Vector2 Position, Size;
    public HAlign HAlign;
    public uint OSDTextFlags = 0;
    public float MaxDistance = 200f;

    public OSDText(
        in Vector2 position, 
        in Vector2 size,
        in string text,
        uint fontSize,
        uint textColor,
        uint fillColor,
        HAlign hAlign)
    {
        Position = position;
        Size = size;
        Text = text;
        FontSize = fontSize;
        TextColor = textColor;
        FillColor = fillColor;
        HAlign = hAlign;
    }
}