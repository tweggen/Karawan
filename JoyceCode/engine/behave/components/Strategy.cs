using System.Text.Json.Serialization;

namespace engine.behave.components;

public struct Strategy
{
    [JsonInclude] public IEntityStrategy EntityStrategy;
    
    public override string ToString()
    {
        return $"Provider={EntityStrategy.GetType()}";
    }

    public Strategy(IEntityStrategy entityStrategy)
    {
        EntityStrategy = entityStrategy;
    }
}