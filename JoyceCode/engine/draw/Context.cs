namespace engine.draw;

public enum HAlign {
    Left,
    Center,
    Right
}

public enum VAlign
{
    Top,
    Center,
    Bottom
}

public class Context
{
    public uint Color;
    public uint FillColor;
    public uint TextColor;
    public uint ClearColor;
    public HAlign HAlign;
    public VAlign VAlign;
}