namespace engine.world.components;

public struct MapIcon
{
    public enum IconCode : int
    {
        None = 0,
        Player0 = 1,
        Player1 = 2,
        Player3 = 3,
        Home = 4,
        Target0 = 5,
        Target1 = 6,
        Target2 = 7
    }
    public IconCode Code;
}