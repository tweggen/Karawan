namespace engine.behave;

public interface IStrategyPart
{
    public IStrategyController Controller { get; init; }
    
    public void OnEnter();
    public void OnExit();
}