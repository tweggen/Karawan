using engine.behave;

namespace engine.behave;

public abstract class AStrategyPart : IStrategyPart
{
    public IStrategyController Controller { get; init; }
    public abstract void OnExit();
    public abstract void OnEnter();
}