namespace engine.world.components;

public struct MapIcon
{
    // TXWTODO: Why is this not game specific?
    public enum IconCode : int
    {
        None = 0,
        Player0 = 1,
        Game2 = 2,
        Game3 = 3,
        Home = 4,
        Target0 = 5,
        Game6 = 6,
        Game7 = 7
    }
    public IconCode Code;
}