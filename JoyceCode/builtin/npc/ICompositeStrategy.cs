namespace builtin.npc;

public interface ICompositeStrategy : IStrategyPart
{
    public IStrategyPart GetActiveStrategy();
}