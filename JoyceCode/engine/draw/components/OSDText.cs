using System.Numerics;

namespace engine.draw.components;

public struct OSDText
{
    public const ushort ENABLE_DISTANCE_FADE = 0x00000001;
    public const ushort GAUGE_TYPE_MASK = 0x00000070;
    public const ushort GAUGE_TYPE_STANDARD = 0x00000000;
    public const ushort GAUGE_TYPE_INSERT = 0x00000010;

    public string Text;
    public uint FontSize;
    public uint TextColor;
    public uint FillColor;
    public uint BorderColor;
    public uint GaugeColor;
    public Vector2 Position, Size;
    public Vector2 ScreenPos;
    public HAlign HAlign;
    public VAlign VAlign;
    public ushort OSDTextFlags = 0;
    public ushort GaugeValue;
    public float MaxDistance = 200f;

    public override string ToString()
    {
        return
            $"Text: {Text}, FontSize: {FontSize}, TextColor: {TextColor}, FillColor: {FillColor}, Position: {Position}, Size: {Size}, HAlign: {HAlign}, OSDTextFlags: {OSDTextFlags:X}, MaxDistance: {MaxDistance};";
    }
    
    public OSDText(
        in Vector2 position, 
        in Vector2 size,
        in string text,
        uint fontSize,
        uint textColor,
        uint fillColor,
        HAlign hAlign,
        VAlign vAlign = VAlign.Top)
    {
        Position = position;
        Size = size;
        Text = text;
        FontSize = fontSize;
        TextColor = textColor;
        FillColor = fillColor;
        HAlign = hAlign;
        VAlign = vAlign;
    }
}