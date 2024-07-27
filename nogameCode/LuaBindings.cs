using engine;
using nogame.config;
using static engine.Logger;

namespace nogame;

public class LuaBindings
{
    private object _lo = new();

    public GameConfig getConfig()
    {
        return I.Get<nogame.config.Module>().GameConfig;
    }
}