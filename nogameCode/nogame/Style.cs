namespace nogame;

public class Style
{
    public float FontSize { get; set; } = 12f;
    public uint FillColor { get; set; } = 0x00000000;
    public uint TextColor { get; set; } = 0xffffffff;
    public uint BorderColor { get; set; } = 0x00000000;
}

public class StyleBook
{
    public Style MenuStyle { get; set; } = new();
    public Style MenuFocusStyle { get; set; } = new();
    public Style MenuSelectedStyle { get; set; }
}