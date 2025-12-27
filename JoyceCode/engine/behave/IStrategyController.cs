namespace engine.behave;

public interface IStrategyController
{
    public IStrategyPart GetActiveStrategy();
    
    public void GiveUpStrategy(IStrategyPart strategy);

}