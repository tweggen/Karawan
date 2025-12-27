using engine.behave;

namespace engine.behave;

public abstract class AStrategyPart : IStrategyPart

{
    public ICompositeStrategy Controller { get; init; }
    public abstract void OnEnter();
    public abstract void OnExit();
}