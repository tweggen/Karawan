using System;
using static engine.Logger;

namespace builtin.npc;

public class StrategyController
{
    private ICompositeStrategy _rootStrategy;
    private IStrategyPart? _activeStrategy;

    public IStrategyPart GetActiveStrategy()
    {
        ErrorThrow<NotImplementedException>("Not yet implemented.");
    }
}