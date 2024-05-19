namespace engine.behave.components;

public struct Nearby
{
    public IBehavior Provider;
    public float MaxDistance = 150f;
        
    public override string ToString()
    {
        return $"Provider={Provider.GetType()}";
    }
        
    public Nearby(IBehavior provider)
    {
        Provider = provider;
    }
}