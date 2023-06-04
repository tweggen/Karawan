using System.Numerics;

namespace engine.draw.components;

public struct OSDText
{
    public string Text;
    public uint FontSize;
    public uint TextColor;
    public uint FillColor;
    public Vector2 Position, Size;

    public OSDText(in Vector2 position, in Vector2 size, in string text, uint fontSize, uint textColor, uint fillColor)
    {
        Position = position;
        Size = size;
        Text = text;
        FontSize = fontSize;
        TextColor = textColor;
        FillColor = fillColor;
    }
}