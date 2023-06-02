namespace engine.draw;

public class Context
{
    public uint Color;
    public byte ColorR
    {
        get => (byte)(Color & 0xff);
    } 
    public byte ColorG
    {
        get => (byte)((Color>>8) & 0xff);
    }
    public byte ColorB
    {
        get => (byte)((Color>>16) & 0xff);
    }

    public byte ColorA
    {
        get => (byte)((Color>>24) & 0xff);
    }


}