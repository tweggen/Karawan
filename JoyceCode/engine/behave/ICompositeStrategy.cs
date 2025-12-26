namespace engine.behave;

public interface ICompositeStrategy : IStrategyPart
{
    public IStrategyPart GetActiveStrategy();
}